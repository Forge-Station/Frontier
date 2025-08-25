using System.Linq;
using Content.Server._NF.Bank;
using Content.Server._NF.Shuttles.Components;
using Content.Server.Mind;
using Content.Server.Stack;
using Content.Server.Store.Systems;
using Content.Shared._Forge.Contractor;
using Content.Shared._Forge.Contractor.Components;
using Content.Shared._Forge.Contracts;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Pinpointer;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Stacks;
using Content.Shared.Store.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Forge.Contracts;

public sealed class ContractsSystem : EntitySystem
{
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly NpcFactionSystem _factionSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPinpointerSystem _pin = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly StoreSystem _store = default!;

    // Validation in case of changes
    [ValidatePrototypeId<NpcFactionPrototype>]
    private const string Pirate = "NFPirate";
    [ValidatePrototypeId<NpcFactionPrototype>]
    private const string Syndicate = "NFSyndicate";

    [ValidatePrototypeId<StackPrototype>]
    private const string Credit = "Credit";
    [ValidatePrototypeId<StackPrototype>]
    private const string Telecrystal = "Telecrystal";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ContractsComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<ContractsComponent, AcceptContractMessage>(OnAcceptContract);
        SubscribeLocalEvent<ContractsComponent, CompleteContractMessage>(OnCompleteContract);
        SubscribeLocalEvent<ContractsComponent, OpenContractorShopMessage>(OnShop);
        SubscribeLocalEvent<ContractsComponent, AfterInteractEvent>(OnInteract);

