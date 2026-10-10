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
        public bool Invincible { get; set; }

        // Control bindings per gameplay action, as key / pad button enum names (see Input.ApplyBindings).
        // Keys holds the two keyboard slots ("" for an empty slot); missing entries use the defaults.
        public Dictionary<string, string[]> Keys { get; set; } = new Dictionary<string, string[]>();
        public Dictionary<string, string> Pads { get; set; } = new Dictionary<string, string>();

        public bool AssistActive => GameSpeed != 10 || Invincible;
    }

    public sealed class SaveData
    {
        public const int CurrentVersion = 3;
        public int Version { get; set; } = CurrentVersion;
        public bool Started { get; set; }
        public string Room { get; set; }
        public string ShrineRoom { get; set; }
        public int MaxHp { get; set; } = 5;
        public int MaxGrace { get; set; } = 99;
        public int Damage { get; set; } = 4;
        public List<string> Equipped { get; set; } = new List<string>();
        /// <summary>Only read when bringing a version 2 save forward.</summary>
        public int MaxFlasks { get; set; } = 2;
        public int Embers { get; set; }
        public List<string> Flags { get; set; } = new List<string>();
        public List<string> Visited { get; set; } = new List<string>();
        public string ShadeRoom { get; set; }
        public int ShadeX { get; set; }
        public int ShadeY { get; set; }
        public int ShadeEmbers { get; set; }
        public int Deaths { get; set; }
        public int Kills { get; set; }
        public double PlayTime { get; set; }
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

        /// <summary>The folder actually in use (honours <see cref="OverrideFolder"/>).</summary>
        public static string Dir => OverrideFolder ?? Folder;

        public static T Load<T>(string name) where T : new()
        {
            try
            {
                string path = Path.Combine(Dir, name);
                if (!File.Exists(path)) return new T();
                var obj = JsonSerializer.Deserialize<T>(File.ReadAllText(path), Opts);
                if (obj is SaveData sd)
                {
                    if (sd.Version == 2) Migrate(sd);
                    if (sd.Version != SaveData.CurrentVersion) return new T();   // a version 1 save
                }
                return obj ?? new T();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"[save] could not read {name}: {e.Message}");
                return new T();
            }
        }

        /// <summary>
        /// Version 3 counts health in half-strikes (so damage doubles), heals with grace instead of balm
        /// flasks (each flask becomes a vessel of grace) and turns the Ember Charm ware into a real charm.
        /// </summary>
        private static void Migrate(SaveData sd)
        {
            sd.Damage *= 2;
            sd.MaxGrace = 99 + 33 * Math.Max(0, sd.MaxFlasks - 2);
            if (sd.Flags.Contains("bought:charm") && !sd.Flags.Contains("charm:ember")) sd.Flags.Add("charm:ember");
            sd.Version = 3;
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
