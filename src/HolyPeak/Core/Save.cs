using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HolyPeak.Core
{
    public sealed class Settings
    {
        public int MusicVolume { get; set; } = 7;
        public int SfxVolume { get; set; } = 8;
        public bool Fullscreen { get; set; }
        public int WindowScale { get; set; } = 4;
        public bool ScreenShake { get; set; } = true;
        public bool ShowTimer { get; set; }
        public int GameSpeed { get; set; } = 10;   // tenths: 10 = 100%
        public bool InfiniteDash { get; set; }
        public bool Invincible { get; set; }

        public bool AssistActive => GameSpeed != 10 || InfiniteDash || Invincible;
    }

    public sealed class SaveData
    {
        public int Version { get; set; } = 1;
        public bool Started { get; set; }
        public int RoomIndex { get; set; }
        public bool HasDash { get; set; }
        public int Deaths { get; set; }
        public double PlayTime { get; set; }
        public List<string> Embers { get; set; } = new List<string>();
        public int MaxChapter { get; set; }
        public bool Completed { get; set; }
        public double FirstClearTime { get; set; }
        public int FirstClearDeaths { get; set; }
        public bool UsedAssist { get; set; }
    }

    /// <summary>Loads and atomically writes the save file and settings as JSON in the user's config folder.</summary>
    public static class Storage
    {
        private static readonly JsonSerializerOptions Opts = new JsonSerializerOptions { WriteIndented = true };

        public static string Folder
        {
            get
            {
                string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (string.IsNullOrEmpty(baseDir)) baseDir = AppContext.BaseDirectory;
                return Path.Combine(baseDir, "HolyPeak");
            }
        }

        /// <summary>Overrides the storage folder (used by automated runs so they never touch real saves).</summary>
        public static string OverrideFolder;

        private static string Dir => OverrideFolder ?? Folder;

        public static T Load<T>(string name) where T : new()
        {
            try
            {
                string path = Path.Combine(Dir, name);
                if (!File.Exists(path)) return new T();
                var obj = JsonSerializer.Deserialize<T>(File.ReadAllText(path), Opts);
                return obj ?? new T();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"[save] could not read {name}: {e.Message}");
                return new T();
            }
        }

        public static void Write<T>(string name, T value)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                string path = Path.Combine(Dir, name);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(value, Opts));
                File.Move(tmp, path, true);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"[save] could not write {name}: {e.Message}");
            }
        }
    }
}
