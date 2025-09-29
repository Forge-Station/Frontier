using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._ADT.Modsuit.Events;

[Serializable, NetSerializable]
public sealed partial class ToggleModPartsDoAfterEvent : SimpleDoAfterEvent;
