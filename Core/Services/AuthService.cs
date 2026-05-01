using D365Assistant.Core.Models;
using D365Assistant.Core.Models.Config;
using Microsoft.Identity.Client;
using Serilog;
using System.IO;

namespace D365Assistant.Core.Services;

public interface IAuthService
{
    Task<string> GetAccessTokenAsync(CancellationToken ct = default);
    Task<Dictionary<string, string>> GetHeadersAsync(CancellationToken ct = default);
    void InvalidateCache();
    event EventHandler<DeviceCodeEventArgs>? DeviceCodeRequired;
}

public class DeviceCodeEventArgs(string userCode, string verificationUrl, string message) : EventArgs
{
    public string UserCode { get; } = userCode;
    public string VerificationUrl { get; } = verificationUrl;
    public string Message { get; } = message;
}

public class AuthService : IAuthService
{
    private readonly AppSettings _settings;
    private AuthenticationResult? _cached;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // ✅ MSIX-safe: usa LocalAppData em vez de diretório relativo
    private static readonly string DataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "D365SupportAssistant");

    private static string TokenCacheFile => Path.Combine(DataDir, ".token_cache.json");

    private IPublicClientApplication? _publicApp;
    private IConfidentialClientApplication? _confApp;

    public event EventHandler<DeviceCodeEventArgs>? DeviceCodeRequired;

    public AuthService(AppSettings settings)
    {
        _settings = settings;
        Directory.CreateDirectory(DataDir); // garante que a pasta existe
        Log.Information("AuthService inicializado | mode={Mode} | clientId={ClientId}",
            settings.AzureAd.AuthMode, settings.AzureAd.ClientId);
    }

    // ── Token ──────────────────────────────────────────────────────────────────

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (IsTokenValid())
            {
                Log.Debug("Token reutilizado do cache (expira {ExpiresOn})", _cached!.ExpiresOn);
                return _cached!.AccessToken;
            }

            var scopes = new[] { _settings.Dataverse.Scope };

            var result = _settings.AzureAd.AuthMode == "ClientCredentials"
                ? await AcquireClientCredentialsAsync(scopes, ct)
                : await AcquireDeviceFlowAsync(scopes, ct);

            _cached = result;
            Log.Information("Token obtido | expira {ExpiresOn:HH:mm:ss}", result.ExpiresOn);
            return result.AccessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Dictionary<string, string>> GetHeadersAsync(CancellationToken ct = default)
    {
        var token = await GetAccessTokenAsync(ct);
        return new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {token}",
            ["Accept"] = "application/json",
            ["OData-MaxVersion"] = "4.0",
            ["OData-Version"] = "4.0",
            ["Prefer"] = "odata.include-annotations=*",
        };
    }

    public void InvalidateCache()
    {
        _cached = null;
        Log.Information("Cache de token invalidado.");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private bool IsTokenValid() =>
        _cached is not null &&
        _cached.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5);

    // ── Device Flow ────────────────────────────────────────────────────────────

    private async Task<AuthenticationResult> AcquireDeviceFlowAsync(
        string[] scopes, CancellationToken ct)
    {
        var app = GetPublicApp();
        var accounts = await app.GetAccountsAsync();
        var account = accounts.FirstOrDefault();

        // Tenta cache silencioso primeiro
        if (account is not null)
        {
            try
            {
                var silent = await app.AcquireTokenSilent(scopes, account).ExecuteAsync(ct);
                Log.Debug("Token restaurado do cache em disco.");
                return silent;
            }
            catch (MsalUiRequiredException)
            {
                Log.Information("Token expirado — iniciando Device Flow.");
            }
        }

        // Fluxo interativo
        return await app
            .AcquireTokenWithDeviceCode(scopes, dcResult =>
            {
                DeviceCodeRequired?.Invoke(this, new DeviceCodeEventArgs(
                    dcResult.UserCode, dcResult.VerificationUrl, dcResult.Message));

                TryOpenBrowser(dcResult.VerificationUrl);

                Log.Information("Device Flow | URL={Url} | Código={Code}",
                    dcResult.VerificationUrl, dcResult.UserCode);

                return Task.CompletedTask;
            })
            .ExecuteAsync(ct);
    }

    private IPublicClientApplication GetPublicApp()
    {
        if (_publicApp is not null) return _publicApp;

        _publicApp = PublicClientApplicationBuilder
            .Create(_settings.AzureAd.ClientId)
            .WithAuthority("https://login.microsoftonline.com/organizations")
            .Build();

        TokenCacheHelper.EnableSerialization(_publicApp.UserTokenCache, TokenCacheFile);
        return _publicApp;
    }

    // ── Client Credentials ─────────────────────────────────────────────────────

    private async Task<AuthenticationResult> AcquireClientCredentialsAsync(
        string[] scopes, CancellationToken ct)
    {
        _confApp ??= ConfidentialClientApplicationBuilder
            .Create(_settings.AzureAd.ClientId)
            .WithAuthority($"https://login.microsoftonline.com/{_settings.AzureAd.TenantId}")
            .Build();

        return await _confApp.AcquireTokenForClient(scopes).ExecuteAsync(ct);
    }

    private static void TryOpenBrowser(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warning("Não foi possível abrir o browser: {Error}", ex.Message);
        }
    }
}

// ── Cache em disco ─────────────────────────────────────────────────────────────

internal static class TokenCacheHelper
{
    private static readonly object _lock = new();

    public static void EnableSerialization(ITokenCache cache, string path)
    {
        cache.SetBeforeAccess(args =>
        {
            lock (_lock)
            {
                if (File.Exists(path))
                    args.TokenCache.DeserializeMsalV3(
                        File.ReadAllBytes(path),
                        shouldClearExistingCache: true);
            }
        });

        cache.SetAfterAccess(args =>
        {
            if (!args.HasStateChanged) return;
            lock (_lock)
                File.WriteAllBytes(path, args.TokenCache.SerializeMsalV3());
        });
    }
}