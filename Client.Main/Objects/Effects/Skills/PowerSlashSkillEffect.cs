#nullable enable
using Client.Main.Core.Utilities;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Dark Lord's Power Slash (ID 56) — wide sweeping slash, uses TwistingSlashEffect on caster.
    /// </summary>
    [SkillVisualEffect(56)]
    public sealed class PowerSlashSkillEffect : ISkillVisualEffect
    {
        public WorldObject? CreateEffect(SkillEffectContext context)
        {
            if (context.Caster == null || context.World == null)
                return null;

            return new TwistingSlashEffect(context.Caster, 0.55f);
        }
    }
}
