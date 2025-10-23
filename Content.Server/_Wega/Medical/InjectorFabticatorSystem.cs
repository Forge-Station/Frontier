using System.Linq;
using Content.Server.Power.EntitySystems;
using Content.Server.Popups;
using Content.Shared.Audio;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Injector.Fabticator;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Containers;

namespace Content.Server.Injector.Fabticator;

public sealed class InjectorFabticatorSystem : EntitySystem
{
    [Dependency] private readonly SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionSystem = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InjectorFabticatorComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<InjectorFabticatorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<InjectorFabticatorComponent, EntInsertedIntoContainerMessage>(OnContainerModified);
        SubscribeLocalEvent<InjectorFabticatorComponent, EntRemovedFromContainerMessage>(OnContainerModified);
        SubscribeLocalEvent<InjectorFabticatorComponent, BoundUIOpenedEvent>(OnUIOpened);

        SubscribeLocalEvent<InjectorFabticatorComponent, InjectorFabticatorTransferBeakerToBufferMessage>(OnTransferBeakerToBufferMessage);
        SubscribeLocalEvent<InjectorFabticatorComponent, InjectorFabticatorTransferBufferToBeakerMessage>(OnTransferBufferToBeakerMessage);
        SubscribeLocalEvent<InjectorFabticatorComponent, InjectorFabticatorSetReagentMessage>(OnSetReagentMessage);
        SubscribeLocalEvent<InjectorFabticatorComponent, InjectorFabticatorRemoveReagentMessage>(OnRemoveReagentMessage);
        SubscribeLocalEvent<InjectorFabticatorComponent, InjectorFabticatorProduceMessage>(OnProduceMessage);
        SubscribeLocalEvent<InjectorFabticatorComponent, InjectorFabticatorEjectMessage>(OnEjectMessage);
        SubscribeLocalEvent<InjectorFabticatorComponent, InjectorFabticatorSyncRecipeMessage>(OnSyncRecipeMessage);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<InjectorFabticatorComponent>();
        while (query.MoveNext(out var uid, out var injectorFabticator))
        {
            if (!injectorFabticator.IsProducing || !this.IsPowered(uid, EntityManager))
                continue;

            injectorFabticator.ProductionTimer += frameTime;
            if (injectorFabticator.ProductionTimer >= injectorFabticator.ProductionTime)
            {
                injectorFabticator.ProductionTimer = 0f;
                ProduceInjector(uid, injectorFabticator);
                injectorFabticator.InjectorsProduced++;

                if (injectorFabticator.InjectorsProduced >= injectorFabticator.InjectorsToProduce)
                {
                    injectorFabticator.IsProducing = false;
                    injectorFabticator.InjectorsToProduce = 0;
                    injectorFabticator.InjectorsProduced = 0;
                    injectorFabticator.Recipe = null;

                    _ambient.SetAmbience(uid, false);
                }

                UpdateAppearance(uid, injectorFabticator);
                UpdateUiState(uid, injectorFabticator);
            }
        }
    }

    private void OnComponentInit(EntityUid uid, InjectorFabticatorComponent component, ComponentInit args)
    {
        // Corvax Forge
        if (component.BeakerSlot == null)
            component.BeakerSlot = new ItemSlot();

        if (!_itemSlotsSystem.TryGetSlot(uid, InjectorFabticatorComponent.BeakerSlotId, out _))
            _itemSlotsSystem.AddItemSlot(uid, InjectorFabticatorComponent.BeakerSlotId, component.BeakerSlot);
        // Corvax Forge end
    }

    private void OnMapInit(EntityUid uid, InjectorFabticatorComponent component, MapInitEvent args)
    {
        _solutionSystem.EnsureSolution(uid, InjectorFabticatorComponent.BufferSolutionName, out _, component.BufferMaxVolume);
    }

    private void OnContainerModified(EntityUid uid, InjectorFabticatorComponent component, ContainerModifiedMessage args)
    {
        if (args.Container.ID == InjectorFabticatorComponent.BeakerSlotId)
            UpdateUiState(uid, component);
    }

    private void OnUIOpened(EntityUid uid, InjectorFabticatorComponent component, BoundUIOpenedEvent args)
    {
        UpdateUiState(uid, component);
    }

    private void OnTransferBeakerToBufferMessage(EntityUid uid, InjectorFabticatorComponent component, InjectorFabticatorTransferBeakerToBufferMessage args)
    {
        if (component.IsProducing)
            return;

        if (!_itemSlotsSystem.TryGetSlot(uid, InjectorFabticatorComponent.BeakerSlotId, out var slot)
            || slot.Item is not { } beaker)
            return;

        if (!_solutionSystem.TryGetSolution(beaker, "beaker", out var beakerSolution, out var beakerSolutionComp) ||
            !_solutionSystem.TryGetSolution(uid, InjectorFabticatorComponent.BufferSolutionName, out var bufferSolution, out _))
            return;

        beakerSolutionComp.TryGetReagentQuantity(args.ReagentId, out var available);
        var transferAmount = FixedPoint2.Min(args.Amount, available);

        if (transferAmount <= 0)
            return;

        var quantity = new ReagentQuantity(args.ReagentId, transferAmount);
        if (_solutionSystem.RemoveReagent(beakerSolution.Value, quantity))
        {
            _solutionSystem.TryAddReagent(bufferSolution.Value, quantity, out _);

            var reagentName = _prototypeManager.Index<ReagentPrototype>(args.ReagentId.Prototype).LocalizedName;
            var message = Loc.GetString("injector-fabticator-transfer-to-buffer-success", ("amount", transferAmount), ("reagent", reagentName));
            _popup.PopupEntity(message, uid);
            UpdateUiState(uid, component);
        }
    }

    private void OnTransferBufferToBeakerMessage(EntityUid uid, InjectorFabticatorComponent component, InjectorFabticatorTransferBufferToBeakerMessage args)
    {
        if (component.IsProducing)
            return;

        if (!_itemSlotsSystem.TryGetSlot(uid, InjectorFabticatorComponent.BeakerSlotId, out var slot)
            || slot.Item is not { } beaker)
            return;

        if (!_solutionSystem.TryGetSolution(beaker, "beaker", out var beakerSolution, out var beakerSolutionComp) ||
            !_solutionSystem.TryGetSolution(uid, InjectorFabticatorComponent.BufferSolutionName, out var bufferSolution, out var bufferSolutionComp))
            return;

        bufferSolutionComp.TryGetReagentQuantity(args.ReagentId, out var available);
        var transferAmount = FixedPoint2.Min(args.Amount, available);
        transferAmount = FixedPoint2.Min(transferAmount, beakerSolutionComp.AvailableVolume);

        if (transferAmount <= 0)
            return;

        var quantity = new ReagentQuantity(args.ReagentId, transferAmount);
        if (_solutionSystem.RemoveReagent(bufferSolution.Value, quantity))
        {
            _solutionSystem.TryAddReagent(beakerSolution.Value, quantity, out _);

            var reagentName = _prototypeManager.Index<ReagentPrototype>(args.ReagentId.Prototype).LocalizedName;
            var message = Loc.GetString("injector-fabticator-transfer-to-beaker-success", ("amount", transferAmount), ("reagent", reagentName));
            _popup.PopupEntity(message, uid);
            UpdateUiState(uid, component);
        }
    }

    private void OnSetReagentMessage(EntityUid uid, InjectorFabticatorComponent component, InjectorFabticatorSetReagentMessage args)
    {
        if (component.IsProducing)
            return;

        if (component.Recipe == null)
            component.Recipe = new Dictionary<ReagentId, FixedPoint2>();

        var exactKey = component.Recipe.Keys.FirstOrDefault(k =>
            k.Prototype == args.ReagentId.Prototype);
        if (exactKey != default)
            component.Recipe[exactKey] += args.Amount;
        else
            component.Recipe[args.ReagentId] = args.Amount;

        UpdateUiState(uid, component);
    }

    private void OnRemoveReagentMessage(EntityUid uid, InjectorFabticatorComponent component, InjectorFabticatorRemoveReagentMessage args)
    {
        if (component.IsProducing || component.Recipe == null)
            return;

        var exactKey = component.Recipe.Keys.FirstOrDefault(k =>
            k.Prototype == args.ReagentId.Prototype);
        if (exactKey != default)
            component.Recipe.Remove(exactKey);

        UpdateUiState(uid, component);
    }

    private void OnProduceMessage(EntityUid uid, InjectorFabticatorComponent component, InjectorFabticatorProduceMessage args)
    {
        if (component.IsProducing)
            return;

        if (component.Recipe == null || component.Recipe.Sum(r => (long)r.Value) > 30)
        {
            _popup.PopupEntity(Loc.GetString("injector-fabticator-invalid-recipe"), uid);
            return;
        }

        if (!_solutionSystem.TryGetSolution(uid, InjectorFabticatorComponent.BufferSolutionName, out var bufferSolution, out var buffer))
            return;

        foreach (var (reagent, amount) in component.Recipe)
        {
            if (buffer.GetReagentQuantity(reagent) < amount * args.Amount)
            {
                _popup.PopupEntity(Loc.GetString("injector-fabticator-not-enough-reagents"), uid);
                return;
            }
        }

        component.CustomName = args.CustomName;
        component.InjectorsToProduce = args.Amount;
        component.InjectorsProduced = 0;
        component.IsProducing = true;
        component.ProductionTimer = 0f;

        _ambient.SetAmbience(uid, true);

        UpdateAppearance(uid, component);
        UpdateUiState(uid, component);
    }

    private void OnEjectMessage(EntityUid uid, InjectorFabticatorComponent component, InjectorFabticatorEjectMessage args)
    {
        if (component.IsProducing)
            return;

        if (!_itemSlotsSystem.TryGetSlot(uid, InjectorFabticatorComponent.BeakerSlotId, out var slot))
            return;

        _itemSlotsSystem.TryEject(uid, slot, null, out _, true);
    }

    private void OnSyncRecipeMessage(EntityUid uid, InjectorFabticatorComponent component, InjectorFabticatorSyncRecipeMessage args)
    {
        if (component.IsProducing)
            return;

        component.Recipe = args.Recipe;
        UpdateUiState(uid, component);
    }

    private void ProduceInjector(EntityUid uid, InjectorFabticatorComponent component)
    {
        if (component.Recipe == null)
            return;

        var injector = Spawn(component.Injector, Transform(uid).Coordinates);
        if (!HasComp<SolutionContainerManagerComponent>(injector))
            return;

        if (!_solutionSystem.TryGetSolution(injector, "pen", out var injectorSolution, out _))
            return;

        if (_solutionSystem.TryGetSolution(uid, InjectorFabticatorComponent.BufferSolutionName, out var bufferSolution, out var buffer))
        {
            foreach (var (reagent, amount) in component.Recipe)
            {
                var available = buffer.GetReagentQuantity(reagent);
                var toTransfer = FixedPoint2.Min(amount, available);

                if (toTransfer > 0 && _solutionSystem.RemoveReagent(bufferSolution.Value, reagent, toTransfer))
                {
                    _solutionSystem.TryAddReagent(injectorSolution.Value, reagent, toTransfer, out _);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(component.CustomName))
            _metaData.SetEntityName(injector, component.CustomName);
    }

    private void UpdateAppearance(EntityUid uid, InjectorFabticatorComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        _appearance.SetData(uid, InjectorFabticatorVisuals.IsRunning, component.IsProducing);
    }

    private void UpdateUiState(EntityUid uid, InjectorFabticatorComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var state = GetUserInterfaceState(uid, component);
        _uiSystem.SetUiState(uid, InjectorFabticatorUiKey.Key, state);
    }

    private InjectorFabticatorBoundUserInterfaceState GetUserInterfaceState(EntityUid uid, InjectorFabticatorComponent component)
    {
        NetEntity? beakerNetEntity = null;
        ContainerInfo? beakerContainerInfo = null;

        if (_itemSlotsSystem.TryGetSlot(uid, InjectorFabticatorComponent.BeakerSlotId, out var slot)
            && slot.Item is { } beaker)
        {
            beakerNetEntity = GetNetEntity(beaker);
            beakerContainerInfo = BuildBeakerContainerInfo(beaker);
        }

        _solutionSystem.TryGetSolution(uid, InjectorFabticatorComponent.BufferSolutionName, out _, out var buffer);

        var canProduce = component.Recipe != null && component.Recipe.Sum(r => (long)r.Value) <= 30;

        return new InjectorFabticatorBoundUserInterfaceState(
            component.IsProducing,
            canProduce,
            beakerNetEntity,
            beakerContainerInfo,
            buffer,
            buffer?.Volume ?? FixedPoint2.Zero,
            component.BufferMaxVolume,
            component.Recipe,
            component.CustomName,
            component.InjectorsToProduce,
            component.InjectorsProduced
        );
    }

    private ContainerInfo? BuildBeakerContainerInfo(EntityUid beaker)
    {
        if (!HasComp<SolutionContainerManagerComponent>(beaker)
            || !_solutionSystem.TryGetSolution(beaker, "beaker", out _, out var solution))
            return null;

        return new ContainerInfo(
            Name(beaker),
            solution.Volume,
            solution.MaxVolume)
        {
            Reagents = solution.Contents.ToList()
        };
    }
}
