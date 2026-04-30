#nullable enable
using System;
using System.Threading.Tasks;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Dark Lord fire pillar effect using darkfirescrem01/02.bmd.
    /// Used for Fire Slash (55) and Fire Scream (78).
    /// </summary>
    public sealed class DarkLordFireScreamEffect : EffectObject
    {
        private const string PillarBmdPath  = "Skill/darkfirescrem01.bmd";
        private const string GroundBmdPath  = "Skill/darkfirescrem02.bmd";

        private readonly Vector3 _center;
        private readonly int _pillarCount;
        private readonly float _radius;
        private readonly bool _addGroundFire;
        private bool _spawned;

        /// <param name="center">Center of the effect in world space.</param>
        /// <param name="pillarCount">Number of fire pillars to spawn in a ring.</param>
        /// <param name="radius">Ring radius (0 = all at center).</param>
        /// <param name="addGroundFire">Also spawn ground fire (darkfirescrem02) at center.</param>
        public DarkLordFireScreamEffect(Vector3 center, int pillarCount = 3, float radius = 120f, bool addGroundFire = false)
        {
            _center = center;
            _pillarCount = Math.Max(1, pillarCount);
            _radius = radius;
            _addGroundFire = addGroundFire;

            IsTransparent = true;
            AffectedByTransparency = true;
            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;
            Position = center;
            BoundingBoxLocal = new BoundingBox(new Vector3(-500f), new Vector3(500f));
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (Status == GameControlStatus.NonInitialized) _ = Load();
            if (Status != GameControlStatus.Ready) return;

            if (!_spawned)
            {
                _spawned = true;
                SpawnPillars();
                RemoveSelf();
            }
        }

        private void SpawnPillars()
        {
            if (World == null) return;

            float randomOffset = (float)(MuGame.Random.NextDouble() * MathHelper.TwoPi);

            for (int i = 0; i < _pillarCount; i++)
            {
                float angle = randomOffset + i * MathHelper.TwoPi / _pillarCount;
                Vector3 pos = _radius > 0.01f
                    ? _center + new Vector3(MathF.Cos(angle) * _radius, MathF.Sin(angle) * _radius, 0f)
                    : _center;

                float lifetime = 30f + MuGame.Random.Next(-3, 8);
                var pillar = new DLFirePillarSubEffect(PillarBmdPath, lifetime)
                {
                    Position = pos,
                    Scale = 1.0f + (float)(MuGame.Random.NextDouble() * 0.3),
                    Angle = new Vector3(0f, 0f, angle)
                };
                World.Objects.Add(pillar);
                _ = pillar.Load();
            }

            if (_addGroundFire)
            {
                float groundLifetime = 40f;
                var ground = new DLFirePillarSubEffect(GroundBmdPath, groundLifetime)
                {
                    Position = _center,
                    Scale = 1.1f,
                    Angle = Vector3.Zero
                };
                World.Objects.Add(ground);
                _ = ground.Load();
            }
        }

        private void RemoveSelf()
        {
            if (Parent != null) Parent.Children.Remove(this);
            else World?.RemoveObject(this);
            Dispose();
        }

        private sealed class DLFirePillarSubEffect : ModelObject
        {
            private readonly string _bmdPath;
            private float _lifeFrames;

            public DLFirePillarSubEffect(string bmdPath, float lifeFrames)
            {
                _bmdPath = bmdPath;
                _lifeFrames = lifeFrames;
                ContinuousAnimation = true;
                AnimationSpeed = 4f;
                LightEnabled = true;
                IsTransparent = true;
                DepthState = DepthStencilState.DepthRead;
                BlendState = BlendState.Additive;
                BlendMeshState = BlendState.Additive;
                BlendMesh = 0;
                BlendMeshLight = 1f;
                BoundingBoxLocal = new BoundingBox(new Vector3(-200f), new Vector3(200f));
            }

            public override async Task Load()
            {
                Model = await BMDLoader.Instance.Prepare(_bmdPath);
                await base.Load();
            }

            public override void Update(GameTime gameTime)
            {
                base.Update(gameTime);
                if (Status == GameControlStatus.NonInitialized) _ = Load();
                if (Status != GameControlStatus.Ready) return;

                float factor = FPSCounter.Instance.FPS_ANIMATION_FACTOR;
                _lifeFrames -= factor;
                BlendMeshLight = Math.Clamp(_lifeFrames / 30f, 0f, 1f);
                if (_lifeFrames <= 0f) RemoveSelf();
            }

            private void RemoveSelf()
            {
                if (Parent != null) Parent.Children.Remove(this);
                else World?.RemoveObject(this);
                Dispose();
            }
        }
    }
}
