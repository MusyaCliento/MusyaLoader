using System;
using System.Linq;
using Dapper;
using Microsoft.Data.Sqlite;

namespace SS14.Launcher.Models.Data.Migrations;

public sealed class Script0007_AddHwidsToLogin : Migrator.IMigrationScript
{
    private sealed class TableInfoRow
    {
        public string Name { get; set; } = "";
    }

    public string Up(SqliteConnection connection)
    {
        var columns = connection
            .Query<TableInfoRow>("PRAGMA table_info(Login);")
            .Select(r => r.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var sql = "";

        if (!columns.Contains("ModernHWId"))
            sql += "ALTER TABLE Login ADD COLUMN ModernHWId TEXT;\n";

        if (!columns.Contains("LegacyHWId"))
            sql += "ALTER TABLE Login ADD COLUMN LegacyHWId TEXT;\n";

        if (string.IsNullOrWhiteSpace(sql))
            return "SELECT 1;";

        return sql;
    }
}
