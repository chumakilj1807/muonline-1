#nullable enable
using System;
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Dark Lord's Fire Slash (ID 55) — fire column at target area.
    /// Reuses the Flame (ScrollOfFlameEffect) with a shorter duration.
    /// </summary>
    [SkillVisualEffect(55)]
    public sealed class FireSlashSkillEffect : ISkillVisualEffect
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
                       + new Vector3(MathF.Cos(az) * 150f, MathF.Sin(az) * 150f, 0f);
            }

            if (world.Terrain != null)
            {
                float gz = world.Terrain.RequestTerrainHeight(center.X, center.Y);
                center = new Vector3(center.X, center.Y, gz);
            }

            bool targeted = context.TargetId != 0;
            return new ScrollOfFlameEffect(center, targeted, context.Caster.IsMainWalker);
        }
    }
}
