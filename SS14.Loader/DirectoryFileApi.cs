using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Robust.LoaderApi;

namespace SS14.Loader;

internal sealed class DirectoryFileApi : IFileApi
{
    private readonly string _root;
    private readonly string? _prefix;

    public DirectoryFileApi(string root, string? prefix)
    {
        _root = root;
        _prefix = string.IsNullOrWhiteSpace(prefix) ? null : prefix.Replace('/', Path.DirectorySeparatorChar);
    }

    public bool TryOpen(string path, [NotNullWhen(true)] out Stream? stream)
    {
        var full = ResolvePath(path);
        if (!File.Exists(full))
        {
            stream = null;
            return false;
        }

        stream = File.OpenRead(full);
        return true;
    }

    public IEnumerable<string> AllFiles
    {
        get
        {
            var baseDir = ResolveBaseDir();
            if (!Directory.Exists(baseDir))
                return Array.Empty<string>();

            return Directory.EnumerateFiles(baseDir, "*", SearchOption.AllDirectories)
                .Select(p => Path.GetRelativePath(baseDir, p).Replace('\\', '/'))
                .ToList();
        }
    }

    private string ResolveBaseDir()
    {
        return _prefix == null ? _root : Path.Combine(_root, _prefix);
    }

    private string ResolvePath(string path)
    {
        var baseDir = ResolveBaseDir();
        return Path.Combine(baseDir, path);
    }
}
