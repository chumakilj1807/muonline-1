#nullable enable
using System;
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Dark Lord's Power Slash (ID 56) — single fire orb at target (DarkLordSkill.bmd).
    /// </summary>
    [SkillVisualEffect(56)]
    public sealed class PowerSlashSkillEffect : ISkillVisualEffect
    {
        public WorldObject? CreateEffect(SkillEffectContext context)
        {
            if (context.Caster == null || context.World == null)
                return null;

            var world = context.World;
            Vector3 center;

            if (context.TargetId != 0 && world.TryGetWalkerById(context.TargetId, out var t))
                center = t.WorldPosition.Translation + Vector3.UnitZ * 50f;
            else
            {
                float az = context.Caster.Angle.Z;
                center = context.Caster.WorldPosition.Translation
                       + new Vector3(MathF.Cos(az) * 180f, MathF.Sin(az) * 180f, 50f);
            }

            float angle = context.Caster.Angle.Z;
            return new DarkLordSkillEffect(center, count: 1, spread: 0f, startAngle: angle);
        }
    }
}
