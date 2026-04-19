using Client.Main.Controllers;
using Client.Main.Controls.UI;
using Client.Main.Helpers;
using Client.Main.Controls;
using Client.Main.Objects.Player;
using Client.Main.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;
using System;

namespace Client.Main.Controls.UI.Android
{
    public class VirtualJoystick : UIControl
    {
        private const float BaseRadius = 90f;
        private const float KnobRadius = 38f;
        private const float MaxKnobOffset = BaseRadius - KnobRadius;
        private const float DeadZone = 0.12f;
        private const float MoveIntervalMs = 120f;

        // ring texture (donut) and filled circle for knob
        private Texture2D _ringTex;
        private Texture2D _circleTex;

        private Vector2 _knobOffset;
        private int _activeTouchId = -1;
        private bool _isActive;
        private double _moveTimer;

        public bool IsActive => _isActive;
        public Vector2 Direction { get; private set; }

        public VirtualJoystick()
        {
            Interactive = true;
            Visible = true;
        }

        public override async System.Threading.Tasks.Task Load()
        {
            _ringTex = CreateRingTexture(256, 18);
            _circleTex = CreateFilledCircleTexture(128);
            await base.Load();
        }

        private Vector2 GetCenter()
        {
            var vp = MuGame.Instance.GraphicsDevice.Viewport;
            return new Vector2(BaseRadius + 80, vp.Height - BaseRadius - 130);
        }

        public override void Update(GameTime gameTime)
        {
            if (!Visible) return;

            var touches = MuGame.Instance.Touch;
            var vp = MuGame.Instance.GraphicsDevice.Viewport;
            var center = GetCenter();
            // Activation zone: circle of radius ActivationRadius around joystick center
            const float ActivationRadius = 160f;

            bool found = false;
            foreach (var touch in touches)
            {
                var pos = touch.Position;

                if (touch.Id == _activeTouchId)
                {
                    if (touch.State == TouchLocationState.Released)
                    {
                        Release();
                        break;
                    }
                    var delta = pos - center;
                    float dist = delta.Length();
                    if (dist > MaxKnobOffset)
                        delta = delta / dist * MaxKnobOffset;
                    _knobOffset = delta;
                    float norm = MaxKnobOffset > 0 ? delta.Length() / MaxKnobOffset : 0f;
                    Direction = norm > DeadZone ? Vector2.Normalize(delta) : Vector2.Zero;
                    AndroidHUD.ConsumedTouchIds.Add(touch.Id);
                    found = true;
                    break;
                }

                float tdx = pos.X - center.X;
                float tdy = pos.Y - center.Y;
                bool inZone = tdx * tdx + tdy * tdy <= ActivationRadius * ActivationRadius;
                if (_activeTouchId == -1 &&
                    touch.State == TouchLocationState.Pressed &&
                    inZone)
                {
                    _activeTouchId = touch.Id;
                    // Fixed center — knob moves, base stays
                    _knobOffset = Vector2.Zero;
                    Direction = Vector2.Zero;
                    _isActive = true;
                    found = true;
                    AndroidHUD.ConsumedTouchIds.Add(touch.Id);
                    break;
                }
            }

            if (!found && _activeTouchId != -1)
                Release();

            if (_isActive && Direction != Vector2.Zero)
            {
                _moveTimer += gameTime.ElapsedGameTime.TotalMilliseconds;
                if (_moveTimer >= MoveIntervalMs)
                {
                    _moveTimer = 0;
                    try { DriveMovement(); } catch { }
                }
            }
            else
            {
                _moveTimer = MoveIntervalMs;
            }
        }

        private void DriveMovement()
        {
            if (MuGame.Instance.ActiveScene is not Scenes.GameScene gs) return;
            var hero = gs.Hero;
            if (hero == null || hero.IsDead) return;
            if (gs.World is not WalkableWorldControl) return;

            var iso = new Vector2(
                Direction.X + Direction.Y,
                Direction.X - Direction.Y);

            var raw = hero.Location + iso * 6f;
            // BuildDirectPath requires integer coordinates to avoid infinite loop
            var targetTile = new Vector2(
                Math.Clamp((float)Math.Round(raw.X), 0, Constants.TERRAIN_SIZE - 1),
                Math.Clamp((float)Math.Round(raw.Y), 0, Constants.TERRAIN_SIZE - 1));

            hero.MoveTo(targetTile, sendToServer: false, usePathfinding: false);
        }

        private void Release()
        {
            _activeTouchId = -1;
            _isActive = false;
            _knobOffset = Vector2.Zero;
            Direction = Vector2.Zero;
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible || _ringTex == null || _circleTex == null) return;

            var sb = GraphicsManager.Instance.Sprite;
            Vector2 drawCenter = GetCenter();

            using (new SpriteBatchScope(sb, SpriteSortMode.Deferred, BlendState.NonPremultiplied,
                SamplerState.LinearClamp, DepthStencilState.None))
            {
                // Outer ring — semi-transparent white
                DrawRing(sb, drawCenter, BaseRadius, new Color(255, 255, 255, 140));

                // Subtle direction dots at N/S/E/W (optional ticks)
                // (skip for cleanliness)

                // Knob filled circle
                Vector2 knobCenter = _isActive ? drawCenter + _knobOffset : drawCenter;
                DrawFilledCircle(sb, knobCenter, KnobRadius, new Color(80, 140, 255, 180));
                DrawFilledCircle(sb, knobCenter, KnobRadius * 0.6f, new Color(180, 210, 255, 150));

                // Inner thin ring on knob edge
                DrawRing(sb, knobCenter, KnobRadius, new Color(200, 225, 255, 200));
            }
        }

        private void DrawRing(SpriteBatch sb, Vector2 center, float radius, Color color)
        {
            int d = (int)(radius * 2);
            var rect = new Rectangle((int)(center.X - radius), (int)(center.Y - radius), d, d);
            sb.Draw(_ringTex, rect, color);
        }

        private void DrawFilledCircle(SpriteBatch sb, Vector2 center, float radius, Color color)
        {
            int d = (int)(radius * 2);
            var rect = new Rectangle((int)(center.X - radius), (int)(center.Y - radius), d, d);
            sb.Draw(_circleTex, rect, color);
        }

        // Ring (donut): alpha=1 near edge, alpha=0 in center
        private static Texture2D CreateRingTexture(int size, int ringWidth)
        {
            var tex = new Texture2D(MuGame.Instance.GraphicsDevice, size, size);
            var data = new Color[size * size];
            float r = size / 2f;
            float cx = r, cy = r;
            float inner = r - ringWidth;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);
                    // alpha ramps up near outer edge and drops in center
                    float alpha = 0f;
                    if (dist <= r && dist >= inner)
                    {
                        float t = (dist - inner) / ringWidth;         // 0 at inner, 1 at outer
                        float edgeFade = MathF.Min(t * 3f, (1f - t) * 3f); // fade at both edges
                        alpha = Math.Clamp(edgeFade, 0f, 1f);
                    }
                    data[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetData(data);
            return tex;
        }

        // Solid filled circle with soft edge
        private static Texture2D CreateFilledCircleTexture(int size)
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
                    float alpha = Math.Clamp(1f - MathF.Max(0f, dist - r + 2f) / 2f, 0f, 1f);
                    data[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetData(data);
            return tex;
        }

        public override void Dispose()
        {
            if (_ringTex != null) { _ringTex.Dispose(); _ringTex = null; }
            if (_circleTex != null) { _circleTex.Dispose(); _circleTex = null; }
            base.Dispose();
        }
    }
}
