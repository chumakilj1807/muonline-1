#nullable enable
using System;
using Client.Main.Core.Utilities;
using Client.Main.Objects.Player;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Dark Lord's Fire Burst (ID 61) — multiple fire chains radiating from caster toward target.
    /// </summary>
    [SkillVisualEffect(61)]
    public sealed class FireBurstSkillEffect : ISkillVisualEffect
    {
        public WorldObject? CreateEffect(SkillEffectContext context)
        {
            if (context.Caster == null || context.World == null)
                return null;

            var caster = context.Caster;
            var world  = context.World;

            Vector3 start = caster is PlayerObject player
                && player.TryGetHandWorldMatrix(isLeftHand: false, out var hand)
                ? hand.Translation + new Vector3(0f, 0f, 20f)
                : caster.WorldPosition.Translation + Vector3.UnitZ * 80f;

            Vector3 target = context.TargetId != 0
                && world.TryGetWalkerById(context.TargetId, out var t)
                ? t.WorldPosition.Translation + Vector3.UnitZ * 60f
                : start + new Vector3(MathF.Cos(caster.Angle.Z) * 280f, MathF.Sin(caster.Angle.Z) * 280f, 0f);

            return new DarkLordFireChainEffect(start, target);
        }
    }
}
