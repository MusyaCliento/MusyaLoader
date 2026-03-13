using System;
using System.IO;

namespace SS14.Launcher.Marseyverse.Engines;

public sealed class CustomEngineInfo
{
    public CustomEngineInfo(
        string sourcePath,
        string name,
        string description,
        string iconPath,
        string? clientZipPath,
        string? signature)
    {
        SourcePath = sourcePath;
        Name = name;
        Description = description;
        IconPath = iconPath;
        ClientZipPath = clientZipPath;
        Signature = signature;
    }

    public string SourcePath { get; }
    public string Name { get; }
    public string Description { get; }
    public string IconPath { get; }
    public string? ClientZipPath { get; }
    public string? Signature { get; }

    public bool Enabled { get; set; }

    public bool CanUse =>
        !string.IsNullOrWhiteSpace(ClientZipPath) &&
        (File.Exists(ClientZipPath) || Directory.Exists(ClientZipPath));
}
