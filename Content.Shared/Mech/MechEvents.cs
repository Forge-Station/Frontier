namespace Content.Shared.Mech.Events;

/// <summay>
/// Raised on an entity that has been inserted into a mech as a pilot.
/// </summary>
public record struct MechInsertedEvent(EntityUid mechUid);

/// <summay>
/// Raised on an entity that has been ejected from a mech as its pilot.
/// </summary>
public record struct MechEjectedEvent(EntityUid mechUid);