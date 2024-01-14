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
        private const double BackDrawDistance = 25;
        private const double FrontDrawDistance = 400;

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

            BveHacker.ScenarioCreated += OnScenarioCreated;

            string path = Path.Combine(BaseDirectory, "Mirror.Config.xml");
            Config = Data.Config.Deserialize(path, true);

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
                double minDrawLocation = location - BackDrawDistance;
                double maxDrawLocation = location + FrontDrawDistance;

                foreach (RenderTarget renderTarget in RenderTargets)
                {
                    renderTarget.MinDrawLocation = minDrawLocation;
                    renderTarget.MaxDrawLocation = maxDrawLocation;

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
                factory.Register(structure.Key, structure.TextureFileName, textureSize, structure.Zoom);
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
