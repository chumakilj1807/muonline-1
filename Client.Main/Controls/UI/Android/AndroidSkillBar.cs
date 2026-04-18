using Client.Data.BMD;
using Client.Main.Controls.UI;
using Client.Main.Helpers;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Objects.Player;
using Client.Main.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;
using System;

namespace Client.Main.Controls.UI.Android
{
    /// <summary>
    /// 4 skill slots in the bottom-right corner.
    /// Layout (Mobile Legends style):
    ///   [2][3]
    ///   [4][1 BIG]
    /// Slot 1 is the primary (large), slots 2-4 are secondary (small).
    /// </summary>
    public class AndroidSkillBar : UIControl
    {
        private const int BigSize = 100;
        private const int SmallSize = 70;
        private const int Gap = 8;
        private const int SlotMargin = 20;

        private Texture2D _pixel;
        private SkillEntryState[] _slots = new SkillEntryState[4]; // 0=primary, 1-3=secondary
        private double[] _cooldownDisplay = new double[4];
        private AreaTargetSelector _areaSelector;
        private AndroidSkillMenu _skillMenu;

        // Rectangle for each slot in screen coords (set in CalcLayout)
        private Rectangle[] _slotRects = new Rectangle[4];

        public AndroidSkillBar(AreaTargetSelector areaSelector, AndroidSkillMenu skillMenu)
        {
            _areaSelector = areaSelector;
            _skillMenu = skillMenu;
            Interactive = true;
        }

        public override async System.Threading.Tasks.Task Load()
        {
            _pixel = new Texture2D(MuGame.Instance.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            await base.Load();
        }

        public void AssignSkill(SkillEntryState skill, int slot)
        {
            if (slot < 0 || slot >= 4) return;
            _slots[slot] = skill;
        }

        public override void Update(GameTime gameTime)
        {
            if (!Visible) return;

            CalcLayout();

            // Animate cooldowns
            for (int i = 0; i < 4; i++)
                if (_cooldownDisplay[i] > 0)
                    _cooldownDisplay[i] = Math.Max(0, _cooldownDisplay[i]
                        - gameTime.ElapsedGameTime.TotalMilliseconds);

            var touches = MuGame.Instance.Touch;
            foreach (var touch in touches)
            {
                if (AndroidHUD.ConsumedTouchIds.Contains(touch.Id)) continue;
                if (touch.State != TouchLocationState.Pressed) continue;

                var pos = touch.Position;
                for (int i = 0; i < 4; i++)
                {
                    if (_slotRects[i].Contains((int)pos.X, (int)pos.Y))
                    {
                        AndroidHUD.ConsumedTouchIds.Add(touch.Id);
                        OnSlotTapped(i);
                        break;
                    }
                }
            }
        }

        private void OnSlotTapped(int slot)
        {
            var skill = _slots[slot];
            if (skill == null) return;

            var skillType = SkillDefinitions.GetSkillType(skill.SkillId);

            if (skillType == SkillType.Area)
            {
                // Show area target selector
                var hero = GetHero();
                if (hero != null)
                    _areaSelector.Activate(skill, hero.Location);
            }
            else
            {
                // Direct use: fire at nearest enemy or self
                AndroidHUD.Current?.InvokeDirectSkill(skill);
            }
        }

        private HeroObject GetHero()
        {
            if (MuGame.Instance.ActiveScene is Scenes.GameScene gs)
                return gs.Hero;
            return null;
        }

        private void CalcLayout()
        {
            var vp = MuGame.Instance.GraphicsDevice.Viewport;
            int right = vp.Width - SlotMargin;
            int bottom = vp.Height - SlotMargin;

            // Slot 0 (primary, big) — bottom-right
            _slotRects[0] = new Rectangle(right - BigSize, bottom - BigSize, BigSize, BigSize);

            // Slot 1 — left of big
            _slotRects[1] = new Rectangle(right - BigSize - Gap - SmallSize, bottom - SmallSize, SmallSize, SmallSize);

            // Slot 2 — above big
            _slotRects[2] = new Rectangle(right - BigSize, bottom - BigSize - Gap - SmallSize, SmallSize, SmallSize);

            // Slot 3 — above-left
            _slotRects[3] = new Rectangle(right - BigSize - Gap - SmallSize, bottom - BigSize - Gap - SmallSize, SmallSize, SmallSize);
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible || _pixel == null) return;

            CalcLayout();
            var sb = GraphicsManager.Instance.Sprite;
            var font = GraphicsManager.Instance.Font;

            using (new SpriteBatchScope(sb, SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None))
            {
                for (int i = 0; i < 4; i++)
                {
                    DrawSlot(sb, font, i, _slotRects[i]);
                }
            }
        }

