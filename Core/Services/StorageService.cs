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
        _dbPath = Path.IsPathRooted(cfg.Database.Path)
            ? cfg.Database.Path
            : Path.Combine(D365Assistant.App.DataDir, cfg.Database.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath) ?? D365Assistant.App.DataDir);
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
                customer_name      TEXT,
                bz_status_kpi_first    INTEGER,
                bz_status_kpi_resolveby INTEGER,
                created_on             TEXT,
                customer_satisfaction_code INTEGER,
                bz_first_response_date INTEGER DEFAULT 0
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
            CREATE TABLE IF NOT EXISTS todos (
                id             INTEGER PRIMARY KEY AUTOINCREMENT,
                title          TEXT NOT NULL,
                description    TEXT DEFAULT '',
                category       TEXT DEFAULT 'Geral',
                priority       INTEGER DEFAULT 2,
                done           INTEGER DEFAULT 0,
                created_at     TEXT NOT NULL,
                due_date       TEXT,
                done_at        TEXT,
                ticket_id      TEXT,
                kanban_status  TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_todos_done ON todos(done);
            CREATE TABLE IF NOT EXISTS notes (
                id           INTEGER PRIMARY KEY AUTOINCREMENT,
                title        TEXT NOT NULL DEFAULT 'Nova nota',
                content      TEXT NOT NULL DEFAULT '',
                incident_id  TEXT,
                incident_title TEXT,
                ticket_number  TEXT,
                color        TEXT NOT NULL DEFAULT '#1E2530',
                created_at   TEXT NOT NULL,
                updated_at   TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_notes_incident ON notes(incident_id);
        ");

        // ── Migrações incrementais ────────────────────────────────────────────
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
            "ALTER TABLE time_entries ADD COLUMN description TEXT DEFAULT ''",
            "ALTER TABLE incidents ADD COLUMN bz_status_kpi_first INTEGER",
            "ALTER TABLE incidents ADD COLUMN bz_status_kpi_resolveby INTEGER",
            "ALTER TABLE incidents ADD COLUMN created_on TEXT",
            "ALTER TABLE incidents ADD COLUMN customer_satisfaction_code INTEGER",
            "ALTER TABLE incidents ADD COLUMN bz_first_response_date INTEGER DEFAULT 0",
            // ── Kanban ────────────────────────────────────────────────────────
            "ALTER TABLE todos ADD COLUMN kanban_status TEXT NULL",
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
                        bz_total_horas,customer_name,bz_status_kpi_first,bz_status_kpi_resolveby,
                        created_on,customer_satisfaction_code,bz_first_response_date)
                    VALUES (@id,@tn,@t,@sc,@stc,@pc,@ctc,@mod,@first,@last,0,
                        @bnc,@burl,@bhe,@bsai,@bms,@bth,@cn,@kpif,@kpir,@creon,@csat,@bfrd)
                    ON CONFLICT(incident_id) DO UPDATE SET
                        ticket_number=excluded.ticket_number, title=excluded.title,
                        state_code=excluded.state_code, status_code=excluded.status_code,
                        priority_code=excluded.priority_code, case_type_code=excluded.case_type_code,
                        modified_on=excluded.modified_on, last_seen_at=excluded.last_seen_at,
                        bzp_nome_cliente=excluded.bzp_nome_cliente, bzp_url=excluded.bzp_url,
                        bz_horas_esgotadas=excluded.bz_horas_esgotadas, bz_sai=excluded.bz_sai,
                        bz_motivo_status=excluded.bz_motivo_status, bz_total_horas=excluded.bz_total_horas,
                        customer_name=excluded.customer_name,
                        bz_status_kpi_first=excluded.bz_status_kpi_first,
                        bz_status_kpi_resolveby=excluded.bz_status_kpi_resolveby,
                        created_on=excluded.created_on,
                        customer_satisfaction_code=excluded.customer_satisfaction_code,
                        bz_first_response_date=excluded.bz_first_response_date";

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
                upsert.Parameters.AddWithValue("@kpif", (object?)inc.BzStatusKpiFirst ?? DBNull.Value);
                upsert.Parameters.AddWithValue("@kpir", (object?)inc.BzStatusKpiResolveby ?? DBNull.Value);
                upsert.Parameters.AddWithValue("@creon", inc.CreatedOn == default ? DBNull.Value : inc.CreatedOn.ToString("o"));
                upsert.Parameters.AddWithValue("@csat", (object?)inc.CustomerSatisfactionCode ?? DBNull.Value);
                upsert.Parameters.AddWithValue("@bfrd", inc.BzFirstResponseDate ? 1 : 0);
                upsert.ExecuteNonQuery();
            }
            tx.Commit();
        }
    }

    public int MarkClosedExcept(IEnumerable<string> activeIncidentIds)
    {
        var ids = activeIncidentIds.ToHashSet();
        lock (_lock)
        {
            using var conn = Open();
            using var tx = conn.BeginTransaction();

            using var sel = conn.CreateCommand();
            sel.Transaction = tx;
            sel.CommandText = "SELECT incident_id FROM incidents WHERE state_code=0";
            var inDb = new List<string>();
            using (var r = sel.ExecuteReader())
                while (r.Read()) inDb.Add(r.GetString(0));

            var toClose = inDb.Where(id => !ids.Contains(id)).ToList();
            foreach (var id in toClose)
            {
                using var upd = conn.CreateCommand();
                upd.Transaction = tx;
                upd.CommandText = "UPDATE incidents SET state_code=1, status_code=5, last_seen_at=@now WHERE incident_id=@id";
                upd.Parameters.AddWithValue("@now", UtcNow());
                upd.Parameters.AddWithValue("@id", id);
                upd.ExecuteNonQuery();
            }

            tx.Commit();
            return toClose.Count;
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
                       bz_motivo_status,bz_total_horas,customer_name,
                       bz_status_kpi_first,bz_status_kpi_resolveby,created_on,
                       customer_satisfaction_code,bz_first_response_date
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
                       bz_motivo_status,bz_total_horas,customer_name,
                       bz_status_kpi_first,bz_status_kpi_resolveby,created_on,
                       customer_satisfaction_code,bz_first_response_date
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

            conn.Execute(@"
                DELETE FROM alert_history
                WHERE id IN (
                    SELECT id FROM alert_history
                    ORDER BY fired_at DESC
                    LIMIT -1 OFFSET 100
                )");
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
                SELECT id,ticket_id,title,start_time,end_time,duration,is_active,COALESCE(description,'')
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

    public int StartTimer(string ticketId, string title, string description = "")
    {
        lock (_lock)
        {
            using var conn = Open();
            conn.Execute("UPDATE time_entries SET is_active=0 WHERE is_active=1");
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO time_entries (ticket_id,title,description,start_time,duration,is_active)
                VALUES (@t,@ti,@desc,@st,0,1)";
            cmd.Parameters.AddWithValue("@t", ticketId);
            cmd.Parameters.AddWithValue("@ti", title);
            cmd.Parameters.AddWithValue("@desc", description);
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

    public void UpdateDescription(int entryId, string description)
    {
        lock (_lock)
        {
            using var conn = Open();
            conn.Execute("UPDATE time_entries SET description=@desc WHERE id=@id",
                ("@desc", description), ("@id", entryId));
        }
    }

    public TimeEntry? GetActiveEntry()
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT id,ticket_id,title,start_time,end_time,duration,is_active,COALESCE(description,'')
                FROM time_entries WHERE is_active=1 LIMIT 1";
            return ReadEntries(cmd).FirstOrDefault();
        }
    }

    public List<TimeEntry> GetEntriesByPeriod(DateTime from, DateTime to)
    {
        var fromStr = from.ToString("yyyy-MM-dd");
        var toStr = to.ToString("yyyy-MM-dd");
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT id,ticket_id,title,start_time,end_time,duration,is_active,COALESCE(description,'')
                FROM time_entries
                WHERE date(start_time) >= @from AND date(start_time) <= @to
                ORDER BY start_time";
            cmd.Parameters.AddWithValue("@from", fromStr);
            cmd.Parameters.AddWithValue("@to", toStr);
            return ReadEntries(cmd);
        }
    }

    public List<TimeEntry> GetAllEntries()
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT id,ticket_id,title,start_time,end_time,duration,is_active,COALESCE(description,'')
                FROM time_entries ORDER BY start_time";
            return ReadEntries(cmd);
        }
    }

    // ── TODO CRUD ─────────────────────────────────────────────────────────────

    public List<Core.Models.Todo.TodoItem> GetAllTodos()
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT id,title,description,category,priority,done,
                       created_at,due_date,done_at,ticket_id,
                       kanban_status
                FROM todos
                ORDER BY done ASC, priority ASC, created_at DESC";
            return ReadTodos(cmd);
        }
    }

    public Core.Models.Todo.TodoItem SaveTodo(Core.Models.Todo.TodoItem item)
    {
        lock (_lock)
        {
            using var conn = Open();
            if (item.Id == 0)
            {
                conn.Execute(@"
                    INSERT INTO todos
                        (title,description,category,priority,done,
                         created_at,due_date,done_at,ticket_id,kanban_status)
                    VALUES
                        (@title,@desc,@cat,@pri,@done,
                         @created,@due,@doneat,@ticket,@kanban)",
                    ("@title", item.Title),
                    ("@desc", item.Description),
                    ("@cat", item.Category),
                    ("@pri", item.Priority),
                    ("@done", item.Done ? 1 : 0),
                    ("@created", item.CreatedAt.ToString("o")),
                    ("@due", item.DueDate?.ToString("o")),
                    ("@doneat", item.DoneAt?.ToString("o")),
                    ("@ticket", item.TicketId),
                    ("@kanban", item.KanbanStatus));
                item.Id = (int)conn.LastInsertRowId();
            }
            else
            {
                conn.Execute(@"
                    UPDATE todos SET
                        title=@title, description=@desc, category=@cat,
                        priority=@pri, done=@done, due_date=@due,
                        done_at=@doneat, ticket_id=@ticket,
                        kanban_status=@kanban
                    WHERE id=@id",
                    ("@title", item.Title),
                    ("@desc", item.Description),
                    ("@cat", item.Category),
                    ("@pri", item.Priority),
                    ("@done", item.Done ? 1 : 0),
                    ("@due", item.DueDate?.ToString("o")),
                    ("@doneat", item.DoneAt?.ToString("o")),
                    ("@ticket", item.TicketId),
                    ("@kanban", item.KanbanStatus),
                    ("@id", item.Id));
            }
            return item;
        }
    }

    public void DeleteTodo(int id)
    {
        lock (_lock)
        {
            using var conn = Open();
            conn.Execute("DELETE FROM todos WHERE id=@id", ("@id", id));
        }
    }

    public void ToggleTodo(int id, bool done)
    {
        lock (_lock)
        {
            using var conn = Open();
            var doneAt = done ? DateTime.Now.ToString("o") : (object)DBNull.Value;
            conn.Execute("UPDATE todos SET done=@done, done_at=@doneat WHERE id=@id",
                ("@done", done ? 1 : 0),
                ("@doneat", doneAt),
                ("@id", id));
        }
    }

    // ── Notes CRUD ────────────────────────────────────────────────────────────

    public List<Core.Models.Notes.Note> GetAllNotes()
    {
        lock (_lock)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id,title,content,incident_id,incident_title,ticket_number,color,created_at,updated_at FROM notes ORDER BY updated_at DESC";
            return ReadNotes(cmd);
        }
    }

    public Core.Models.Notes.Note SaveNote(Core.Models.Notes.Note note)
    {
        lock (_lock)
        {
            using var conn = Open();
            note.UpdatedAt = DateTime.Now;
            if (note.Id == 0)
            {
                note.CreatedAt = DateTime.Now;
                conn.Execute(@"
                    INSERT INTO notes (title,content,incident_id,incident_title,ticket_number,color,created_at,updated_at)
                    VALUES (@ti,@co,@inc,@inctitle,@tkt,@color,@ca,@ua)",
                    ("@ti", note.Title),
                    ("@co", note.Content),
                    ("@inc", (object?)note.IncidentId ?? DBNull.Value),
                    ("@inctitle", (object?)note.IncidentTitle ?? DBNull.Value),
                    ("@tkt", (object?)note.TicketNumber ?? DBNull.Value),
                    ("@color", note.Color),
                    ("@ca", note.CreatedAt.ToString("o")),
                    ("@ua", note.UpdatedAt.ToString("o")));
                note.Id = (int)conn.LastInsertRowId();
            }
            else
            {
                conn.Execute(@"
                    UPDATE notes SET title=@ti,content=@co,incident_id=@inc,incident_title=@inctitle,
                        ticket_number=@tkt,color=@color,updated_at=@ua WHERE id=@id",
                    ("@ti", note.Title),
                    ("@co", note.Content),
                    ("@inc", (object?)note.IncidentId ?? DBNull.Value),
                    ("@inctitle", (object?)note.IncidentTitle ?? DBNull.Value),
                    ("@tkt", (object?)note.TicketNumber ?? DBNull.Value),
                    ("@color", note.Color),
                    ("@ua", note.UpdatedAt.ToString("o")),
                    ("@id", note.Id));
            }
            return note;
        }
    }

    public void DeleteNote(int id)
    {
        lock (_lock)
        {
            using var conn = Open();
            conn.Execute("DELETE FROM notes WHERE id=@id", ("@id", id));
        }
    }

    // ── Helpers privados ──────────────────────────────────────────────────────

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
            BzStatusKpiFirst = n > 18 && !r.IsDBNull(18) ? r.GetInt32(18) : null,
            BzStatusKpiResolveby = n > 19 && !r.IsDBNull(19) ? r.GetInt32(19) : null,
            CreatedOn = n > 20 && !r.IsDBNull(20)
                                           ? DateTime.Parse(r.GetString(20))
                                           : default,
            CustomerSatisfactionCode = n > 21 && !r.IsDBNull(21) ? r.GetInt32(21) : null,
            BzFirstResponseDate = n > 22 && !r.IsDBNull(22) && r.GetInt32(22) == 1,
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

    private static List<Core.Models.Todo.TodoItem> ReadTodos(SqliteCommand cmd)
    {
        var list = new List<Core.Models.Todo.TodoItem>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new Core.Models.Todo.TodoItem
            {
                Id = r.GetInt32(r.GetOrdinal("id")),
                Title = r.GetString(r.GetOrdinal("title")),
                Description = r.IsDBNull(r.GetOrdinal("description")) ? "" : r.GetString(r.GetOrdinal("description")),
                Category = r.IsDBNull(r.GetOrdinal("category")) ? "Geral" : r.GetString(r.GetOrdinal("category")),
                Priority = r.GetInt32(r.GetOrdinal("priority")),
                Done = r.GetInt32(r.GetOrdinal("done")) == 1,
                CreatedAt = DateTime.Parse(r.GetString(r.GetOrdinal("created_at"))),
                DueDate = r.IsDBNull(r.GetOrdinal("due_date")) ? null : DateTime.Parse(r.GetString(r.GetOrdinal("due_date"))),
                DoneAt = r.IsDBNull(r.GetOrdinal("done_at")) ? null : DateTime.Parse(r.GetString(r.GetOrdinal("done_at"))),
                TicketId = r.IsDBNull(r.GetOrdinal("ticket_id")) ? null : r.GetString(r.GetOrdinal("ticket_id")),
                KanbanStatus = r.IsDBNull(r.GetOrdinal("kanban_status")) ? null : r.GetString(r.GetOrdinal("kanban_status")),
            });
        }
        return list;
    }

    private static List<Core.Models.Notes.Note> ReadNotes(SqliteCommand cmd)
    {
        var list = new List<Core.Models.Notes.Note>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new Core.Models.Notes.Note
            {
                Id = r.GetInt32(0),
                Title = r.IsDBNull(1) ? "Nova nota" : r.GetString(1),
                Content = r.IsDBNull(2) ? "" : r.GetString(2),
                IncidentId = r.IsDBNull(3) ? null : r.GetString(3),
                IncidentTitle = r.IsDBNull(4) ? null : r.GetString(4),
                TicketNumber = r.IsDBNull(5) ? null : r.GetString(5),
                Color = r.IsDBNull(6) ? "#1E2530" : r.GetString(6),
                CreatedAt = DateTime.Parse(r.GetString(7)),
                UpdatedAt = DateTime.Parse(r.GetString(8)),
            });
        return list;
    }

    private static string UtcNow() => DateTime.UtcNow.ToString("o");

    public void Dispose() { }
}

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