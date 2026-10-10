using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Audio;

namespace HolyPeak.Audio
{
    /// <summary>
    /// Owns all audio: SFX are synthesised at startup; music renders on worker threads and starts
    /// as soon as it is ready. Survives machines without an audio device by going silent.
    /// </summary>
    public static class AudioManager
    {
        public static bool Enabled { get; private set; }
        public static float MusicVolume = 0.7f, SfxVolume = 0.8f;

        private static readonly Dictionary<string, SoundEffect> Sfx = new Dictionary<string, SoundEffect>();
        private static readonly Dictionary<SongId, SoundEffect> SongFx = new Dictionary<SongId, SoundEffect>();
        private static readonly ConcurrentQueue<(SongId id, byte[] pcm)> Ready = new ConcurrentQueue<(SongId, byte[])>();
        private static readonly Random Rand = new Random();

        private static SoundEffectInstance _cur, _old;
        private static SongId? _curId, _wantId;
        private static float _curFade = 1f, _oldFade, _fadeSpeed = 1f, _duck = 1f, _duckTarget = 1f;
        private static SoundEffectInstance _wind;
        private static float _windVol, _windTarget;

        public static void Init()
        {
            try
            {
                var clips = SfxGen.All();
                foreach (var kv in clips)
                {
                    var pcm = Synth.ToPcm16(kv.Value.Data);
                    Sfx[kv.Key] = new SoundEffect(pcm, Synth.Rate, kv.Value.Channels == 2 ? AudioChannels.Stereo : AudioChannels.Mono);
                }
                Enabled = true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("[audio] disabled: " + e.Message);
                Enabled = false;
                return;
            }

            // Title first so the menu has music quickly, then everything else in parallel.
            Task.Run(() =>
            {
                // Each song renders its notes in parallel, so songs go one after another, title first.
                foreach (var id in new[] { SongId.Title, SongId.Foothills, SongId.Ruins, SongId.Frost, SongId.Peak, SongId.Ending })
                    Render(id);
            });
        }

        /// <summary>Bump when any song or instrument changes so stale caches are ignored.</summary>
        private const string CacheVersion = "v4";

        private static string CachePath(SongId id)
            => System.IO.Path.Combine(Core.Storage.Folder, "cache", $"music-{CacheVersion}-{id}.pcm");

        private static void Render(SongId id)
        {
            try
            {
                string path = CachePath(id);
                if (System.IO.File.Exists(path))
                {
                    try { Ready.Enqueue((id, System.IO.File.ReadAllBytes(path))); return; }
                    catch (Exception) { /* fall through and re-render */ }
                }
                var pcm = Synth.ToPcm16(Music.Render(Songs.Get(id)));
                Ready.Enqueue((id, pcm));
                try
                {
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                    System.IO.File.WriteAllBytes(path + ".tmp", pcm);
                    System.IO.File.Move(path + ".tmp", path, true);
                }
                catch (Exception e) { Console.Error.WriteLine("[audio] cache write failed: " + e.Message); }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"[audio] song {id} failed: {e}");
            }
        }

        public static void Update(float dt)
        {
            if (!Enabled) return;
            while (Ready.TryDequeue(out var r))
            {
                try { SongFx[r.id] = new SoundEffect(r.pcm, Synth.Rate, AudioChannels.Stereo); }
                catch (Exception e) { Console.Error.WriteLine("[audio] " + e.Message); }
            }

            // Start the wanted song once it exists.
            if (_wantId.HasValue && _curId != _wantId && SongFx.TryGetValue(_wantId.Value, out var fx))
            {
                if (_old != null) { _old.Stop(); _old.Dispose(); }
                _old = _cur; _oldFade = _curFade;
                _cur = fx.CreateInstance();
                _cur.IsLooped = true;
                _cur.Volume = 0;
                _cur.Play();
                _curId = _wantId;
                _curFade = 0f;
            }

            _duck += (_duckTarget - _duck) * Math.Min(1, dt * 4f);
            if (_cur != null)
            {
                _curFade = Math.Min(1, _curFade + dt * _fadeSpeed);
                _cur.Volume = Clamp(_curFade * MusicVolume * _duck);
            }
            if (_old != null)
            {
                _oldFade -= dt * _fadeSpeed;
                if (_oldFade <= 0) { _old.Stop(); _old.Dispose(); _old = null; }
                else _old.Volume = Clamp(_oldFade * MusicVolume * _duck);
            }

            if (_wind != null)
            {
                _windVol += (_windTarget - _windVol) * Math.Min(1, dt * 2f);
                _wind.Volume = Clamp(_windVol * SfxVolume);
                if (_windVol < 0.001f && _windTarget == 0 && _wind.State == SoundState.Playing) _wind.Pause();
                else if (_windVol > 0.001f && _wind.State != SoundState.Playing) _wind.Play();
            }
        }

        public static void PlayMusic(SongId id, float fadeSeconds = 1.5f)
        {
            _fadeSpeed = 1f / Math.Max(0.05f, fadeSeconds);
            _wantId = id;
        }

        public static void StopMusic(float fadeSeconds = 1.5f)
        {
            _fadeSpeed = 1f / Math.Max(0.05f, fadeSeconds);
            _wantId = null;
            if (_cur != null)
            {
                if (_old != null) { _old.Stop(); _old.Dispose(); }
                _old = _cur; _oldFade = _curFade;
                _cur = null; _curId = null;
            }
        }

        /// <summary>Temporarily lowers the music (pause menu, big moments).</summary>
        public static void Duck(float amount) => _duckTarget = amount;

        public static void SetWind(float volume)
        {
            if (!Enabled) return;
            if (_wind == null && volume > 0 && Sfx.TryGetValue("wind", out var w))
            {
                _wind = w.CreateInstance();
                _wind.IsLooped = true;
                _wind.Volume = 0;
            }
            _windTarget = volume;
        }

        public static void Play(string name, float volume = 1f, float pitch = 0f, float pan = 0f, float pitchVariance = 0f)
        {
            if (!Enabled || !Sfx.TryGetValue(name, out var fx)) return;
            try
            {
                if (pitchVariance > 0) pitch += (float)(Rand.NextDouble() * 2 - 1) * pitchVariance;
                fx.Play(Clamp(volume * SfxVolume), Math.Clamp(pitch, -1f, 1f), Math.Clamp(pan, -1f, 1f));
            }
            catch (Exception) { /* too many voices; drop it */ }
        }

        private static float Clamp(float v) => v < 0 ? 0 : (v > 1 ? 1 : v);
    }
}
