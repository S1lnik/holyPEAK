using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HolyPeak.Audio;
using HolyPeak.Core;
using HolyPeak.Gfx;

namespace HolyPeak.Scenes
{
    /// <summary>Options and controls screens shared by the title screen and the pause menu.</summary>
    public static class OptionsMenu
    {
        /// <summary>Draws the options menu inside its framed panel.</summary>
        public static void Draw(SpriteBatch sb, Menu m, float alpha, Color accent)
        {
            float h = m.Height + 16;
            float top = MathF.Round(G.ViewH / 2f - h / 2f);
            Fx.Panel(sb, 64, top, G.ViewW - 128, h, alpha, accent);
            m.Draw(sb, new Vector2(G.ViewW / 2f, G.ViewH / 2f + 2), alpha, accent, 172);
        }

        public static Menu Build(Action back, Action showControls)
        {
            var g = HolyPeakGame.Instance;
            var s = g.Settings;
            var m = new Menu { Title = "Options", LineHeight = 11, Back = () => { g.SaveSettings(); back(); } };

            string Bar(int v) => new string('|', v) + new string('.', 10 - v);
            string OnOff(bool b) => b ? "On" : "Off";

            m.Option("Music", () => Bar(s.MusicVolume), d => { s.MusicVolume = Math.Clamp(s.MusicVolume + d, 0, 10); g.ApplyAudioSettings(); });
            m.Option("Sound", () => Bar(s.SfxVolume), d =>
            {
                s.SfxVolume = Math.Clamp(s.SfxVolume + d, 0, 10);
                g.ApplyAudioSettings();
                AudioManager.Play("jump", 0.8f);
            });
            m.Option("Fullscreen", () => OnOff(s.Fullscreen), d => { s.Fullscreen = !s.Fullscreen; g.ApplyVideo(); }, 3);
            m.Option("Window Size", () => $"{s.WindowScale}x", d =>
            {
                s.WindowScale = Math.Clamp(s.WindowScale + d, 2, 8);
                if (!s.Fullscreen) g.ApplyVideo();
            });
            m.Option("Screen Shake", () => OnOff(s.ScreenShake), d => s.ScreenShake = !s.ScreenShake);
            m.Option("Speedrun Timer", () => OnOff(s.ShowTimer), d => s.ShowTimer = !s.ShowTimer);
            m.Option("Game Speed", () => $"{s.GameSpeed * 10}%", d => s.GameSpeed = Math.Clamp(s.GameSpeed + d, 5, 10), 3);
            m.Option("Invincibility", () => OnOff(s.Invincible), d => s.Invincible = !s.Invincible);
            m.Add("Controls", showControls, spacing: 3);
            m.Add("Back", m.Back);
            return m;
        }

        public static readonly (string action, string keys, string pad)[] Controls =
        {
            ("Move / look", "Arrows / WASD", "Stick / D-pad"),
            ("Jump", "Z / Space", "A"),
            ("Strike", "X / J", "X"),
            ("Toll the bell", "C / K", "Y / RB"),
            ("Heal (hold)", "V / L", "B / LB"),
            ("Use / talk", "Up / W", "Up"),
            ("Map", "Tab / M", "Back"),
            ("Pause", "Esc / Enter", "Start"),
        };

        public static void DrawControls(SpriteBatch sb, float alpha, Color accent)
        {
            var f = G.Font;
            Fx.Panel(sb, 20, 10, G.ViewW - 40, G.ViewH - 20, alpha, accent);
            f.DrawCentered(sb, "Controls", new Vector2(G.ViewW / 2f, 17), accent * alpha, Color.Black * alpha);
            float y = 32;
            f.Draw(sb, "Keyboard", new Vector2(118, y), new Color(150, 150, 170) * alpha);
            f.Draw(sb, "Gamepad", new Vector2(212, y), new Color(150, 150, 170) * alpha);
            y += 12;
            foreach (var (action, keys, pad) in Controls)
            {
                f.Draw(sb, action, new Vector2(30, y), Color.White * alpha);
                f.Draw(sb, keys, new Vector2(118, y), new Color(220, 220, 235) * alpha);
                f.Draw(sb, pad, new Vector2(212, y), new Color(220, 220, 235) * alpha);
                y += 11;
            }
            y += 3;
            f.DrawCentered(sb, "Hold Down and strike in the air to bounce off foes and thorns.",
                new Vector2(G.ViewW / 2f, y), new Color(190, 190, 210) * alpha);
            f.DrawCentered(sb, Input.Format("{CONFIRM} back"), new Vector2(G.ViewW / 2f, G.ViewH - 21), accent * (0.8f * alpha));
        }
    }
}
