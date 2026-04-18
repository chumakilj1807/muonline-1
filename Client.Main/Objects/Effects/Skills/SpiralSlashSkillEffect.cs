#nullable enable
using Client.Main.Core.Utilities;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Dark Lord's Spiral Slash (ID 57) — spinning slash AoE, uses TwistingSlashEffect.
    /// </summary>
    [SkillVisualEffect(57)]
    public sealed class SpiralSlashSkillEffect : ISkillVisualEffect
    {
        public WorldObject? CreateEffect(SkillEffectContext context)
        {
            if (context.Caster == null || context.World == null)
                return null;

            return new TwistingSlashEffect(context.Caster, 0.80f);
        }
    }
}
