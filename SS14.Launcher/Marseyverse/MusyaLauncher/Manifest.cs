using System.Collections.Generic;
using System.Text.Json.Serialization;


namespace SS14.Launcher.Models.ContentManagement
{
    public class Manifest
    {
        public string Version { get; set; } = "";
        public string Notes { get; set; } = "";
        public string ArchiveFileName { get; set; } = "";
        public List<ManifestFile> Files { get; set; } = new List<ManifestFile>();
    }



    public class ManifestFile
    {
        public string Path { get; set; } = "";
        public string FileName
        {
            get => Path;
            set => Path = value;
        }

        public string Version { get; set; } = "";
        public int Size { get; set; }
        public string Sha256 { get; set; } = "";
    }

}
