#nullable enable
using System;
using Client.Main.Core.Utilities;
using Client.Main.Objects.Player;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Dark Lord's Electric Spike (ID 65) — lightning bolt from caster to target.
    /// </summary>
    [SkillVisualEffect(65)]
    public sealed class ElectricSpikeSkillEffect : ISkillVisualEffect
    {
        public WorldObject? CreateEffect(SkillEffectContext context)
        {
            if (context.Caster == null || context.World == null)
                return null;

            var caster = context.Caster;
            var world  = context.World;
            ushort targetId = context.TargetId;

            Vector3 lockedSource = caster is PlayerObject p
                && p.TryGetHandWorldMatrix(isLeftHand: false, out var hand)
                ? hand.Translation
                : caster.WorldPosition.Translation + Vector3.UnitZ * 100f;

            Vector3 SourceProvider() => lockedSource;

            Vector3 TargetProvider()
            {
                if (targetId != 0 && world.TryGetWalkerById(targetId, out var t))
                    return t.WorldPosition.Translation + Vector3.UnitZ * 60f;
                float az = caster.Angle.Z;
                return caster.WorldPosition.Translation
                     + new Vector3(MathF.Cos(az) * 200f, MathF.Sin(az) * 200f, 60f);
            }

            return new ScrollOfLightningEffect(SourceProvider, TargetProvider);
        }
    }
}
