using Content.Shared._Forge.ExtendedPinpointer;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._Forge.ExtendedPinpointer;

public sealed class ExtendedPinpointerSystem : SharedExtendedPinpointerSystem
{
    [Dependency] private readonly IEyeManager _eyeManager = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // we want to show pinpointers arrow direction relative
        // to players eye rotation

        // because eye can change it rotation anytime
        // we need to update this arrow in a update loop
        var query = EntityQueryEnumerator<ExtendedPinpointerComponent, SpriteComponent>();
        while (query.MoveNext(out var _, out var pinpointer, out var sprite))
        {
            if (!pinpointer.HasTarget)
                continue;
            var eye = _eyeManager.CurrentEye;
            var angle = pinpointer.ArrowAngle + eye.Rotation;

            switch (pinpointer.DistanceToTarget)
            {
                case Distance.Close:
                case Distance.Medium:
                case Distance.Far:
                    sprite.LayerSetRotation(PinpointerLayers.Screen, angle);
                    break;
                default:
                    sprite.LayerSetRotation(PinpointerLayers.Screen, Angle.Zero);
                    break;
            }
        }
    }
}
