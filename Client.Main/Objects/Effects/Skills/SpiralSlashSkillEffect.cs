#nullable enable
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Dark Lord's Spiral Slash (ID 57) — 5 fire orbs spinning around caster (DarkLordSkill.bmd).
    /// </summary>
    [SkillVisualEffect(57)]
    public sealed class SpiralSlashSkillEffect : ISkillVisualEffect
    {
        public WorldObject? CreateEffect(SkillEffectContext context)
        {
            if (context.Caster == null || context.World == null)
                return null;

            Vector3 center = context.Caster.WorldPosition.Translation + Vector3.UnitZ * 40f;
            float startAngle = context.Caster.Angle.Z;

            // 5 orbs in a ring around caster, radius 150
            return new DarkLordSkillEffect(center, count: 5, spread: 150f, startAngle: startAngle);
        }
    }
}
