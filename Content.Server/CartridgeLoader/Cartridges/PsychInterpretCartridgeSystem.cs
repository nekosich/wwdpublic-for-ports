using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.Psychiatry;
using Content.Shared.CartridgeLoader;
using Content.Shared.CartridgeLoader.Cartridges;
using Content.Shared.Popups;

namespace Content.Server.CartridgeLoader.Cartridges;

public sealed class PsychInterpretCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly PsychotherapySystem _psychotherapy = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PsychInterpretCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<PsychInterpretCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
        SubscribeLocalEvent<PsychInterpretCartridgeComponent, CartridgeAfterInteractEvent>(OnAfterInteract);
    }

    private void OnUiReady(Entity<PsychInterpretCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        RefreshLastScan(ent, args.Loader);
        UpdateUiState(ent, args.Loader);
    }

    private void OnUiMessage(Entity<PsychInterpretCartridgeComponent> ent, ref CartridgeMessageEvent args)
    {
        if (args is not PsychInterpretUiMessageEvent message)
            return;

        var loader = GetEntity(args.LoaderUid);
        if (!loader.IsValid())
            return;

        ent.Comp.SelectedSymptoms = message.Symptoms
            .Where(ent.Comp.SymptomOptions.Contains)
            .Distinct()
            .Take(5)
            .ToList();

        RefreshLastScan(ent, loader);
        BuildInterpretation(ent);
        UpdateUiState(ent, loader);
    }

    private void OnAfterInteract(Entity<PsychInterpretCartridgeComponent> ent, ref CartridgeAfterInteractEvent args)
    {
        if (args.InteractEvent.Handled
            || !args.InteractEvent.CanReach
            || args.InteractEvent.Target is not { } target)
        {
            return;
        }

        if (ent.Comp.LastScan == null)
        {
            _popup.PopupCursor(Loc.GetString("psych-interpret-popup-no-scan"), args.InteractEvent.User, PopupType.SmallCaution);
            return;
        }

        if (_psychotherapy.TryStartSession(
                ent,
                args.Loader,
                args.InteractEvent.User,
                target,
                ent.Comp.ProtocolHint,
                ent.Comp.Confidence))
        {
            args.InteractEvent.Handled = true;
        }
    }

    private void RefreshLastScan(Entity<PsychInterpretCartridgeComponent> ent, EntityUid loader)
    {
        ent.Comp.LastScan = null;

        if (!_cartridgeLoader.TryGetProgram<BrainWaveScannerCartridgeComponent>(loader, out _, out var scanner))
            return;

        if (scanner.Scans.Count <= 0)
            return;

        ent.Comp.LastScan = scanner.Scans[^1];
    }

    private void BuildInterpretation(Entity<PsychInterpretCartridgeComponent> ent)
    {
        if (ent.Comp.LastScan is not { } scan)
        {
            ent.Comp.PatternCode = "P-NODATA";
            ent.Comp.Confidence = 0f;
            ent.Comp.ProtocolHint = "Protocol-OBS0";
            ent.Comp.Notes = Loc.GetString("psych-interpret-notes-no-scan");
            return;
        }

        var signalScore =
            (scan.ThetaDrift * 0.19f
             + scan.GammaSpikes * 0.22f
             + scan.CoherenceDrop * 0.27f
             + scan.NoiseIndex * 0.19f
             + scan.StressConductivity * 0.13f) / 100f;

        var symptomWeight = ent.Comp.SelectedSymptoms.Count * 0.06f;
        if (ent.Comp.SelectedSymptoms.Contains("visual_phantoms") && ent.Comp.SelectedSymptoms.Contains("auditory_whispers"))
            symptomWeight += 0.08f;
        if (ent.Comp.SelectedSymptoms.Contains("thought_fragmentation"))
            symptomWeight += 0.05f;

        var score = Math.Clamp(signalScore + symptomWeight, 0f, 1f);

        if (score >= 0.75f)
        {
            ent.Comp.PatternCode = "P-BRK4";
            ent.Comp.ProtocolHint = "Protocol-BRK4";
            ent.Comp.Notes = Loc.GetString("psych-interpret-notes-break");
            ent.Comp.Confidence = CalcConfidence(score, 0.80f, ent.Comp.SelectedSymptoms.Count);
            return;
        }

        if (score >= 0.45f)
        {
            ent.Comp.PatternCode = "P-ACT2";
            ent.Comp.ProtocolHint = "Protocol-ACT2";
            ent.Comp.Notes = Loc.GetString("psych-interpret-notes-active");
            ent.Comp.Confidence = CalcConfidence(score, 0.58f, ent.Comp.SelectedSymptoms.Count);
            return;
        }

        if (score >= 0.20f)
        {
            ent.Comp.PatternCode = "P-PR1";
            ent.Comp.ProtocolHint = "Protocol-PR1";
            ent.Comp.Notes = Loc.GetString("psych-interpret-notes-prodromal");
            ent.Comp.Confidence = CalcConfidence(score, 0.32f, ent.Comp.SelectedSymptoms.Count);
            return;
        }

        ent.Comp.PatternCode = "P-RM0";
        ent.Comp.ProtocolHint = "Protocol-RM0";
        ent.Comp.Notes = Loc.GetString("psych-interpret-notes-remission");
        ent.Comp.Confidence = CalcConfidence(score, 0.15f, ent.Comp.SelectedSymptoms.Count);
    }

    private static float CalcConfidence(float score, float center, int selectedSymptoms)
    {
        var spread = MathF.Abs(score - center);
        return Math.Clamp(0.32f + spread * 1.4f + selectedSymptoms * 0.06f, 0.05f, 0.98f);
    }

    private void UpdateUiState(Entity<PsychInterpretCartridgeComponent> ent, EntityUid loader)
    {
        var state = new PsychInterpretUiState(
            ent.Comp.PatternCode,
            ent.Comp.Confidence,
            ent.Comp.ProtocolHint,
            ent.Comp.Notes,
            ent.Comp.LastScan,
            new List<string>(ent.Comp.SelectedSymptoms),
            new List<string>(ent.Comp.SymptomOptions));

        _cartridgeLoader.UpdateCartridgeUiState(loader, state);
    }
}
