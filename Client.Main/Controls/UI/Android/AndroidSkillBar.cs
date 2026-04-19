using Client.Data.BMD;
using Client.Main.Controllers;
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
    public class AndroidSkillBar : UIControl
    {
        private const int BigSize = 100;
        private const int SmallSize = 68;
        private const int Gap = 8;
        private const int SlotMarginRight = 70;   // indent from right edge
        private const int SlotMarginBottom = 90;  // indent from bottom (above action bar)

        private Texture2D _circleTex;
        private SkillEntryState[] _slots = new SkillEntryState[4];
        private double[] _cooldownDisplay = new double[4];
        private AreaTargetSelector _areaSelector;
        private AndroidSkillMenu _skillMenu;

        // Center points for hit-testing circles
        private Vector2[] _slotCenters = new Vector2[4];
        private float[] _slotRadii = new float[4];

        public AndroidSkillBar(AreaTargetSelector areaSelector, AndroidSkillMenu skillMenu)
        {
            _areaSelector = areaSelector;
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
                    float dx = pos.X - _slotCenters[i].X;
                    float dy = pos.Y - _slotCenters[i].Y;
                    if (dx * dx + dy * dy <= _slotRadii[i] * _slotRadii[i])
                    {
                        AndroidHUD.ConsumedTouchIds.Add(touch.Id);
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

            if (skillType == SkillType.Area)
            {
                // Show area target ring; skill fires when user confirms location
                if (_areaSelector == null) return;
                HeroObject hero = null;
                if (MuGame.Instance.ActiveScene is Scenes.GameScene gs)
                    hero = gs.Hero;
                if (hero != null)
                    _areaSelector.Activate(skill, hero.Location);
            }
            else if (skillType == SkillType.Target)
            {
                // Fire once at nearest visible monster
                AndroidHUD.Current?.InvokeDirectSkill(skill);
            }
            else
            {
                // Self skill — fire once, no target needed
                AndroidHUD.Current?.InvokeSelfSkill(skill);
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

            // Outer glow ring
            Color ringColor = slot == 0
                ? new Color(255, 200, 50, 180)
                : new Color(150, 160, 220, 150);
            DrawCircle(sb, center, r + 5, ringColor * 0.5f);

            // Background fill
            Color bgColor = skill != null
                ? (type == SkillType.Area ? new Color(160, 60, 10, 210)
                 : type == SkillType.Target ? new Color(130, 100, 10, 210)
                 : new Color(20, 70, 160, 210))
                : new Color(30, 30, 55, 190);
            DrawCircle(sb, center, r, bgColor);

            // Inner highlight (top half lighter)
            DrawCircle(sb, center - new Vector2(0, r * 0.2f), r * 0.6f, Color.White * 0.06f);

            // Border ring
            DrawCircle(sb, center, r, ringColor * 0.8f);
            DrawCircle(sb, center, r - 4, bgColor);  // re-fill to make ring look thin

            if (_cooldownDisplay[slot] > 0)
            {
                DrawCircle(sb, center, r - 4, new Color(0, 0, 0, 160));
            }

            // Text
            if (font != null)
            {
                if (skill != null)
                {
                    // Type letter centered
                    string letter = type == SkillType.Area ? "A" : type == SkillType.Target ? "T" : "S";
                    Color lc = type == SkillType.Area ? Color.OrangeRed
                             : type == SkillType.Target ? Color.Yellow : Color.LightGreen;
                    float letterScale = slot == 0 ? 1.1f : 0.75f;
                    DrawTextCentered(sb, font, letter, center + new Vector2(0, -r * 0.15f), lc, letterScale);

                    // Skill name (small, at bottom of circle)
                    string name = SkillDatabase.GetSkillName(skill.SkillId);
                    if (name.Length > 7) name = name[..7];
                    float nameScale = slot == 0 ? 0.42f : 0.36f;
                    var nameSize = font.MeasureString(name) * nameScale;
                    var namePos = new Vector2(center.X - nameSize.X / 2f, center.Y + r * 0.45f);
                    sb.DrawString(font, name, namePos + Vector2.One, Color.Black * 0.7f, 0, Vector2.Zero, nameScale, SpriteEffects.None, 0);
                    sb.DrawString(font, name, namePos, Color.White * 0.85f, 0, Vector2.Zero, nameScale, SpriteEffects.None, 0);

                    // Level (top-right arc)
                    string lvl = $"Lv{skill.SkillLevel}";
                    float lvlScale = 0.38f;
                    var lvlSize = font.MeasureString(lvl) * lvlScale;
                    var lvlPos = new Vector2(center.X + r * 0.4f - lvlSize.X / 2f, center.Y - r * 0.82f);
                    sb.DrawString(font, lvl, lvlPos, Color.White * 0.75f, 0, Vector2.Zero, lvlScale, SpriteEffects.None, 0);
                }
                else
                {
                    // Empty slot number
                    string num = $"{slot + 1}";
                    float numScale = slot == 0 ? 0.9f : 0.65f;
                    DrawTextCentered(sb, font, num, center, new Color(100, 110, 140, 180), numScale);
                }
            }
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
