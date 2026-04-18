#nullable enable
using System;
using Client.Main.Core.Utilities;
using Client.Main.Objects.Player;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Dark Lord's Fire Burst (ID 61) — dark crimson fireball projectile.
    /// </summary>
    [SkillVisualEffect(61)]
    public sealed class FireBurstSkillEffect : ISkillVisualEffect
    {
        // Dark crimson / blood-red theme for Dark Lord
        private static readonly Color Core  = new(0.85f, 0.05f, 0.05f, 1f);
        private static readonly Color Glow  = new(0.95f, 0.10f, 0.05f, 0.85f);
        private static readonly Color Tail  = new(0.70f, 0.02f, 0.02f, 0.90f);
        private static readonly Color Spark = new(1.00f, 0.15f, 0.05f, 1.00f);
        private static readonly Color Smoke = new(0.30f, 0.05f, 0.05f, 0.70f);

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
                ? t.WorldPosition.Translation + Vector3.UnitZ * 80f
                : start + new Vector3(MathF.Cos(caster.Angle.Z) * 200f, MathF.Sin(caster.Angle.Z) * 200f, 0f);

            return new ScrollOfFireBallEffect(start, target, Core, Glow, Tail, Spark, Smoke, 1600f);
        }
    }
}
