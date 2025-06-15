using Content.Server.Power.EntitySystems;
using Content.Server.Stack;
using Content.Shared._Corvax.DataMiner.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Corvax.DataMiner;

public sealed class DataMinerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;

    [ValidatePrototypeId<StackPrototype>]
    private const string Credit = "CreditCounterfeit";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DataMinerSyndicateComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<DataMinerSyndicateComponent, InteractUsingEvent>(OnInteractUsing);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var dataMinerQuery = EntityQueryEnumerator<DataMinerSyndicateComponent>();
        while (dataMinerQuery.MoveNext(out var uid, out var data))
        {
            if (!this.IsPowered(uid, EntityManager))
                continue;

            if (data.NextTimeTick <= 0)
            {
                data.NextTimeTick = data.Cooldown;
                data.Balance += data.AmountAccruals;
            }

            data.NextTimeTick -= frameTime;
        }
    }

    private void OnExamined(Entity<DataMinerSyndicateComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
        {
            _proto.TryIndex<ToolQualityPrototype>(ent.Comp.ExtractQuality, out var proto);
            args.AddMarkup(Loc.GetString("data-miner-title", ("balance", ent.Comp.Balance), ("tool", proto!.ToolName)));
        }
    }

    private void OnInteractUsing(Entity<DataMinerSyndicateComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !this.IsPowered(ent, EntityManager))
            return;

        if (_tool.HasQuality(args.Used, ent.Comp.ExtractQuality) && ent.Comp.Balance != 0)
        {
            _stack.Spawn(ent.Comp.Balance, Credit, Transform(ent).Coordinates);

            ent.Comp.Balance = 0;
        }
    }
}
