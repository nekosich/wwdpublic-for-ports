using System;
using Content.Shared.Psychiatry;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Psychiatry.Overlays;

public sealed class PsychosisOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly ShaderInstance _shader;

    private const float MaxVisualSeverity = 20f;
    private const float MaxVisualIntensity = 0.20f;
    private const float MaxBurst = 0.10f;

    private float _intensity;
    public float BurstStrength;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    public PsychosisOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index<ShaderPrototype>("Psychosis").InstanceUnique();
    }

    public void TriggerBurst(float burst)
    {
        BurstStrength = Math.Clamp(MathF.Max(BurstStrength, burst), 0f, MaxBurst);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var baseIntensity = 0f;
        if (_player.LocalEntity is { } local
            && _entityManager.TryGetComponent(local, out SchizophreniaComponent? psychosis))
        {
            baseIntensity = StageIntensity(psychosis);
            if (_timing.CurTime < psychosis.SuppressionUntil)
                baseIntensity *= 0.45f;
        }

        BurstStrength = MathF.Max(0f, BurstStrength - args.DeltaSeconds * 0.9f);
        _intensity = Math.Clamp(baseIntensity + BurstStrength, 0f, MaxVisualIntensity);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_player.LocalEntity, out EyeComponent? eyeComp))
            return false;

        if (args.Viewport.Eye != eyeComp.Eye)
            return false;

        return _intensity > 0.01f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("effectScale", _intensity);
        _shader.SetParameter("noiseStrength", 0.06f + _intensity * 0.42f);
        _shader.SetParameter("aberration", 0.08f + _intensity * 0.50f);
        _shader.SetParameter("vignette", 0.05f + _intensity * 0.35f);
        _shader.SetParameter("glitch", Math.Clamp(_intensity * 1.2f + BurstStrength * 1.4f, 0f, 0.35f));

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }

    private static float StageIntensity(SchizophreniaComponent psychosis)
    {
        var severity = Math.Clamp(psychosis.Severity, 0f, MaxVisualSeverity) / MaxVisualSeverity;

        return psychosis.Stage switch
        {
            SchizophreniaStage.Remission => Math.Clamp(severity * 0.01f, 0f, 0.01f),
            SchizophreniaStage.Prodromal => Math.Clamp(0.02f + severity * 0.03f, 0f, 0.05f),
            SchizophreniaStage.Active => Math.Clamp(0.05f + severity * 0.05f, 0f, 0.10f),
            _ => Math.Clamp(0.09f + severity * 0.07f, 0f, 0.16f)
        };
    }
}
