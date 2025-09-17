using Content.Shared._Forge.ExtendedPinpointer;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Content.Shared.Pinpointer;

namespace Content.Client._Forge.ExtendedPinpointer;

public sealed class ExtendedPinpointerSystem : SharedExtendedPinpointerSystem
{
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // we want to show pinpointers arrow direction relative
        // to players eye rotation

        // because eye can change it rotation anytime
        // we need to update this arrow in a update loop
        var query = EntityQueryEnumerator<ExtendedPinpointerComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var pinpointer, out var sprite))
        {
            // Frontier: ensure question mark is aligned with the screen
            if (!pinpointer.HasTarget)
            {
                sprite.LayerSetRotation(PinpointerLayers.Screen, Angle.Zero);
                continue;
            }
            // End Frontier: ensure question mark is aligned with the screen

            var eye = _eyeManager.CurrentEye;
            var angle = pinpointer.ArrowAngle + eye.Rotation;

            switch (pinpointer.DistanceToTarget)
            {
                case Content.Shared._Forge.ExtendedPinpointer.Distance.Close:
                case Content.Shared._Forge.ExtendedPinpointer.Distance.Medium:
                case Content.Shared._Forge.ExtendedPinpointer.Distance.Far:
                    _sprite.LayerSetRotation((uid, sprite), PinpointerLayers.Screen, angle);
                    break;
                default:
                    _sprite.LayerSetRotation((uid, sprite), PinpointerLayers.Screen, Angle.Zero);
                    break;
            }
        }
    }
}
