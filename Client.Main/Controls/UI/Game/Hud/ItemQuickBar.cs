#nullable enable
using Client.Main.Controllers;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Core.Client;
using Client.Main.Models;
using Client.Main.Networking;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Threading.Tasks;

namespace Client.Main.Controls.UI.Game.Hud
{
    /// <summary>
    /// Quick-use item bar: 4 slots bound to Q / W / E / R.
    /// Assign: Ctrl+Q/W/E/R while hovering an item in inventory.
    /// Use:    Q / W / E / R in-game (outside chat).
    /// </summary>
    public class ItemQuickBar : UIControl
    {
        private const int SlotCount = 4;
        private const int SlotSize = 44;
        private const int SlotSpacing = 4;
        private const int LabelHeight = 14;

        private static readonly Keys[] SlotKeys = { Keys.Q, Keys.W, Keys.E, Keys.R };
        private static readonly string[] SlotLabels = { "Q", "W", "E", "R" };

        // Each slot stores the inventory slot index (or -1 if empty) and a display copy
        public static ItemQuickBar? Instance { get; private set; }

        private readonly int[] _slotInventoryIndex = new int[SlotCount];
        private readonly InventoryItem?[] _slotItem = new InventoryItem?[SlotCount];

        private readonly NetworkManager _network;
        private SpriteFont? _font;
        private Texture2D? _pixel;

        public ItemQuickBar(NetworkManager network)
        {
            _network = network;
            Instance = this;

            int totalW = SlotCount * SlotSize + (SlotCount - 1) * SlotSpacing;
            ViewSize = new Point(totalW, SlotSize + LabelHeight);
            AutoViewSize = false;
            Interactive = false;
            BackgroundColor = Color.Transparent;
            BorderColor = Color.Transparent;

            for (int i = 0; i < SlotCount; i++)
                _slotInventoryIndex[i] = -1;
        }

        public override async Task Load()
        {
            await base.Load();
            _font = GraphicsManager.Instance.Font;
            _pixel = GraphicsManager.Instance.Pixel;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Try to assign an inventory item to a slot.
        /// Called from inventory when user presses Ctrl+Q/W/E/R.
        /// </summary>
        public void AssignItem(int slotIndex, InventoryItem item, int inventorySlot)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) return;
            _slotItem[slotIndex] = item;
            _slotInventoryIndex[slotIndex] = inventorySlot;
        }

