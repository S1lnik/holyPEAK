using System;
using System.IO;
using HolyPeak.Audio;

namespace HolyPeak
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            AutomationOptions auto = null;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"missing value for {a}");
                switch (a)
                {
                    case "--render-audio":
                        return RenderAudio(Next());
                    case "--shot": (auto ??= new AutomationOptions()).ShotPath = Next(); break;
                    case "--frames": (auto ??= new AutomationOptions()).Frames = int.Parse(Next()); break;
                    case "--every": (auto ??= new AutomationOptions()).Every = int.Parse(Next()); break;
                    case "--scene": (auto ??= new AutomationOptions()).Scene = Next(); break;
                    case "--room": (auto ??= new AutomationOptions()).Room = Next(); break;
                    case "--replay": (auto ??= new AutomationOptions()).Replay = Next(); break;
                    case "--give": (auto ??= new AutomationOptions()).Give = Next(); break;
                    case "--pos": (auto ??= new AutomationOptions()).Pos = Next(); break;
                    case "--invincible": (auto ??= new AutomationOptions()).Invincible = true; break;
                    case "--autofight": (auto ??= new AutomationOptions()).AutoFight = true; break;
                    case "--nohints": (auto ??= new AutomationOptions()).NoHints = true; break;
                    case "--peaceful": (auto ??= new AutomationOptions()).Peaceful = true; break;
                    case "--dump-sprites": (auto ??= new AutomationOptions()).DumpSprites = Next(); break;
                    case "--keys": (auto ??= new AutomationOptions()).Keys = Next(); break;
                    case "--help":
                        Console.WriteLine("Holy Peak\n  (no arguments)   play the game\n  --render-audio <dir>   write every sound and song as WAV");
                        return 0;
                }
            }

            using var game = new HolyPeakGame(auto);
            game.Run();
            return 0;
        }

        /// <summary>Developer tool: renders the whole soundtrack and all effects to WAV files.</summary>
        private static int RenderAudio(string dir)
        {
            Directory.CreateDirectory(dir);
            foreach (var kv in SfxGen.All())
                WriteWav(Path.Combine(dir, "sfx_" + kv.Key + ".wav"), kv.Value.Data, kv.Value.Channels);
            foreach (SongId id in Enum.GetValues(typeof(SongId)))
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var data = Music.Render(Songs.Get(id));
                Console.WriteLine($"{id}: {data.Length / 2 / (float)Synth.Rate:0.0}s rendered in {sw.ElapsedMilliseconds} ms");
                WriteWav(Path.Combine(dir, "song_" + id + ".wav"), data, 2);
            }
            return 0;
        }

        private static void WriteWav(string path, float[] data, int channels)
        {
            var pcm = Synth.ToPcm16(data);
            using var w = new BinaryWriter(File.Create(path));
            w.Write("RIFF"u8.ToArray()); w.Write(36 + pcm.Length); w.Write("WAVE"u8.ToArray());
            w.Write("fmt "u8.ToArray()); w.Write(16); w.Write((short)1); w.Write((short)channels);
            w.Write(Synth.Rate); w.Write(Synth.Rate * channels * 2); w.Write((short)(channels * 2)); w.Write((short)16);
            w.Write("data"u8.ToArray()); w.Write(pcm.Length); w.Write(pcm);
        }
    }
}
