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
    /// DarkLordSkill.bmd-based effect — fire orb(s) at target position.
    /// Used for Power Slash (56), Spiral Slash (57), Fire Burst (61).
    /// </summary>
    public sealed class DarkLordSkillEffect : EffectObject
    {
        private const string BmdPath = "Skill/DarkLordSkill.bmd";

        private readonly Vector3 _center;
        private readonly int _count;
        private readonly float _spread;
        private readonly float _startAngle;
        private bool _spawned;

        /// <param name="center">World position of the effect center.</param>
        /// <param name="count">Number of orbs (1 for single-target, 5 for AoE ring).</param>
        /// <param name="spread">Radius of the orb ring (0 = all at center).</param>
        /// <param name="startAngle">Starting angle in radians for the first orb.</param>
        public DarkLordSkillEffect(Vector3 center, int count = 1, float spread = 0f, float startAngle = 0f)
        {
            _center = center;
            _count = Math.Max(1, count);
            _spread = spread;
            _startAngle = startAngle;

            IsTransparent = true;
            AffectedByTransparency = true;
            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;
            Position = center;
            BoundingBoxLocal = new BoundingBox(new Vector3(-450f), new Vector3(450f));
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (Status == GameControlStatus.NonInitialized) _ = Load();
            if (Status != GameControlStatus.Ready) return;

            if (!_spawned)
            {
                _spawned = true;
                SpawnOrbs();
                RemoveSelf();
            }
        }

        private void SpawnOrbs()
        {
            if (World == null) return;

            for (int i = 0; i < _count; i++)
            {
                float angle = _count == 1
                    ? _startAngle
                    : _startAngle + i * MathHelper.TwoPi / _count;

                Vector3 pos = _spread > 0.01f
                    ? _center + new Vector3(MathF.Cos(angle) * _spread, MathF.Sin(angle) * _spread, 0f)
                    : _center;

                float lifetime = 32f + MuGame.Random.Next(-4, 5);
                var orb = new DLOrbSubEffect(lifetime)
                {
                    Position = pos,
                    Scale = 1.2f,
                    Angle = new Vector3(0f, 0f, angle)
                };
                World.Objects.Add(orb);
                _ = orb.Load();
            }
        }

        private void RemoveSelf()
        {
            if (Parent != null) Parent.Children.Remove(this);
            else World?.RemoveObject(this);
            Dispose();
        }

        private sealed class DLOrbSubEffect : ModelObject
        {
            private float _lifeFrames;

            public DLOrbSubEffect(float lifeFrames)
            {
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
                BoundingBoxLocal = new BoundingBox(new Vector3(-180f), new Vector3(180f));
            }

            public override async Task Load()
            {
                Model = await BMDLoader.Instance.Prepare(BmdPath);
                await base.Load();
            }

            public override void Update(GameTime gameTime)
            {
                base.Update(gameTime);
                if (Status == GameControlStatus.NonInitialized) _ = Load();
                if (Status != GameControlStatus.Ready) return;

                float factor = FPSCounter.Instance.FPS_ANIMATION_FACTOR;
                _lifeFrames -= factor;
                BlendMeshLight = Math.Clamp(_lifeFrames / 32f, 0f, 1f);
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
