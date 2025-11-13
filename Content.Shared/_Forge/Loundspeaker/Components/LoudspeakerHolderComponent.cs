namespace Content._Forge.Shared.Loudspeaker.Components;

/// <summary>
///     Обозначает объект, который держит в руках как громкоговоритель.
/// </summary>
[RegisterComponent]
public sealed partial class LoudspeakerHolderComponent : Component
{
    [DataField]
    public List<EntityUid> Loudspeakers = new();
}
