using Client.Main.Controls.UI;
using Client.Main.Helpers;
using Client.Main.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Client.Main.Controls.UI.Android
{
    /// <summary>
    /// Renders animated HP and Mana bars above the local player's head
    /// using world-to-screen projection.
    /// </summary>
    public class CharacterHPManaBar : UIControl
    {
        private const int BarWidth = 160;
        private const int BarHeight = 14;
        private const int BarSpacing = 4;
        private const float AnimSpeed = 6f; // lerp speed per second

        private Texture2D _pixel;
        private float _hpDisplayRatio = 1f;
        private float _mpDisplayRatio = 1f;
        private bool _visible3D;

        public CharacterHPManaBar()
        {
            Interactive = false;
        }

        public override async System.Threading.Tasks.Task Load()
        {
            _pixel = new Texture2D(MuGame.Instance.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            if (!Visible) return;
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            var state = MuGame.Network?.GetCharacterState();
            if (state == null) { _visible3D = false; return; }

            float hpTarget = state.MaximumHealth > 0
                ? Math.Clamp((float)state.CurrentHealth / state.MaximumHealth, 0f, 1f)
                : 0f;
            float mpTarget = state.MaximumMana > 0
                ? Math.Clamp((float)state.CurrentMana / state.MaximumMana, 0f, 1f)
                : 0f;

            _hpDisplayRatio += (hpTarget - _hpDisplayRatio) * AnimSpeed * dt;
            _mpDisplayRatio += (mpTarget - _mpDisplayRatio) * AnimSpeed * dt;

            // Try to project hero position to screen
            if (MuGame.Instance.ActiveScene is Scenes.GameScene gs)
            {
                var hero = gs.Hero;
                if (hero != null && !hero.IsDead)
                {
                    _visible3D = OverheadNameplateRenderer.TryProject(
                        hero.BoundingBox, 1.6f, out _);
                }
                else _visible3D = false;
            }
            else _visible3D = false;
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible || _pixel == null) return;

            var state = MuGame.Network?.GetCharacterState();
            if (state == null) return;

            // Compute screen position from hero bounding box
            Vector3 screen = Vector3.Zero;
            bool projected = false;
            if (MuGame.Instance.ActiveScene is Scenes.GameScene gs)
            {
                var hero = gs.Hero;
                if (hero != null)
                    projected = OverheadNameplateRenderer.TryProject(hero.BoundingBox, 1.6f, out screen);
            }

            if (!projected) return;

            int bx = (int)screen.X - BarWidth / 2;
            int by = (int)screen.Y - (BarHeight * 2 + BarSpacing + 6);

            var sb = GraphicsManager.Instance.Sprite;
            using (new SpriteBatchScope(sb, SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None))
            {
                DrawBar(sb, bx, by, _hpDisplayRatio,
                    new Color(180, 30, 30, 220),
                    new Color(80, 0, 0, 180),
                    state.CurrentHealth, state.MaximumHealth, "HP");

                DrawBar(sb, bx, by + BarHeight + BarSpacing, _mpDisplayRatio,
                    new Color(30, 80, 200, 220),
                    new Color(0, 20, 80, 180),
                    state.CurrentMana, state.MaximumMana, "MP");
            }
        }

        private void DrawBar(SpriteBatch sb, int x, int y, float ratio,
            Color fillColor, Color bgColor, uint current, uint max, string label)
        {
            int borderW = 2;

            // Shadow / border
            sb.Draw(_pixel, new Rectangle(x - borderW, y - borderW,
                BarWidth + borderW * 2, BarHeight + borderW * 2), new Color(0, 0, 0, 160));

            // Background
            sb.Draw(_pixel, new Rectangle(x, y, BarWidth, BarHeight), bgColor);

            // Fill
            int fillW = (int)(BarWidth * ratio);
            if (fillW > 0)
            {
                // Gradient effect: slightly lighter at top
                sb.Draw(_pixel, new Rectangle(x, y, fillW, BarHeight / 2),
                    fillColor * 1.15f);
                sb.Draw(_pixel, new Rectangle(x, y + BarHeight / 2, fillW, BarHeight - BarHeight / 2),
                    fillColor);
            }

            // Text
            var font = GraphicsManager.Instance.Font;
            if (font != null)
            {
                string text = $"{label} {current}/{max}";
                var size = font.MeasureString(text) * 0.55f;
                var pos = new Vector2(x + BarWidth / 2f - size.X / 2f, y + BarHeight / 2f - size.Y / 2f);
                // Shadow
                sb.DrawString(font, text, pos + Vector2.One, Color.Black * 0.8f, 0f,
                    Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
                sb.DrawString(font, text, pos, Color.White * 0.95f, 0f,
                    Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
            }
        }

        protected override void Dispose(bool disposing)
        {
            _pixel?.Dispose();
            base.Dispose(disposing);
        }
    }
}
