// =============================================================================
//  Core/Services/VaultService.cs
//
//  Cofre de credenciais com criptografia AES-256-GCM.
//
//  Segurança:
//   • Chave derivada via PBKDF2-HMAC-SHA256 (310 000 iterações)
//   • Cada campo sensível criptografado com AES-256-GCM + nonce único
//   • Senha mestre nunca armazenada em texto claro (apenas hash PBKDF2)
//   • Vault bloqueado por padrão; chave zerada ao bloquear
// =============================================================================

using D365Assistant.Core.Models.Vault;
using Microsoft.Data.Sqlite;
using Serilog;
using System.Security.Cryptography;
using System.Text;

namespace D365Assistant.Core.Services;

public class VaultService : IDisposable
{
    // ── Configuração de criptografia ──────────────────────────────────────────
    private const int Pbkdf2Iterations = 310_000;
    private const int SaltLen = 32;
    private const int KeyLen = 32;    // AES-256
    private const int NonceLen = 12;    // AES-GCM nonce
    private const int TagLen = 16;    // AES-GCM tag

    // ── Estado interno ────────────────────────────────────────────────────────
    private readonly string _dbPath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private byte[]? _key;   // null = bloqueado

    public bool IsUnlocked => _key is not null;
    public bool HasMaster => CheckHasMaster();

    public VaultService(string dbPath)
    {
        _dbPath = dbPath;
        EnsureSchema();
        Log.Information("VaultService inicializado | db={Path}", dbPath);
    }

    // ── Senha mestre ──────────────────────────────────────────────────────────

    public void SetupMaster(string password)
    {
        if (HasMaster)
            throw new InvalidOperationException("Senha mestre já configurada.");
        if (password.Length < 4)
            throw new ArgumentException("Mínimo de 4 caracteres.");

        var pwdSalt = RandomBytes(SaltLen);
        var kdfSalt = RandomBytes(SaltLen);
        var pwdHash = HashPassword(password, pwdSalt);

        using var conn = OpenConn();
        conn.Execute(
            "INSERT INTO vault_master (pwd_hash, pwd_salt, kdf_salt, created_at) VALUES (@h,@ps,@ks,@ca)",
            new { h = pwdHash, ps = HexEncode(pwdSalt), ks = HexEncode(kdfSalt), ca = UtcNow() });

        _key = DeriveKey(password, kdfSalt);
        Log.Information("Vault: senha mestre configurada.");
    }

