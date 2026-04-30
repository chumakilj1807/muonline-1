#nullable enable
using System;
using System.Threading.Tasks;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Graphics;
using Client.Main.Helpers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Dark Lord Fire Burst (ID 61) — multiple fire chains radiating from caster toward target.
    /// </summary>
    public sealed class DarkLordFireChainEffect : EffectObject
    {
        private const string FireTexPath  = "Effect/firehik01.jpg";
        private const string GlowTexPath  = "Effect/flare.jpg";
        private const float  Duration     = 0.70f;
        private const int    ChainCount   = 5;
        private const int    SegPerChain  = 9;
        private const float  VisScale     = 10f;

        private readonly Vector3 _start;
        private readonly Vector3 _target;
        private float _time;

        private Texture2D? _fireTex;
        private Texture2D? _glowTex;
        private SpriteBatch? _sb;

        private readonly float[] _spreadAngle = new float[ChainCount];
        private readonly float[] _wavePhase   = new float[ChainCount];

        private readonly DynamicLight _light;
        private bool _lightAdded;

        public DarkLordFireChainEffect(Vector3 start, Vector3 target)
        {
            _start  = start;
            _target = target;

            IsTransparent         = true;
            AffectedByTransparency = true;
            BlendState            = BlendState.Additive;
            DepthState            = DepthStencilState.DepthRead;
            BoundingBoxLocal      = new BoundingBox(new Vector3(-400f), new Vector3(400f));

            for (int i = 0; i < ChainCount; i++)
            {
                _spreadAngle[i] = MathHelper.Lerp(-0.42f, 0.42f, i / (float)(ChainCount - 1));
                _wavePhase[i]   = (float)(MuGame.Random.NextDouble() * MathHelper.TwoPi);
            }

            _light = new DynamicLight
            {
                Owner     = this,
                Position  = start,
                Color     = new Vector3(1f, 0.28f, 0.04f),
                Radius    = 280f,
                Intensity = 0f
            };
        }

        public override async Task LoadContent()
        {
            await base.LoadContent();
            await TextureLoader.Instance.Prepare(FireTexPath);
            await TextureLoader.Instance.Prepare(GlowTexPath);
            _fireTex = TextureLoader.Instance.GetTexture2D(FireTexPath) ?? GraphicsManager.Instance.Pixel;
            _glowTex = TextureLoader.Instance.GetTexture2D(GlowTexPath) ?? GraphicsManager.Instance.Pixel;
            _sb      = GraphicsManager.Instance.Sprite;

            if (World?.Terrain != null && !_lightAdded)
            {
                World.Terrain.AddDynamicLight(_light);
                _lightAdded = true;
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (Status == GameControlStatus.NonInitialized) _ = Load();
            if (Status != GameControlStatus.Ready) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _time += dt;

            float t = MathHelper.Clamp(_time / Duration, 0f, 1f);
            _light.Position  = Vector3.Lerp(_start, _target, t);
            _light.Intensity = MathF.Sin(t * MathF.PI) * 1.6f;

            if (_time >= Duration) RemoveSelf();
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
            if (_sb == null || _fireTex == null || _glowTex == null) return;
            if (!Visible) return;

            float t = MathHelper.Clamp(_time / Duration, 0f, 1f);
            float overallAlpha = MathF.Sin(t * MathF.PI);

            using (new SpriteBatchScope(_sb, SpriteSortMode.Deferred, BlendState.Additive,
                SamplerState.LinearClamp, DepthStencilState.DepthRead))
            {
                Vector3 baseDir = _target - _start;
                float dist = baseDir.Length();
                if (dist < 0.1f) return;
                baseDir /= dist;

                // Perpendicular axis for spread/wave
                Vector3 up = Math.Abs(Vector3.Dot(baseDir, Vector3.UnitZ)) < 0.9f
                    ? Vector3.UnitZ : Vector3.UnitX;
                Vector3 perp = Vector3.Normalize(Vector3.Cross(baseDir, up));

                // How far along the chains have travelled (0→1 over first 70% of duration)
                float travelT = MathHelper.Clamp(_time / (Duration * 0.70f), 0f, 1f);

                for (int c = 0; c < ChainCount; c++)
                {
                    float spread   = _spreadAngle[c];
                    float wavePh   = _wavePhase[c] + _time * 7f;

                    // Spread direction: rotate baseDir slightly toward perp
                    Vector3 chainDir = Vector3.Normalize(baseDir + perp * spread);

                    for (int s = 0; s < SegPerChain; s++)
                    {
                        float segFrac  = (s + 0.5f) / SegPerChain;
                        if (segFrac > travelT) break;  // chain tip hasn't reached this segment yet

                        float wave     = MathF.Sin(wavePh + s * 1.1f) * 18f;
                        Vector3 wPos   = _start + chainDir * (dist * segFrac) + perp * wave;

                        // Brightness: brighter at the leading tip
                        float segAlpha = overallAlpha * MathHelper.Lerp(0.30f, 0.95f, segFrac / travelT);

                        // Alternate orange/crimson for each chain segment
                        float rg = c % 2 == 0 ? 0.18f : 0.10f;
                        Color col = new Color(1f, rg, 0.03f, segAlpha);

                        // Elongated sprite aligned to chain direction
                        float rotation = MathF.Atan2(chainDir.Y + wave * 0.002f, chainDir.X);
                        DrawSprite(_fireTex, wPos, col, rotation, new Vector2(1.6f, 3.2f), VisScale);
                    }

                    // Bright tip glow at leading edge
                    if (travelT > 0f)
                    {
                        float wave   = MathF.Sin(wavePh + (SegPerChain - 1) * 1.1f) * 18f;
                        Vector3 tip  = _start + chainDir * (dist * travelT) + perp * wave;
                        Color tipCol = new Color(1f, 0.45f, 0.15f, overallAlpha * 0.85f);
                        DrawSprite(_glowTex, tip, tipCol, _time * 5f, new Vector2(1.4f), VisScale);
                    }
                }

                // Origin flash at start
                Color originCol = new Color(1f, 0.5f, 0.2f, overallAlpha * 0.6f);
                DrawSprite(_glowTex, _start, originCol, _time * 3f,
                    new Vector2(2.0f * (1f - t * 0.5f)), VisScale);
            }
        }

        private void DrawSprite(Texture2D tex, Vector3 worldPos, Color color,
            float rotation, Vector2 scale, float scaleMultiplier = 1f)
        {
            var vp   = GraphicsDevice.Viewport;
            var proj = vp.Project(worldPos, Camera.Instance.Projection, Camera.Instance.View, Matrix.Identity);
            if (proj.Z < 0f || proj.Z > 1f) return;

            float baseScale  = ComputeScreenScale(worldPos, 1f);
            Vector2 finalScale = scale * baseScale * scaleMultiplier;
            float depth = MathHelper.Clamp(proj.Z, 0f, 1f);

            _sb!.Draw(tex, new Vector2(proj.X, proj.Y), null, color,
                rotation,
                new Vector2(tex.Width * 0.5f, tex.Height * 0.5f),
                finalScale, SpriteEffects.None, depth);
        }

        private static float ComputeScreenScale(Vector3 worldPos, float baseScale)
        {
            float dist = Vector3.Distance(Camera.Instance.Position, worldPos);
            return baseScale / (MathF.Max(dist, 0.1f) / Constants.TERRAIN_SIZE) * Constants.RENDER_SCALE;
        }

        private void RemoveSelf()
        {
            if (Parent != null) Parent.Children.Remove(this);
            else World?.RemoveObject(this);
            Dispose();
        }

        public override void Dispose()
        {
            if (_lightAdded && World?.Terrain != null)
            {
                World.Terrain.RemoveDynamicLight(_light);
                _lightAdded = false;
            }
            base.Dispose();
        }
    }
}
