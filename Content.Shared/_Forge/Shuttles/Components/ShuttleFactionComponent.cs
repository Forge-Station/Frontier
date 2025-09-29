using Robust.Shared.GameStates;

namespace Content.Shared.Shuttles.Components
{
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
    public sealed partial class ShuttleFactionComponent : Component
    {
        [DataField("faction"), AutoNetworkedField]
        [ViewVariables(VVAccess.ReadWrite)]
        public string Faction = string.Empty;
    }
}
