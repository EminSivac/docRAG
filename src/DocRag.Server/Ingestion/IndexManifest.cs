using Microsoft.Data.Sqlite;

namespace DocRag.Server.Ingestion;

/// <summary>
/// Tracks one row per (file, embedding model) combination. Deliberately tiny: no vectors
/// or full text live here, only enough metadata to decide whether a file needs re-indexing.
/// The embedding model is part of the key so switching models in the settings
/// automatically forces a full re-index instead of silently skipping stale vectors.
/// </summary>
public sealed class IndexManifest
{
    private readonly string _connectionString;

    public IndexManifest(string dbPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _connectionString = $"Data Source={dbPath}";
        Initialize();
    }

    private void Initialize()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // Migration from the pre-embedding_model schema: the manifest is a disposable
        // cache of ingestion state, so it's safe to drop and let it be rebuilt on the
        // next run instead of hand-migrating old rows.
        if (TableExists(connection, "files") && !ColumnExists(connection, "files", "embedding_model"))
        {
            using var dropCmd = connection.CreateCommand();
            dropCmd.CommandText = "DROP TABLE files";
            dropCmd.ExecuteNonQuery();
        }

        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS files (
                file_path TEXT NOT NULL,
                embedding_model TEXT NOT NULL,
                content_hash TEXT NOT NULL,
                last_modified_utc TEXT NOT NULL,
                chunk_count INTEGER NOT NULL,
                indexed_at_utc TEXT NOT NULL,
                PRIMARY KEY (file_path, embedding_model)
            );
            """;
        cmd.ExecuteNonQuery();
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = @name";
        cmd.Parameters.AddWithValue("@name", tableName);
        return cmd.ExecuteScalar() is not null;
    }

    private static bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({tableName})";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public FileRecord? GetRecord(string filePath, string embeddingModel)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT content_hash, last_modified_utc, chunk_count
            FROM files WHERE file_path = @path AND embedding_model = @model
            """;
        cmd.Parameters.AddWithValue("@path", filePath);
        cmd.Parameters.AddWithValue("@model", embeddingModel);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new FileRecord(
            reader.GetString(0),
            DateTime.Parse(reader.GetString(1)),
            reader.GetInt32(2));
    }

    public void Upsert(string filePath, string embeddingModel, string contentHash, DateTime lastModifiedUtc, int chunkCount)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO files (file_path, embedding_model, content_hash, last_modified_utc, chunk_count, indexed_at_utc)
            VALUES (@path, @model, @hash, @modified, @count, @now)
            ON CONFLICT(file_path, embedding_model) DO UPDATE SET
                content_hash = excluded.content_hash,
                last_modified_utc = excluded.last_modified_utc,
                chunk_count = excluded.chunk_count,
                indexed_at_utc = excluded.indexed_at_utc;
            """;
        cmd.Parameters.AddWithValue("@path", filePath);
        cmd.Parameters.AddWithValue("@model", embeddingModel);
        cmd.Parameters.AddWithValue("@hash", contentHash);
        cmd.Parameters.AddWithValue("@modified", lastModifiedUtc.ToString("O"));
        cmd.Parameters.AddWithValue("@count", chunkCount);
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public List<string> GetAllTrackedPaths(string embeddingModel)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT file_path FROM files WHERE embedding_model = @model";
        cmd.Parameters.AddWithValue("@model", embeddingModel);

        var results = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            results.Add(reader.GetString(0));
        }

        return results;
    }

    public void ClearAll()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM files";
        cmd.ExecuteNonQuery();
    }

    public void Remove(string filePath, string embeddingModel)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM files WHERE file_path = @path AND embedding_model = @model";
        cmd.Parameters.AddWithValue("@path", filePath);
        cmd.Parameters.AddWithValue("@model", embeddingModel);
        cmd.ExecuteNonQuery();
    }
}

public sealed record FileRecord(string ContentHash, DateTime LastModifiedUtc, int ChunkCount);
