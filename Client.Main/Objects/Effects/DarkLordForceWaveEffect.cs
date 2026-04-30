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
    /// Dark Lord Force (ID 60) visual effect using WaveForce.bmd and impact_force.bmd.
    /// An expanding force wave centered on the caster.
    /// </summary>
    public sealed class DarkLordForceWaveEffect : EffectObject
    {
        private const string WaveBmdPath   = "Skill/WaveForce.bmd";
        private const string ImpactBmdPath = "Skill/impact_force.bmd";

        private readonly Vector3 _center;
        private bool _spawned;

        public DarkLordForceWaveEffect(Vector3 center)
        {
            _center = center;
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
                SpawnWave();
                RemoveSelf();
            }
        }

        private void SpawnWave()
        {
            if (World == null) return;

            // Main expanding wave ring
            var wave = new ForceModelSubEffect(WaveBmdPath, 35f, expanding: true)
            {
                Position = _center,
                Scale = 0.8f,
                Angle = new Vector3(0f, 0f, (float)(MuGame.Random.NextDouble() * MathHelper.TwoPi))
            };
            World.Objects.Add(wave);
            _ = wave.Load();

            // Impact burst at center
            var impact = new ForceModelSubEffect(ImpactBmdPath, 20f, expanding: false)
            {
                Position = _center,
                Scale = 1.0f,
                Angle = Vector3.Zero
            };
            World.Objects.Add(impact);
            _ = impact.Load();

            // Second wave slightly rotated for layered effect
            var wave2 = new ForceModelSubEffect(WaveBmdPath, 30f, expanding: true)
            {
                Position = _center,
                Scale = 0.6f,
                Angle = new Vector3(0f, 0f, MathHelper.Pi * 0.25f)
            };
            World.Objects.Add(wave2);
            _ = wave2.Load();
        }

        private void RemoveSelf()
        {
            if (Parent != null) Parent.Children.Remove(this);
            else World?.RemoveObject(this);
            Dispose();
        }

        private sealed class ForceModelSubEffect : ModelObject
        {
            private readonly string _bmdPath;
            private readonly bool _expanding;
            private float _lifeFrames;

            public ForceModelSubEffect(string bmdPath, float lifeFrames, bool expanding)
            {
                _bmdPath = bmdPath;
                _lifeFrames = lifeFrames;
                _expanding = expanding;
                ContinuousAnimation = true;
                AnimationSpeed = 4f;
                LightEnabled = true;
                IsTransparent = true;
                DepthState = DepthStencilState.DepthRead;
                BlendState = BlendState.Additive;
                BlendMeshState = BlendState.Additive;
                BlendMesh = 0;
                BlendMeshLight = 1f;
                BoundingBoxLocal = new BoundingBox(new Vector3(-400f), new Vector3(400f));
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
                float maxFrames = _lifeFrames + factor * (int)(_lifeFrames / factor);
                _lifeFrames -= factor;

                float t = Math.Clamp(1f - _lifeFrames / (_expanding ? 35f : 20f), 0f, 1f);

                if (_expanding)
                {
                    // Wave expands as it fades
                    Scale = MathHelper.Lerp(0.6f, 2.2f, t);
                    BlendMeshLight = Math.Clamp(1f - t * 1.1f, 0f, 1f);
                }
                else
                {
                    BlendMeshLight = MathF.Sin(t * MathF.PI);
                }

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
