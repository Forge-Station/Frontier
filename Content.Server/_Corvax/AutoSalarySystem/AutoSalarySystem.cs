using Content.Server._NF.Bank;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Access.Components;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.PDA;
using Content.Shared.Roles;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Server.Popups;


namespace Content.Server._Corvax.AutoSalarySystem;

public sealed class AutoSalarySystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inv = default!;
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;


    // ВРЕМЯ ВЫПЛАТЫ ЗАРПЛАТЫ (секунды)
    private static readonly TimeSpan PayInterval = TimeSpan.FromSeconds(1200); // 20 минут

    [ValidatePrototypeId<DepartmentPrototype>]
    private const string SecurityDep = "Security";
    [ValidatePrototypeId<DepartmentPrototype>]
    private const string FrontierDep = "Frontier";

    private readonly Dictionary<EntityUid, TimeSpan> _elapsed = new();

    private readonly Dictionary<string, int> _salary = new()
    {
        { "job-name-bailiff",         13500 },
        { "job-name-brigmedic",       11000 },
        { "job-name-cadet-nf",         8000 },
        { "job-name-deputy",          10000 },
        { "job-name-nf-detective",    11000 },
        { "job-name-security-guard",  10000 },
        { "job-name-sheriff",         17000 },
        { "job-name-stc",              6000 },
        { "job-name-sr",              14000 },
        { "job-name-pal",             12000 },
        { "job-name-doc",             10000 },
        { "job-name-senior-officer",  12000 },
        { "job-name-janitor",          7000 },
        { "job-name-mail-carrier",     8000 }
    };

    public override void Initialize()
    {
        var addLocalized = new Dictionary<string, int>();
        foreach (var kv in _salary)
        {
            var localized = Loc.GetString(kv.Key);
            if (!string.IsNullOrEmpty(localized) && !_salary.ContainsKey(localized))
                addLocalized[localized] = kv.Value;
        }
        foreach (var kv in addLocalized)
        {
            _salary[kv.Key] = kv.Value;
        }

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _elapsed.Clear();
            Log.Info("[AutoSalary] round restart — timers reset");
        });
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<BankAccountComponent, HumanoidAppearanceComponent>();
        while (query.MoveNext(out var body, out _, out _))
        {
            if (!TryComp<MobStateComponent>(body, out var mobState))
                continue;
            if (_mobState.IsDead(body, mobState))
                continue;

            if (!TryGetActiveJobTitleHybrid(body, out var jobKey))
            {
                _elapsed.Remove(body);
                continue;
            }

            if (!_salary.TryGetValue(jobKey, out var pay))
                continue;

            var t = _elapsed.GetValueOrDefault(body, TimeSpan.Zero) + TimeSpan.FromSeconds(frameTime);
            while (t >= PayInterval)
            {
                if (_bank.TryBankDeposit(body, pay))
                {
                    Log.Info($"[AutoSalary] +{pay} Cr → {jobKey} ({body})");
                    _popup.PopupEntity($"Вам начислена зарплата: {pay} кредитов.", body, body);
                }
                else
                {
                    Log.Warning($"[AutoSalary] deposit FAIL {pay} Cr → {body} ({jobKey})");
                }
                t -= PayInterval;
            }
            _elapsed[body] = t;
        }
    }

    private bool TryGetActiveJobTitleHybrid(EntityUid body, out string jobKey)
    {
        jobKey = string.Empty;

        if (!_inv.TryGetSlotEntity(body, "id", out var idUid))
            return false;

        if (EntityManager.TryGetComponent(idUid, out PdaComponent? pda) && pda.ContainedId != null)
            idUid = pda.ContainedId.Value;

        if (!EntityManager.TryGetComponent(idUid, out IdCardComponent? id))
            return false;

        foreach (var dep in id.JobDepartments)
        {
            if (dep == SecurityDep || dep == FrontierDep)
            {
                if (id.JobTitle != null && _salary.ContainsKey(id.JobTitle.Value))
                {
                    jobKey = id.JobTitle.Value;
                    return true;
                }
                if (!string.IsNullOrEmpty(id.LocalizedJobTitle) && _salary.ContainsKey(id.LocalizedJobTitle))
                {
                    jobKey = id.LocalizedJobTitle;
                    return true;
                }
                return false;
            }
        }
        return false;
    }
}
