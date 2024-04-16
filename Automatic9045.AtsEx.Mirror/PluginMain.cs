using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using SlimDX;
using SlimDX.Direct3D9;

using BveTypes.ClassWrappers;
using FastMember;
using ObjectiveHarmonyPatch;
using TypeWrapping;

using AtsEx.PluginHost;
using AtsEx.PluginHost.Plugins;

namespace Automatic9045.AtsEx.Mirror
{
    [PluginType(PluginType.MapPlugin)]
    internal class PluginMain : AssemblyPluginBase
    {
        private const string ConfigFileName = "Mirror.Config.xml";

        private static readonly string AssemblyLocation = Assembly.GetExecutingAssembly().Location;
        private static readonly string BaseDirectory = Path.GetDirectoryName(AssemblyLocation);

        private readonly Data.Config Config;

        private readonly HarmonyPatch DrawPatch;
        private readonly HarmonyPatch OnDeviceLostPatch;

        private readonly Renderer Renderer = new Renderer();
        private readonly List<RenderTarget> RenderTargets = new List<RenderTarget>();

        private Surface OriginalRenderTarget = null;

        public PluginMain(PluginBuilder builder) : base(builder)
        {
            ClassMemberSet mainFormMembers = BveHacker.BveTypes.GetClassInfoOf<Scenario>();
            FastMethod drawMethod = mainFormMembers.GetSourceMethodOf(nameof(Scenario.Draw));
            DrawPatch = HarmonyPatch.Patch(null, drawMethod.Source, PatchType.Prefix);

            ClassMemberSet assistantDrawerMembers = BveHacker.BveTypes.GetClassInfoOf<AssistantDrawer>();
            FastMethod onDeviceLostMethod = assistantDrawerMembers.GetSourceMethodOf(nameof(AssistantDrawer.OnDeviceLost));
            OnDeviceLostPatch = HarmonyPatch.Patch(null, onDeviceLostMethod.Source, PatchType.Prefix);

            string path = Path.Combine(BaseDirectory, ConfigFileName);
            Config = Data.Config.Deserialize(path, true);

            for (int i = 0; i < Config.MirrorStructures.Length; i++)
            {
                Data.MirrorStructure structure = Config.MirrorStructures[i];
                if (structure.Key is null)
                {
                    throw new BveFileLoadException($"{i + 1} 個目のストラクチャーのキーが指定されていません。", ConfigFileName);
                }
                else if (structure.TextureFileName is null)
                {
                    throw new BveFileLoadException($"{i + 1} 個目のストラクチャーのテクスチャファイル名が指定されていません。", ConfigFileName);
                }
            }

            BveHacker.ScenarioCreated += OnScenarioCreated;

            DrawPatch.Invoked += (sender, e) =>
            {
                if (!BveHacker.IsScenarioCreated) return PatchInvokationResult.DoNothing(e);

                Renderer.Scenario = Renderer.Scenario ?? BveHacker.Scenario;
                Device device = Direct3DProvider.Instance.Device;

                if (OriginalRenderTarget is null || OriginalRenderTarget.Disposed)
                {
                    OriginalRenderTarget = device.GetRenderTarget(0);
                }

                RectangleF originalPlane = BveHacker.Scenario.Vehicle.CameraLocation.Plane;
                Renderer.Tick();

                double location = BveHacker.Scenario.LocationManager.Location;
                foreach (RenderTarget renderTarget in RenderTargets)
                {
                    renderTarget.Location = location;
                    renderTarget.Render();
                }

                device.SetRenderTarget(0, OriginalRenderTarget);
                device.SetTransform(TransformState.View, Matrix.Identity);
                BveHacker.Scenario.Vehicle.CameraLocation.Plane = originalPlane;

                return PatchInvokationResult.DoNothing(e);
            };

            OnDeviceLostPatch.Invoked += (sender, e) =>
            {
                FreeResources();
                return PatchInvokationResult.DoNothing(e);
            };
        }

        public override void Dispose()
        {
            DrawPatch.Dispose();
            OnDeviceLostPatch.Dispose();

            BveHacker.ScenarioCreated -= OnScenarioCreated;

            FreeResources();
        }

        private void FreeResources()
        {
            OriginalRenderTarget?.Dispose();
            foreach (RenderTarget renderTarget in RenderTargets)
            {
                renderTarget.Dispose();
            }
        }

        private void OnScenarioCreated(ScenarioCreatedEventArgs e)
        {
            Route route = e.Scenario.Route;
            RenderTargetFactory factory = new RenderTargetFactory(Renderer, route.StructureModels, route.Structures.Put);

            foreach (Data.MirrorStructure structure in Config.MirrorStructures)
            {
                Size textureSize = new Size(structure.TextureWidth, structure.TextureHeight);
                try
                {
                    factory.Register(structure.Key, structure.TextureFileName, textureSize, structure.Zoom, structure.BackDrawDistance, structure.FrontDrawDistance, structure.MaxFps);
                }
                catch (KeyNotFoundException)
                {
                    throw new BveFileLoadException($"キー '{structure.Key}' のストラクチャーが見つかりませんでした。", ConfigFileName);
                }
            }

            IEnumerable<RenderTarget> renderTargets = factory.Create();
            RenderTargets.AddRange(renderTargets);
        }

        public override TickResult Tick(TimeSpan elapsed)
        {
            return new MapPluginTickResult();
        }
    }
}
