#nullable enable
using System;
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Dark Lord's Fire Scream (ID 78) — wide fire pillar AoE (darkfirescrem01/02.bmd).
    /// </summary>
    [SkillVisualEffect(78)]
    public sealed class FireScreamSkillEffect : ISkillVisualEffect
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
                       + new Vector3(MathF.Cos(az) * 220f, MathF.Sin(az) * 220f, 0f);
            }

            if (world.Terrain != null)
            {
                float gz = world.Terrain.RequestTerrainHeight(center.X, center.Y);
                center = new Vector3(center.X, center.Y, gz);
            }

            // Fire Scream: 7 pillars in a wider ring + ground fire at center
            return new DarkLordFireScreamEffect(center, pillarCount: 7, radius: 160f, addGroundFire: true);
        }
    }
}
