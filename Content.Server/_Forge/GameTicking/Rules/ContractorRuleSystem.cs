using System.Threading.Tasks;
using Content.Server.Antag;
using Content.Server.GameTicking.Events;
using Content.Shared._Forge.Contracts;
using Content.Server.EUI;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Shared._Forge.Contractor.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Mindshield.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Server._Forge.Contractor.UI;
using Content.Shared.Clothing;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Ghost;

// If someone is going to integrate it outside the frontier, do it through the normal logic of the antagonists
// Here I just need to do this
namespace Content.Server.GameTicking.Rules
{
    /// <summary>
    /// Alternative type of simple antagonists for more flexible use in the main idea of Contractor
    /// </summary>
    public sealed class ContractorRuleSystem : GameRuleSystem<ContractorRuleComponent>
    {
        [Dependency] private readonly AntagSelectionSystem _antag = default!;
        [Dependency] private readonly AudioSystem _audio = default!;
        [Dependency] private readonly EuiManager _euiMan = default!;
        [Dependency] private readonly NpcFactionSystem _factionSystem = default!;
        [Dependency] private readonly ISharedPlayerManager _player = default!;
        [Dependency] private readonly MindSystem _mind = default!;
        [Dependency] private readonly PopupSystem _popup = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly RoleSystem _role = default!;
        [Dependency] private readonly SharedHandsSystem _hands = default!;
        [Dependency] private readonly SharedJobSystem _jobSystem = default!;
        [Dependency] private readonly LoadoutSystem _loadout = default!;

        // Validation in case of changes
        [ValidatePrototypeId<NpcFactionPrototype>]
        private const string Pirate = "NFPirate";
        [ValidatePrototypeId<NpcFactionPrototype>]
        private const string Syndicate = "NFSyndicate";

        [ValidatePrototypeId<JobPrototype>]
        private const string Mercenary = "Mercenary"; // ID профессии наёмника. Измените, если он другой.

        [ValidatePrototypeId<EntityPrototype>]
        private const string MindRole = "MindRoleContractor";
        [ValidatePrototypeId<EntityPrototype>]
        private const string Uplink = "ContractorUplink";

        [ValidatePrototypeId<StartingGearPrototype>]
        private const string Gear = "ContractorGear";

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<ContractorRuleComponent, ComponentStartup>(OnRuleStart);
        }

        protected override void AppendRoundEndText(EntityUid uid,
            ContractorRuleComponent component,
            GameRuleComponent gameRule,
            ref RoundEndTextAppendEvent args)
        {
            var contractors = new List<(string Name, string Username, int Contracts)>();
            var query = EntityQueryEnumerator<ContractorComponent, ActorComponent>();
            while (query.MoveNext(out var ent, out var contractor, out var actor))
            {
                contractors.Add((EntityManager.GetComponent<MetaDataComponent>(ent).EntityName,
                            actor.PlayerSession.Name,
                            contractor.CountContracts));
            }

            args.AddLine(Loc.GetString("contractor-round-end-total-contracts", ("count", contractors.Count)));

            contractors.Sort((x, y) => y.Contracts.CompareTo(x.Contracts));
            foreach (var (name, username, count) in contractors)
            {
                args.AddLine(Loc.GetString("contractor-round-end-contractor-stats", ("name", name),
                    ("username", username), ("count", count)));
            }
        }

        #region Processed

        /// <summary>
        /// Waits, and then tries to select candidates
        /// </summary>
        private void OnRuleStart(Entity<ContractorRuleComponent> ent, ref ComponentStartup args)
        {
            Timer.Spawn(TimeSpan.FromMinutes(ent.Comp.Duration), () =>
            {
                if (!Exists(ent.Owner))
                    return;

                // Count players who are not ghosts.
                var query = EntityQueryEnumerator<ActorComponent>();
                var playerCount = 0;
                while (query.MoveNext(out var uid, out _))
                {
                    if (!HasComp<GhostComponent>(uid))
                        playerCount++;
                }

                if (playerCount <= 15)
                    return;

                SelectCandidates(ent.Comp);
            });
        }

