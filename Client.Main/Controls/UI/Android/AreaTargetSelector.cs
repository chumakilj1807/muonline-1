using Client.Main.Controls;
using Client.Main.Controllers;
using Client.Main.Controls.UI;
using Client.Main.Helpers;
using Client.Main.Core.Client;
using Client.Main.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;
using System;

namespace Client.Main.Controls.UI.Android
{
    /// <summary>
    /// Displayed when the player taps an area/targeted skill slot on mobile.
    /// Shows a draggable circle in the world; while active, auto-attacks enemies in the circle.
    /// Cancelled by tapping outside or via the cancel button.
    /// </summary>
    public class AreaTargetSelector : UIControl
    {
        private const float AreaTileRadius = 5f;
        private const float AutoAttackIntervalMs = 800f;

        private Texture2D _pixel;
        private Texture2D _circleTex;
        private SkillEntryState _skill;
        private Vector2 _targetTile;
        private bool _isDragging;
        private int _dragTouchId = -1;
        private double _attackTimer;
        private bool _autoAttacking;

        public bool IsActive => Visible;

        public AreaTargetSelector()
        {
            Interactive = true;
            Visible = false;
        }

        public override async System.Threading.Tasks.Task Load()
        {
            _pixel = new Texture2D(MuGame.Instance.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            _circleTex = CreateCircleTexture(256);
            await base.Load();
        }

        public void Activate(SkillEntryState skill, Vector2 initialTile)
        {
            _skill = skill;
            _targetTile = initialTile;
            _attackTimer = AutoAttackIntervalMs; // fire immediately
            _autoAttacking = true;
            _isDragging = false;
            _dragTouchId = -1;
            Visible = true;
            BringToFront();
        }

        public void Deactivate()
        {
            Visible = false;
            _autoAttacking = false;
            _dragTouchId = -1;
        }

        public override void Update(GameTime gameTime)
        {
            if (!Visible) return;

            var touches = MuGame.Instance.Touch;
            var vp = MuGame.Instance.GraphicsDevice.Viewport;

            // Cancel button region (top-center, 80x50)
            var cancelRect = new Rectangle(vp.Width / 2 - 60, 10, 120, 50);

            foreach (var touch in touches)
            {
                if (AndroidHUD.ConsumedTouchIds.Contains(touch.Id)) continue;

                var pos = touch.Position;

                if (touch.State == TouchLocationState.Pressed)
                {
                    // Cancel button
                    if (cancelRect.Contains((int)pos.X, (int)pos.Y))
                    {
                        AndroidHUD.ConsumedTouchIds.Add(touch.Id);
                        Deactivate();
                        return;
                    }

                    // Begin drag to reposition circle
                    if (_dragTouchId == -1 && pos.X > vp.Width * 0.4f)
                    {
                        _dragTouchId = touch.Id;
                        _isDragging = true;
                        _targetTile = ScreenToTile(pos);
                        AndroidHUD.ConsumedTouchIds.Add(touch.Id);
                    }
                }
                else if (touch.State == TouchLocationState.Moved && touch.Id == _dragTouchId)
                {
                    _targetTile = ScreenToTile(pos);
                    AndroidHUD.ConsumedTouchIds.Add(touch.Id);
                }
                else if (touch.State == TouchLocationState.Released && touch.Id == _dragTouchId)
                {
                    _dragTouchId = -1;
                    _isDragging = false;
                    AndroidHUD.ConsumedTouchIds.Add(touch.Id);
                }
            }

            // Auto attack loop
            if (_autoAttacking)
            {
                _attackTimer += gameTime.ElapsedGameTime.TotalMilliseconds;
                if (_attackTimer >= AutoAttackIntervalMs)
                {
                    _attackTimer = 0;
                    TryFireSkill();
                }
            }
        }

        private void TryFireSkill()
        {
            if (MuGame.Instance.ActiveScene is not Scenes.GameScene gs) return;
            var hero = gs.Hero;
            if (hero == null || hero.IsDead) return;

            // Use Android skill invocation via AndroidHUD
            AndroidHUD.Current?.InvokeAreaSkill(_skill, _targetTile);
        }

        private Vector2 ScreenToTile(Vector2 screenPos)
        {
            if (MuGame.Instance.ActiveScene is Scenes.GameScene gs &&
                gs.World is WalkableWorldControl world)
            {
                // Use ray-casting from the active camera to get tile coords
                var viewport = MuGame.Instance.GraphicsDevice.Viewport;
                var cam = Camera.Instance;

                var near = viewport.Unproject(
                    new Vector3(screenPos, 0f),
                    cam.Projection, cam.View, Matrix.Identity);
                var far = viewport.Unproject(
                    new Vector3(screenPos, 1f),
                    cam.Projection, cam.View, Matrix.Identity);

                var dir = Vector3.Normalize(far - near);

                // Intersect with terrain plane (Y = 0 in world space, or use terrain height)
                if (MathF.Abs(dir.Y) > 0.001f)
                {
                    float t = -near.Y / dir.Y;
                    var hit = near + dir * t;

                    // Convert world space hit to tile coords
                    // MU uses a specific coordinate system — approximate:
                    float tileX = hit.X / Constants.TERRAIN_SCALE;
                    float tileY = hit.Z / Constants.TERRAIN_SCALE;

                    tileX = Math.Clamp(tileX, 0, Constants.TERRAIN_SIZE - 1);
                    tileY = Math.Clamp(tileY, 0, Constants.TERRAIN_SIZE - 1);
                    return new Vector2(tileX, tileY);
                }
            }
            return _targetTile; // keep last known
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible || _pixel == null) return;

            var vp = MuGame.Instance.GraphicsDevice.Viewport;
            var sb = GraphicsManager.Instance.Sprite;

            if (!TryTileToScreen(_targetTile, out var screenCenter)) return;

            TryTileToScreen(_targetTile + new Vector2(AreaTileRadius, 0), out var edgeScreen);
            float screenRadius = MathF.Max(35, Vector2.Distance(
                new Vector2(screenCenter.X, screenCenter.Y),
                new Vector2(edgeScreen.X, edgeScreen.Y)));

            float pulse = 0.7f + 0.3f * MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 4f);

