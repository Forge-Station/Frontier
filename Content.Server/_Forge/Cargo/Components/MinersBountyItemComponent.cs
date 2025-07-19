using Robust.Shared.Prototypes;

namespace Content.Server.Cargo.Systems;
[RegisterComponent]
public sealed partial class MinersBountyItemComponent : Component
{
    [IdDataField]
    public string ID;
}
