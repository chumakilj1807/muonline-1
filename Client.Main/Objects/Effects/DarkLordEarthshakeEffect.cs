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
    /// Earthshake (ID 62) visual effect — ground eruptions using EarthQuake01-08.bmd.
    /// Same assets as Rageful Blow but in a circular AoE pattern.
    /// </summary>
    public sealed class DarkLordEarthshakeEffect : EffectObject
    {
        private const string EarthQuakeBase = "Skill/EarthQuake.bmd";
        private const string EarthQuakeFormat = "Skill/EarthQuake0{0}.bmd";

        private readonly Vector3 _center;
        private readonly string[] _paths = new string[9];
        private bool _pathsResolved;
        private bool _spawned;

        public DarkLordEarthshakeEffect(Vector3 center)
        {
            _center = center;
            IsTransparent = true;
            AffectedByTransparency = true;
            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;
            Position = center;
            BoundingBoxLocal = new BoundingBox(new Vector3(-500f), new Vector3(500f));
        }

        public override async Task LoadContent()
        {
            await base.LoadContent();
            await ResolvePaths();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (Status == GameControlStatus.NonInitialized) _ = Load();
            if (Status != GameControlStatus.Ready) return;

            if (!_spawned && _pathsResolved)
            {
                _spawned = true;
                SpawnEruptions();
                RemoveSelf();
            }
        }

        private async Task ResolvePaths()
        {
            _paths[0] = EarthQuakeBase;
            for (int i = 1; i <= 8; i++)
            {
                string indexed = string.Format(EarthQuakeFormat, i);
                if (await BMDLoader.Instance.AssestExist(indexed))
                    _paths[i] = indexed;
                else
                    _paths[i] = EarthQuakeBase;
            }
            _pathsResolved = true;
        }

        private string GetPath(int variant) =>
            _paths[Math.Clamp(variant, 0, 8)];

        private void SpawnEruptions()
        {
            if (World == null) return;

            float startAngle = (float)(MuGame.Random.NextDouble() * MathHelper.TwoPi);

            // Inner ring: 5 eruptions
            for (int i = 0; i < 5; i++)
            {
                float angle = startAngle + i * MathHelper.TwoPi / 5f;
                float radius = (float)(MuGame.Random.NextDouble() * 80f + 80f);
                Vector3 pos = GetTerrainPos(_center, angle, radius);

                float angleZ = 45f + MuGame.Random.Next(0, 30) - 15;
                float scale = 0.6f + (float)(MuGame.Random.NextDouble() * 0.5);

                SpawnModel(4, pos, scale, angleZ);
                SpawnModel(5, pos, scale, angleZ);
            }

            // Outer ring: 5 more
            for (int i = 0; i < 5; i++)
            {
                float angle = startAngle + MathHelper.Pi / 5f + i * MathHelper.TwoPi / 5f;
                float radius = (float)(MuGame.Random.NextDouble() * 80f + 180f);
                Vector3 pos = GetTerrainPos(_center, angle, radius);

                float angleZ = (float)(MuGame.Random.NextDouble() * 360f);
                SpawnModel(7, pos, 0.9f, angleZ);
                SpawnModel(8, pos, 0.9f, angleZ);
            }
        }

        private Vector3 GetTerrainPos(Vector3 origin, float angle, float radius)
        {
            float x = origin.X + MathF.Cos(angle) * radius;
            float y = origin.Y + MathF.Sin(angle) * radius;
            float z = origin.Z;
            if (World?.Terrain != null)
                z = World.Terrain.RequestTerrainHeight(x, y) + 70f;
            return new Vector3(x, y, z);
        }

        private void SpawnModel(int variant, Vector3 pos, float scale, float angleZDeg)
        {
            if (World == null) return;
            float lifetime = variant switch { 4 => 35f, 5 => 40f, 7 => 40f, 8 => 40f, _ => 35f };
            var mdl = new EQSubEffect(GetPath(variant), variant, lifetime)
            {
                Position = pos,
                Scale = scale,
                Angle = new Vector3(0f, 0f, MathHelper.ToRadians(angleZDeg))
            };
            World.Objects.Add(mdl);
            _ = mdl.Load();
        }

        private void RemoveSelf()
        {
            if (Parent != null) Parent.Children.Remove(this);
            else World?.RemoveObject(this);
            Dispose();
        }

        private sealed class EQSubEffect : ModelObject
        {
            private readonly string _path;
            private readonly int _variant;
            private float _lifeFrames;

            public EQSubEffect(string path, int variant, float lifeFrames)
            {
                _path = path;
                _variant = variant;
                _lifeFrames = lifeFrames;
                ContinuousAnimation = true;
                AnimationSpeed = 4f;
                LightEnabled = true;
                IsTransparent = true;
                DepthState = DepthStencilState.DepthRead;
                BlendState = BlendState.Additive;
                BlendMeshState = BlendState.Additive;
                BlendMesh = -2;
                BlendMeshLight = 1f;
                BoundingBoxLocal = new BoundingBox(new Vector3(-200f), new Vector3(200f));
            }

            public override async Task Load()
            {
                Model = await BMDLoader.Instance.Prepare(_path);
                await base.Load();
            }

            public override void Update(GameTime gameTime)
            {
                base.Update(gameTime);
                if (Status == GameControlStatus.NonInitialized) _ = Load();
                if (Status != GameControlStatus.Ready) return;

                float factor = FPSCounter.Instance.FPS_ANIMATION_FACTOR;
                _lifeFrames -= factor;

                BlendMeshLight = _variant switch
                {
                    4 or 7 => Math.Clamp(_lifeFrames * 0.1f / 3f, 0f, 1f),
                    5 or 8 => _lifeFrames >= 30f
                        ? Math.Clamp((40f - _lifeFrames) * 0.1f, 0f, 1f)
                        : Math.Clamp(_lifeFrames * 0.1f, 0f, 1f),
                    _ => Math.Clamp(_lifeFrames / 35f, 0f, 1f)
                };

                if (_lifeFrames < 15f)
                    Position = new Vector3(Position.X, Position.Y, Position.Z - 0.5f * factor);

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
