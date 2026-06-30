// =============================================================================
//  IExternalAuthService.cs — Autenticação para qualquer ambiente Dynamics 365
// =============================================================================
// Diferente do IAuthService (que usa o tenant fixo do appsettings),
// esta service autentica contra QUALQUER URL de ambiente informada pelo usuário.
// Usa o Client ID público da Microsoft para Dynamics/Power Platform.
// =============================================================================

namespace D365Assistant.Core.Services;

/// <summary>
/// Retorna headers de autorização prontos para usar em HttpRequestMessage
/// contra qualquer ambiente Dynamics 365 informado pelo usuário.
/// </summary>
public interface IExternalAuthService
{
    /// <summary>
    /// Obtém um token de acesso para a URL de ambiente informada.
    /// Cache por URL — reutiliza token válido sem novo login.
    /// </summary>
    Task<Dictionary<string, string>> GetHeadersAsync(
        string environmentUrl,
        CancellationToken ct = default);

    /// <summary>
    /// Remove o token em cache para a URL informada, forçando novo login.
    /// </summary>
    void InvalidateCache(string environmentUrl);

    /// <summary>
    /// Remove todos os tokens em cache.
    /// </summary>
    void InvalidateAll();

    /// <summary>
    /// Disparado quando o Device Flow requer que o usuário acesse uma URL
    /// e digite um código para autenticar.
    /// </summary>
    event EventHandler<DeviceCodeEventArgs>? DeviceCodeRequired;
}