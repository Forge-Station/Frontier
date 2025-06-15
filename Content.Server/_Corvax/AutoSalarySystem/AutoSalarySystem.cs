using Content.Server._NF.Bank;
using Content.Server.Mind;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Access.Components;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.PDA;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Server.Popups;
using Content.Shared._Corvax.AutoSalarySystem;
using Robust.Shared.Prototypes;

namespace Content.Server._Corvax.AutoSalarySystem;

public sealed class AutoSalarySystem : EntitySystem
{
    [Dependency] private readonly InventorySystem   _inv        = default!;
    [Dependency] private readonly BankSystem        _bank       = default!;
    [Dependency] private readonly PopupSystem       _popup      = default!;
    [Dependency] private readonly MobStateSystem    _mobState   = default!;
    [Dependency] private readonly IPrototypeManager _protoMan   = default!;
    [Dependency] private readonly MindSystem        _mindSystem = default!;

    private TimeSpan _payInterval = TimeSpan.FromSeconds(1200);
    private readonly Dictionary<EntityUid, TimeSpan> _elapsed = new();
    private readonly Dictionary<string, int> _salary = new();

    public override void Initialize()
    {
        LoadConfig();
        LoadSalaryPrototypes();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => ResetElapsedTimers());
    }

    private void LoadConfig()
    {
        _payInterval = _protoMan.TryIndex<AutoSalaryConfigPrototype>("AutoSalaryConfig", out var config)
            ? TimeSpan.FromSeconds(config.PayIntervalSeconds)
            : TimeSpan.FromSeconds(1200);
    }
    private bool HasActivePlayer(EntityUid body)
    {
        if (!_mindSystem.TryGetMind(body, out _, out var mind))
            return false;
        if (mind.Session == null)
            return false;
        if (mind.IsVisitingEntity)
            return false;
        return true;
    }

    private void LoadSalaryPrototypes()
    {
        _salary.Clear();
        foreach (var proto in _protoMan.EnumeratePrototypes<AutoSalaryJobPrototype>())
        {
            _salary[proto.ID] = proto.Salary;
        }
    }

    private void ResetElapsedTimers()
    {
        _elapsed.Clear();
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<BankAccountComponent, HumanoidAppearanceComponent>();
        while (query.MoveNext(out var body, out _, out _))
        {
            ProcessEntity(body, frameTime);
        }
    }

    private void ProcessEntity(EntityUid body, float frameTime)
    {
        if (ShouldSkipEntity(body, out var pay))
        {
            _elapsed.Remove(body);
            return;
        }
        ProcessSalary(body, pay, frameTime);
    }

    private bool ShouldSkipEntity(EntityUid body, out int pay)
    {
        pay = 0;
        if (IsEntityDead(body))
            return true;
        if (!HasActivePlayer(body))
            return true;
        if (!TryGetJobKey(body, out var jobKey))
            return true;
        if (!_salary.TryGetValue(jobKey, out pay))
            return true;
        return false;
    }

    private bool IsEntityDead(EntityUid body)
    {
        return !TryComp<MobStateComponent>(body, out var mobState) || _mobState.IsDead(body, mobState);
    }

    private void ProcessSalary(EntityUid body, int pay, float frameTime)
    {
        var t = _elapsed.GetValueOrDefault(body, TimeSpan.Zero) + TimeSpan.FromSeconds(frameTime);
        var payout = false;
        while (t >= _payInterval)
        {
            TryPaySalary(body, pay);
            t -= _payInterval;
            payout = true;
        }
        if (payout || t > TimeSpan.Zero)
            _elapsed[body] = t;
        else
            _elapsed.Remove(body);
    }

    private void TryPaySalary(EntityUid body, int pay)
    {
        if (_bank.TryBankDeposit(body, pay))
        {
            _popup.PopupEntity($"Вам начислена зарплата: {pay} кредитов.", body, body);
        }
    }

    private bool TryGetJobKey(EntityUid body, out string jobKey)
    {
        jobKey = string.Empty;

        if (!TryGetIdCard(body, out var id) || id == null)
            return false;

        if (!string.IsNullOrEmpty(id.JobTitle) && _salary.ContainsKey(id.JobTitle))
        {
            jobKey = id.JobTitle;
            return true;
        }

        if (!string.IsNullOrEmpty(id.LocalizedJobTitle) && _salary.ContainsKey(id.LocalizedJobTitle))
        {
            jobKey = id.LocalizedJobTitle;
            return true;
        }

        return false;
    }

    private bool TryGetIdCard(EntityUid body, out IdCardComponent? id)
    {
        id = null;
        if (!_inv.TryGetSlotEntity(body, "id", out var idUid))
            return false;
        if (EntityManager.TryGetComponent(idUid, out PdaComponent? pda) && pda.ContainedId != null)
            idUid = pda.ContainedId.Value;
        if (!EntityManager.TryGetComponent(idUid, out id))
            return false;
        return true;
    }
}
