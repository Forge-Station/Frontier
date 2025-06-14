using Content.Server._NF.Bank;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Access.Components;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.PDA;
using Content.Shared.Roles;
using Robust.Shared.Timing;

namespace Content.Server._Corvax.AutoSalarySystem;


public sealed class AutoSalarySystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inv   = default!;
    [Dependency] private readonly BankSystem      _bank  = default!;
    [Dependency] private readonly IGameTiming     _time  = default!;

    private static readonly TimeSpan PayInterval = TimeSpan.FromSeconds(1200);

    [ValidatePrototypeId<DepartmentPrototype>]
    private const string SecurityDep  = "Security";
    [ValidatePrototypeId<DepartmentPrototype>]
    private const string FrontierDep  = "Frontier";

    private readonly Dictionary<EntityUid, TimeSpan> _next = new();

    private readonly Dictionary<string, int> _salary = new();

    public override void Initialize()
    {
        void Add(string locKey, int pay) => _salary[Loc.GetString(locKey)] = pay;

        Add("job-name-bailiff",        13_500);
        Add("job-name-brigmedic",      11_000);
        Add("job-name-cadet-nf",        8_000);
        Add("job-name-deputy",         10_000);
        Add("job-name-nf-detective",   11_000);
        Add("job-name-security-guard", 10_000);
        Add("job-name-sheriff",        17_000);
        Add("job-name-stc",             6_000);
        Add("job-name-sr",             14_000);
        Add("job-name-pal",            12_000);
        Add("job-name-doc",            10_000);
        Add("job-name-senior-officer", 12_000);
        Add("job-name-janitor",         7_000);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _next.Clear();
            Log.Info("[AutoSalary] round restart — timers reset");
        });
    }

    public override void Update(float frameTime)
    {
        var now = _time.CurTime;

        var query = EntityQueryEnumerator<BankAccountComponent, HumanoidAppearanceComponent>();
        while (query.MoveNext(out var body, out _, out _))
        {
            if (!TryGetActiveJob(body, out var title))
            {
                _next.Remove(body);
                continue;
            }

            if (!_salary.TryGetValue(title, out var pay))
                continue;

            if (!_next.TryGetValue(body, out var due))
                due = now + PayInterval;

            if (now < due)
            {
                _next[body] = due;
                continue;
            }

            if (_bank.TryBankDeposit(body, pay))
                Log.Info("[AutoSalary] round restart — timers reset");
            else
                Log.Warning($"[AutoSalary] deposit FAIL {pay} Cr → {body} ({title})");

            _next[body] = now + PayInterval;
        }
    }

    private bool TryGetActiveJob(EntityUid body, out string title)
    {
        title = string.Empty;

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
                title = id.LocalizedJobTitle ?? string.Empty;
                return title.Length > 0;
            }
        }
        return false;
    }
}
