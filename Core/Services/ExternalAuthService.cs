// =============================================================================
//  ExternalAuthService.cs — Autenticação MSAL para qualquer ambiente D365
// =============================================================================
// Client ID público da Microsoft para Dynamics/Power Platform
// (mesmo usado pelo Power Platform CLI, Power Apps, etc.)
// Autentica via Device Code Flow — sem necessidade de registrar app no tenant
// do cliente, pois este Client ID já tem as permissões necessárias.
// =============================================================================

using Microsoft.Identity.Client;
using Serilog;

namespace D365Assistant.Core.Services;

public sealed class ExternalAuthService : IExternalAuthService
{
    // ── Client ID público da Microsoft para Dynamics / Power Platform ─────────
    // O mesmo usado pelo JS (51f81489-12ee-4a9e-aaae-a2591f45987d)
    private const string PublicClientId = "51f81489-12ee-4a9e-aaae-a2591f45987d";

    // ── Cache de tokens por URL de ambiente ───────────────────────────────────
    // Chave: environmentUrl normalizado (lowercase, sem trailing slash)
    private readonly Dictionary<string, AuthenticationResult> _cache = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public event EventHandler<DeviceCodeEventArgs>? DeviceCodeRequired;

    // ══════════════════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<Dictionary<string, string>> GetHeadersAsync(
        string environmentUrl,
        CancellationToken ct = default)
    {
        var token = await AcquireTokenAsync(environmentUrl, ct);
        return BuildHeaders(token);
    }

    public void InvalidateCache(string environmentUrl)
    {
        var key = NormalizeUrl(environmentUrl);
        lock (_cache) { _cache.Remove(key); }
        Log.Information("[ExternalAuth] Cache invalidado para {Url}", key);
    }

    public void InvalidateAll()
    {
        lock (_cache) { _cache.Clear(); }
        Log.Information("[ExternalAuth] Todos os caches de token removidos.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  TOKEN ACQUISITION
    // ══════════════════════════════════════════════════════════════════════════

    private async Task<AuthenticationResult> AcquireTokenAsync(
        string environmentUrl, CancellationToken ct)
    {
        var key = NormalizeUrl(environmentUrl);

        await _lock.WaitAsync(ct);
        try
        {
            // 1. Cache hit — token ainda válido (>5 min restantes)
            if (_cache.TryGetValue(key, out var cached)
                && cached.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5))
            {
                Log.Debug("[ExternalAuth] Token em cache para {Url}", key);
                return cached;
            }

            // 2. Cria cliente MSAL para este ambiente específico
            //    Authority "common" permite autenticar em qualquer tenant
            var pca = BuildPublicClient(environmentUrl);

            // 3. Tenta silent (conta em cache do MSAL)
            var accounts = await pca.GetAccountsAsync();
            if (accounts.Any())
            {
                try
                {
                    var scopes = BuildScopes(environmentUrl);
                    var silent = await pca.AcquireTokenSilent(scopes, accounts.First())
                                           .ExecuteAsync(ct);
                    _cache[key] = silent;
                    Log.Debug("[ExternalAuth] Token silent para {Url}", key);
                    return silent;
                }
                catch (MsalUiRequiredException)
                {
                    Log.Debug("[ExternalAuth] Silent falhou, tentando Device Code para {Url}", key);
                }
            }

            // 4. Device Code Flow
            var result = await AcquireViaDeviceCodeAsync(pca, environmentUrl, ct);
            _cache[key] = result;
            return result;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<AuthenticationResult> AcquireViaDeviceCodeAsync(
        IPublicClientApplication pca,
        string environmentUrl,
        CancellationToken ct)
    {
        var scopes = BuildScopes(environmentUrl);

        var result = await pca.AcquireTokenWithDeviceCode(scopes, deviceCode =>
        {
            Log.Information("[ExternalAuth] Device Code para {Url}: {Code} → {Url2}",
                environmentUrl, deviceCode.UserCode, deviceCode.VerificationUrl);

            // Notifica a UI para abrir o browser
            DeviceCodeRequired?.Invoke(this, new DeviceCodeEventArgs(
                deviceCode.UserCode,
                deviceCode.VerificationUrl,
                deviceCode.Message));

            // Tenta abrir o browser automaticamente
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    deviceCode.VerificationUrl)
                { UseShellExecute = true });
            }
            catch { /* ignora — usuário abre manualmente */ }

            return Task.CompletedTask;
        }).ExecuteAsync(ct);

        Log.Information("[ExternalAuth] Token obtido via Device Code para {Url}", environmentUrl);
        return result;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    private static IPublicClientApplication BuildPublicClient(string environmentUrl)
    {
        // "common" aceita qualquer tenant (pessoal ou corporativo)
        return PublicClientApplicationBuilder
            .Create(PublicClientId)
            .WithAuthority("https://login.microsoftonline.com/common")
            .WithDefaultRedirectUri()
            .Build();
    }

    /// <summary>
    /// O scope para Dynamics é sempre {environmentUrl}/.default
    /// Ex: https://org.crm.dynamics.com/.default
    /// </summary>
    private static string[] BuildScopes(string environmentUrl)
    {
        var baseUrl = NormalizeUrl(environmentUrl);
        return [$"{baseUrl}/.default"];
    }

    private static Dictionary<string, string> BuildHeaders(AuthenticationResult token) =>
        new()
        {
            ["Authorization"] = $"Bearer {token.AccessToken}",
            ["OData-MaxVersion"] = "4.0",
            ["OData-Version"] = "4.0",
            ["Accept"] = "application/json",
        };

    private static string NormalizeUrl(string url) =>
        url.TrimEnd('/').ToLowerInvariant();
}