using System;
using System.Collections.Generic;
using Content.Server.CartridgeLoader.Cartridges;
using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared.Mood;
using Content.Shared.Psychiatry;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Psychiatry;

public sealed class PsychotherapySystem : EntitySystem
{
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SchizophreniaSystem _schizophrenia = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _patientCooldowns = new();

    private static readonly TimeSpan BaseSessionTime = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan PsychBedSessionTime = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PatientCooldownTime = TimeSpan.FromSeconds(120);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PsychInterpretCartridgeComponent, PsychotherapyDoAfterEvent>(OnPsychotherapyDoAfter);
    }

    public bool TryStartSession(
        Entity<PsychInterpretCartridgeComponent> program,
        EntityUid loaderUid,
        EntityUid therapist,
        EntityUid patient,
        string protocol,
        float confidence)
    {
        if (string.IsNullOrWhiteSpace(protocol))
        {
            _popup.PopupEntity(Loc.GetString("psychotherapy-popup-no-protocol"), program, therapist);
            return false;
        }

        if (!HasComp<SchizophreniaComponent>(patient))
        {
            _popup.PopupEntity(Loc.GetString("psychotherapy-popup-no-patient-data"), patient, therapist);
            return false;
        }

        if (IsOnCooldown(patient))
        {
            _popup.PopupEntity(Loc.GetString("psychotherapy-popup-cooldown"), patient, therapist);
            return false;
        }

        var duration = IsOnPsychBed(patient)
            ? PsychBedSessionTime
            : BaseSessionTime;

        var doAfter = new DoAfterArgs(
            EntityManager,
            therapist,
            duration,
            new PsychotherapyDoAfterEvent(protocol, confidence),
            program,
            target: patient,
            used: loaderUid)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
            BlockDuplicate = true
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupEntity(Loc.GetString("psychotherapy-popup-started"), patient, therapist);
        return true;
    }

    private void OnPsychotherapyDoAfter(Entity<PsychInterpretCartridgeComponent> ent, ref PsychotherapyDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } patient)
            return;

        if (!TryComp<SchizophreniaComponent>(patient, out var psychosis))
        {
            _popup.PopupEntity(Loc.GetString("psychotherapy-popup-no-patient-data"), patient, args.User);
            args.Handled = true;
            return;
        }

        var expectedProtocol = _schizophrenia.GetRecommendedProtocol(patient, psychosis);
        var protocolMatch = string.Equals(args.Protocol, expectedProtocol, StringComparison.OrdinalIgnoreCase);
        var onPsychBed = IsOnPsychBed(patient);

        var severityDelta = 0f;
        if (protocolMatch)
        {
            var baseReduction = onPsychBed ? -15f : -11f;
            var confidenceScale = Math.Clamp(0.8f + args.Confidence * 0.7f, 0.8f, 1.45f);
            severityDelta = baseReduction * confidenceScale;

            _schizophrenia.SetSuppression(
                patient,
                onPsychBed ? TimeSpan.FromSeconds(95) : TimeSpan.FromSeconds(65),
                psychosis);
            RaiseLocalEvent(patient, new MoodEffectEvent("TherapyRelief"));

            _popup.PopupEntity(
                Loc.GetString("psychotherapy-popup-success", ("protocol", args.Protocol)),
                patient,
                args.User);
        }
        else
        {
            severityDelta = onPsychBed ? -2.0f : -1.0f;

            // Incorrect protocol can destabilize high-severity episodes.
            if (psychosis.Stage >= SchizophreniaStage.Active && _random.Prob(0.35f))
                severityDelta = 2.5f;

            RaiseLocalEvent(patient, new MoodEffectEvent("PsychoticDestabilization"));
            _popup.PopupEntity(
                Loc.GetString("psychotherapy-popup-mismatch", ("protocol", args.Protocol)),
                patient,
                args.User);
        }

        _schizophrenia.AdjustSeverity(
            patient,
            severityDelta,
            incident: severityDelta > 0f,
            psychosis: psychosis);

        _patientCooldowns[patient] = _timing.CurTime + PatientCooldownTime;
        args.Handled = true;
    }

    private bool IsOnCooldown(EntityUid patient)
    {
        if (!_patientCooldowns.TryGetValue(patient, out var until))
            return false;

        if (_timing.CurTime >= until)
        {
            _patientCooldowns.Remove(patient);
            return false;
        }

        return true;
    }

    private bool IsOnPsychBed(EntityUid patient)
    {
        if (!TryComp<BuckleComponent>(patient, out var buckle) || buckle.BuckledTo is not { } buckledTo)
            return false;

        return MetaData(buckledTo).EntityPrototype?.ID == "PsychBed";
    }
}
