namespace Content._Forge.Shared.Loudspeaker.Events;

[ByRefEvent]
public record struct GetLoudspeakerEvent(
    List<EntityUid> Loudspeakers);
