#nullable enable
using System;
using Client.Main.Core.Utilities;
using Client.Main.Objects.Player;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Dark Lord's Fire Burst (ID 61) — fire orb at target (DarkLordSkill.bmd).
    /// </summary>
    [SkillVisualEffect(61)]
    public sealed class FireBurstSkillEffect : ISkillVisualEffect
    {
        public WorldObject? CreateEffect(SkillEffectContext context)
        {
            if (context.Caster == null || context.World == null)
                return null;

            var world = context.World;
            Vector3 center;

            if (context.TargetId != 0 && world.TryGetWalkerById(context.TargetId, out var t))
                center = t.WorldPosition.Translation + Vector3.UnitZ * 60f;
            else
            {
                float az = context.Caster.Angle.Z;
                center = context.Caster.WorldPosition.Translation
                       + new Vector3(MathF.Cos(az) * 280f, MathF.Sin(az) * 280f, 60f);
            }

            float angle = context.Caster.Angle.Z;
            return new DarkLordSkillEffect(center, count: 1, spread: 0f, startAngle: angle);
        }
    }
}
