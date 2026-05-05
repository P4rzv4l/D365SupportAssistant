namespace D365Assistant.Core.Models.Vault;

public record VaultClient
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string CrmUrl { get; init; } = "";
    public string Notes { get; init; } = "";
    public string Color { get; init; } = "#1A6CF5";
    public string CreatedAt { get; init; } = "";
    public string UpdatedAt { get; init; } = "";
}

public record VaultCredential
{
    public int Id { get; init; }
    public int ClientId { get; init; }
    public string Label { get; init; } = "";
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    public string Extra { get; init; } = "";
    public string Notes { get; init; } = "";
    public string CreatedAt { get; init; } = "";
    public string UpdatedAt { get; init; } = "";
    public DateTime ExpiresAt { get; init; }
    public string Url { get; init; } = "";
}

public record VaultLink
{
    public int Id { get; init; }
    public int ClientId { get; init; }
    public string EnvName { get; init; } = "";
    public string Url { get; init; } = "";
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    public string Notes { get; init; } = "";
    public string CreatedAt { get; init; } = "";
}

public class VaultLockedException()
    : Exception("Vault bloqueado. Desbloqueie com a senha mestre.");

public class WrongPasswordException()
    : Exception("Senha mestre incorreta.");