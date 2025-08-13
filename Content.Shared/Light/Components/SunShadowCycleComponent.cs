using System.Numerics;
using Robust.Shared.GameStates;
using System; // Forge-Change
using Robust.Shared.Serialization; // Forge-Change

namespace Content.Shared.Light.Components;

/// <summary>
/// Applies <see cref="SunShadowComponent"/> direction vectors based on a time-offset. Will track <see cref="LightCycleComponent"/> on on MapInit
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SunShadowCycleComponent : Component
{
    /// <summary>
    /// How long an entire cycle lasts
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan Duration = TimeSpan.FromMinutes(30);

    [DataField, AutoNetworkedField]
    public TimeSpan Offset;

    // Originally had this as ratios but it was slightly annoying to use.

    /// <summary>
    /// Time to have each direction applied. Will lerp from the current value to the next one.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<SunShadowDirection> Directions = new() // Forge-Change
    {
        new() { Ratio = 0f, Direction = new(0f, 3f), Alpha = 0f }, // Forge-Change
        new() { Ratio = 0.25f, Direction = new(-3f, -0.1f), Alpha = 0.5f }, // Forge-Change
        new() { Ratio = 0.5f, Direction = new(0f, -3f), Alpha = 0.8f }, // Forge-Change
        new() { Ratio = 0.75f, Direction = new(3f, -0.1f), Alpha = 0.5f }, // Forge-Change
    };
}

// Forge-Change-Start

/// <summary>
/// A single keyframe for the sun's shadow direction over time.
/// Replaces a ValueTuple to allow for serialization.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public partial struct SunShadowDirection
{
    [DataField("ratio", required: true)]
    public float Ratio;

    [DataField("direction", required: true)]
    public Vector2 Direction;

    [DataField("alpha", required: true)]
    public float Alpha;
}
 // Forge-Change-End
