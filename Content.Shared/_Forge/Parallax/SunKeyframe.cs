using Robust.Shared.Serialization;
using System.Numerics;

namespace Content.Shared.Parallax;

/// <summary>
/// A single keyframe for the sun's position and rotation over time.
/// Replaces a ValueTuple to allow for serialization.
/// </summary>
[DataDefinition]
public partial struct SunKeyframe
{
    [DataField("time", required: true)] public float Time;
    [DataField("position", required: true)] public Vector2 Position;
    [DataField("rotation", required: true)] public float Rotation;
}

