using Content.Shared._Mono.Company;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.PDA;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Server.Audio; // Forge-change: spawnSound
using Robust.Shared.Audio; // Forge-change: spawnSound
using Content.Shared.Examine;

namespace Content.Server._Mono.Company;

/// <summary>
/// This system handles assigning a company to players when they join.
/// TODO: remove hardcoded slop.
/// whoever hardcoded ts is getting slimed out no joke.
/// </summary>
public sealed class CompanySystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedJobSystem _job = default!;
    [Dependency] private readonly SharedIdCardSystem _idCardSystem = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    // Dictionary to store original company preferences for players
    private readonly Dictionary<string, string> _playerOriginalCompanies = new();

    private readonly HashSet<string> _nfsdJobs = new()
    {
        "Sheriff",
        "Bailiff",
        "SeniorOfficer", // Sergeant
        "Deputy",
        "Brigmedic",
        "NFDetective",
        "EngineerNfsd",
        "PublicAffairsLiaison",
        "Cadet",
    };

    private readonly HashSet<string> _syndicateJobs = new()
    {
        "Commander",
        "EngineerSyn",
        "MedSyn",
        "Stormtrooper",
        "smuggler",
        "smugglerBodyguard",
    };

    private readonly HashSet<string> _ntJobs = new()
    {
        "StationRepresentative",
        "PlantManager",
        "PlantTechnician",
        "StationTrafficController",
        "SecurityGuard",
        "OutpostEngineer",
        "OutpostMedic",
        "OutpostService",
        "MailCarrier",
    };

    // Freelancers = no company
    private readonly HashSet<string> _noneJobs = new()
    {
        "NFPirateCaptain",
        "NFPirateFirstMate",
        "NFPirate",
    };

    private readonly HashSet<string> _hospitalJobs = new()
    {
        "DirectorOfCare",
        "paraMedical",
    };

    public override void Initialize()
    {
        base.Initialize();

        // Subscribe to player spawn event to add the company component
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);

        // Subscribe to player detached event to clean up stored preferences
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);

        // Subscribe to examination to show the company on examine
        SubscribeLocalEvent<Shared._Mono.Company.CompanyComponent, ExaminedEvent>(OnExamined);
    }

    private void OnPlayerDetached(PlayerDetachedEvent args)
    {
        // Clean up stored preferences when player disconnects
        _playerOriginalCompanies.Remove(args.Player.UserId.ToString());
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        // Add the company component with the player's saved company
        var companyComp = EnsureComp<Shared._Mono.Company.CompanyComponent>(args.Mob);

        var playerId = args.Player.UserId.ToString();
        var profileCompany = args.Profile.Company;

        // Store the player's original company preference if not already stored
        if (!_playerOriginalCompanies.ContainsKey(playerId))
        {
            _playerOriginalCompanies[playerId] = profileCompany;
        }

        // todo - make this a switch statement or something lol. who cares.
        // Check if player's job is one of the TSF jobs
        if (args.JobId != null && _nfsdJobs.Contains(args.JobId))
        {
            // Assign TSF company
            companyComp.CompanyName = "NFSD";
        }
        // Check if player's job is one of the Rogue jobs
        else if (args.JobId != null && _syndicateJobs.Contains(args.JobId))
        {
            // Assign Syndicate company
            companyComp.CompanyName = "Syndicate";
        }
        else if (args.JobId != null && _hospitalJobs.Contains(args.JobId))
        {
            // Assign Hospial company
            companyComp.CompanyName = "Hospital";
        }
        else if (args.JobId != null && _ntJobs.Contains(args.JobId))
        {
            // Assign Syndicate company
            companyComp.CompanyName = "Nanotrasen";
        }
        else if (args.JobId != null && _noneJobs.Contains(args.JobId))
        {
            // Assign "None" company
            companyComp.CompanyName = "None";
        }
        else
        {
            // Only consider whitelist if the player has NO specific company preference
            bool loginFound = false;

            // Only check logins if the player hasn't explicitly set a company preference
            // or if their preference is "None"
            if (string.IsNullOrEmpty(profileCompany))
            {
                // Check for company login whitelists
                foreach (var companyProto in _prototypeManager.EnumeratePrototypes<CompanyPrototype>())
                {
                    if (companyProto.Logins.Contains(args.Player.Name))
                    {
                        companyComp.CompanyName = companyProto.ID;
                        loginFound = true;
                        break;
                    }
                }
            }

            // If no login was found or login check was skipped due to player preference, use the player's preference
            if (!loginFound)
            {
                // Use "None" as fallback for empty company
                if (string.IsNullOrEmpty(profileCompany))
                    profileCompany = "None";

                // Restore the player's original company preference
                companyComp.CompanyName = profileCompany;
            }
        }

        if (_prototypeManager.TryIndex<CompanyPrototype>(companyComp.CompanyName, out var proto))
        {
            if (proto.SpawnSound != null)
            {
                var audioParams = AudioParams.Default.WithVolume(-5f);
                _audio.PlayPvs(proto.SpawnSound, args.Mob, audioParams);
            }
        }

        // Ensure the component is networked to clients
        Dirty(args.Mob, companyComp);

        // Update the player's ID card with the company information
        UpdateIdCardCompany(args.Mob, companyComp.CompanyName);
    }

    /// <summary>
    /// Updates the player's ID card with their company information
    /// </summary>
    private void UpdateIdCardCompany(EntityUid playerEntity, string companyName)
    {
        // Try to get the player's ID card
        if (!_inventorySystem.TryGetSlotEntity(playerEntity, "id", out var idUid))
            return;

        var cardId = idUid.Value;

        // Check if it's a PDA with an ID card inside
        if (TryComp<PdaComponent>(idUid, out var pdaComponent) && pdaComponent.ContainedId != null)
            cardId = pdaComponent.ContainedId.Value;

        // Update the ID card with company information
        if (TryComp<IdCardComponent>(cardId, out var idCard))
        {
            _idCardSystem.TryChangeCompanyName(cardId, companyName, idCard);
        }
    }

    private void OnExamined(EntityUid uid, Shared._Mono.Company.CompanyComponent component, ExaminedEvent args)
    {
        if (_prototypeManager.TryIndex<CompanyPrototype>(component.CompanyName, out var prototype) && component.CompanyName != "None")
        {
            // Use the color from the prototype with gender-appropriate pronoun
            args.PushMarkup(Loc.GetString("examine-company",
                ("entity", uid),
                ("company", $"[color={prototype.Color.ToHex()}]{prototype.Name}[/color]")),
                priority: 100); // Much higher priority (100) will ensure it's at the top
        }
        else if (component.CompanyName != "None" && component.CompanyName != "Syndicate") // Forge-change: add Syndicate
        {
            // Fallback for companies without prototypes
            args.PushMarkup(Loc.GetString("examine-company",
                ("entity", uid),
                ("company", $"[color=yellow]{component.CompanyName}[/color]")),
                priority: 100);
        }
        // Don't show anything for "None" company // Forge-change: and for Syndicate
    }
}
