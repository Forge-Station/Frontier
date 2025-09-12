using Content.Server.Atmos.Components; // Forge-Change
using Content.Server.Mech.Systems;
using Content.Server.Power.Components;
using Content.Shared.Construction;
using Content.Shared.Mech.Components;
using JetBrains.Annotations;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using System.Linq;

namespace Content.Server.Construction.Completions;

/// <summary>
/// Creates the mech entity while transferring all relevant parts inside of it,
/// for right now, the cell that was used in construction.
/// </summary>
[UsedImplicitly, DataDefinition]
public sealed partial class BuildMech : IGraphAction
{
    [DataField("mechPrototype", required: true, customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string MechPrototype = string.Empty;

    [DataField("batteryContainer")]
    public string BatteryContainer = "battery-container";

    [DataField("gasTankContainer")] // Forge-Change
    public string GasTankContainer = "gas-tank-container";

    [DataField("CapacitorContainer")] // Forge-Change
    public string CapacitorContainer = "capacitor-container";

    // TODO use or generalize ConstructionSystem.ChangeEntity();
    public void PerformAction(EntityUid uid, EntityUid? userUid, IEntityManager entityManager)
    {
        var transform = entityManager.GetComponent<TransformComponent>(uid);
        var newMech = entityManager.SpawnEntity(MechPrototype, transform.Coordinates);
        if (!entityManager.TryGetComponent<MechComponent>(newMech, out var mechComp))
        {
            Logger.Warning($"Mech construct entity {uid} did not have a mech component! Aborting build mech action.");
            return;
        }

        TryTransferContainerContents(uid, entityManager, BatteryContainer, mechComp.BatterySlot);
        TryTransferContainerContents(uid, entityManager, GasTankContainer, mechComp.GasTankSlot);   // Forge-Change
        TryTransferContainerContents(uid, entityManager, CapacitorContainer, mechComp.CapacitorSlot); // Forge-Change

        var entChangeEv = new ConstructionChangeEntityEvent(newMech, uid);
        entityManager.EventBus.RaiseLocalEvent(uid, entChangeEv);
        entityManager.EventBus.RaiseLocalEvent(newMech, entChangeEv, broadcast: true);
        entityManager.QueueDeleteEntity(uid);
    }

    private void TryTransferContainerContents(EntityUid uid, IEntityManager entityManager, string sourceContainerID, ContainerSlot targetSlot)
    {
        if (!entityManager.TryGetComponent(uid, out ContainerManagerComponent? containerManager))
        {
            Logger.Warning($"Mech construct entity {uid} did not have a container manager! Aborting build mech action.");
            return;
        }

        var containerSystem = entityManager.EntitySysManager.GetEntitySystem<ContainerSystem>();

        if (!containerSystem.TryGetContainer(uid, sourceContainerID, out var originalContainer, containerManager))
        {
            return;
        }

        List<EntityUid> EntitiesToTransfer = originalContainer.ContainedEntities.ToList();

        foreach (var entity in EntitiesToTransfer)
        {
            if (containerSystem.TryRemoveFromContainer(entity, true, out bool wasInContainer))
            {
                containerSystem.Insert(entity, targetSlot);
            }
        }
    }
}