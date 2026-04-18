#nullable enable
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Effects.Skills
{
    /// <summary>
    /// Dark Lord's Force (ID 60) — golden energy ring expanding from caster.
    /// </summary>
    [SkillVisualEffect(60)]
    public sealed class ForceSkillEffect : ISkillVisualEffect
    {
        public WorldObject? CreateEffect(SkillEffectContext context)
        {
            if (context.Caster == null || context.World == null)
                return null;

            Vector3 center = context.Caster.WorldPosition.Translation;
            if (context.World.Terrain != null)
            {
                float gz = context.World.Terrain.RequestTerrainHeight(center.X, center.Y);
                center = new Vector3(center.X, center.Y, gz);
            }

            return new DarkLordForceEffect(center);
        }
    }
}
