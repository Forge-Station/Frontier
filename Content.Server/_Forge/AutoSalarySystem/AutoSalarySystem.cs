using Content.Server._NF.Bank;
using Content.Server.Mind;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Access.Components;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.PDA;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Server.Popups;
using Robust.Shared.Prototypes;
using Robust.Server.Player;
using Content.Shared.Roles;
using Robust.Shared.Timing;
using Content.Server.Access.Components;

namespace Content.Server._Forge.AutoSalarySystem;

public sealed class AutoSalarySystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly InventorySystem _inv = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BankAccountComponent, PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<AutoSalaryComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!TryGetIdCard(uid, out var id) || id == null)
                continue;

            if (!_proto.TryIndex(id.JobPrototype, out var job))
                continue;

            if (comp.LastSalaryAt + job.SalaryInterval > _timing.CurTime)
                continue;

            if (!ShouldSkipEntity(uid))
                TryPaySalary(uid, job.Salary);

            comp.LastSalaryAt = _timing.CurTime;
        }
    }

    private void OnPlayerSpawned(EntityUid uid, BankAccountComponent _, PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId == null)
            return;

        if (!_proto.TryIndex<JobPrototype>(ev.JobId, out var job))
            return;

        if (job.Salary <= 0)
            return;

        var comp = EnsureComp<AutoSalaryComponent>(uid);
        comp.LastSalaryAt = _timing.CurTime; // just not to pay salary just when player spawned
    }

    private bool HasActivePlayer(EntityUid body)
    {
        if (!_mindSystem.TryGetMind(body, out _, out var mind))
            return false;
        if (!_playerManager.TryGetSessionByEntity(body, out var session) && session == null)
            return false;
        if (mind.IsVisitingEntity)
            return false;
        return true;
    }

    private bool ShouldSkipEntity(EntityUid body)
    {
        if (IsEntityDead(body))
            return true;
        if (!HasActivePlayer(body))
            return true;
        return false;
    }

    private bool IsEntityDead(EntityUid body)
    {
        return !TryComp<MobStateComponent>(body, out var mobState) || _mobState.IsDead(body, mobState);
    }

    private void TryPaySalary(EntityUid body, int salary)
    {
        if (_bank.TryBankDeposit(body, salary))
        {
            _popup.PopupEntity(Loc.GetString("auto-salary-popup", ("salary", salary)), body, body);
        }
    }

    private bool TryGetIdCard(EntityUid body, out IdCardComponent? id)
    {
        id = null;

        var enumerator = _inv.GetHandOrInventoryEntities(body);
        foreach (var ent in enumerator)
        {
            if (!TryComp<PdaComponent>(ent, out var pda))
                continue;

            if (pda.ContainedId != null)
            {
                if (HasComp<AgentIDCardComponent>(pda.ContainedId))
                    return false;

                if (TryComp(pda.ContainedId, out id))
                    return true;
            }
        }

        return true;
    }
}
