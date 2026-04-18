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
    /// <summary>
    /// Semi-transparent virtual joystick for mobile movement control.
    /// Occupies the left third of the screen. The first touch in that area
    /// sets the joystick center and the knob tracks the finger.
    /// </summary>
    public class VirtualJoystick : UIControl
    {
        private const float BaseRadius = 80f;
        private const float KnobRadius = 36f;
        private const float MaxKnobOffset = BaseRadius - KnobRadius;
        private const float DeadZone = 0.15f;
        private const float MoveIntervalMs = 180f;

        private Texture2D _circleTex;
        private Vector2 _center;
        private Vector2 _knobOffset;
        private int _activeTouchId = -1;
        private bool _isActive;
        private double _moveTimer;

        // Default resting position (bottom-left area)
        private Vector2 _defaultCenter;

        public bool IsActive => _isActive;
        public Vector2 Direction { get; private set; }

        public VirtualJoystick()
        {
            Interactive = true;
            Visible = true;
        }

        public override async System.Threading.Tasks.Task Load()
        {
            _circleTex = CreateCircleTexture(128);
            var vp = MuGame.Instance.GraphicsDevice.Viewport;
            _defaultCenter = new Vector2(
                BaseRadius + 30,
                vp.Height - BaseRadius - 30);
            _center = _defaultCenter;
            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            if (!Visible) return;

            var touches = MuGame.Instance.Touch;
            var vp = MuGame.Instance.GraphicsDevice.Viewport;
            float leftBoundary = vp.Width * 0.4f;

            bool found = false;
            foreach (var touch in touches)
            {
                // Scale touch position from screen pixels to viewport
                var pos = touch.Position;

                if (touch.Id == _activeTouchId)
                {
                    if (touch.State == TouchLocationState.Released)
                    {
                        Release();
                        break;
                    }
                    // Update knob
                    var delta = pos - _center;
                    float dist = delta.Length();
                    if (dist > MaxKnobOffset)
                        delta = delta / dist * MaxKnobOffset;
                    _knobOffset = delta;
                    float norm = MaxKnobOffset > 0 ? delta.Length() / MaxKnobOffset : 0;
                    Direction = norm > DeadZone ? Vector2.Normalize(delta) : Vector2.Zero;
                    found = true;
                    break;
                }

                if (_activeTouchId == -1 &&
                    (touch.State == TouchLocationState.Pressed) &&
                    pos.X < leftBoundary && pos.Y > vp.Height * 0.3f)
                {
                    _activeTouchId = touch.Id;
                    _center = pos;
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

            // Drive character movement
            if (_isActive && Direction != Vector2.Zero)
            {
                _moveTimer += gameTime.ElapsedGameTime.TotalMilliseconds;
                if (_moveTimer >= MoveIntervalMs)
                {
                    _moveTimer = 0;
                    DriveMovement();
                }
            }
            else
            {
                _moveTimer = MoveIntervalMs; // fire immediately on next active frame
            }
        }

        private void DriveMovement()
        {
            if (MuGame.Instance.ActiveScene is not Scenes.GameScene gs) return;
            var hero = gs.Hero;
            if (hero == null || hero.IsDead) return;
            if (gs.World is not WalkableWorldControl world) return;

            // Map joystick direction to isometric movement
            // Joystick Y axis: up = north-west, down = south-east
            // Joystick X axis: right = north-east, left = south-west
            var iso = new Vector2(
                Direction.X - Direction.Y,  // isometric X
                Direction.X + Direction.Y   // isometric Y
            );

            var targetTile = hero.Location + iso * 8f;
            targetTile = Vector2.Clamp(targetTile,
                Vector2.Zero,
                new Vector2(Constants.TERRAIN_SIZE - 1, Constants.TERRAIN_SIZE - 1));

            hero.MoveTo(targetTile, sendToServer: true, usePathfinding: false);
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
            if (!Visible || _circleTex == null) return;

            var sb = GraphicsManager.Instance.Sprite;
            var vp = MuGame.Instance.GraphicsDevice.Viewport;

            // When inactive draw the resting outline at default position
            Vector2 drawCenter = _isActive ? _center : _defaultCenter;

            using (new SpriteBatchScope(sb, SpriteSortMode.Deferred, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None))
            {
                // Base ring (semi-transparent)
                DrawCircle(sb, drawCenter, BaseRadius, new Color(255, 255, 255, 60));
                DrawCircle(sb, drawCenter, BaseRadius - 6, new Color(0, 0, 0, 40));

                // Knob
                Vector2 knobCenter = _isActive ? drawCenter + _knobOffset : drawCenter;
                DrawCircle(sb, knobCenter, KnobRadius, new Color(255, 255, 255, 110));
                DrawCircle(sb, knobCenter, KnobRadius - 4, new Color(100, 160, 255, 90));
            }
        }

        private void DrawCircle(SpriteBatch sb, Vector2 center, float radius, Color color)
        {
            int size = (int)(radius * 2);
            var rect = new Rectangle((int)(center.X - radius), (int)(center.Y - radius), size, size);
            sb.Draw(_circleTex, rect, color);
        }

        private Texture2D CreateCircleTexture(int size)
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
                    float alpha = MathF.Max(0, 1f - (dist - r + 1f));
                    alpha = MathF.Min(1f, alpha);
                    data[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetData(data);
            return tex;
        }

        protected override void Dispose(bool disposing)
        {
            _circleTex?.Dispose();
            base.Dispose(disposing);
        }
    }
}
