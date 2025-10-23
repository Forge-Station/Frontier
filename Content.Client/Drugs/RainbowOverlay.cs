using Content.Shared.CCVar;
using Content.Shared.Drugs;
using Content.Shared.StatusEffectNew; // Forge-Change
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Drugs;

public sealed class RainbowOverlay : Overlay
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntitySystemManager _sysMan = default!;
    [Dependency] private readonly IGameTiming _timing = default!; // Forge-Change
    private readonly StatusEffectsSystem _statusEffects = default!; // Forge-Change

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;
    private readonly ShaderInstance _rainbowShader;

    public float Intoxication = 0.0f;
    public float TimeTicker = 0.0f;

    private const float VisualThreshold = 10.0f;
    private const float PowerDivisor = 250.0f;

    private float EffectScale => Math.Clamp((Intoxication - VisualThreshold) / PowerDivisor, 0.0f, 1.0f);

    public RainbowOverlay()
    {
        IoCManager.InjectDependencies(this);

        _statusEffects = _sysMan.GetEntitySystem<StatusEffectsSystem>(); // Forge-Change

        _rainbowShader = _prototypeManager.Index<ShaderPrototype>("Rainbow").InstanceUnique();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var playerEntity = _playerManager.LocalEntity;

        if (playerEntity == null)
            return;

        if (!_statusEffects.TryGetEffectsEndTimeWithComp<SeeingRainbowsStatusEffectComponent>(playerEntity, out var endTime)) // Forge-Change
            return;

        endTime ??= TimeSpan.MaxValue; // Forge-Change
        var timeLeft = (float)(endTime - _timing.CurTime).Value.TotalSeconds; // Forge-Change

        TimeTicker += args.DeltaSeconds;
        if (timeLeft - TimeTicker > timeLeft / 16f)
        {
            Intoxication += (timeLeft - Intoxication) * args.DeltaSeconds / 16f;
        }
        else
        {
            Intoxication -= Intoxication / (timeLeft - TimeTicker) * args.DeltaSeconds;
        }
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_playerManager.LocalEntity, out EyeComponent? eyeComp))
            return false;

        if (args.Viewport.Eye != eyeComp.Eye)
            return false;

        return EffectScale > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        // TODO disable only the motion part or ike's idea (single static frame of the overlay)
        if (_config.GetCVar(CCVars.ReducedMotion))
            return;

        if (ScreenTexture == null)
            return;

        var handle = args.WorldHandle;
        _rainbowShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _rainbowShader.SetParameter("effectScale", EffectScale);
        handle.UseShader(_rainbowShader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