        public void ClearSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount) return;
            _slotItem[slotIndex] = null;
            _slotInventoryIndex[slotIndex] = -1;
        }

        /// <summary>
        /// Returns the Keys array so GameSceneHotkeys can check them.
        /// </summary>
        public static Keys GetKey(int slotIndex) => SlotKeys[slotIndex];

        // ── Slot key usage ────────────────────────────────────────────────────

        public void HandleKeys(KeyboardState current, KeyboardState prev)
        {
            for (int i = 0; i < SlotCount; i++)
            {
                bool justPressed = current.IsKeyDown(SlotKeys[i]) && !prev.IsKeyDown(SlotKeys[i]);
                if (justPressed)
                    UseSlot(i);
            }
        }

        private void UseSlot(int slotIndex)
        {
            int invSlot = _slotInventoryIndex[slotIndex];
            var item = _slotItem[slotIndex];
            if (invSlot < 0 || item == null) return;

            var svc = _network?.GetCharacterService();
            var state = _network?.GetCharacterState();
            if (svc == null || state == null) return;

            // Play sound
            string? name = item.Definition?.Name?.ToLowerInvariant();
            if (name?.Contains("apple") == true)
                SoundController.Instance.PlayBuffer("Sound/pEatApple.wav");
            else
                SoundController.Instance.PlayBuffer("Sound/pDrink.wav");

            _ = Task.Run(async () =>
            {
                await svc.SendConsumeItemRequestAsync((byte)invSlot);
                await Task.Delay(350);
                MuGame.ScheduleOnMainThread(() =>
                {
                    // Re-check if item still exists at that slot; if not, clear
                    var items = state.GetInventoryItems();
                    if (!items.ContainsKey((byte)invSlot))
                        ClearSlot(slotIndex);
                    state.RaiseInventoryChanged();
                });
            });
        }

        // ── Drawing ───────────────────────────────────────────────────────────

        public override void Draw(GameTime gameTime)
        {
            if (_pixel == null || _font == null) return;

            var sb = GraphicsManager.Instance.Sprite;

            for (int i = 0; i < SlotCount; i++)
            {
                int slotX = X + i * (SlotSize + SlotSpacing);
                int slotY = Y;
                var slotRect = new Rectangle(slotX, slotY, SlotSize, SlotSize);

                // Background
                Color bg = _slotItem[i] != null
                    ? new Color(10, 10, 20, 210)
                    : new Color(6, 6, 12, 180);
                sb.Draw(_pixel, slotRect, bg);

                // Border
                Color border = _slotItem[i] != null
                    ? new Color(160, 130, 50)
                    : new Color(60, 55, 40);
                DrawBorder(sb, slotRect, border);

                // Item name (abbreviated)
                var item = _slotItem[i];
                if (item != null)
                {
                    string shortName = GetShortName(item.Definition?.Name ?? "?");
                    float scale = 0.52f;
                    Vector2 sz = _font.MeasureString(shortName) * scale;
                    float tx = slotX + (SlotSize - sz.X) / 2f;
                    float ty = slotY + (SlotSize - sz.Y) / 2f - 4;
                    sb.DrawString(_font, shortName, new Vector2(tx + 1, ty + 1), Color.Black * 0.8f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                    sb.DrawString(_font, shortName, new Vector2(tx, ty), new Color(220, 200, 120), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }

                // Key label at bottom
                string keyLabel = SlotLabels[i];
                float ks = 0.55f;
                Vector2 ksz = _font.MeasureString(keyLabel) * ks;
                float kx = slotX + (SlotSize - ksz.X) / 2f;
                float ky = slotY + SlotSize + 1;
                sb.DrawString(_font, keyLabel, new Vector2(kx, ky), new Color(120, 110, 70), 0f, Vector2.Zero, ks, SpriteEffects.None, 0f);
            }
        }

        private void DrawBorder(SpriteBatch sb, Rectangle r, Color c)
        {
            sb.Draw(_pixel!, new Rectangle(r.X, r.Y, r.Width, 1), c);
            sb.Draw(_pixel!, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), c);
            sb.Draw(_pixel!, new Rectangle(r.X, r.Y, 1, r.Height), c);
            sb.Draw(_pixel!, new Rectangle(r.Right - 1, r.Y, 1, r.Height), c);
        }

        private static string GetShortName(string name)
        {
            // Shorten common potion names
            if (name.Contains("Small", StringComparison.OrdinalIgnoreCase)) return "HP-S";
            if (name.Contains("Medium", StringComparison.OrdinalIgnoreCase) && name.Contains("Health", StringComparison.OrdinalIgnoreCase)) return "HP-M";
            if (name.Contains("Large", StringComparison.OrdinalIgnoreCase) && name.Contains("Health", StringComparison.OrdinalIgnoreCase)) return "HP-L";
            if (name.Contains("Health", StringComparison.OrdinalIgnoreCase)) return "HP";
            if (name.Contains("Mana", StringComparison.OrdinalIgnoreCase) && name.Contains("Small", StringComparison.OrdinalIgnoreCase)) return "MP-S";
            if (name.Contains("Mana", StringComparison.OrdinalIgnoreCase) && name.Contains("Medium", StringComparison.OrdinalIgnoreCase)) return "MP-M";
            if (name.Contains("Mana", StringComparison.OrdinalIgnoreCase) && name.Contains("Large", StringComparison.OrdinalIgnoreCase)) return "MP-L";
            if (name.Contains("Mana", StringComparison.OrdinalIgnoreCase)) return "MP";
            if (name.Contains("Shield", StringComparison.OrdinalIgnoreCase)) return "SD";
            if (name.Contains("Apple", StringComparison.OrdinalIgnoreCase)) return "Apple";
            if (name.Contains("Antidote", StringComparison.OrdinalIgnoreCase)) return "Anti";
            if (name.Length <= 6) return name;
            return name[..5] + "..";
        }
    }
}
