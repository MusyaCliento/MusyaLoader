using System;
using System.Linq;
using Dapper;
using Microsoft.Data.Sqlite;

namespace SS14.Launcher.Models.Data.Migrations;

public sealed class Script0009_LoginSchemaCompat : Migrator.IMigrationScript
{
    private sealed class TableInfoRow
    {
        public string Name { get; set; } = "";
    }

    public string Up(SqliteConnection connection)
    {
        var loginColumns = connection
            .Query<TableInfoRow>("PRAGMA table_info(Login);")
            .Select(r => r.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var hasExpandedLogin = loginColumns.Contains("ModernHWId") || loginColumns.Contains("LegacyHWId");
        var sql = """
            CREATE TABLE IF NOT EXISTS LoginHwid (
                UserId TEXT PRIMARY KEY NOT NULL,
                ModernHWId TEXT,
                LegacyHWId TEXT
            );
            """;

        if (!hasExpandedLogin)
            return sql;

        sql += """

            INSERT OR REPLACE INTO LoginHwid (UserId, ModernHWId, LegacyHWId)
            SELECT UserId, COALESCE(ModernHWId, ''), COALESCE(LegacyHWId, '')
            FROM Login;

            ALTER TABLE Login RENAME TO Login_Legacy;

            CREATE TABLE Login (
                UserId TEXT PRIMARY KEY NOT NULL,
                UserName TEXT NOT NULL,
                Token TEXT NOT NULL,
                Expires DATETIME NOT NULL
            );

            INSERT INTO Login (UserId, UserName, Token, Expires)
            SELECT UserId, UserName, Token, Expires
            FROM Login_Legacy;

            DROP TABLE Login_Legacy;
            """;

        return sql;
    }
}
