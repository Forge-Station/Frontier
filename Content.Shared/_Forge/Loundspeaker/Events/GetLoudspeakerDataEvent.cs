using Content.Shared.Speech;
using Robust.Shared.Prototypes;

namespace Content._Forge.Shared.Loudspeaker.Events;

[ByRefEvent]
public record struct GetLoudspeakerDataEvent(
    bool IsActive = false,
    int? FontSize = null,
    bool AffectRadio = false,
    bool AffectChat = false);
