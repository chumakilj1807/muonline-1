using Client.Main.Content;
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Login
{
    public class BlendedObjects : ModelObject
    {
        public override async Task Load()
        {
            var idx = (Type + 1).ToString().PadLeft(2, '0');
            StepLogger.Log($"BlendedObjects.Load: BMD Object{idx}");
            Model = await BMDLoader.Instance.Prepare($"Object95/Object{idx}.bmd");
            StepLogger.Log($"BlendedObjects.Load: base.Load Object{idx}");
            BlendState = BlendState.AlphaBlend;
            LightEnabled = true;
            IsTransparent = true;
            BlendMesh = 0;
            BlendMeshState = BlendState.Additive;
            Position = new Vector3(Position.X, Position.Y, Position.Z - 10f);
            await base.Load();
            StepLogger.Log($"BlendedObjects.Load: done Object{idx}");
        }
        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }
    }
}
