using Content.Shared.Inventory;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content._Forge.Shared.Loudspeaker.Components;

/// <summary>
/// Используется для предметов (или сущностей), обладающих возможностями громкоговорителя.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LoudspeakerComponent : Component
{
    /// <summary>
    /// Должно ли это работать в руках?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool WorksInHand;

    /// <summary>
    /// Может ли оно переключаться (ВКЛ/ВЫКЛ)?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool CanToggle;

    /// <summary>
    /// Громкоговоритель активен?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsActive;

    /// <summary>
    /// Должно ли это повлиять на чат "рядом"?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AffectChat;

    /// <summary>
    /// Должно ли это повлиять на чат "рация"?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AffectRadio;

    /// <summary>
    /// Какого размера должен быть новый шрифт текста?
    /// </summary>
    [DataField, AutoNetworkedField]
    public int FontSize = 18;

    /// <summary>
    /// Слот, в котором он будет работать.
    /// </summary>
    [DataField]
    public SlotFlags RequiredSlot = SlotFlags.EARS;

    /// <summary>
    /// Звук, который он должен воспроизводить для пользователя при переключении.
    /// </summary>
    [DataField]
    public SoundPathSpecifier ToggleSound = new("/Audio/Items/pen_click.ogg");
}
