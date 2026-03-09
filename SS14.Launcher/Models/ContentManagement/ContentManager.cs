using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Serilog;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Utility;

namespace SS14.Launcher.Models.ContentManagement;

public sealed class ContentManager
{
    private static bool _initialized = false;

    public void Initialize()
    {
        if (_initialized)
            return;

        using var con = GetSqliteConnection();

        con.Execute("PRAGMA journal_mode=WAL");
        con.Execute("PRAGMA synchronous=OFF");
        con.Execute("PRAGMA mmap_size=268435456");

        Log.Debug("Migrating content database...");

        var sw = Stopwatch.StartNew();
        var success = Migrator.Migrate(con, "SS14.Launcher.Models.ContentManagement.Migrations");
        if (!success)
            throw new Exception("Migrations failed!");

        Log.Debug("Did content DB migrations in {MigrationTime}", sw.Elapsed);
        _initialized = true;
    }

    public async Task<bool> ClearAll()
    {
        return await Task.Run(() =>
        {
            try
            {
                using var con = GetSqliteConnection();
                using var transact = con.BeginTransaction(deferred: true);

                if (GetRunningClientVersions(con).Count > 0)
                {
                    transact.Commit();
                    return false;
                }

                con.Execute("DELETE FROM InterruptedDownload");
                con.Execute("DELETE FROM ContentVersion");
                con.Execute("DELETE FROM Content");
                transact.Commit();

                con.Execute("VACUUM");
                return true;
            }
            catch (Exception e)
            {
                Log.Error(e, "Error while truncating content DB!");
                return true;
            }
        });
    }

    public static Stream? OpenBlob(SqliteConnection con, long versionId, string fileName)
    {
        var (manifestRowId, manifestCompression) = con.QueryFirstOrDefault<(long id, ContentCompressionScheme compression)>(
            @"SELECT c.ROWID, c.Compression FROM ContentManifest cm, Content c
            WHERE Path = @FileName AND VersionId = @Version AND c.Id = cm.ContentId",
            new
            {
                Version = versionId,
                FileName = fileName
            });

        if (manifestRowId == 0)
            return null;

        var blob = new SqliteBlob(con, "Content", "Data", manifestRowId, readOnly: true);

        switch (manifestCompression)
        {
            case ContentCompressionScheme.None:
                return blob;

            case ContentCompressionScheme.Deflate:
                return new DeflateStream(blob, CompressionMode.Decompress);

            case ContentCompressionScheme.ZStd:
                return new ZStdDecompressStream(blob);

            default:
                throw new InvalidDataException("Unknown compression scheme in ContentDB!");
        }
    }

    public static SqliteConnection GetSqliteConnection()
    {
        var con = new SqliteConnection(GetContentDbConnectionString());
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "PRAGMA mmap_size=268435456; PRAGMA cache_size=-32768;";
        cmd.ExecuteNonQuery();
        return con;
    }

    private static string GetContentDbConnectionString()
    {
        return $"Data Source={LauncherPaths.PathContentDb};Mode=ReadWriteCreate;Pooling=False;Foreign Keys=True";
    }

    internal static List<long> GetRunningClientVersions(SqliteConnection con)
    {
        var running = new List<long>();
        var toRemove = new List<int>();

        var dbRunning = con.Query<(int, string, long)>("SELECT ProcessId, MainModule, UsedVersion FROM RunningClient");

        foreach (var (pid, mainModule, usedVersion) in dbRunning)
        {
            if (IsProcessStillRunning(pid, mainModule))
                running.Add(usedVersion);
            else
                toRemove.Add(pid);
        }

        foreach (var pid in toRemove)
        {
            Log.Debug("Removing died client {Pid} from RunningClient", pid);
            con.Execute("DELETE FROM RunningClient WHERE ProcessId = @ProcessId", new { ProcessId = pid });
        }

        return running;
    }

    private static bool IsProcessStillRunning(int pid, string mainModule)
    {
        Process proc;
        try { proc = Process.GetProcessById(pid); }
        catch (ArgumentException) { return false; }
        return proc.MainModule?.FileName == mainModule;
    }
}