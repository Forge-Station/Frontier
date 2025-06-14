using Robust.Shared.Map;

namespace Content.Server.Corvax.PortalAutoLink;

[RegisterComponent, Access(typeof(PortalAutoLinkSystem))]
public sealed partial class PortalAutoLinkComponent : Component
{
    [DataField]
    public string? LinkKey { get; set; } = "ReplaceMe";
}
