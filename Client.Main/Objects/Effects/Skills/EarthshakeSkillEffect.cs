#nullable enable
using System;
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Dark Lord's Earthshake (ID 62) — ground eruptions at target area (EarthQuake01-08.bmd).
    /// </summary>
    [SkillVisualEffect(62)]
    public sealed class EarthshakeSkillEffect : ISkillVisualEffect
    {
        public WorldObject? CreateEffect(SkillEffectContext context)
        {
            if (context.Caster == null || context.World == null)
                return null;

            var world = context.World;
            Vector3 center;

            if (context.TargetPosition.HasValue)
                center = context.TargetPosition.Value;
            else if (context.TargetId != 0 && world.TryGetWalkerById(context.TargetId, out var t))
                center = t.WorldPosition.Translation;
            else
            {
                float az = context.Caster.Angle.Z;
                center = context.Caster.WorldPosition.Translation
                       + new Vector3(MathF.Cos(az) * 200f, MathF.Sin(az) * 200f, 0f);
            }

            if (world.Terrain != null)
            {
                float gz = world.Terrain.RequestTerrainHeight(center.X, center.Y);
                center = new Vector3(center.X, center.Y, gz);
            }

            return new DarkLordEarthshakeEffect(center);
        }
    }
}