        SubscribeLocalEvent<ContractCompletedEvent>(OnContractCompleted);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ContractsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.NextUpdateTime == TimeSpan.Zero)
            {
                comp.NextUpdateTime = _gameTiming.CurTime + comp.UpdateInterval;
                GenerateNewContracts(uid, comp);
                continue;
            }

            if (_gameTiming.CurTime >= comp.NextUpdateTime)
            {
                GenerateNewContracts(uid, comp);
            }

            UpdateActiveContracts(uid, comp);
        }
    }

    /// <summary>
    /// Opens the contracts UI for the user if they are a contractor.
    /// </summary>
    private void OnUIOpened(EntityUid uid, ContractsComponent component, BoundUIOpenedEvent args)
    {
        // Only contractors can see the range of contracts.
        if (!HasComp<ContractorComponent>(args.Actor))
            return;

        UpdateUI(uid, component);
    }

    /// <summary>
    /// Handles contract acceptance by the user, moves the contract to active contracts.
    /// </summary>
    private void OnAcceptContract(EntityUid uid, ContractsComponent component, AcceptContractMessage msg)
    {
        var contract = component.AvailableContracts.FirstOrDefault(c => c.ContractId == msg.ContractId);
        if (contract == null)
            return;

        component.AvailableContracts.Remove(contract);
        var activeContract = new ActiveContract(
            contract.ContractId,
            GetNetEntity(uid),
            contract.TargetEntity,
            contract.TargetName,
            contract.TargetJob,
            contract.Description,
            contract.Reward,
            contract.TelecrystalReward,
            contract.EvacPoint,
            contract.EvacPointName,
            _gameTiming.CurTime + contract.Deadline,
            ContractStatus.Active
        );

        component.ActiveContracts.Add(activeContract);
        UpdateUI(uid, component);
    }

    /// <summary>
    /// Handles contract completion, gives rewards and removes the contract from active contracts.
    /// </summary>
    private void OnCompleteContract(EntityUid uid, ContractsComponent component, CompleteContractMessage msg)
    {
        var contract = component.ActiveContracts.FirstOrDefault(c => c.ContractId == msg.ContractId);
        if (contract == null || contract.Status != ContractStatus.Completed || component.UplinkOwner == null
            || !TryComp<ContractorComponent>(component.UplinkOwner, out var contractor))
            return;

        // Trying to add a reward to a bank account
        if (!_bank.TryBankDeposit(component.UplinkOwner.Value, contract.Reward))
            _stack.Spawn(contract.Reward, Credit, Transform(uid).Coordinates);

        _stack.Spawn(contract.TelecrystalReward, Telecrystal, Transform(uid).Coordinates);

        component.ActiveContracts.Remove(contract);
        contractor.CountContracts += 1;
        UpdateUI(uid, component);
    }

    /// <summary>
    /// Opens the contractor shop UI.
    /// </summary>
    private void OnShop(EntityUid uid, ContractsComponent component, OpenContractorShopMessage args)
    {
        if (!TryComp<StoreComponent>(uid, out var store) || component.UplinkOwner == null)
            return;

        _store.ToggleUi(component.UplinkOwner.Value, uid, store);
    }

    /// <summary>
    /// Handles interaction with a pinpointer, sets the target for tracking.
    /// </summary>
    private void OnInteract(EntityUid uid, ContractsComponent component, AfterInteractEvent args)
    {
        if (args.Target == null || !TryComp<PinpointerComponent>(args.Target, out var pin))
            return;

        var user = args.User;
        if (component.ActiveContracts.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("contract-no-active-contracts"), user, user);
            return;
        }

        var randomContract = _random.Pick(component.ActiveContracts);
        var targetEntity = GetEntity(randomContract.TargetEntity);
        if (!Exists(targetEntity))
        {
            _popup.PopupEntity(Loc.GetString("contract-target-no-longer-exists"), user, user);
            return;
        }

        _pin.SetTarget(uid, targetEntity, pin);

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("contract-pinpointer-target-set",
            ("target", randomContract.TargetName)), user, user);
    }

    /// <summary>
    /// Updates the contracts UI for the specified user.
    /// </summary>
    private void UpdateUI(EntityUid uid, ContractsComponent component)
    {
        if (!_uiSystem.HasUi(uid, ContractsUiKey.Key))
            return;

        var state = new ContractsUpdateStateMessage(
            component.AvailableContracts,
            component.ActiveContracts
        );

        _uiSystem.ServerSendUiMessage(uid, ContractsUiKey.Key, state);
    }

    /// <summary>
    /// Generates new available contracts for the component.
    /// </summary>
    private void GenerateNewContracts(EntityUid uid, ContractsComponent component)
    {
        component.AvailableContracts.Clear();
        var existingTargets = new HashSet<NetEntity>();
        foreach (var activeContract in component.ActiveContracts)
            existingTargets.Add(activeContract.TargetEntity);

        for (int i = 0; i < 3; i++)
        {
            ContractData? contract = null;
            bool targetExists;
            int attempts = 0;
            const int maxAttempts = 2;

            do
            {
                contract = GenerateRandomContract(uid);
                targetExists = contract != null && existingTargets.Contains(contract.TargetEntity);
                attempts++;
            }
            while (targetExists && attempts < maxAttempts);

            if (contract == null || targetExists)
                continue;

            existingTargets.Add(contract.TargetEntity);
            component.AvailableContracts.Add(contract);
        }

        component.NextUpdateTime = _gameTiming.CurTime + component.UpdateInterval;
    }

    /// <summary>
    /// Updates the state of active contracts, removes expired ones.
    /// </summary>
    private void UpdateActiveContracts(EntityUid uid, ContractsComponent component)
    {
        var currentTime = _gameTiming.CurTime;
        var contractsToRemove = new List<ActiveContract>();

        var needsUiUpdate = false;
        foreach (var contract in component.ActiveContracts)
        {
            var timeLeft = contract.TimeRemaining - currentTime;
            if (timeLeft <= TimeSpan.Zero && contract.Status == ContractStatus.Active)
            {
                contractsToRemove.Add(contract);
                needsUiUpdate = true;
                continue;
            }
        }

        foreach (var contract in contractsToRemove)
        {
            component.ActiveContracts.Remove(contract);
        }

        if (needsUiUpdate)
        {
            UpdateUI(uid, component);
        }
    }

    /// <summary>
    /// Handles contract completion event, updates status and rewards.
    /// </summary>
    private void OnContractCompleted(ref ContractCompletedEvent ev)
    {
        var currentContract = ev.Contract;
        var query = EntityQueryEnumerator<ContractsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var contract = comp.ActiveContracts.FirstOrDefault(c => c.ContractId == currentContract.ContractId);
            if (contract == null)
                continue;

            var target = GetEntity(contract.TargetEntity);
            var modifier = _mobState.IsDead(target) || _mobState.IsCritical(target)
                ? 3 : 1;

            var completedContract = new ActiveContract(
                contract.ContractId,
                contract.ContractorEntity,
                contract.TargetEntity,
                contract.TargetName,
                contract.TargetJob,
                contract.Description,
                contract.Reward / modifier,
                contract.TelecrystalReward / modifier,
                contract.EvacPoint,
                contract.EvacPointName,
                TimeSpan.Zero,
                ContractStatus.Completed
            );

            comp.ActiveContracts.Remove(contract);

            comp.ActiveContracts.Add(completedContract);
            UpdateUI(uid, comp);
            break;
        }
    }

    #region Contracts generation

    /// <summary>
    /// Generates a random contract based on difficulty and rewards.
    /// </summary>
    private ContractData? GenerateRandomContract(EntityUid uid)
    {
        var target = GetRandomTarget();
        if (target == null)
            return null;

        float jobDifficultyModifier = 1.0f;
        string job = Loc.GetString("job-name-unknown");
        if (_mindSystem.TryGetMind(target.Value, out _, out var mind))
        {
            var jobProto = GetRole(mind);
            if (jobProto != null)
            {
                job = jobProto.LocalizedName;
                jobDifficultyModifier = GetJobDifficultyModifier(jobProto.ID);
            }
        }

        float distanceModifier = CalculateDistanceModifier(uid, target.Value);
        float randomFactor = (float)_random.NextDouble();
        float rawDifficulty = (distanceModifier * 0.6f + jobDifficultyModifier * 0.4f) *
                    MathHelper.Lerp(0.8f, 1.2f, randomFactor);
        float totalDifficulty = Math.Clamp(rawDifficulty, 0.4f, 2.0f);

        (int moneyReward, int telecrystalReward) = CalculateRewards(totalDifficulty);

        EntityUid evacPoint = GetRandomEvacPoint();
        if (evacPoint == EntityUid.Invalid)
            return null;

        string contractId = $"contract_{_random.Next(1, 1_000_000):D6}"; // The chance of a match = 0.0001%

        return new ContractData(
            id: contractId,
            targetEntity: GetNetEntity(target.Value),
            targetName: Name(target.Value),
            targetJob: job,
            description: GetRandomDescription(),
            reward: moneyReward,
            telecrystalReward: telecrystalReward,
            evacPoint: GetNetEntity(evacPoint),
            evacPointName: Name(evacPoint),
            difficulty: totalDifficulty,
            deadline: TimeSpan.FromMinutes(_random.Next(20, 60))
        );
    }

    /// <summary>
    /// Selects a random target for the contract from eligible characters.
    /// </summary>
    private EntityUid? GetRandomTarget()
    {
        var candidates = new List<EntityUid>();
        var query = EntityQueryEnumerator<HumanoidAppearanceComponent, ActorComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            if (HasComp<ContractorComponent>(uid))
                continue;

            if (_factionSystem.IsMember((uid, null), Pirate)
                || _factionSystem.IsMember((uid, null), Syndicate))
                continue;

            candidates.Add(uid);
        }

        return candidates.Count > 0
            ? candidates[_random.Next(candidates.Count)]
            : null;
    }

    /// <summary>
    /// Gets the job (role) of a character from their MindComponent.
    /// </summary>
    private JobPrototype? GetRole(MindComponent mind)
    {
        foreach (var roleId in mind.MindRoles)
        {
            if (!TryComp<MindRoleComponent>(roleId, out var role))
                continue;

            if (role.JobPrototype != null)
            {
                _proto.TryIndex<JobPrototype>(role.JobPrototype, out var jobPrototype);
                return jobPrototype;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the difficulty modifier for the specified job.
    /// </summary>
    private float GetJobDifficultyModifier(string jobId)
    {
        return jobId switch
        {
            // Change this if you had a problem
            "StationRepresentative" => 2.0f,
            "Sheriff" => 1.8f,
            "Bailiff" => 1.6f,
            "SeniorOfficer" => 1.4f,
            "Brigmedic" => 1.2f,
            "NFDetective" => 1.0f,
            "Deputy" => 0.8f,
            "Cadet" => 0.6f,
            "SecurityGuard" => 0.5f,
            _ => 0.4f
        };
    }

    /// <summary>
    /// Calculates the difficulty modifier based on the distance between contractor and target.
    /// </summary>
    private float CalculateDistanceModifier(EntityUid uid, EntityUid target)
    {
        var xformUid = Transform(uid);
        var xformTarget = Transform(target);
        if (xformUid.MapID != xformTarget.MapID)
            return 1.5f;

        var uidPos = _transform.GetWorldPosition(xformUid);
        var targetPos = _transform.GetWorldPosition(xformTarget);

        float distance = (targetPos - uidPos).Length();

        return MathHelper.Lerp(0.5f, 2.5f, Math.Clamp(distance / 5000f, 0f, 1f));
    }

    /// <summary>
    /// Calculates contract rewards based on difficulty.
    /// </summary>
    private (int money, int telecrystals) CalculateRewards(float difficulty)
    {
        int money = (int)(50000 + 150000 * MathF.Pow(difficulty, 1.5f));
        int telecrystals = (int)(2 + 8 * MathF.Pow(difficulty, 1.2f));

        return (money, telecrystals);
    }

    /// <summary>
    /// Generates a random description for the contract.
    /// </summary>
    private string GetRandomDescription()
    {
        int category = _random.Next(1, 6);
        int variant = _random.Next(1, 4);

        return category switch
        {
            1 => Loc.GetString($"contract-description-nanotrasen-{variant}"),
            2 => Loc.GetString($"contract-description-syndicate-{variant}"),
            3 => Loc.GetString($"contract-description-personal-{variant}"),
            4 => Loc.GetString($"contract-description-political-{variant}"),
            5 => Loc.GetString($"contract-description-alien-{variant}"),
            _ => Loc.GetString("contract-description-default")
        };
    }

    /// <summary>
    /// Selects a random evacuation point for the contract.
    /// </summary>
    private EntityUid GetRandomEvacPoint()
    {
        var anchors = new List<EntityUid>();
        var query = EntityQueryEnumerator<ForceAnchorComponent>();

        while (query.MoveNext(out var uid, out _))
        {
            if (HasComp<BlockEvacuationComponent>(uid))
                continue;

            anchors.Add(uid);
        }

        return anchors.Count > 0
            ? anchors[_random.Next(anchors.Count)]
            : EntityUid.Invalid;
    }

    #endregion
}