    public void Unlock(string password)
    {
        using var conn = OpenConn();
        var row = conn.QueryOne(
            "SELECT pwd_hash, pwd_salt, kdf_salt FROM vault_master LIMIT 1");

        if (row is null)
            throw new InvalidOperationException("Vault não configurado.");

        var pwdSalt = HexDecode((string)row["pwd_salt"]);
        var kdfSalt = HexDecode((string)row["kdf_salt"]);
        var expected = HashPassword(password, pwdSalt);

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes((string)row["pwd_hash"])))
            throw new WrongPasswordException();

        _key = DeriveKey(password, kdfSalt);
        Log.Information("Vault desbloqueado.");
    }

    public void Lock()
    {
        if (_key is not null)
        {
            CryptographicOperations.ZeroMemory(_key);
            _key = null;
        }
        Log.Information("Vault bloqueado.");
    }

    public void ChangeMaster(string oldPassword, string newPassword)
    {
        Unlock(oldPassword);
        var data = ExportPlain();

        using var conn = OpenConn();
        conn.Execute("DELETE FROM vault_master");
        conn.Execute("DELETE FROM vault_clients");

        Lock();
        SetupMaster(newPassword);
        ReimportPlain(data);
        Log.Information("Vault: senha mestre alterada.");
    }

    // ── Clientes ──────────────────────────────────────────────────────────────

    public int AddClient(string name, string crmUrl = "", string notes = "", string color = "#1A6CF5")
    {
        using var conn = OpenConn();
        return conn.ExecuteScalar<int>(
            "INSERT INTO vault_clients (name,crm_url,notes,color,created_at,updated_at) VALUES (@n,@u,@no,@c,@ca,@ca); SELECT last_insert_rowid();",
            new { n = name, u = crmUrl, no = notes, c = color, ca = UtcNow() });
    }

    public void UpdateClient(int id, string name, string crmUrl = "", string notes = "", string color = "#1A6CF5")
    {
        using var conn = OpenConn();
        conn.Execute(
            "UPDATE vault_clients SET name=@n,crm_url=@u,notes=@no,color=@c,updated_at=@ua WHERE id=@id",
            new { n = name, u = crmUrl, no = notes, c = color, ua = UtcNow(), id });
    }

    public void DeleteClient(int id)
    {
        using var conn = OpenConn();
        conn.Execute("DELETE FROM vault_clients WHERE id=@id", new { id });
    }

    public List<VaultClient> ListClients()
    {
        using var conn = OpenConn();
        return conn.Query(
            "SELECT id,name,crm_url,notes,color,created_at,updated_at FROM vault_clients ORDER BY name COLLATE NOCASE",
            MapClient);
    }

    public VaultClient? GetClient(int id)
    {
        using var conn = OpenConn();
        var row = conn.QueryOne("SELECT id,name,crm_url,notes,color,created_at,updated_at FROM vault_clients WHERE id=@id", new { id });
        return row is null ? null : MapClient(row);
    }

    // ── Credenciais ───────────────────────────────────────────────────────────

    public int AddCredential(int clientId, string label, string username = "",
                             string password = "", string extra = "", string notes = "")
    {
        RequireUnlock();
        using var conn = OpenConn();
        return conn.ExecuteScalar<int>(
            "INSERT INTO vault_credentials (client_id,label,username,password,extra,notes,created_at,updated_at) VALUES (@cid,@l,@u,@p,@e,@n,@ca,@ca); SELECT last_insert_rowid();",
            new { cid = clientId, l = label, u = Enc(username), p = Enc(password), e = Enc(extra), n = notes, ca = UtcNow() });
    }

    public void UpdateCredential(int id, string label, string username = "",
                                 string password = "", string extra = "", string notes = "")
    {
        RequireUnlock();
        using var conn = OpenConn();
        conn.Execute(
            "UPDATE vault_credentials SET label=@l,username=@u,password=@p,extra=@e,notes=@n,updated_at=@ua WHERE id=@id",
            new { l = label, u = Enc(username), p = Enc(password), e = Enc(extra), n = notes, ua = UtcNow(), id });
    }

    public void DeleteCredential(int id)
    {
        using var conn = OpenConn();
        conn.Execute("DELETE FROM vault_credentials WHERE id=@id", new { id });
    }

    public List<VaultCredential> GetCredentials(int clientId)
    {
        RequireUnlock();
        using var conn = OpenConn();
        var rows = conn.Query(
            "SELECT id,client_id,label,username,password,extra,notes,created_at,updated_at FROM vault_credentials WHERE client_id=@cid ORDER BY label",
            r => new VaultCredential
            {
                Id = (int)(long)r["id"],
                ClientId = (int)(long)r["client_id"],
                Label = (string)r["label"],
                Username = Dec((string)r["username"]),
                Password = Dec((string)r["password"]),
                Extra = Dec((string)r["extra"]),
                Notes = (string)r["notes"],
                CreatedAt = (string)r["created_at"],
                UpdatedAt = (string)r["updated_at"],
            },
            new { cid = clientId });
        return rows;
    }

    // ── Links ─────────────────────────────────────────────────────────────────

    public int AddLink(int clientId, string envName, string url = "",
                       string username = "", string password = "", string notes = "")
    {
        RequireUnlock();
        using var conn = OpenConn();
        return conn.ExecuteScalar<int>(
            "INSERT INTO vault_links (client_id,env_name,url,username,password,notes,created_at) VALUES (@cid,@en,@u,@un,@p,@n,@ca); SELECT last_insert_rowid();",
            new { cid = clientId, en = envName, u = url, un = Enc(username), p = Enc(password), n = notes, ca = UtcNow() });
    }

    public void UpdateLink(int id, string envName, string url = "",
                           string username = "", string password = "", string notes = "")
    {
        RequireUnlock();
        using var conn = OpenConn();
        conn.Execute(
            "UPDATE vault_links SET env_name=@en,url=@u,username=@un,password=@p,notes=@n WHERE id=@id",
            new { en = envName, u = url, un = Enc(username), p = Enc(password), n = notes, id });
    }

    public void DeleteLink(int id)
    {
        using var conn = OpenConn();
        conn.Execute("DELETE FROM vault_links WHERE id=@id", new { id });
    }

    public List<VaultLink> GetLinks(int clientId)
    {
        RequireUnlock();
        using var conn = OpenConn();
        return conn.Query(
            "SELECT id,client_id,env_name,url,username,password,notes,created_at FROM vault_links WHERE client_id=@cid ORDER BY env_name",
            r => new VaultLink
            {
                Id = (int)(long)r["id"],
                ClientId = (int)(long)r["client_id"],
                EnvName = (string)r["env_name"],
                Url = (string)r["url"],
                Username = Dec((string)r["username"]),
                Password = Dec((string)r["password"]),
                Notes = (string)r["notes"],
                CreatedAt = (string)r["created_at"],
            },
            new { cid = clientId });
    }

    // ── Criptografia AES-256-GCM ──────────────────────────────────────────────

    private string Enc(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return "";

        var nonce = RandomBytes(NonceLen);
        var tag = new byte[TagLen];
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plainBytes.Length];

        using var aes = new AesGcm(_key!, TagLen);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        // Formato: nonce(12) | tag(16) | cipher
        var combined = new byte[NonceLen + TagLen + cipher.Length];
        nonce.CopyTo(combined, 0);
        tag.CopyTo(combined, NonceLen);
        cipher.CopyTo(combined, NonceLen + TagLen);

        return Convert.ToBase64String(combined);
    }

    private string Dec(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext)) return "";

        try
        {
            var combined = Convert.FromBase64String(ciphertext);
            var nonce = combined[..NonceLen];
            var tag = combined[NonceLen..(NonceLen + TagLen)];
            var cipher = combined[(NonceLen + TagLen)..];
            var plain = new byte[cipher.Length];

            using var aes = new AesGcm(_key!, TagLen);
            aes.Decrypt(nonce, cipher, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return "⚠ Erro ao descriptografar";
        }
    }

    // ── PBKDF2 ────────────────────────────────────────────────────────────────

    private static byte[] DeriveKey(string password, byte[] salt)
        => Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            KeyLen);

    private static string HashPassword(string password, byte[] salt)
        => HexEncode(Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Pbkdf2Iterations,
            HashAlgorithmName.SHA256,
            32));

    // ── Schema ────────────────────────────────────────────────────────────────

    private void EnsureSchema()
    {
        using var conn = OpenConn();
        conn.Execute("""
            CREATE TABLE IF NOT EXISTS vault_master (
                id         INTEGER PRIMARY KEY,
                pwd_hash   TEXT NOT NULL,
                pwd_salt   TEXT NOT NULL,
                kdf_salt   TEXT NOT NULL,
                created_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS vault_clients (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                name       TEXT NOT NULL,
                crm_url    TEXT DEFAULT '',
                notes      TEXT DEFAULT '',
                color      TEXT DEFAULT '#1A6CF5',
                created_at TEXT NOT NULL DEFAULT (datetime('now')),
                updated_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            CREATE TABLE IF NOT EXISTS vault_credentials (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                client_id  INTEGER NOT NULL REFERENCES vault_clients(id) ON DELETE CASCADE,
                label      TEXT NOT NULL,
                username   TEXT DEFAULT '',
                password   TEXT DEFAULT '',
                extra      TEXT DEFAULT '',
                notes      TEXT DEFAULT '',
                created_at TEXT NOT NULL DEFAULT (datetime('now')),
                updated_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            CREATE TABLE IF NOT EXISTS vault_links (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                client_id  INTEGER NOT NULL REFERENCES vault_clients(id) ON DELETE CASCADE,
                env_name   TEXT NOT NULL,
                url        TEXT DEFAULT '',
                username   TEXT DEFAULT '',
                password   TEXT DEFAULT '',
                notes      TEXT DEFAULT '',
                created_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
            CREATE INDEX IF NOT EXISTS idx_vc_client ON vault_credentials(client_id);
            CREATE INDEX IF NOT EXISTS idx_vl_client ON vault_links(client_id);
            PRAGMA journal_mode=WAL;
            PRAGMA foreign_keys=ON;
            """);
    }

    private bool CheckHasMaster()
    {
        using var conn = OpenConn();
        return conn.QueryOne("SELECT 1 FROM vault_master LIMIT 1") is not null;
    }

    // ── Export/Import para re-criptografia ────────────────────────────────────

    private record PlainExport(VaultClient Client, List<VaultCredential> Creds, List<VaultLink> Links);

    private List<PlainExport> ExportPlain()
    {
        return ListClients().Select(c => new PlainExport(
            c,
            GetCredentials(c.Id),
            GetLinks(c.Id))).ToList();
    }

    private void ReimportPlain(List<PlainExport> data)
    {
        foreach (var (client, creds, links) in data)
        {
            var newId = AddClient(client.Name, client.CrmUrl, client.Notes, client.Color);
            foreach (var c in creds)
                AddCredential(newId, c.Label, c.Username, c.Password, c.Extra, c.Notes);
            foreach (var l in links)
                AddLink(newId, l.EnvName, l.Url, l.Username, l.Password, l.Notes);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RequireUnlock()
    {
        if (!IsUnlocked) throw new VaultLockedException();
    }

    private static byte[] RandomBytes(int len) { var b = new byte[len]; RandomNumberGenerator.Fill(b); return b; }
    private static string HexEncode(byte[] b) => Convert.ToHexString(b).ToLower();
    private static byte[] HexDecode(string s) => Convert.FromHexString(s);
    private static string UtcNow() => DateTime.UtcNow.ToString("o");

    private static VaultClient MapClient(Dictionary<string, object> r) => new()
    {
        Id = (int)(long)r["id"],
        Name = (string)r["name"],
        CrmUrl = (string)(r["crm_url"] ?? ""),
        Notes = (string)(r["notes"] ?? ""),
        Color = (string)(r["color"] ?? "#1A6CF5"),
        CreatedAt = (string)(r["created_at"] ?? ""),
        UpdatedAt = (string)(r["updated_at"] ?? ""),
    };

    // ── SQLite micro-ORM ──────────────────────────────────────────────────────

    private SimpleConn OpenConn() => new(_dbPath);

    private class SimpleConn(string path) : IDisposable
    {
        private readonly SqliteConnection _conn = new($"Data Source={path}");

        private SqliteConnection Open() { if (_conn.State != System.Data.ConnectionState.Open) _conn.Open(); return _conn; }

        public void Execute(string sql, object? p = null)
        {
            using var cmd = Open().CreateCommand();
            cmd.CommandText = sql;
            BindParams(cmd, p);
            cmd.ExecuteNonQuery();
        }

        public T ExecuteScalar<T>(string sql, object? p = null)
        {
            using var cmd = Open().CreateCommand();
            cmd.CommandText = sql;
            BindParams(cmd, p);
            return (T)Convert.ChangeType(cmd.ExecuteScalar()!, typeof(T));
        }

        public Dictionary<string, object>? QueryOne(string sql, object? p = null)
        {
            using var cmd = Open().CreateCommand();
            cmd.CommandText = sql;
            BindParams(cmd, p);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            var d = new Dictionary<string, object>();
            for (int i = 0; i < reader.FieldCount; i++)
                d[reader.GetName(i)] = reader.GetValue(i);
            return d;
        }

        public List<T> Query<T>(string sql, Func<Dictionary<string, object>, T> map, object? p = null)
        {
            using var cmd = Open().CreateCommand();
            cmd.CommandText = sql;
            BindParams(cmd, p);
            using var reader = cmd.ExecuteReader();
            var result = new List<T>();
            while (reader.Read())
            {
                var d = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                    d[reader.GetName(i)] = reader.GetValue(i) ?? "";
                result.Add(map(d));
            }
            return result;
        }

        private static void BindParams(SqliteCommand cmd, object? p)
        {
            if (p is null) return;
            foreach (var prop in p.GetType().GetProperties())
                cmd.Parameters.AddWithValue("@" + prop.Name, prop.GetValue(p) ?? DBNull.Value);
        }

        public void Dispose() => _conn.Dispose();
    }

    public void Dispose()
    {
        Lock();
        _lock.Dispose();
    }
}