using Client.Main.Controls.UI;
using Client.Main.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;
using System;

namespace Client.Main.Controls.UI.Android
{
    /// <summary>
    /// Bottom-center action bar with buttons:
    /// [Skills] [Teleport] [Inventory] [Stats] [Guild] [Party]
    /// </summary>
    public class AndroidActionBar : UIControl
    {
        private const int ButtonSize = 60;
        private const int ButtonSpacing = 10;

        private Texture2D _pixel;
        private Rectangle[] _buttonRects = new Rectangle[6];
        private string[] _labels = { "SKL", "TEL", "INV", "STA", "GLD", "PTY" };
        private Color[] _colors =
        {
            new Color(100, 50, 160),  // Skills
            new Color(30, 120, 180),  // Teleport
            new Color(160, 120, 30),  // Inventory
            new Color(30, 150, 80),   // Stats
            new Color(150, 60, 60),   // Guild
            new Color(60, 100, 160)   // Party
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
            _pixel = new Texture2D(MuGame.Instance.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            CalcLayout();
            await base.Load();
        }

        private void CalcLayout()
        {
            var vp = MuGame.Instance.GraphicsDevice.Viewport;
            int totalWidth = 6 * ButtonSize + 5 * ButtonSpacing;
            int startX = (vp.Width - totalWidth) / 2;
            int y = vp.Height - ButtonSize - 12;

            for (int i = 0; i < 6; i++)
                _buttonRects[i] = new Rectangle(startX + i * (ButtonSize + ButtonSpacing), y, ButtonSize, ButtonSize);
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
                    if (_buttonRects[i].Contains((int)pos.X, (int)pos.Y))
                    {
                        AndroidHUD.ConsumedTouchIds.Add(touch.Id);
                        OnButtonTapped(i);
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
            if (!Visible || _pixel == null) return;

            var sb = GraphicsManager.Instance.Sprite;
            var font = GraphicsManager.Instance.Font;

            using (new SpriteBatchScope(sb, SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None))
            {
                for (int i = 0; i < 6; i++)
                {
                    var rect = _buttonRects[i];

                    // Shadow
                    sb.Draw(_pixel, new Rectangle(rect.X + 2, rect.Y + 2, rect.Width, rect.Height),
                        new Color(0, 0, 0, 100));

                    // Button background
                    sb.Draw(_pixel, rect, _colors[i] * 0.85f);

                    // Border
                    DrawBorder(sb, rect, new Color(200, 200, 220, 160), 2);

                    // Label
                    if (font != null)
                    {
                        float scale = 0.6f;
                        var size = font.MeasureString(_labels[i]) * scale;
                        var pos = new Vector2(
                            rect.X + rect.Width / 2f - size.X / 2f,
                            rect.Y + rect.Height / 2f - size.Y / 2f);
                        sb.DrawString(font, _labels[i], pos + Vector2.One, Color.Black * 0.5f,
                            0, Vector2.Zero, scale, SpriteEffects.None, 0);
                        sb.DrawString(font, _labels[i], pos, Color.White,
                            0, Vector2.Zero, scale, SpriteEffects.None, 0);
                    }
                }
            }
        }

        private void DrawBorder(SpriteBatch sb, Rectangle rect, Color color, int thickness)
        {
            sb.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            sb.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            sb.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            sb.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }

        protected override void Dispose(bool disposing)
        {
            _pixel?.Dispose();
            base.Dispose(disposing);
        }
    }
}