        private void DrawSlot(SpriteBatch sb, SpriteFont font, int slot, Rectangle rect)
        {
            var skill = _slots[slot];
            var type = skill != null ? SkillDefinitions.GetSkillType(skill.SkillId) : SkillType.Self;

            // Background
            Color bgColor = skill != null
                ? (type == SkillType.Area ? new Color(120, 50, 20, 200)
                 : type == SkillType.Target ? new Color(100, 80, 20, 200)
                 : new Color(20, 60, 120, 200))
                : new Color(40, 40, 60, 180);

            sb.Draw(_pixel, rect, bgColor);

            // Border
            DrawBorder(sb, rect, slot == 0 ? new Color(255, 200, 50, 200) : new Color(150, 150, 200, 180), 3);

            if (skill != null)
            {
                // Skill type icon (letter)
                string letter = type == SkillType.Area ? "A" : type == SkillType.Target ? "T" : "S";
                Color letterColor = type == SkillType.Area ? Color.OrangeRed
                    : type == SkillType.Target ? Color.Yellow : Color.LightGreen;

                DrawTextCentered(sb, font, letter, rect, letterColor,
                    slot == 0 ? 1.2f : 0.9f);

                // Skill level (bottom-right corner of slot)
                if (font != null)
                {
                    string lvl = $"Lv{skill.SkillLevel}";
                    var lvlPos = new Vector2(rect.Right - 32, rect.Bottom - 22);
                    sb.DrawString(font, lvl, lvlPos, Color.White * 0.8f, 0, Vector2.Zero, 0.45f, SpriteEffects.None, 0);
                }

                // Skill name truncated
                if (font != null)
                {
                    string name = SkillDatabase.GetSkillName(skill.SkillId);
                    if (name.Length > 8) name = name[..8];
                    var namePos = new Vector2(rect.X + 4, rect.Y + 4);
                    sb.DrawString(font, name, namePos, Color.White * 0.7f, 0, Vector2.Zero, 0.38f, SpriteEffects.None, 0);
                }

                // Cooldown overlay
                if (_cooldownDisplay[slot] > 0)
                {
                    sb.Draw(_pixel, rect, new Color(0, 0, 0, 160));
                }
            }
            else
            {
                // Empty slot
                DrawTextCentered(sb, font, $"{slot + 1}", rect, new Color(100, 100, 120, 180),
                    slot == 0 ? 1.0f : 0.7f);
            }
        }

        private void DrawBorder(SpriteBatch sb, Rectangle rect, Color color, int thickness)
        {
            // Top
            sb.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            // Bottom
            sb.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            // Left
            sb.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            // Right
            sb.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }

        private void DrawTextCentered(SpriteBatch sb, SpriteFont font, string text,
            Rectangle rect, Color color, float scale)
        {
            if (font == null) return;
            var size = font.MeasureString(text) * scale;
            var pos = new Vector2(rect.X + rect.Width / 2f - size.X / 2f,
                                  rect.Y + rect.Height / 2f - size.Y / 2f);
            sb.DrawString(font, text, pos + Vector2.One, Color.Black * 0.5f, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
            sb.DrawString(font, text, pos, color, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        }

        public override void Dispose()
        {
            if (_pixel != null) { _pixel.Dispose(); _pixel = null; }
            base.Dispose();
        }
    }
}
