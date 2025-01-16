using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SlimDX;
using SlimDX.Direct3D9;

using BveTypes.ClassWrappers;

namespace Automatic9045.BveEx.Mirror
{
    internal class RenderTarget : IDisposable
    {
        private readonly Stopwatch Stopwatch = Stopwatch.StartNew();

        private readonly Device Device;
        private readonly Renderer Renderer;

        private readonly LocatableMapObject MapObject;
        private readonly IEnumerable<MaterialInfo> TargetMaterials;
        private readonly Size TextureSize;

        private readonly float Zoom;
        private readonly float BackDrawDistance;
        private readonly float FrontDrawDistance;
        private readonly double MaxFps;

        private Texture Texture = null;
        private Surface Stencil = null;
        private Surface TextureSurface = null;

        public double Location { get; set; } = 0;

        public bool IsEnabled { get; set; } = true;

        public RenderTarget(Device device, Renderer renderer, LocatableMapObject mapObject, IEnumerable<MaterialInfo> targetMaterials, Size textureSize,
            float zoom, float backDrawDistance, float frontDrawDistance, double maxFps)
        {
            Device = device;
            Renderer = renderer;

            MapObject = mapObject;
            TargetMaterials = targetMaterials;
            TextureSize = textureSize;

            Zoom = zoom;
            BackDrawDistance = backDrawDistance;
            FrontDrawDistance = frontDrawDistance;
            MaxFps = maxFps;
        }

        public void Dispose()
        {
            FreeResources();
        }

        public void FreeResources()
        {
            Texture?.Dispose();
            Stencil?.Dispose();
            TextureSurface?.Dispose();
        }

        public void Render()
        {
            if (Texture is null || Texture.Disposed)
            {
                Texture = new Texture(Device, TextureSize.Width, TextureSize.Height, 0, Usage.RenderTarget, Format.A8R8G8B8, Pool.Default);
                foreach (MaterialInfo material in TargetMaterials)
                {
                    material.Texture.Dispose();
                    material.Texture = Texture;
                }
            }

            if (Stencil is null || Stencil.Disposed)
            {
                SurfaceDescription surfaceDescription = Device.DepthStencilSurface.Description;
                Stencil = Surface.CreateDepthStencil(Device, Math.Max(surfaceDescription.Width, TextureSize.Width), Math.Max(surfaceDescription.Height, TextureSize.Height),
                    surfaceDescription.Format, surfaceDescription.MultisampleType, surfaceDescription.MultisampleQuality, true);

                Surface oldStencil = Device.DepthStencilSurface;
                Device.DepthStencilSurface = Stencil;
                oldStencil.Dispose();
            }

            if (TextureSurface is null || TextureSurface.Disposed)
            {
                TextureSurface = Texture.GetSurfaceLevel(0);
            }

            if (Stopwatch.Elapsed.TotalSeconds < 1 / MaxFps) return;
            Stopwatch.Restart();

            Device.SetRenderTarget(0, TextureSurface);
            Device.Clear(ClearFlags.Target, Color.Black, 1, 0);

            if (!IsEnabled) return;
            if (MapObject.Location < Location - BackDrawDistance) return;
            if (Location + FrontDrawDistance < MapObject.Location) return;

            {
                Matrix blockToObject = Renderer.GetTrackMatrixFromCurrentBlockOrigin(MapObject);
                Matrix objectToBlock = Matrix.Invert(blockToObject);

                Vector3 objectUp = Vector3.Transform(Vector3.UnitY, objectToBlock.RemoveTranslation()).ToVector3();
                Vector3 objectNormal = Vector3.Transform(Vector3.UnitZ, blockToObject.RemoveTranslation()).ToVector3();

                Vector3 objectPosition = Vector3.Transform(Vector3.Zero, blockToObject).ToVector3();
                Vector3 relativeObjectPosition = objectPosition - Renderer.CameraPosition;

                Vector3 cameraPositionReflection = objectPosition + (relativeObjectPosition - 2 * Vector3.Dot(relativeObjectPosition, objectNormal) * objectNormal);
                
                Matrix lookAt = Matrix.LookAtLH(objectPosition, cameraPositionReflection, objectUp);

                Device.SetTransform(TransformState.View, Renderer.CameraToBlock * lookAt);
                Renderer.Render(TextureSize, Zoom);
            }
        }
    }
}
