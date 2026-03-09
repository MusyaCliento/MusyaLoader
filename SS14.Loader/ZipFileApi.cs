using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Robust.LoaderApi;

namespace SS14.Loader;

internal sealed class ZipFileApi : IFileApi, IDisposable
{
    private readonly ZipArchive _archive;
    private readonly string? _prefix;

    public ZipFileApi(ZipArchive archive, string? prefix)
    {
        _archive = archive;
        _prefix = prefix;
    }

    public bool TryOpen(string path, [NotNullWhen(true)] out Stream? stream)
    {
        var entryName = _prefix != null ? _prefix + path : path;
        
        ZipArchiveEntry? entry;
        lock (_archive)
        {
            entry = _archive.GetEntry(entryName);
        }

        if (entry == null)
        {
            stream = null;
            return false;
        }

        var buffer = new byte[entry.Length];
        lock (_archive)
        {
            using var zipStream = entry.Open();
            int read;
            int offset = 0;
            while ((read = zipStream.Read(buffer, offset, buffer.Length - offset)) > 0)
            {
                offset += read;
            }
        }

        stream = new MemoryStream(buffer, writable: false);
        
        return true;
    }

    public IEnumerable<string> AllFiles
    {
        get
        {
            lock (_archive)
            {
                if (_prefix != null)
                {
                    return _archive.Entries
                        .Where(e => e.Name != "" && e.FullName.StartsWith(_prefix))
                        .Select(e => e.FullName[_prefix.Length..])
                        .ToList(); // Materialize to release lock
                }
                return _archive.Entries
                    .Where(e => e.Name != "")
                    .Select(entry => entry.FullName)
                    .ToList();
            }
        }
    }

    public void Dispose()
    {
        lock (_archive)
        {
            _archive.Dispose();
        }
    }
}