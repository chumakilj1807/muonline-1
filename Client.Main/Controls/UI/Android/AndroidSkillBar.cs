using Client.Data.BMD;
using Client.Main.Controllers;
using Client.Main.Controls.UI;
using Client.Main.Helpers;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;
using System;

namespace Client.Main.Controls.UI.Android
{
    public class AndroidSkillBar : UIControl
    {
        private const int BigSize = 200;
        private const int SmallSize = 136;
        private const int Gap = 12;
        private const int SlotMarginRight = 80;   // indent from right edge
        private const int SlotMarginBottom = 120;  // indent from bottom (above action bar)

        private Texture2D _circleTex;
        private SkillEntryState[] _slots = new SkillEntryState[4];
        private double[] _cooldownDisplay = new double[4];
        private double[] _flashTimer = new double[4];
        private const double FlashDurationMs = 180.0;
        private AndroidSkillMenu _skillMenu;

        // Center points for hit-testing circles
        private Vector2[] _slotCenters = new Vector2[4];
        private float[] _slotRadii = new float[4];

        public AndroidSkillBar(AndroidSkillMenu skillMenu)
        {
            _skillMenu = skillMenu;
            Interactive = true;
        }

        public override async System.Threading.Tasks.Task Load()
        {
            _circleTex = CreateCircleTexture(256);
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

            double elapsed = gameTime.ElapsedGameTime.TotalMilliseconds;
            for (int i = 0; i < 4; i++)
            {
                if (_cooldownDisplay[i] > 0)
                    _cooldownDisplay[i] = Math.Max(0, _cooldownDisplay[i] - elapsed);
                if (_flashTimer[i] > 0)
                    _flashTimer[i] = Math.Max(0, _flashTimer[i] - elapsed);
            }

            var touches = MuGame.Instance.Touch;
            foreach (var touch in touches)
            {
                if (AndroidHUD.ConsumedTouchIds.Contains(touch.Id)) continue;
                if (touch.State != TouchLocationState.Pressed) continue;

                var pos = touch.Position;
                for (int i = 0; i < 4; i++)
                {
                    float dx = pos.X - _slotCenters[i].X;
                    float dy = pos.Y - _slotCenters[i].Y;
                    if (dx * dx + dy * dy <= _slotRadii[i] * _slotRadii[i])
                    {
                        AndroidHUD.ConsumedTouchIds.Add(touch.Id);
                        _flashTimer[i] = FlashDurationMs;
                        try { OnSlotTapped(i); } catch { }
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

            if (skillType == SkillType.Target)
            {
                // Fire at nearest visible monster (falls back to hero location if no target)
                AndroidHUD.Current?.InvokeDirectSkill(skill);
            }
            else if (skillType == SkillType.Self)
            {
                // Self buff/cast — fires immediately at hero
                AndroidHUD.Current?.InvokeSelfSkill(skill);
            }
            else
            {
                // Area skill — fire immediately in hero's facing direction
                if (MuGame.Instance.ActiveScene is Scenes.GameScene gs && gs.Hero != null)
                    AndroidHUD.Current?.InvokeAreaSkill(skill, gs.Hero.Location);
            }
        }

        private void CalcLayout()
        {
            var vp = MuGame.Instance.GraphicsDevice.Viewport;
            // Anchor to bottom-right but inset enough to stay on-screen
            int anchorX = vp.Width - SlotMarginRight;
            int anchorY = vp.Height - SlotMarginBottom;

            float bigR = BigSize / 2f;
            float smallR = SmallSize / 2f;

            // Slot 0 (primary big) — anchor point
            _slotCenters[0] = new Vector2(anchorX - bigR, anchorY - bigR);
            _slotRadii[0] = bigR;

            // Slot 1 — left of big
            _slotCenters[1] = new Vector2(anchorX - BigSize - Gap - smallR, anchorY - smallR);
            _slotRadii[1] = smallR;

            // Slot 2 — above big
            _slotCenters[2] = new Vector2(anchorX - bigR, anchorY - BigSize - Gap - smallR);
            _slotRadii[2] = smallR;

            // Slot 3 — above-left
            _slotCenters[3] = new Vector2(anchorX - BigSize - Gap - smallR, anchorY - BigSize - Gap - smallR);
            _slotRadii[3] = smallR;
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
                for (int i = 0; i < 4; i++)
                    DrawSlot(sb, font, i);
            }
        }

        private void DrawSlot(SpriteBatch sb, SpriteFont font, int slot)
        {
            var center = _slotCenters[slot];
            float r = _slotRadii[slot];
            var skill = _slots[slot];
            var type = skill != null ? SkillDefinitions.GetSkillType(skill.SkillId) : SkillType.Self;

            bool isFlashing = _flashTimer[slot] > 0;
            float flashAlpha = isFlashing ? (float)(_flashTimer[slot] / FlashDurationMs) : 0f;

            // Outer glow ring — brighter on flash
            Color ringColor = slot == 0
                ? new Color(255, 200, 50, 180)
                : new Color(150, 160, 220, 150);
            if (isFlashing) ringColor = Color.White;
            DrawCircle(sb, center, r + 8, ringColor * (0.5f + flashAlpha * 0.4f));

            // Background fill — type-specific color
            Color bgColor = skill != null
                ? GetSkillBgColor(skill.SkillId, type)
                : new Color(30, 30, 55, 190);
            DrawCircle(sb, center, r, bgColor);

            // Flash overlay
            if (isFlashing)
                DrawCircle(sb, center, r - 4, Color.White * (flashAlpha * 0.35f));

            // Inner highlight
            DrawCircle(sb, center - new Vector2(0, r * 0.18f), r * 0.6f, Color.White * 0.07f);

            // Border ring (thin)
            DrawCircle(sb, center, r, ringColor * 0.85f);
            DrawCircle(sb, center, r - 5, bgColor);

            // Cooldown dark overlay
            if (_cooldownDisplay[slot] > 0)
                DrawCircle(sb, center, r - 5, new Color(0, 0, 0, 170));

            // Text rendering
            if (font != null)
            {
                if (skill != null)
                {
                    // Skill name — up to 9 chars, centered
                    string name = SkillDatabase.GetSkillName(skill.SkillId);
                    if (name.Length > 9) name = name[..9];
                    float nameScale = slot == 0 ? 0.7f : 0.52f;
                    var nameSize = font.MeasureString(name) * nameScale;
                    var namePos = new Vector2(center.X - nameSize.X / 2f, center.Y - nameSize.Y / 2f + r * 0.05f);
                    sb.DrawString(font, name, namePos + new Vector2(1, 1), Color.Black * 0.75f, 0, Vector2.Zero, nameScale, SpriteEffects.None, 0);
                    sb.DrawString(font, name, namePos, Color.White, 0, Vector2.Zero, nameScale, SpriteEffects.None, 0);

                    // Type badge (small, top-left arc)
                    string badge = type == SkillType.Area ? "AOE" : type == SkillType.Target ? "TGT" : "BUF";
                    Color badgeCol = type == SkillType.Area ? new Color(255, 120, 30)
                                   : type == SkillType.Target ? new Color(255, 230, 50)
                                   : new Color(80, 220, 120);
                    float badgeScale = slot == 0 ? 0.38f : 0.30f;
                    var badgeSize = font.MeasureString(badge) * badgeScale;
                    var badgePos = new Vector2(center.X - r * 0.6f, center.Y - r * 0.72f);
                    sb.DrawString(font, badge, badgePos, badgeCol * 0.9f, 0, Vector2.Zero, badgeScale, SpriteEffects.None, 0);

                    // Level label (bottom-right)
                    string lvl = $"Lv{skill.SkillLevel}";
                    float lvlScale = slot == 0 ? 0.42f : 0.32f;
                    var lvlSize = font.MeasureString(lvl) * lvlScale;
                    var lvlPos = new Vector2(center.X + r * 0.25f, center.Y + r * 0.52f);
                    sb.DrawString(font, lvl, lvlPos, Color.White * 0.7f, 0, Vector2.Zero, lvlScale, SpriteEffects.None, 0);
                }
                else
                {
                    // Empty slot — show slot number
                    string num = $"{slot + 1}";
                    float numScale = slot == 0 ? 1.4f : 0.95f;
                    DrawTextCentered(sb, font, num, center, new Color(80, 90, 120, 160), numScale);
                    // Small hint text
                    string hint = "TAP+";
                    float hintScale = slot == 0 ? 0.36f : 0.28f;
                    var hintSize = font.MeasureString(hint) * hintScale;
                    var hintPos = new Vector2(center.X - hintSize.X / 2f, center.Y + r * 0.45f);
                    sb.DrawString(font, hint, hintPos, new Color(80, 90, 120, 120), 0, Vector2.Zero, hintScale, SpriteEffects.None, 0);
                }
            }
        }

        private static Color GetSkillBgColor(int skillId, SkillType type)
        {
            // Dark Lord skills: fire-themed (55-79, 230-238)
            bool isDarkLord = (skillId >= 55 && skillId <= 79) || (skillId >= 230 && skillId <= 238);
            if (isDarkLord)
            {
                return type switch
                {
                    SkillType.Area => new Color(180, 50, 10, 215),
                    SkillType.Target => new Color(160, 80, 10, 215),
                    _ => new Color(60, 20, 140, 215),
                };
            }
            return type switch
            {
                SkillType.Area => new Color(160, 60, 10, 210),
                SkillType.Target => new Color(130, 100, 10, 210),
                _ => new Color(20, 70, 160, 210),
            };
        }

        private void DrawCircle(SpriteBatch sb, Vector2 center, float radius, Color color)
        {
            if (radius <= 0) return;
            int d = (int)(radius * 2);
            var rect = new Rectangle((int)(center.X - radius), (int)(center.Y - radius), d, d);
            sb.Draw(_circleTex, rect, color);
        }

        private void DrawTextCentered(SpriteBatch sb, SpriteFont font, string text,
            Vector2 center, Color color, float scale)
        {
            if (font == null) return;
            var size = font.MeasureString(text) * scale;
            var pos = center - size / 2f;
            sb.DrawString(font, text, pos + Vector2.One, Color.Black * 0.55f, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
            sb.DrawString(font, text, pos, color, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
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
                    float alpha = MathF.Max(0f, 1f - MathF.Max(0f, dist - r + 1.5f) / 1.5f);
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
