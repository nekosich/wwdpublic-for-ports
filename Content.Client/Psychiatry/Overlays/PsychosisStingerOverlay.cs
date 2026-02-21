using System.Numerics;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.Psychiatry.Overlays;

public sealed class PsychosisStingerOverlay : Overlay
{
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    private readonly Font _font;

    private string? _text;
    private float _life;
    private float _intensity;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public PsychosisStingerOverlay()
    {
        IoCManager.InjectDependencies(this);
        _font = _resourceCache.GetFont("/Fonts/NotoSans/NotoSans-Bold.ttf", 28);
    }

    public void Trigger(string text, float intensity = 1f)
    {
        _text = text;
        _intensity = Math.Clamp(intensity, 0.2f, 1f);
        _life = MathF.Max(_life, 0.35f + _intensity * 0.55f);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _life > 0.01f && !string.IsNullOrWhiteSpace(_text);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        if (_life <= 0f)
            return;

        _life = MathF.Max(0f, _life - args.DeltaSeconds);
        _intensity = MathF.Max(0f, _intensity - args.DeltaSeconds * 1.6f);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (string.IsNullOrWhiteSpace(_text))
            return;

        var handle = args.ScreenHandle;
        var viewport = args.ViewportBounds;
        var flash = Math.Clamp(_life * 1.8f, 0f, 1f) * (0.12f + _intensity * 0.30f);
        handle.DrawRect(viewport, new Color(0.08f, 0f, 0f, flash));

        var scale = 1.05f + _intensity * 0.30f;
        var center = viewport.Center + new Vector2(
            MathF.Sin(_life * 37f) * (6f * _intensity),
            MathF.Cos(_life * 29f) * (4f * _intensity));
        var dimensions = handle.GetDimensions(_font, _text, scale);
        var color = new Color(0.95f, 0.1f, 0.1f, Math.Clamp(0.35f + _life * 2.2f, 0f, 1f));
        handle.DrawString(_font, center - dimensions / 2f, _text, scale, color);
    }
}
