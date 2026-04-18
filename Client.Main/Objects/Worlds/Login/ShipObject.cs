using Client.Main.Content;
using Client.Main.Core.Utilities;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using Client.Main.Controllers;
using Microsoft.Xna.Framework;
using Client.Main.Graphics;

namespace Client.Main.Objects.Worlds.Login
{
    public class ShipObject : ModelObject
    {
        public override async Task Load()
        {
            var idx = (Type + 1).ToString().PadLeft(2, '0');
            StepLogger.Log($"ShipObject.Load: BMD Object{idx}");
            Model = await BMDLoader.Instance.Prepare($"Object95/Object{idx}.bmd");
            StepLogger.Log($"ShipObject.Load: BMD done, base.Load");
            Position = new Vector3(Position.X, Position.Y, Position.Z + 15f);
            IsTransparent = false;
            LightEnabled = true;
            await base.Load();
            StepLogger.Log($"ShipObject.Load: done Object{idx}");
        }

        public override void DrawMesh(int mesh)
        {
            if (mesh == 1 || mesh == 21)
            {
                BlendState = BlendState.NonPremultiplied;
            }
            else if (mesh == 5 || mesh == 4)
            {
                BlendState = BlendState.NonPremultiplied;
            }

            base.DrawMesh(mesh);

            BlendState = Blendings.Alpha;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        }
    }

}
