using Client.Data.BMD;
using Client.Main.Controllers;
using Client.Main.Controls.UI;
using Client.Main.Helpers;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Client.Main.Controls.UI.Android
{
    /// <summary>
    /// Full-screen overlay for selecting and assigning skills to the 4 skill slots.
    /// Flow: open → tap a skill → choose slot (1-4) → close.
    /// </summary>
    public class AndroidSkillMenu : UIControl
    {
        private const int ItemHeight = 70;
        private const int ItemPadding = 8;
        private const int SlotButtonSize = 80;

        private Texture2D _pixel;
        private List<SkillMenuItem> _items = new();
        private SkillMenuItem _pendingSkill;
        private bool _awaitingSlotChoice;
        private int _scrollOffset;
        private float _scrollY;
        private Vector2 _lastTouchPos;
        private bool _dragging;

        public Action<Core.Client.SkillEntryState, int> SkillAssigned;

        private struct SkillMenuItem
        {
            public Core.Client.SkillEntryState Entry;
            public string Name;
            public SkillType Type;
            public Color TypeColor;
        }

        public AndroidSkillMenu()
        {
            Interactive = true;
            Visible = false;
        }

        public override async System.Threading.Tasks.Task Load()
        {
            _pixel = new Texture2D(MuGame.Instance.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            await base.Load();
        }

        public void Open()
        {
            RefreshSkillList();
            _pendingSkill = default;
            _awaitingSlotChoice = false;
            _scrollY = 0;
            Visible = true;
            BringToFront();
        }

        public void Close()
        {
            Visible = false;
            _awaitingSlotChoice = false;
        }

        private void RefreshSkillList()
        {
            _items.Clear();
            var state = MuGame.Network?.GetCharacterState();
            if (state == null) return;

            foreach (var skill in state.GetSkills())
            {
                var type = SkillDefinitions.GetSkillType(skill.SkillId);
                _items.Add(new SkillMenuItem
                {
                    Entry = skill,
                    Name = SkillDatabase.GetSkillName(skill.SkillId),
                    Type = type,
                    TypeColor = type switch
                    {
                        SkillType.Area => new Color(255, 120, 30),
                        SkillType.Target => new Color(255, 220, 30),
                        _ => new Color(120, 220, 120)
                    }
                });
            }
        }

        public override void Update(GameTime gameTime)
        {
            if (!Visible) return;

            var vp = MuGame.Instance.GraphicsDevice.Viewport;
            var touches = MuGame.Instance.Touch;

            foreach (var touch in touches)
            {
                if (AndroidHUD.ConsumedTouchIds.Contains(touch.Id)) continue;
                AndroidHUD.ConsumedTouchIds.Add(touch.Id);

                var pos = touch.Position;

                if (touch.State == TouchLocationState.Pressed)
                {
                    _lastTouchPos = pos;
                    _dragging = false;

                    // Close button (top-right X)
                    if (pos.X > vp.Width - 80 && pos.Y < 80)
                    {
                        Close();
                        return;
                    }

                    if (_awaitingSlotChoice)
                    {
                        HandleSlotChoice(pos, vp);
                    }
                    else
                    {
                        HandleSkillTap(pos, vp);
                    }
                }
                else if (touch.State == TouchLocationState.Moved)
                {
                    float dy = pos.Y - _lastTouchPos.Y;
                    if (MathF.Abs(dy) > 5) _dragging = true;
                    if (!_awaitingSlotChoice && _dragging)
                    {
                        _scrollY += dy;
                        float maxScroll = Math.Max(0, _items.Count * (ItemHeight + ItemPadding) - (vp.Height - 120));
                        _scrollY = Math.Clamp(_scrollY, -maxScroll, 0);
                    }
                    _lastTouchPos = pos;
                }
            }
        }

        private void HandleSkillTap(Vector2 pos, Viewport vp)
        {
            if (_dragging) return;

            int startY = 80;
            int contentY = startY + (int)_scrollY;
            int listWidth = vp.Width - 40;

            for (int i = 0; i < _items.Count; i++)
            {
                int iy = contentY + i * (ItemHeight + ItemPadding);
                var rect = new Rectangle(20, iy, listWidth, ItemHeight);
                if (rect.Contains((int)pos.X, (int)pos.Y))
                {
                    _pendingSkill = _items[i];
                    _awaitingSlotChoice = true;
                    return;
                }
            }
        }

        private void HandleSlotChoice(Vector2 pos, Viewport vp)
        {
            // 4 slot buttons centered on screen
            int cx = vp.Width / 2;
            int cy = vp.Height / 2;
            int spacing = SlotButtonSize + 16;
            int totalW = spacing * 4 - 16;
            int startX = cx - totalW / 2;
            int startY2 = cy - SlotButtonSize / 2;

            for (int slot = 0; slot < 4; slot++)
            {
                int sx = startX + slot * spacing;
                var rect = new Rectangle(sx, startY2, SlotButtonSize, SlotButtonSize);
                if (rect.Contains((int)pos.X, (int)pos.Y))
                {
                    SkillAssigned?.Invoke(_pendingSkill.Entry, slot);
                    _awaitingSlotChoice = false;
                    Close();
                    return;
                }
            }

            // Tap anywhere else to cancel slot choice
            _awaitingSlotChoice = false;
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible || _pixel == null) return;

            var vp = MuGame.Instance.GraphicsDevice.Viewport;
            var sb = GraphicsManager.Instance.Sprite;
            var font = GraphicsManager.Instance.Font;

            using (new SpriteBatchScope(sb, SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None))
            {
                // Full-screen dim background
                sb.Draw(_pixel, new Rectangle(0, 0, vp.Width, vp.Height), new Color(0, 0, 0, 200));

                // Title
                DrawText(sb, font, "SKILLS", new Vector2(20, 20), Color.Gold, 1.0f);

                // Close button
                DrawText(sb, font, "[X]", new Vector2(vp.Width - 75, 20), Color.OrangeRed, 0.9f);

                if (_awaitingSlotChoice)
                {
                    DrawSlotPicker(sb, font, vp);
                }
                else
                {
                    DrawSkillList(sb, font, vp);
                }
            }
        }

        private void DrawSkillList(SpriteBatch sb, SpriteFont font, Viewport vp)
        {
            int listWidth = vp.Width - 40;
            int startY = 80;
            int contentY = startY + (int)_scrollY;

            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                int iy = contentY + i * (ItemHeight + ItemPadding);

                if (iy + ItemHeight < startY || iy > vp.Height) continue;

                // Background
                sb.Draw(_pixel, new Rectangle(20, iy, listWidth, ItemHeight),
                    new Color(30, 30, 50, 200));

                // Type color stripe
                sb.Draw(_pixel, new Rectangle(20, iy, 6, ItemHeight), item.TypeColor);

                // Skill icon placeholder
                int iconSize = ItemHeight - 10;
                sb.Draw(_pixel, new Rectangle(32, iy + 5, iconSize, iconSize),
                    item.TypeColor * 0.3f);
                DrawTextCentered(sb, font, SkillIconLetter(item.Type),
                    new Rectangle(32, iy + 5, iconSize, iconSize), item.TypeColor, 0.8f);

                // Name + level
                string nameText = $"{item.Name} Lv.{item.Entry.SkillLevel}";
                DrawText(sb, font, nameText,
                    new Vector2(32 + iconSize + 10, iy + 8), Color.White, 0.7f);

                // Type label
                string typeLabel = item.Type == SkillType.Area ? "AREA" :
                                   item.Type == SkillType.Target ? "TARGET" : "SELF";
                DrawText(sb, font, typeLabel,
                    new Vector2(32 + iconSize + 10, iy + 36), item.TypeColor * 0.9f, 0.55f);
            }
        }

        private void DrawSlotPicker(SpriteBatch sb, SpriteFont font, Viewport vp)
        {
            // Dim + prompt
            DrawText(sb, font, $"Assign '{_pendingSkill.Name}' to slot:",
                new Vector2(vp.Width / 2f - 200, vp.Height / 2f - 80), Color.White, 0.75f);

            int cx = vp.Width / 2;
            int cy = vp.Height / 2;
            int spacing = SlotButtonSize + 16;
            int totalW = spacing * 4 - 16;
            int startX = cx - totalW / 2;

            string[] labels = { "①", "②", "③", "④" };
            for (int slot = 0; slot < 4; slot++)
            {
                int sx = startX + slot * spacing;
                int sy = cy - SlotButtonSize / 2;
                sb.Draw(_pixel, new Rectangle(sx, sy, SlotButtonSize, SlotButtonSize),
                    new Color(60, 80, 140, 220));
                sb.Draw(_pixel, new Rectangle(sx + 2, sy + 2, SlotButtonSize - 4, SlotButtonSize - 4),
                    new Color(80, 100, 180, 200));
                DrawTextCentered(sb, font, labels[slot],
                    new Rectangle(sx, sy, SlotButtonSize, SlotButtonSize), Color.White, 1.0f);
            }
        }

        private static string SkillIconLetter(SkillType type) => type switch
        {
            SkillType.Area => "A",
            SkillType.Target => "T",
            _ => "S"
        };

        private void DrawText(SpriteBatch sb, SpriteFont font, string text,
            Vector2 pos, Color color, float scale)
        {
            if (font == null) return;
            sb.DrawString(font, text, pos + Vector2.One, Color.Black * 0.6f, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
            sb.DrawString(font, text, pos, color, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        }

        private void DrawTextCentered(SpriteBatch sb, SpriteFont font, string text,
            Rectangle rect, Color color, float scale)
        {
            if (font == null) return;
            var size = font.MeasureString(text) * scale;
            var pos = new Vector2(rect.X + rect.Width / 2f - size.X / 2f,
                                  rect.Y + rect.Height / 2f - size.Y / 2f);
            sb.DrawString(font, text, pos, color, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        }

        public override void Dispose()
        {
            if (_pixel != null) { _pixel.Dispose(); _pixel = null; }
            base.Dispose();
        }
    }
}
