using System.Linq;
using Content.Shared._NC.Trade;
using Robust.Client.Player;
using Robust.Client.UserInterface;


namespace Content.Client._NC.Trade;


public sealed class NcStoreStructuredBoundUi(EntityUid owner, Enum uiKey)
    : BoundUserInterface(owner, uiKey)
{
    private readonly IPlayerManager _player = IoCManager.Resolve<IPlayerManager>();
    private int _lastRevision = int.MinValue;

    private NcStoreMenu? _menu;

    private EntityUid? Actor => _player.LocalSession?.AttachedEntity;


    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not StoreUiState st)
            return;

        EnsureMenuCreated();
        if (_menu == null)
            return;

        if (st.Revision == _lastRevision)
            return;

        _lastRevision = st.Revision;

        _menu.ApplyState(st.Balance, st.Listings.ToList(), st.MassSellTotals);
        _menu.PopulateContracts(st.Contracts);
        _menu.Visible = true;
    }

    private void EnsureMenuCreated()
    {
        if (_menu != null)
            return;

        _menu = this.CreateWindow<NcStoreMenu>();
        _lastRevision = int.MinValue;


        if (EntMan.TryGetComponent(Owner, out MetaDataComponent? meta))
            _menu.Title = meta.EntityName;

        _menu.OnBuyPressed += OnBuy;
        _menu.OnSellPressed += OnSell;
        _menu.OnMassSellPulledCrate += OnMassSellPulledCrate;
        _menu.OnContractClaim += OnContractClaim;

        _menu.OnClose += () =>
        {
            _menu.Orphan();
            _menu = null;
            _lastRevision = int.MinValue;
        };
    }


    private void OnBuy(StoreListingData data, int qty)
    {
        if (Actor is null)
            return;

        SendMessage(new StoreBuyListingBoundUiMessage(data.Id, qty));
    }

    private void OnSell(StoreListingData data, int qty)
    {
        if (Actor is null)
            return;

        SendMessage(new StoreSellListingBoundUiMessage(data.Id, qty));
    }

    private void OnContractClaim(string contractId)
    {
        if (Actor is null)
            return;

        SendMessage(new ClaimContractBoundMessage(contractId));
    }

    private void OnMassSellPulledCrate()
    {
        if (Actor is null)
            return;

        SendMessage(new StoreMassSellPulledCrateBoundUiMessage());
    }


    protected override void Dispose(bool disposing)
    {
        if (_menu != null)
        {
            _menu.OnBuyPressed -= OnBuy;
            _menu.OnSellPressed -= OnSell;
            _menu.OnMassSellPulledCrate -= OnMassSellPulledCrate;
            _menu.OnContractClaim -= OnContractClaim;

            _menu.Orphan();
            _menu = null;
        }

        base.Dispose(disposing);
    }
}
