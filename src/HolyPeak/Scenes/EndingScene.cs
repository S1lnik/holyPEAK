using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using HolyPeak.Audio;
using HolyPeak.Core;
using HolyPeak.Gfx;
using HolyPeak.World;

namespace HolyPeak.Scenes
{
    /// <summary>Epilogue text, the journey's statistics and the credits.</summary>
    public sealed class EndingScene : Scene
    {
        private static readonly string[] Epilogue =
        {
            "The bell rang out across the mountain.",
            "Down past the frozen cliffs and the forgotten ruins,",
            "over the foothills, into the valley below.",
            "",
            "Somewhere far away, someone looked up.",
        };

        private Backdrop _bd;
        private float _time, _t;
        private int _stage;   // 0 epilogue, 1 stats, 2 credits
        private readonly Particles _parts = new Particles(512);
        private float _white = 1f;

        private SaveData Save => Game.Save;

        public override void Begin()
        {
            _white = 1f;
            _bd = new Backdrop(Style.Chapters[3], 118, 0.5f, 0.08f);
            AudioManager.PlayMusic(SongId.Ending, 3f);
            AudioManager.SetWind(0);
        }

        public override void Update(float dt)
        {
            _time += dt;
            _t += dt;
            _white = Math.Max(0, _white - dt * 0.5f);
            _parts.Update(dt);
            if (_parts.Rng.Chance(dt * 12))
                _parts.Emit(new Vector2(_parts.Rng.Range(0, G.ViewW), G.ViewH + 4), new Vector2(_parts.Rng.Range(-4, 4), _parts.Rng.Range(-18, -8)),
                            G.Hex(0xffe9a8), 9f, 1, end: G.Hex(0xffe9a8) * 0f, additive: true);
            if (Game.Transitioning) return;

            bool advance = Input.Confirm.Pressed || Input.Jump.Pressed;
            switch (_stage)
            {
                case 0:
                    if ((advance && _t > 1.5f) || _t > 15f) { Next(); }
                    break;
                case 1:
                    if (advance && _t > 1.5f) Next();
                    break;
                case 2:
                    if ((advance && _t > 2f) || _t > 26f)
                    {
                        Input.Confirm.Consume(); Input.Jump.Consume();
                        AudioManager.Play("select", 0.7f);
                        Game.ChangeScene(new TitleScene(), 1.5f);
                    }
                    break;
            }
        }

        private void Next()
        {
            Input.Confirm.Consume(); Input.Jump.Consume();
            AudioManager.Play("select", 0.6f);
            _stage++;
            _t = 0;
        }

        public override void DrawWorld(SpriteBatch sb)
        {
            SubPixel = Vector2.Zero;
            float rise = Calc.EaseInOutCubic(Calc.Clamp01(_time / 30f)) * 40f;
            sb.Begin(samplerState: SamplerState.PointClamp, transformMatrix: Matrix.CreateTranslation(1, 1, 0));
            _bd.Draw(sb, new Vector2(_time * 2f, rise), new Point(G.ViewW, G.ViewH), _time, 0.9f);
            sb.Draw(G.Vignette, Vector2.Zero, Color.White);
            sb.End();
            sb.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.Additive, transformMatrix: Matrix.CreateTranslation(1, 1, 0));
            _parts.Draw(sb, true);
            sb.End();
        }

        public override void DrawUI(SpriteBatch sb)
        {
            var font = G.Font;
            Color accent = G.Hex(0xffd88a);
            sb.Begin(samplerState: SamplerState.PointClamp);
            G.Rect(sb, 0, 0, G.ViewW, G.ViewH, Color.Black * 0.35f);

            switch (_stage)
            {
                case 0:
                {
                    float y = 52;
                    for (int i = 0; i < Epilogue.Length; i++)
                    {
                        float a = Calc.Clamp01((_t - 1.2f - i * 1.7f) / 1.2f);
                        if (Epilogue[i].Length > 0)
                            font.DrawCentered(sb, Epilogue[i], new Vector2(G.ViewW / 2f, y + (1 - Calc.EaseOutCubic(a)) * 4), new Color(240, 235, 225) * a, Color.Black * a);
                        y += 13;
                    }
                    break;
                }
                case 1:
                {
                    float a = Calc.Clamp01(_t / 0.8f);
                    Fx.Panel(sb, 54, 22, G.ViewW - 108, 138, a, accent);
                    font.DrawCentered(sb, "The Summit", new Vector2(G.ViewW / 2f, 30), accent * a, Color.Black * a, 2f);
                    Fx.Ornament(sb, new Vector2(G.ViewW / 2f, 55), 60, accent * (0.7f * a));
                    double time = Save.FirstClearTime > 0 ? Save.FirstClearTime : Save.PlayTime;
                    int deaths = Save.FirstClearTime > 0 ? Save.FirstClearDeaths : Save.Deaths;
                    Row(sb, "Time", Calc.FormatTime(time), 64, a);
                    Row(sb, "Deaths", deaths.ToString(), 76, a);
                    Row(sb, "Embers", $"{Save.Embers.Count} / {Levels.EmberCount}", 88, a);
                    string note = Save.Embers.Count >= Levels.EmberCount
                        ? "Every flame was carried to the top.\nYour scarf now glows gold."
                        : (Save.Embers.Count > 0 ? "Some flames still wait on the mountain." : "The flames of the mountain wait for you.");
                    font.DrawCentered(sb, note, new Vector2(G.ViewW / 2f, 104), new Color(220, 210, 190) * a);
                    if (Save.UsedAssist) font.DrawCentered(sb, "(assist mode was used)", new Vector2(G.ViewW / 2f, 128), new Color(150, 150, 170) * a);
                    if (_t > 1.5f) font.DrawCentered(sb, Input.Format("{CONFIRM}"), new Vector2(G.ViewW / 2f, 142), Color.White * (0.5f + 0.3f * MathF.Sin(_time * 4)));
                    break;
                }
                case 2:
                {
                    string[] credits =
                    {
                        "HOLY PEAK", "", "A pilgrimage in four chapters", "", "",
                        "Design & Programming", "Claude", "",
                        "Pixel Art, Music & Sound", "Generated entirely in code", "",
                        "Built with", "MonoGame", "", "", "Thank you for climbing.",
                    };
                    float y = G.ViewH + 10 - _t * 16f;
                    for (int i = 0; i < credits.Length; i++)
                    {
                        string l = credits[i];
                        bool head = i == 0;
                        bool label = l == "Design & Programming" || l == "Pixel Art, Music & Sound" || l == "Built with";
                        Color c = head ? accent : (label ? new Color(170, 165, 200) : Color.White);
                        float yy = MathF.Max(y, i == credits.Length - 1 ? G.ViewH / 2f - 4 : -100);
                        if (l.Length > 0) font.DrawCentered(sb, l, new Vector2(G.ViewW / 2f, MathF.Round(yy)), c, Color.Black, head ? 2f : 1f);
                        y += head ? 24 : 12;
                    }
                    break;
                }
            }
            if (_white > 0) G.Rect(sb, 0, 0, G.ViewW, G.ViewH, Color.White * _white);
            sb.End();
        }

        private static void Row(SpriteBatch sb, string label, string value, float y, float a)
        {
            var font = G.Font;
            font.Draw(sb, label, new Vector2(96, y), new Color(170, 165, 200) * a);
            float w = font.Measure(value);
            font.Draw(sb, value, new Vector2(G.ViewW - 96 - w, y), Color.White * a);
        }
    }
}
