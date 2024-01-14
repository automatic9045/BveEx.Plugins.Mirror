using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SlimDX;
using SlimDX.Direct3D9;

using BveTypes.ClassWrappers;

namespace Automatic9045.AtsEx.Mirror
{
    internal class Renderer
    {
        private int CurrentBlockOriginLocation = 0;

        public Scenario Scenario { get; set; } = null;

        public Matrix BlockToCamera { get; private set; }
        public Matrix CameraToBlock { get; private set; }

        public Vector3 CameraPosition { get; private set; }

        public Renderer()
        {
        }

        public void Tick()
        {
            CurrentBlockOriginLocation = Scenario.LocationManager.BlockIndex * 25;

            BlockToCamera = Scenario.Vehicle.CameraLocation.TransformFromBlock;
            CameraToBlock = Matrix.Invert(BlockToCamera);

            CameraPosition = Vector3.Transform(Vector3.Zero, CameraToBlock).ToVector3();
        }

        public Matrix GetTrackMatrixFromCurrentBlockOrigin(LocatableMapObject mapObject)
            => Scenario.Route.GetTrackMatrix(mapObject, mapObject.Location, CurrentBlockOriginLocation);

        public void Render(Size renderSize, float zoom)
        {
            float aspect = (float)renderSize.Width / renderSize.Height;
            Vector2 planeVertex = new Vector2(1, -1 / aspect) / zoom;
            Scenario.Vehicle.CameraLocation.Plane = new RectangleF(-planeVertex.X / 2, -planeVertex.Y / 2, planeVertex.X, planeVertex.Y);
            Scenario.ObjectDrawer.Draw();
        }
    }
}
