using Dapper;
using Microsoft.Data.Sqlite;

namespace core_banking_lite.Infrastructure
{
    public sealed class SqliteDatabaseInitializer(string connectionString)
    {
        public async Task InitializeAsync()
        {
            await using var conn = new SqliteConnection(connectionString);
            await conn.OpenAsync();

            await conn.ExecuteAsync("""
                CREATE TABLE IF NOT EXISTS accounts (
                    id         INTEGER PRIMARY KEY,
                    balance    REAL    NOT NULL DEFAULT 0,
                    is_active  INTEGER NOT NULL DEFAULT 1,
                    created_at TEXT    NOT NULL,
                    updated_at TEXT
                );

                CREATE TABLE IF NOT EXISTS outbox_messages (
                    id         TEXT    PRIMARY KEY,
                    event_type TEXT    NOT NULL,
                    payload    TEXT    NOT NULL,
                    created_at TEXT    NOT NULL,
                    processed_at TEXT  NULL,
                    processed  INTEGER NOT NULL DEFAULT 0
                );

                INSERT OR IGNORE INTO accounts (id, balance, is_active, created_at)
                VALUES
                    (1234567890, 5000.00, 1, datetime('now')),
                    (9876543210, 1200.50, 1, datetime('now'));
                """);
        }
    }
}