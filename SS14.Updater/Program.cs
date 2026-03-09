using System.Diagnostics;
using System.IO.Compression;

try
{
    var parsed = ParseArgs(args);
    if (!parsed.TryGetValue("--zip", out var zipPath) ||
        !parsed.TryGetValue("--target", out var targetDir) ||
        !parsed.TryGetValue("--launcher", out var launcherName))
    {
        Console.Error.WriteLine("Required args: --zip <path> --target <dir> --launcher <file>");
        return 2;
    }

    if (parsed.TryGetValue("--wait-pid", out var waitPidStr) &&
        int.TryParse(waitPidStr, out var waitPid))
    {
        try
        {
            using var proc = Process.GetProcessById(waitPid);
            proc.WaitForExit(30000);
        }
        catch
        {
            // Process already gone.
        }
    }

    if (!File.Exists(zipPath))
        throw new FileNotFoundException("Update archive does not exist.", zipPath);

    Directory.CreateDirectory(targetDir);

    var extractRoot = Path.Combine(Path.GetTempPath(), $"musyaloader_update_extract_{Guid.NewGuid():N}");
    Directory.CreateDirectory(extractRoot);
    ZipFile.ExtractToDirectory(zipPath, extractRoot);

    var sourceRoot = ResolveContentRoot(extractRoot, launcherName);
    CopyTree(sourceRoot, targetDir, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Marsey" });

    var launcherPath = Path.Combine(targetDir, launcherName);
    if (!File.Exists(launcherPath))
        throw new FileNotFoundException("Updated launcher executable was not found.", launcherPath);

    Process.Start(new ProcessStartInfo
    {
        FileName = launcherPath,
        UseShellExecute = true
    });

    return 0;
}
catch (Exception e)
{
    Console.Error.WriteLine(e);
    return 1;
}

static Dictionary<string, string> ParseArgs(string[] args)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length - 1; i += 2)
    {
        var key = args[i];
        var value = args[i + 1];
        if (!key.StartsWith("--", StringComparison.Ordinal))
            continue;
        result[key] = value;
    }

    return result;
}

static string ResolveContentRoot(string extractRoot, string launcherName)
{
    // Prefer directory that actually contains the launcher binary we're replacing.
    var foundLauncher = Directory
        .GetFiles(extractRoot, launcherName, SearchOption.AllDirectories)
        .FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(foundLauncher))
    {
        var dir = Path.GetDirectoryName(foundLauncher);
        if (!string.IsNullOrWhiteSpace(dir))
            return dir;
    }

    var dirs = Directory.GetDirectories(extractRoot);
    var files = Directory.GetFiles(extractRoot);
    if (dirs.Length == 1 && files.Length == 0)
        return dirs[0];

    return extractRoot;
}

static void CopyTree(string sourceRoot, string targetRoot, HashSet<string> excludedRootDirs)
{
    foreach (var dir in Directory.GetDirectories(sourceRoot, "*", SearchOption.AllDirectories))
    {
        var rel = Path.GetRelativePath(sourceRoot, dir);
        if (ShouldSkip(rel, excludedRootDirs))
            continue;

        Directory.CreateDirectory(Path.Combine(targetRoot, rel));
    }

    var selfPath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
    foreach (var file in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
    {
        var rel = Path.GetRelativePath(sourceRoot, file);
        if (ShouldSkip(rel, excludedRootDirs))
            continue;

        var dest = Path.Combine(targetRoot, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);

        if (string.Equals(selfPath, dest, StringComparison.OrdinalIgnoreCase))
        {
            // Can't overwrite running updater executable on Windows.
            // Stage replacement and let launcher finalize it on next startup.
            File.Copy(file, dest + ".pending", true);
            continue;
        }

        if (File.Exists(dest))
            File.SetAttributes(dest, FileAttributes.Normal);

        File.Copy(file, dest, true);
    }
}

static bool ShouldSkip(string relativePath, HashSet<string> excludedRootDirs)
{
    var first = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .FirstOrDefault() ?? "";
    return excludedRootDirs.Contains(first);
}
