#nullable enable
using System;
using System.Threading.Tasks;
using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Controllers;
using Client.Main.Graphics;
using Client.Main.Helpers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Dark Lord Force skill — expanding golden energy ring + orbs radiating outward.
    /// </summary>
    public sealed class DarkLordForceEffect : EffectObject
    {
        private const string GlowPath  = "Effect/flare.jpg";
        private const string SparkPath = "Effect/Spark03.jpg";

        private const float TotalDuration = 0.85f;
        private const float MaxRingRadius = 300f;
        private const int   OrbCount = 8;

        private readonly Vector3 _center;
        private float _time;

        private Texture2D? _glowTex;
        private Texture2D? _sparkTex;
        private SpriteBatch? _sb;

        private readonly DynamicLight _light;
        private bool _lightAdded;

        public DarkLordForceEffect(Vector3 center)
        {
            _center = center;
            IsTransparent = true;
            AffectedByTransparency = true;
            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;
            BoundingBoxLocal = new BoundingBox(new Vector3(-400f), new Vector3(400f));

            _light = new DynamicLight
            {
                Owner = this,
                Position = center,
                Color = new Vector3(1f, 0.85f, 0.3f),
                Radius = 250f,
                Intensity = 0f
            };
        }

        public override async Task LoadContent()
        {
            await base.LoadContent();
            await TextureLoader.Instance.Prepare(GlowPath);
            await TextureLoader.Instance.Prepare(SparkPath);
            _glowTex  = TextureLoader.Instance.GetTexture2D(GlowPath)  ?? GraphicsManager.Instance.Pixel;
            _sparkTex = TextureLoader.Instance.GetTexture2D(SparkPath) ?? GraphicsManager.Instance.Pixel;
            _sb = GraphicsManager.Instance.Sprite;

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

            float t = MathHelper.Clamp(_time / TotalDuration, 0f, 1f);
            _light.Intensity = MathF.Sin(t * MathF.PI) * 1.4f;
            _light.Position  = _center;

            if (_time >= TotalDuration) RemoveSelf();
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
            if (_sb == null || _glowTex == null || _sparkTex == null) return;

            float t = MathHelper.Clamp(_time / TotalDuration, 0f, 1f);
            float ringR = MaxRingRadius * t;
            float alpha = MathF.Sin(t * MathF.PI);

            using (new SpriteBatchScope(_sb, SpriteSortMode.Deferred, BlendState, SamplerState.LinearClamp, DepthState))
            {
                // Expanding ring — draw multiple glow sprites in a circle
                int ringPoints = 20;
                for (int i = 0; i < ringPoints; i++)
                {
                    float angle = i / (float)ringPoints * MathHelper.TwoPi;
                    Vector3 pos = _center + new Vector3(MathF.Cos(angle) * ringR, MathF.Sin(angle) * ringR, 30f);
                    Color col = new Color(1f, 0.85f, 0.3f, alpha * 0.55f);
                    DrawSprite(_glowTex, pos, col, angle, new Vector2(0.8f, 0.8f), 10f);
                }

                // Central flash
                Color coreCol = new Color(1f, 0.95f, 0.5f, alpha * 0.7f);
                DrawSprite(_glowTex, _center + Vector3.UnitZ * 40f, coreCol, _time * 3f, new Vector2(2f * (1f - t) + 0.5f), 14f);

                // Radiating orbs
                for (int i = 0; i < OrbCount; i++)
                {
                    float baseAngle = i / (float)OrbCount * MathHelper.TwoPi;
                    float orbR = ringR * 0.7f;
                    Vector3 orbPos = _center + new Vector3(MathF.Cos(baseAngle) * orbR, MathF.Sin(baseAngle) * orbR, 40f + orbR * 0.1f);
                    Color orbCol = new Color(1f, 0.9f, 0.4f, alpha * 0.8f);
                    DrawSprite(_sparkTex, orbPos, orbCol, _time * 5f + i, new Vector2(1.2f), 10f);
                }
            }
        }

        private void DrawSprite(Texture2D tex, Vector3 worldPos, Color color, float rotation, Vector2 scale, float scaleMultiplier = 1f)
        {
            var vp = GraphicsDevice.Viewport;
            Vector3 proj = vp.Project(worldPos, Camera.Instance.Projection, Camera.Instance.View, Matrix.Identity);
            if (proj.Z < 0f || proj.Z > 1f) return;

            float baseScale = ComputeScreenScale(worldPos, 1f);
            Vector2 finalScale = scale * baseScale * scaleMultiplier;
            float depth = MathHelper.Clamp(proj.Z, 0f, 1f);

            _sb!.Draw(tex, new Vector2(proj.X, proj.Y), null, color, rotation,
                new Vector2(tex.Width * 0.5f, tex.Height * 0.5f), finalScale, SpriteEffects.None, depth);
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
