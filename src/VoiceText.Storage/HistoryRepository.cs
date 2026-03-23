// src/VoiceText.Storage/HistoryRepository.cs
using Microsoft.Data.Sqlite;

namespace VoiceText.Storage;

public class HistoryRepository : IHistoryRepository
{
    private readonly string _connectionString;

    public HistoryRepository(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
        InitDb();
    }

    private void InitDb()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                created_at TEXT NOT NULL,
                raw_text TEXT NOT NULL,
                polished_text TEXT,
                translated_text TEXT,
                language TEXT NOT NULL DEFAULT '',
                duration_ms REAL NOT NULL DEFAULT 0
            )
            """;
        cmd.ExecuteNonQuery();
    }

    public async Task<int> AddAsync(HistoryEntry entry)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO history (created_at, raw_text, polished_text, translated_text, language, duration_ms)
            VALUES (@created_at, @raw, @polished, @translated, @lang, @dur);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("@created_at", entry.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@raw", entry.RawText);
        cmd.Parameters.AddWithValue("@polished", (object?)entry.PolishedText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@translated", (object?)entry.TranslatedText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@lang", entry.Language);
        cmd.Parameters.AddWithValue("@dur", entry.DurationMs);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<IReadOnlyList<HistoryEntry>> GetRecentAsync(int limit = 50)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM history ORDER BY created_at DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@limit", limit);
        return await ReadEntries(cmd);
    }

    public async Task<IReadOnlyList<HistoryEntry>> SearchAsync(string query)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM history WHERE raw_text LIKE @q OR polished_text LIKE @q ORDER BY created_at DESC LIMIT 100";
        cmd.Parameters.AddWithValue("@q", $"%{query}%");
        return await ReadEntries(cmd);
    }

    public async Task DeleteAsync(int id)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM history WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<List<HistoryEntry>> ReadEntries(SqliteCommand cmd)
    {
        var list = new List<HistoryEntry>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new HistoryEntry(
                reader.GetInt32(0),
                DateTime.Parse(reader.GetString(1)),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetString(5),
                reader.GetDouble(6)));
        }
        return list;
    }
}