            using (new SpriteBatchScope(sb, SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None))
            {
                // Thin ring only — draw as outer circle minus a slightly smaller circle
                // Using the ring texture baked as donut
                int d = (int)(screenRadius * 2);
                var ringRect = new Rectangle(
                    (int)(screenCenter.X - screenRadius),
                    (int)(screenCenter.Y - screenRadius),
                    d, d);

                // Primary ring — orange/red pulsing
                sb.Draw(_circleTex, ringRect, new Color(255, 140, 30, (int)(220 * pulse)));

                // Bright inner highlight ring (slightly smaller)
                float innerR = screenRadius * 0.88f;
                int di = (int)(innerR * 2);
                var innerRect = new Rectangle(
                    (int)(screenCenter.X - innerR),
                    (int)(screenCenter.Y - innerR),
                    di, di);
                sb.Draw(_circleTex, innerRect, new Color(255, 220, 80, (int)(120 * pulse)));

                // Cancel button (top-center)
                int cx = vp.Width / 2;
                sb.Draw(_pixel, new Rectangle(cx - 55, 12, 110, 44), new Color(180, 20, 20, 220));
                DrawText(sb, "CANCEL", new Vector2(cx - 32, 22), Color.White, 0.65f);
            }
        }

        private bool TryTileToScreen(Vector2 tile, out Vector3 screen)
        {
            screen = Vector3.Zero;
            if (MuGame.Instance.ActiveScene is not Scenes.GameScene gs) return false;
            if (gs.World is not WalkableWorldControl world) return false;

            // Convert tile to world position (approximate — using terrain scale)
            float wx = tile.X * Constants.TERRAIN_SCALE;
            float wz = tile.Y * Constants.TERRAIN_SCALE;
            float wy = 0; // world Y (up) — terrain height approximation

            var worldPos = new Vector3(wx, wy, wz);
            var vp = MuGame.Instance.GraphicsDevice.Viewport;
            screen = vp.Project(worldPos, Camera.Instance.Projection, Camera.Instance.View, Matrix.Identity);
            return screen.Z is >= 0 and <= 1;
        }

        private void DrawText(SpriteBatch sb, string text, Vector2 pos, Color color, float scale)
        {
            var font = GraphicsManager.Instance.Font;
            if (font == null) return;
            sb.DrawString(font, text, pos + Vector2.One, Color.Black * 0.6f, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
            sb.DrawString(font, text, pos, color, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        }

        // Ring (donut) texture — thick band near the edge, transparent in center
        private static Texture2D CreateCircleTexture(int size)
        {
            var tex = new Texture2D(MuGame.Instance.GraphicsDevice, size, size);
            var data = new Color[size * size];
            float r = size / 2f;
            float cx = r, cy = r;
            int ringWidth = Math.Max(6, size / 12);
            float inner = r - ringWidth;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    float alpha = 0f;
                    if (dist >= inner && dist <= r)
                    {
                        float t = (dist - inner) / ringWidth;
                        float fade = MathF.Min(t * 4f, (1f - t) * 4f);
                        alpha = MathF.Clamp(fade, 0f, 1f);
                    }
                    data[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetData(data);
            return tex;
        }

        public override void Dispose()
        {
            if (_pixel != null) { _pixel.Dispose(); _pixel = null; }
            if (_circleTex != null) { _circleTex.Dispose(); _circleTex = null; }
            base.Dispose();
        }
    }
}
