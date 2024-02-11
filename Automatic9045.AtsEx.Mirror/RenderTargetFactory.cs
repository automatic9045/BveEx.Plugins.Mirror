using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SlimDX.Direct3D9;

using BveTypes.ClassWrappers;

namespace Automatic9045.AtsEx.Mirror
{
    internal class RenderTargetFactory
    {
        private readonly Renderer Renderer;
        private readonly IReadOnlyDictionary<string, Model> Models;
        private readonly IEnumerable<MapObjectBase> Structures;

        private readonly Dictionary<Model, TargetInfo> Registered = new Dictionary<Model, TargetInfo>();

        public RenderTargetFactory(Renderer renderer, IReadOnlyDictionary<string, Model> models, IEnumerable<MapObjectBase> structures)
        {
            Renderer = renderer;
            Models = models;
            Structures = structures;
        }

        private void Register(string structureKey, TargetInfo textureInfo)
        {
            Registered.Add(Models[structureKey.ToLowerInvariant()], textureInfo);
        }

        public void Register(string structureKey, string textureFileNameEnding, Size textureSize, float zoom)
        {
            Model model = Models[structureKey.ToLowerInvariant()];

            ExtendedMaterial[] extendedMaterials = model.Mesh.GetMaterials();
            List<MaterialInfo> materials = new List<MaterialInfo>();
            for (int i = 0; i < extendedMaterials.Length; i++)
            {
                string textureFileName = extendedMaterials[i].TextureFileName;
                if (textureFileName.EndsWith(textureFileNameEnding, StringComparison.OrdinalIgnoreCase))
                {
                    materials.Add(model.Materials[i]);
                }
            }

            TargetInfo targetInfo = new TargetInfo(materials, textureSize, zoom);
            Registered.Add(model, targetInfo);
        }

        public IEnumerable<RenderTarget> Create()
        {
            foreach (Structure structure in Structures)
            {
                if (structure is null) continue;
                if (!Registered.TryGetValue(structure.Model, out TargetInfo target)) continue;

                RenderTarget renderTarget = new RenderTarget(Direct3DProvider.Instance.Device, Renderer, structure, target.Materials, target.TextureSize, target.Zoom);
                yield return renderTarget;
            }
        }


        private class TargetInfo
        {
            public IEnumerable<MaterialInfo> Materials { get; }
            public Size TextureSize { get; }
            public float Zoom { get; }

            public TargetInfo(IEnumerable<MaterialInfo> materials, Size size, float zoom)
            {
                Materials = materials;
                TextureSize = size;
                Zoom = zoom;
            }
        }
    }
}
