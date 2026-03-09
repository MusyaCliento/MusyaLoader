using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace SS14.Launcher.Utility
{
    public class AppConfig
    {
        public List<string> RecentFiles { get; set; } = new();
        public int SomeIntValue { get; set; } = 0;
        public float SomeFloatValue { get; set; } = 0.0f;
        public string UserName { get; set; } = "";
        public string UserPassword { get; set; } = "";
        public string ApiUrl { get; set; } = "";
        public int ApiPort { get; set; } = 5000;

        // ----- Instance-backed property which WILL be serialized as "ApiIpList" -----
        [JsonPropertyName("ApiIpList")]
        public string[] ApiIpListData { get; set; } = Array.Empty<string>();

        // Static proxy (backwards-compatible): AppConfig.ApiIpList -> Instance.ApiIpListData
        public static string[] ApiIpList
        {
            get => Instance?.ApiIpListData ?? Array.Empty<string>();
            private set
            {
                if (Instance != null)
                    Instance.ApiIpListData = value ?? Array.Empty<string>();
            }
        }

        // Singleton instance used for saving/loading
        public static AppConfig Instance { get; private set; } = new AppConfig();

        private const string ConfigFileName = "config.json";

        private static string? TryGetSpecialFolder(Environment.SpecialFolder sf)
        {
            try
            {
                var p = Environment.GetFolderPath(sf);
                if (!string.IsNullOrWhiteSpace(p))
                    return p;
            }
            catch { }
            return null;
        }

        private static string GetConfigDirectory()
        {
            string? dir = TryGetSpecialFolder(Environment.SpecialFolder.ApplicationData)
                          ?? TryGetSpecialFolder(Environment.SpecialFolder.LocalApplicationData)
                          ?? TryGetSpecialFolder(Environment.SpecialFolder.Personal);

            if (string.IsNullOrWhiteSpace(dir))
            {
                // Платформенный fallback
                try
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("ANDROID")) ||
                        RuntimeInformation.IsOSPlatform(OSPlatform.Create("BROWSER")))
                    {
                        dir = AppContext.BaseDirectory;
                    }
                }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(dir))
            {
                dir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
            }

            var appDir = Path.Combine(dir, "MusyaApp");
            if (!Directory.Exists(appDir))
                Directory.CreateDirectory(appDir);
            return appDir;
        }

        private static string GetConfigPath()
            => Path.Combine(GetConfigDirectory(), ConfigFileName);

        // ---------------------------
        // Синхронный API (без deadlock)
        // ---------------------------
        public static AppConfig Load()
        {
            var path = GetConfigPath();
            if (!File.Exists(path))
            {
                Instance = new AppConfig();
                return Instance;
            }

            try
            {
                var json = File.ReadAllText(path);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });
                Instance = cfg ?? new AppConfig();
                return Instance;
            }
            catch
            {
                Instance = new AppConfig();
                return Instance;
            }
        }

        public void Save()
        {
            var path = GetConfigPath();
            var tmp = path + ".tmp";

            var options = new JsonSerializerOptions { WriteIndented = true };

            try
            {
                // Сериализуем текущий экземпляр (Instance)
                var json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(tmp, json);

                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(tmp, path, null);
                    }
                    catch
                    {
                        // fallback
                        File.Delete(path);
                        File.Move(tmp, path);
                    }
                }
                else
                {
                    File.Move(tmp, path);
                }
            }
            catch
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                throw;
            }
        }

        // Static helper to update IP list and persist it
        public static void SetIpList(string[] ips)
        {
            // Normalize
            var arr = (ips ?? Array.Empty<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            // Update proxy property -> this writes into Instance.ApiIpListData
            ApiIpList = arr;

            // Persist instance
            Instance.Save();
        }

        // ---------------------------
        // Асинхронный API (опционально)
        // ---------------------------
        public static async Task<AppConfig> LoadAsync()
        {
            var path = GetConfigPath();
            if (!File.Exists(path))
            {
                Instance = new AppConfig();
                return Instance;
            }

            try
            {
                await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var cfg = await JsonSerializer.DeserializeAsync<AppConfig>(fs, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });
                Instance = cfg ?? new AppConfig();
                return Instance;
            }
            catch
            {
                Instance = new AppConfig();
                return Instance;
            }
        }

        public async Task SaveAsync()
        {
            var path = GetConfigPath();
            var tmp = path + ".tmp";

            var options = new JsonSerializerOptions { WriteIndented = true };

            try
            {
                await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await JsonSerializer.SerializeAsync(fs, this, options);
                    await fs.FlushAsync();
                }

                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(tmp, path, null);
                    }
                    catch
                    {
                        File.Delete(path);
                        File.Move(tmp, path);
                    }
                }
                else
                {
                    File.Move(tmp, path);
                }
            }
            catch
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                throw;
            }
        }
    }
}
