using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LazyChat.Services
{
    /// <summary>
    /// Unified application settings - stored in settings.json
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// Unique identifier for this peer, persisted across restarts
        /// </summary>
        [JsonPropertyName("peerId")]
        public string PeerId { get; set; }

        /// <summary>
        /// User display name
        /// </summary>
        [JsonPropertyName("userName")]
        public string UserName { get; set; }

        /// <summary>
        /// Send message shortcut: true = Enter to send, false = Ctrl+Enter to send
        /// </summary>
        [JsonPropertyName("enterToSend")]
        public bool EnterToSend { get; set; } = true;

        /// <summary>
        /// Settings file version for future migrations
        /// </summary>
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;
    }

    /// <summary>
    /// Centralized settings manager - single source of truth for all app configuration
    /// </summary>
    public static class SettingsManager
    {
        private static readonly object _lock = new object();
        private static AppSettings _cachedSettings;
        private static string _settingsPath;

        public static string SettingsDirectory
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LazyChat");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string SettingsFilePath
        {
            get
            {
                if (_settingsPath == null)
                    _settingsPath = Path.Combine(SettingsDirectory, "settings.json");
                return _settingsPath;
            }
        }

        /// <summary>
        /// Load settings from disk (or create default if not exists)
        /// </summary>
        public static AppSettings Load()
        {
            lock (_lock)
            {
                if (_cachedSettings != null)
                    return _cachedSettings;

                try
                {
                    if (File.Exists(SettingsFilePath))
                    {
                        string json = File.ReadAllText(SettingsFilePath);
                        _cachedSettings = JsonSerializer.Deserialize<AppSettings>(json);
                        
                        // Ensure PeerId exists (migration from old versions)
                        if (string.IsNullOrEmpty(_cachedSettings.PeerId))
                        {
                            _cachedSettings.PeerId = GenerateNewPeerId();
                            Save(_cachedSettings);
                        }
                        
                        // Migrate from old config.txt if username is empty
                        if (string.IsNullOrEmpty(_cachedSettings.UserName))
                        {
                            _cachedSettings.UserName = MigrateOldUsername();
                            if (!string.IsNullOrEmpty(_cachedSettings.UserName))
                                Save(_cachedSettings);
                        }
                        
                        // Migrate from old local_peer_id.txt
                        MigrateOldPeerId();
                        
                        return _cachedSettings;
                    }
                }
                catch
                {
                    // If parsing fails, create new settings
                }

                // Create default settings
                _cachedSettings = new AppSettings
                {
                    PeerId = MigrateOldPeerId() ?? GenerateNewPeerId(),
                    UserName = MigrateOldUsername(),
                    EnterToSend = true,
                    Version = 1
                };
                
                Save(_cachedSettings);
                CleanupOldFiles();
                
                return _cachedSettings;
            }
        }

        /// <summary>
        /// Save settings to disk
        /// </summary>
        public static void Save(AppSettings settings)
        {
            lock (_lock)
            {
                try
                {
                    _cachedSettings = settings;
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(settings, options);
                    File.WriteAllText(SettingsFilePath, json);
                }
                catch
                {
                    // Silently fail - settings will use cached values
                }
            }
        }

        /// <summary>
        /// Update a single setting
        /// </summary>
        public static void Update(Action<AppSettings> updateAction)
        {
            lock (_lock)
            {
                var settings = Load();
                updateAction(settings);
                Save(settings);
            }
        }

        /// <summary>
        /// Get or create a persistent PeerId
        /// </summary>
        public static string GetPeerId()
        {
            return Load().PeerId;
        }

        /// <summary>
        /// Get or set username
        /// </summary>
        public static string GetUserName()
        {
            return Load().UserName;
        }

        public static void SetUserName(string userName)
        {
            Update(s => s.UserName = userName);
        }

        /// <summary>
        /// Get or set enter to send preference
        /// </summary>
        public static bool GetEnterToSend()
        {
            return Load().EnterToSend;
        }

        public static void SetEnterToSend(bool value)
        {
            Update(s => s.EnterToSend = value);
        }

        private static string GenerateNewPeerId()
        {
            return Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Migrate from old config.txt
        /// </summary>
        private static string MigrateOldUsername()
        {
            try
            {
                string oldPath = Path.Combine(SettingsDirectory, "config.txt");
                if (File.Exists(oldPath))
                {
                    return File.ReadAllText(oldPath).Trim();
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Migrate from old local_peer_id.txt
        /// </summary>
        private static string MigrateOldPeerId()
        {
            try
            {
                string oldPath = Path.Combine(SettingsDirectory, "local_peer_id.txt");
                if (File.Exists(oldPath))
                {
                    string peerId = File.ReadAllText(oldPath).Trim();
                    if (!string.IsNullOrEmpty(peerId) && Guid.TryParse(peerId, out _))
                    {
                        return peerId;
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Clean up old config files after migration
        /// </summary>
        private static void CleanupOldFiles()
        {
            try
            {
                string[] oldFiles = { "config.txt", "local_peer_id.txt" };
                foreach (var file in oldFiles)
                {
                    string path = Path.Combine(SettingsDirectory, file);
                    if (File.Exists(path))
                        File.Delete(path);
                }
            }
            catch { }
        }

        /// <summary>
        /// Clear cached settings (for testing)
        /// </summary>
        public static void ClearCache()
        {
            lock (_lock)
            {
                _cachedSettings = null;
            }
        }
    }
}