        /// <summary>
        /// Selects a list of suitable candidates
        /// </summary>
        private void SelectCandidates(ContractorRuleComponent comp)
        {
            var candidates = new List<EntityUid>();

            var humanoidQuery = EntityQueryEnumerator<HumanoidAppearanceComponent, ActorComponent>();
            while (humanoidQuery.MoveNext(out var candidate, out _, out _))
            {
                if (HasComp<MindShieldComponent>(candidate) || HasComp<ContractorComponent>(candidate))
                    continue;

                // You can add any new faction here if necessary
                if (_factionSystem.IsMember((candidate, null), Pirate)
                    || _factionSystem.IsMember((candidate, null), Syndicate))
                    continue;

                if (!_mind.TryGetMind(candidate, out var mindId, out _) ||
                    !_jobSystem.MindTryGetJob(mindId, out var jobProto) ||
                    jobProto.ID != Mercenary)
                    continue;

                candidates.Add(candidate);
            }

            comp.SelectedCandidates = candidates;
            if (comp.SelectedCandidates.Count == 0)
                return;

            var count = _random.Next(2, 4); // Number of antagonist roles
            count = Math.Min(count, comp.SelectedCandidates.Count);
            for (var i = 0; i < count; i++)
            {
                var randomIndex = _random.Next(comp.SelectedCandidates.Count);
                var selectedCandidate = comp.SelectedCandidates[randomIndex];

                comp.SelectedCandidates.RemoveAt(randomIndex);

                ProcessCandidate(selectedCandidate);
            }
        }

        /// <summary>
        /// Offers the candidate to become a contractor
        /// </summary>
        /// <param name="candidate">The selected player</param>
        private async void ProcessCandidate(EntityUid candidate)
        {
            if (_mind.TryGetMind(candidate, out _, out var mind) &&
                mind is { UserId: not null } && _player.TryGetSessionById(mind.UserId, out var session))
            {
                var window = new ContractorEui(candidate, this, _popup);
                _euiMan.OpenEui(window, session);

                var filter = Filter.Empty();
                filter.AddPlayer(session);

                await PlayRingtoneSequence(filter);
            }
        }

        /// <summary>
        /// Trying to choose a new candidate to replace the rejected one
        /// </summary>
        private void TryFindReplacementCandidate()
        {
            var query = EntityQueryEnumerator<ContractorRuleComponent>();
            while (query.MoveNext(out _, out var comp))
            {
                if (comp.SelectedCandidates.Count == 0)
                    return;

                var randomIndex = _random.Next(comp.SelectedCandidates.Count);
                var newCandidate = comp.SelectedCandidates[randomIndex];
                comp.SelectedCandidates.RemoveAt(randomIndex);

                ProcessCandidate(newCandidate);
                break;
            }
        }

        /// <summary>
        /// The logic of accepted
        /// </summary>
        public void OnContractAccepted(EntityUid contractor)
        {
            if (!_mind.TryGetMind(contractor, out var mindId, out var mind) || HasComp<ContractorComponent>(contractor))
                return;

            var comp = EnsureComp<ContractorComponent>(contractor);
            if (mindId == default || !_role.MindHasRole<ContractorComponent>(mindId))
                _role.MindAddRole(mindId, MindRole);
            if (mind is { UserId: not null } && _player.TryGetSessionById(mind.UserId, out var session))
                _antag.SendBriefing(session, Loc.GetString("contractor-role-greeting"), Color.OrangeRed,
                    new SoundPathSpecifier("/Audio/_Forge/Ambience/Antag/contractor_spawn.ogg"));

            var uplink = Spawn(Uplink, Transform(contractor).Coordinates);
            EnsureComp<ContractsComponent>(uplink).UplinkOwner = contractor;

            comp.Uplink = uplink;

            _hands.TryPickupAnyHand(contractor, uplink, animate: false);
            _loadout.Equip(contractor, new List<ProtoId<StartingGearPrototype>>() { Gear }, null);
        }

        /// <summary>
        /// The logic of rejected
        /// </summary>
        public void OnContractRejected()
            => TryFindReplacementCandidate();

        // Yes, I'm not looking for easy ways
        private async Task PlayRingtoneSequence(Filter filter)
        {
            // It means "Hello" in English using Morse code
            var durations = new[] { 150, 150, 150, 150, 150, 150, 450, 150, 150, 150, 450, 150, 150, 450, 450, 450 };

            var noteIndex = 0;
            var notes = new[] { "a", "b", "c", "d", "e", "f", "g" };
            foreach (var duration in durations)
            {
                var notePath = new ResPath($"/Audio/Effects/RingtoneNotes/{notes[noteIndex]}.ogg");
                var noteSpecifier = new SoundPathSpecifier(notePath);

                _audio.PlayGlobal(noteSpecifier, filter, false);
                await Task.Delay(duration);

                noteIndex = (noteIndex + 1) % notes.Length;

                await Task.Delay(50);
            }
        }

        #endregion
    }
}
