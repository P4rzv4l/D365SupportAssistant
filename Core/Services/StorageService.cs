using Microsoft.Data.Sqlite;
using Serilog;
using System.IO;
using D365Assistant.Core.Models.Config;
using D365Assistant.Core.Models.Incident;
using D365Assistant.Core.Models.Alerts;
using D365Assistant.Core.Models.Time;

namespace D365Assistant.Core.Services;

public class StorageService : IDisposable
{
    private readonly string _dbPath;
    private readonly object _lock = new();

    public StorageService(AppSettings cfg)
    {
        _dbPath = cfg.Database.Path;
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath) ?? "data");
    }

    // ── Inicialização ─────────────────────────────────────────────────────────

    public void Initialize()
    {
        Log.Information("Inicializando banco: {Path}", _dbPath);
        using var conn = Open();
        conn.Execute(@"PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;");

        conn.Execute(@"
            CREATE TABLE IF NOT EXISTS schema_version (
                version INTEGER NOT NULL, applied_at TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS incidents (
                incident_id        TEXT PRIMARY KEY,
                ticket_number      TEXT NOT NULL,
                title              TEXT NOT NULL,
                state_code         INTEGER NOT NULL DEFAULT 0,
                status_code        INTEGER NOT NULL DEFAULT 1,
                priority_code      INTEGER,
                case_type_code     INTEGER,
                modified_on        TEXT NOT NULL,
                first_seen_at      TEXT NOT NULL,
                last_seen_at       TEXT NOT NULL,
                alert_count        INTEGER NOT NULL DEFAULT 0,
                bzp_nome_cliente   TEXT,
                bzp_url            TEXT,
                bz_horas_esgotadas INTEGER DEFAULT 0,
                bz_sai             INTEGER,
                bz_motivo_status   TEXT,
                bz_total_horas     REAL,
                customer_name      TEXT
            );
            CREATE TABLE IF NOT EXISTS alert_history (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                incident_id TEXT NOT NULL,
                alert_type  TEXT NOT NULL,
                fired_at    TEXT NOT NULL,
                message     TEXT NOT NULL,
                channel     TEXT NOT NULL DEFAULT 'app'
            );
            CREATE TABLE IF NOT EXISTS poll_runs (
                id                INTEGER PRIMARY KEY AUTOINCREMENT,
                started_at        TEXT NOT NULL,
                finished_at       TEXT,
                incidents_fetched INTEGER DEFAULT 0,
                new_incidents     INTEGER DEFAULT 0,
                alerts_fired      INTEGER DEFAULT 0,
                error             TEXT
            );
            CREATE TABLE IF NOT EXISTS time_entries (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                ticket_id  TEXT NOT NULL,
                title      TEXT DEFAULT '',
                start_time TEXT NOT NULL,
                end_time   TEXT,
                duration   INTEGER DEFAULT 0,
                is_active  INTEGER DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_alerts_incident ON alert_history(incident_id);
            CREATE INDEX IF NOT EXISTS idx_te_ticket       ON time_entries(ticket_id);
        ");

        foreach (var sql in new[]
        {
            "ALTER TABLE incidents ADD COLUMN bzp_nome_cliente TEXT",
            "ALTER TABLE incidents ADD COLUMN bzp_url TEXT",
            "ALTER TABLE incidents ADD COLUMN bz_horas_esgotadas INTEGER DEFAULT 0",
            "ALTER TABLE incidents ADD COLUMN bz_sai INTEGER",
            "ALTER TABLE incidents ADD COLUMN bz_motivo_status TEXT",
            "ALTER TABLE incidents ADD COLUMN bz_total_horas REAL",
            "ALTER TABLE incidents ADD COLUMN customer_name TEXT",
            "ALTER TABLE incidents ADD COLUMN case_type_code INTEGER",
        })
        {
            try { conn.Execute(sql); }
            catch { /* coluna já existe */ }
        }

        Log.Information("Banco pronto.");
    }

    // ── Incidents ─────────────────────────────────────────────────────────────

    public HashSet<string> FindNewIncidentIds(IEnumerable<Incident> incidents)
    {
        var incoming = incidents.Select(i => i.IncidentId).ToHashSet();
        if (incoming.Count == 0) return [];

        lock (_lock)
        {
            using var conn = Open();
            var ph = string.Join(",", incoming.Select((_, i) => $"@id{i}"));
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT incident_id FROM incidents WHERE incident_id IN ({ph})";
            for (int i = 0; i < incoming.Count; i++)
                cmd.Parameters.AddWithValue($"@id{i}", incoming.ElementAt(i));

            var known = new HashSet<string>();
            using var r = cmd.ExecuteReader();
            while (r.Read()) known.Add(r.GetString(0));
            return incoming.Except(known).ToHashSet();
        }
    }

    public void UpsertIncidents(IEnumerable<Incident> incidents)
    {
        var now = UtcNow();
        lock (_lock)
        {
            using var conn = Open();
            using var tx = conn.BeginTransaction();

            foreach (var inc in incidents)
            {
                using var sel = conn.CreateCommand();
                sel.Transaction = tx;
                sel.CommandText = "SELECT first_seen_at FROM incidents WHERE incident_id=@id";
                sel.Parameters.AddWithValue("@id", inc.IncidentId);
                var firstSeen = sel.ExecuteScalar() as string ?? now;

                using var upsert = conn.CreateCommand();
                upsert.Transaction = tx;
                upsert.CommandText = @"
                    INSERT INTO incidents (
                        incident_id,ticket_number,title,state_code,status_code,priority_code,
                        case_type_code,modified_on,first_seen_at,last_seen_at,alert_count,
                        bzp_nome_cliente,bzp_url,bz_horas_esgotadas,bz_sai,bz_motivo_status,
                        bz_total_horas,customer_name)
                    VALUES (@id,@tn,@t,@sc,@stc,@pc,@ctc,@mod,@first,@last,0,
                        @bnc,@burl,@bhe,@bsai,@bms,@bth,@cn)
                    ON CONFLICT(incident_id) DO UPDATE SET
                        ticket_number=excluded.ticket_number, title=excluded.title,
                        state_code=excluded.state_code, status_code=excluded.status_code,
                        priority_code=excluded.priority_code, case_type_code=excluded.case_type_code,
                        modified_on=excluded.modified_on, last_seen_at=excluded.last_seen_at,
                        bzp_nome_cliente=excluded.bzp_nome_cliente, bzp_url=excluded.bzp_url,
                        bz_horas_esgotadas=excluded.bz_horas_esgotadas, bz_sai=excluded.bz_sai,
                        bz_motivo_status=excluded.bz_motivo_status, bz_total_horas=excluded.bz_total_horas,
                        customer_name=excluded.customer_name";

                upsert.Parameters.AddWithValue("@id", inc.IncidentId);
                upsert.Parameters.AddWithValue("@tn", inc.TicketNumber);
                upsert.Parameters.AddWithValue("@t", inc.Title);
                upsert.Parameters.AddWithValue("@sc", inc.StateCode);
                upsert.Parameters.AddWithValue("@stc", inc.StatusCode);
                upsert.Parameters.AddWithValue("@pc", (object?)inc.PriorityCode ?? DBNull.Value);
                upsert.Parameters.AddWithValue("@ctc", (object?)inc.CaseTypeCode ?? DBNull.Value);
                upsert.Parameters.AddWithValue("@mod", inc.ModifiedOn.ToString("o"));
                upsert.Parameters.AddWithValue("@first", firstSeen);
                upsert.Parameters.AddWithValue("@last", now);
                upsert.Parameters.AddWithValue("@bnc", (object?)inc.BzpNomeCliente ?? DBNull.Value);
                upsert.Parameters.AddWithValue("@burl", (object?)inc.BzpUrl ?? DBNull.Value);
                upsert.Parameters.AddWithValue("@bhe", inc.BzHorasEsgotadas ? 1 : 0);
                upsert.Parameters.AddWithValue("@bsai", (object?)inc.BzSai ?? DBNull.Value);
                upsert.Parameters.AddWithValue("@bms", (object?)inc.BzMotivoStatus ?? DBNull.Value);
                upsert.Parameters.AddWithValue("@bth", (object?)inc.BzTotalHoras ?? DBNull.Value);
                upsert.Parameters.AddWithValue("@cn", (object?)inc.CustomerName ?? DBNull.Value);
                upsert.ExecuteNonQuery();
            }
            tx.Commit();
        }
    }

    public List<IncidentSnapshot> GetAllSnapshots(bool activeOnly = true)
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT incident_id,ticket_number,title,state_code,status_code,priority_code,
                       case_type_code,modified_on,first_seen_at,last_seen_at,alert_count,
                       bzp_nome_cliente,bzp_url,bz_horas_esgotadas,bz_sai,
                       bz_motivo_status,bz_total_horas,customer_name
                FROM incidents" +
                (activeOnly ? " WHERE state_code=0" : "") +
                " ORDER BY first_seen_at DESC";

            var list = new List<IncidentSnapshot>();
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(RowToSnapshot(r));
            return list;
        }
    }

    public IncidentSnapshot? GetSnapshot(string incidentId)
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT incident_id,ticket_number,title,state_code,status_code,priority_code,
                       case_type_code,modified_on,first_seen_at,last_seen_at,alert_count,
                       bzp_nome_cliente,bzp_url,bz_horas_esgotadas,bz_sai,
                       bz_motivo_status,bz_total_horas,customer_name
                FROM incidents WHERE incident_id=@id";
            cmd.Parameters.AddWithValue("@id", incidentId);
            using var r = cmd.ExecuteReader();
            return r.Read() ? RowToSnapshot(r) : null;
        }
    }

    // ── Alertas ───────────────────────────────────────────────────────────────

    public void RecordAlert(string incidentId, AlertType type, string message, string channel = "app")
    {
        lock (_lock)
        {
            using var conn = Open();
            conn.Execute(@"
                INSERT INTO alert_history (incident_id,alert_type,fired_at,message,channel)
                VALUES (@id,@type,@at,@msg,@ch);
                UPDATE incidents SET alert_count=alert_count+1 WHERE incident_id=@id;",
                ("@id", incidentId), ("@type", type.ToString().ToLower()),
                ("@at", UtcNow()), ("@msg", message), ("@ch", channel));
        }
    }

    public bool WasAlertFiredRecently(string incidentId, AlertType type, int withinMinutes = 60)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-withinMinutes).ToString("o");
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*) FROM alert_history
                WHERE incident_id=@id AND alert_type=@type AND fired_at>=@cutoff";
            cmd.Parameters.AddWithValue("@id", incidentId);
            cmd.Parameters.AddWithValue("@type", type.ToString().ToLower());
            cmd.Parameters.AddWithValue("@cutoff", cutoff);
            return (long)(cmd.ExecuteScalar() ?? 0L) > 0;
        }
    }

    // ── Ciclos de monitoramento ────────────────────────────────────────────────

    public int StartPollRun()
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO poll_runs (started_at) VALUES (@at)";
            cmd.Parameters.AddWithValue("@at", UtcNow());
            cmd.ExecuteNonQuery();
            return (int)conn.LastInsertRowId();
        }
    }

    public void FinishPollRun(int runId, int fetched = 0, int newInc = 0,
                               int alerts = 0, string? error = null)
    {
        lock (_lock)
        {
            using var conn = Open();
            conn.Execute(@"
                UPDATE poll_runs SET finished_at=@fin, incidents_fetched=@f,
                new_incidents=@n, alerts_fired=@a, error=@e WHERE id=@id",
                ("@fin", UtcNow()), ("@f", fetched), ("@n", newInc),
                ("@a", alerts), ("@e", (object?)error ?? DBNull.Value), ("@id", runId));
        }
    }

    // ── Time Tracker ──────────────────────────────────────────────────────────

    public List<TimeEntry> GetTodayEntries()
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT id,ticket_id,title,start_time,end_time,duration,is_active
                FROM time_entries WHERE date(start_time)=@d ORDER BY start_time";
            cmd.Parameters.AddWithValue("@d", today);
            return ReadEntries(cmd);
        }
    }

    public int GetTrackedSecondsForTicket(string ticketId)
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT COALESCE(SUM(duration),0) FROM time_entries
                WHERE ticket_id=@t AND date(start_time)=@d";
            cmd.Parameters.AddWithValue("@t", ticketId);
            cmd.Parameters.AddWithValue("@d", today);
            return (int)(long)(cmd.ExecuteScalar() ?? 0L);
        }
    }

    public int StartTimer(string ticketId, string title)
    {
        lock (_lock)
        {
            using var conn = Open();
            conn.Execute("UPDATE time_entries SET is_active=0 WHERE is_active=1");
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO time_entries (ticket_id,title,start_time,duration,is_active)
                VALUES (@t,@ti,@st,0,1)";
            cmd.Parameters.AddWithValue("@t", ticketId);
            cmd.Parameters.AddWithValue("@ti", title);
            cmd.Parameters.AddWithValue("@st", DateTime.Now.ToString("o"));
            cmd.ExecuteNonQuery();
            return (int)conn.LastInsertRowId();
        }
    }

    public void UpdateTimer(int entryId, int seconds)
    {
        lock (_lock)
        {
            using var conn = Open();
            conn.Execute("UPDATE time_entries SET duration=@d WHERE id=@id",
                ("@d", seconds), ("@id", entryId));
        }
    }

    public void StopTimer(int entryId, int seconds)
    {
        lock (_lock)
        {
            using var conn = Open();
            conn.Execute(@"
                UPDATE time_entries SET is_active=0, end_time=@et, duration=@d WHERE id=@id",
                ("@et", DateTime.Now.ToString("o")), ("@d", seconds), ("@id", entryId));
        }
    }

    public TimeEntry? GetActiveEntry()
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT id,ticket_id,title,start_time,end_time,duration,is_active
                FROM time_entries WHERE is_active=1 LIMIT 1";
            return ReadEntries(cmd).FirstOrDefault();
        }
    }

    // ── Helpers internos ──────────────────────────────────────────────────────

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        return conn;
    }

    private static IncidentSnapshot RowToSnapshot(SqliteDataReader r)
    {
        int n = r.FieldCount;
        return new IncidentSnapshot
        {
            IncidentId = r.GetString(0),
            TicketNumber = r.GetString(1),
            Title = r.GetString(2),
            StateCode = r.GetInt32(3),
            StatusCode = r.GetInt32(4),
            PriorityCode = r.IsDBNull(5) ? null : r.GetInt32(5),
            CaseTypeCode = n > 6 && !r.IsDBNull(6) ? r.GetInt32(6) : null,
            ModifiedOn = DateTime.Parse(r.GetString(7)),
            FirstSeenAt = DateTime.Parse(r.GetString(8)),
            LastSeenAt = DateTime.Parse(r.GetString(9)),
            AlertCount = r.GetInt32(10),
            BzpNomeCliente = n > 11 && !r.IsDBNull(11) ? r.GetString(11) : null,
            BzpUrl = n > 12 && !r.IsDBNull(12) ? r.GetString(12) : null,
            BzHorasEsgotadas = n > 13 && r.GetInt32(13) == 1,
            BzSai = n > 14 && !r.IsDBNull(14) ? r.GetInt32(14) : null,
            BzMotivoStatus = n > 15 && !r.IsDBNull(15) ? r.GetString(15) : null,
            BzTotalHoras = n > 16 && !r.IsDBNull(16) ? r.GetDouble(16) : null,
            CustomerName = n > 17 && !r.IsDBNull(17) ? r.GetString(17) : null,
        };
    }

    private static List<TimeEntry> ReadEntries(SqliteCommand cmd)
    {
        var list = new List<TimeEntry>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new TimeEntry
            {
                Id = r.GetInt32(0),
                TicketId = r.GetString(1),
                Title = r.IsDBNull(2) ? "" : r.GetString(2),
                Start = DateTime.Parse(r.GetString(3)),
                End = r.IsDBNull(4) ? null : DateTime.Parse(r.GetString(4)),
                Seconds = r.GetInt32(5),
                IsActive = r.GetInt32(6) == 1,
            });
        return list;
    }

    private static string UtcNow() => DateTime.UtcNow.ToString("o");

    public void Dispose() { }
}

// ── Extension helpers ─────────────────────────────────────────────────────────

internal static class SqliteExtensions
{
    public static void Execute(this SqliteConnection conn, string sql,
        params (string name, object? value)[] parameters)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public static long LastInsertRowId(this SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT last_insert_rowid()";
        return (long)(cmd.ExecuteScalar() ?? 0L);
    }
}
