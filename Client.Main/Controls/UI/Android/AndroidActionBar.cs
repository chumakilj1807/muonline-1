using Client.Main.Controllers;
using Client.Main.Controls.UI;
using Client.Main.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;
using System;

namespace Client.Main.Controls.UI.Android
{
    /// <summary>
    /// Bottom action bar — 6 round buttons with keyboard-key labels.
    /// [sk] [M] [I] [C] [G] [P]
    /// </summary>
    public class AndroidActionBar : UIControl
    {
        private const float BtnRadius = 36f;
        private const float BtnSpacing = 14f;

        private Texture2D _circleTex;

        private Vector2[] _centers = new Vector2[6];
        private readonly string[] _labels = { "sk", "M", "I", "C", "G", "P" };
        private readonly Color[] _colors =
        {
            new Color(120, 60, 200),  // sk — skills
            new Color(30, 130, 200),  // M  — map/teleport
            new Color(190, 140, 20),  // I  — inventory
            new Color(30, 160, 80),   // C  — character/stats
            new Color(160, 50, 50),   // G  — guild
            new Color(60, 110, 180)   // P  — party
        };

        public Action OnSkillsButton;
        public Action OnTeleportButton;
        public Action OnInventoryButton;
        public Action OnStatsButton;
        public Action OnGuildButton;
        public Action OnPartyButton;

        public AndroidActionBar()
        {
            Interactive = true;
        }

        public override async System.Threading.Tasks.Task Load()
        {
            _circleTex = CreateCircleTexture(128);
            await base.Load();
        }

        private void CalcLayout()
        {
            var vp = MuGame.Instance.GraphicsDevice.Viewport;
            float diameter = BtnRadius * 2;
            float totalW = 6 * diameter + 5 * BtnSpacing;
            float startX = (vp.Width - totalW) / 2f + BtnRadius;
            float cy = vp.Height - BtnRadius - 14;

            for (int i = 0; i < 6; i++)
                _centers[i] = new Vector2(startX + i * (diameter + BtnSpacing), cy);
        }

        public override void Update(GameTime gameTime)
        {
            if (!Visible) return;
            CalcLayout();

            var touches = MuGame.Instance.Touch;
            foreach (var touch in touches)
            {
                if (AndroidHUD.ConsumedTouchIds.Contains(touch.Id)) continue;
                if (touch.State != TouchLocationState.Pressed) continue;

                var pos = touch.Position;
                for (int i = 0; i < 6; i++)
                {
                    float dx = pos.X - _centers[i].X;
                    float dy = pos.Y - _centers[i].Y;
                    if (dx * dx + dy * dy <= BtnRadius * BtnRadius)
                    {
                        AndroidHUD.ConsumedTouchIds.Add(touch.Id);
                        try { OnButtonTapped(i); } catch { }
                        break;
                    }
                }
            }
        }

        private void OnButtonTapped(int index)
        {
            switch (index)
            {
                case 0: OnSkillsButton?.Invoke(); break;
                case 1: OnTeleportButton?.Invoke(); break;
                case 2: OnInventoryButton?.Invoke(); break;
                case 3: OnStatsButton?.Invoke(); break;
                case 4: OnGuildButton?.Invoke(); break;
                case 5: OnPartyButton?.Invoke(); break;
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible || _circleTex == null) return;

            CalcLayout();
            var sb = GraphicsManager.Instance.Sprite;
            var font = GraphicsManager.Instance.Font;

            using (new SpriteBatchScope(sb, SpriteSortMode.Deferred, BlendState.NonPremultiplied,
                SamplerState.LinearClamp, DepthStencilState.None))
            {
                for (int i = 0; i < 6; i++)
                    DrawButton(sb, font, i);
            }
        }

        private void DrawButton(SpriteBatch sb, SpriteFont font, int i)
        {
            var c = _centers[i];
            float r = BtnRadius;
            var col = _colors[i];

            // Outer glow
            DrawCircle(sb, c, r + 4, col * 0.3f);
            // Fill
            DrawCircle(sb, c, r, col * 0.85f);
            // Inner highlight
            DrawCircle(sb, c - new Vector2(0, r * 0.18f), r * 0.55f, Color.White * 0.07f);
            // Border ring
            DrawCircle(sb, c, r, new Color(220, 220, 240, 150) * 0.8f);
            DrawCircle(sb, c, r - 3, col * 0.85f);

            if (font == null) return;

            string lbl = _labels[i];
            float scale = lbl.Length > 1 ? 0.55f : 0.75f;
            var size = font.MeasureString(lbl) * scale;
            var pos = new Vector2(c.X - size.X / 2f, c.Y - size.Y / 2f);
            sb.DrawString(font, lbl, pos + Vector2.One, Color.Black * 0.6f, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
            sb.DrawString(font, lbl, pos, Color.White, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        }

        private void DrawCircle(SpriteBatch sb, Vector2 center, float radius, Color color)
        {
            if (radius <= 0) return;
            int d = (int)(radius * 2);
            var rect = new Rectangle((int)(center.X - radius), (int)(center.Y - radius), d, d);
            sb.Draw(_circleTex, rect, color);
        }

        private static Texture2D CreateCircleTexture(int size)
        {
            var tex = new Texture2D(MuGame.Instance.GraphicsDevice, size, size);
            var data = new Color[size * size];
            float r = size / 2f;
            float cx = r, cy = r;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    float alpha = Math.Clamp(1f - Math.Max(0f, dist - r + 1.5f) / 1.5f, 0f, 1f);
                    data[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetData(data);
            return tex;
        }

        public override void Dispose()
        {
            if (_circleTex != null) { _circleTex.Dispose(); _circleTex = null; }
            base.Dispose();
        }
    }
}
