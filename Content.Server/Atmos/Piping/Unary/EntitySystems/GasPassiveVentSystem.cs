using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Server.NodeContainer;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Atmos;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Piping.Unary.EntitySystems
{
    [UsedImplicitly]
    public sealed class GasPassiveVentSystem : EntitySystem
    {
        [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
        [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<GasPassiveVentComponent, AtmosDeviceUpdateEvent>(OnPassiveVentUpdated);
        }

        private void OnPassiveVentUpdated(EntityUid uid, GasPassiveVentComponent vent, ref AtmosDeviceUpdateEvent args)
        {
            var environment = _atmosphereSystem.GetContainingMixture(uid, args.Grid, args.Map, true, true);

            if (environment == null)
                return;

            if (!_nodeContainer.TryGetNode(uid, vent.InletName, out PipeNode? inlet))
                return;

            var pressureDelta = inlet.Air.Pressure - environment.Pressure;

            if (MathF.Abs(pressureDelta) < 0.1f)
                return;

            const float transferCoefficient = 0.1f;

            var transferMoles = pressureDelta * transferCoefficient * args.dt;

            if (transferMoles > 0)
                _atmosphereSystem.Merge(environment, inlet.Air.Remove(transferMoles));
            else
                _atmosphereSystem.Merge(inlet.Air, environment.Remove(-transferMoles));
        }
    }
}
