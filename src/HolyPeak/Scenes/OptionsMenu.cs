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
            m.Option("Infinite Dash", () => OnOff(s.InfiniteDash), d => s.InfiniteDash = !s.InfiniteDash);
            m.Option("Invincibility", () => OnOff(s.Invincible), d => s.Invincible = !s.Invincible);
            m.Add("Controls", showControls, spacing: 3);
            m.Add("Back", m.Back);
            return m;
        }

        public static readonly (string action, string keys, string pad)[] Controls =
        {
            ("Move", "Arrow keys / WASD", "Left stick / D-pad"),
            ("Jump", "C / Space / J", "A"),
            ("Dash", "X / Shift / K", "X / B / RT"),
            ("Pause", "Esc / Enter", "Start"),
            ("Restart room", "R", "Back"),
            ("Fullscreen", "F11", "-"),
        };

        public static void DrawControls(SpriteBatch sb, float alpha, Color accent)
        {
            var f = G.Font;
            Fx.Panel(sb, 24, 22, G.ViewW - 48, G.ViewH - 44, alpha, accent);
            f.DrawCentered(sb, "Controls", new Vector2(G.ViewW / 2f, 30), accent * alpha, Color.Black * alpha);
            float y = 50;
            f.Draw(sb, "Keyboard", new Vector2(120, y), new Color(150, 150, 170) * alpha);
            f.Draw(sb, "Gamepad", new Vector2(222, y), new Color(150, 150, 170) * alpha);
            y += 14;
            foreach (var (action, keys, pad) in Controls)
            {
                f.Draw(sb, action, new Vector2(36, y), Color.White * alpha);
                f.Draw(sb, keys, new Vector2(120, y), new Color(220, 220, 235) * alpha);
                f.Draw(sb, pad, new Vector2(222, y), new Color(220, 220, 235) * alpha);
                y += 13;
            }
            y += 4;
            f.DrawCentered(sb, "Dash in any of eight directions. Touching the ground,\na spring or a crystal gives your dash back.",
                new Vector2(G.ViewW / 2f, y), new Color(190, 190, 210) * alpha);
            f.DrawCentered(sb, Input.Format("{CONFIRM} back"), new Vector2(G.ViewW / 2f, G.ViewH - 34), accent * (0.8f * alpha));
        }
    }
}
