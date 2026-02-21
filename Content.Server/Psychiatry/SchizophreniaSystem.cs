using System;
using Content.Server.Body.Components;
using Content.Server.Mood;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mood;
using Content.Shared.Psychiatry;
using Content.Shared.Rejuvenate;
using Content.Server.Psychiatry.Components;
using Robust.Shared.Timing;

namespace Content.Server.Psychiatry;

public sealed class SchizophreniaSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;

    private TimeSpan _nextUpdate;
    private readonly Dictionary<EntityUid, float> _acquisitionProgress = new();
    private const bool AllowNonTraitAcquisition = false;

    private const float MinSeverity = 0f;
    private const float MaxSeverity = 100f;
    private const float AcquisitionSeverity = 20f;
    private const float StressGrowthPerSecond = 0.08f;
    private const float ChemistryGrowthPerUnit = 0.06f;
    private const float PassiveRecoveryPerSecond = 0.06f;
    private const float StableRecoveryBonus = 0.03f;

    private const float ChemistryAcquisitionThreshold = 6f;
    private const float MaxChemistryGrowthLoad = 8f;
    private const float MaxChemistryAcquisitionLoad = 12f;

    private const float AcquisitionProgressThreshold = 14f;
    private const float AcquisitionDecayPerSecond = 0.12f;
    private const float StressAcquisitionPerSecond = 0.22f;
    private const float ChemistryAcquisitionPerSecond = 0.12f;
    private const float MixedTriggerBonusPerSecond = 0.30f;

    private static readonly string[] PsychosisTriggerReagents =
    {
        "MindbreakerToxin",
        "SpaceDrugs",
        "THC",
        "Psicodine"
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SchizophreniaComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SchizophreniaComponent, RejuvenateEvent>(OnRejuvenate);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);

        TickExistingCases();

        if (AllowNonTraitAcquisition)
            TryAcquireFromRoundTriggers();
    }

    private void OnStartup(EntityUid uid, SchizophreniaComponent component, ComponentStartup args)
    {
        EnsureComp<SchizophreniaMemoryComponent>(uid);

        component.Severity = Math.Clamp(component.Severity, MinSeverity, MaxSeverity);
        component.Stage = SchizophreniaRules.SeverityToStage(component.Severity);
        component.Remission = component.Stage == SchizophreniaStage.Remission;
        Dirty(uid, component);
    }

    private void OnRejuvenate(EntityUid uid, SchizophreniaComponent component, RejuvenateEvent args)
    {
        RemComp<SchizophreniaMemoryComponent>(uid);

        component.Severity = MinSeverity;
        component.Remission = true;
        component.Stage = SchizophreniaStage.Remission;
        component.AcquiredFlags = SchizophreniaAcquiredFlags.None;
        component.SuppressionUntil = TimeSpan.Zero;
        component.LastIncident = TimeSpan.Zero;
        Dirty(uid, component);
        RaiseLocalEvent(uid, new MoodRemoveEffectEvent("PsychoticDestabilization"));
    }

    private void TickExistingCases()
    {
        var query = EntityQueryEnumerator<SchizophreniaComponent>();
        while (query.MoveNext(out var uid, out var psychosis))
        {
            if (TryComp<MobStateComponent>(uid, out var mobState) && mobState.CurrentState == MobState.Dead)
                continue;

            TickSingleEntity(uid, psychosis);
        }
    }

    private void TickSingleEntity(EntityUid uid, SchizophreniaComponent psychosis)
    {
        var growth = 0f;
        var flags = SchizophreniaAcquiredFlags.None;

        var moodStress = 0f;
        var stableMood = false;

        if (TryComp<MoodComponent>(uid, out var mood))
        {
            moodStress = GetMoodStressLevel(mood.CurrentMoodThreshold);
            stableMood = mood.CurrentMoodThreshold >= MoodThreshold.Good;

            if (moodStress > 0f)
            {
                growth += StressGrowthPerSecond * moodStress;
                flags |= SchizophreniaAcquiredFlags.MoodStress;
            }
        }

        var chemistryLoad = GetPsychosisChemLoad(uid);
        if (chemistryLoad > 0f)
        {
            growth += MathF.Min(chemistryLoad, MaxChemistryGrowthLoad) * ChemistryGrowthPerUnit;
            flags |= SchizophreniaAcquiredFlags.Chemistry;
        }

        if (_timing.CurTime < psychosis.SuppressionUntil && growth > 0f)
            growth *= 0.45f;

        if (growth > 0f && psychosis.Severity >= 75f)
            growth *= 0.60f;
        else if (growth > 0f && psychosis.Severity >= 55f)
            growth *= 0.80f;

        if (growth <= 0f)
        {
            growth -= PassiveRecoveryPerSecond;
            if (stableMood && chemistryLoad <= 0.05f)
                growth -= StableRecoveryBonus;
        }

        AdjustSeverity(uid, growth, flags, growth > 0f, psychosis);
        UpdateMoodHint(uid, psychosis);
    }

    private void TryAcquireFromRoundTriggers()
    {
        var query = EntityQueryEnumerator<MindContainerComponent, MoodComponent>();
        while (query.MoveNext(out var uid, out _, out var mood))
        {
            if (HasComp<SchizophreniaComponent>(uid))
            {
                _acquisitionProgress.Remove(uid);
                continue;
            }

            if (!Exists(uid) || (TryComp<MobStateComponent>(uid, out var mobState) && mobState.CurrentState == MobState.Dead))
            {
                _acquisitionProgress.Remove(uid);
                continue;
            }

            var moodStress = GetAcquisitionMoodStressLevel(mood.CurrentMoodThreshold);
            var chemistryLoad = GetPsychosisChemLoad(uid);

            var progressDelta = 0f;

            if (moodStress > 0f)
                progressDelta += moodStress * StressAcquisitionPerSecond;

            if (chemistryLoad > 0.01f)
                progressDelta += MathF.Min(chemistryLoad, MaxChemistryAcquisitionLoad) * ChemistryAcquisitionPerSecond;

            if (moodStress > 0f && chemistryLoad >= 1f)
                progressDelta += MixedTriggerBonusPerSecond;

            _acquisitionProgress.TryGetValue(uid, out var progress);
            progress = progressDelta <= 0f
                ? MathF.Max(0f, progress - AcquisitionDecayPerSecond)
                : progress + progressDelta;

            if (progress < AcquisitionProgressThreshold)
            {
                if (progress <= 0.01f)
                    _acquisitionProgress.Remove(uid);
                else
                    _acquisitionProgress[uid] = progress;

                continue;
            }

            _acquisitionProgress.Remove(uid);

            var component = EnsureComp<SchizophreniaComponent>(uid);
            component.Severity = Math.Clamp(
                AcquisitionSeverity + moodStress * 5f + MathF.Min(chemistryLoad, MaxChemistryAcquisitionLoad) * 0.6f,
                20f,
                45f);
            component.Stage = SchizophreniaRules.SeverityToStage(component.Severity);
            component.Remission = component.Stage == SchizophreniaStage.Remission;
            component.LastIncident = _timing.CurTime;
            component.AcquiredFlags = SchizophreniaAcquiredFlags.None;

            if (moodStress > 0f)
                component.AcquiredFlags |= SchizophreniaAcquiredFlags.MoodStress;

            if (chemistryLoad > 0f)
                component.AcquiredFlags |= SchizophreniaAcquiredFlags.Chemistry;

            Dirty(uid, component);
            UpdateMoodHint(uid, component);
        }
    }

    private void UpdateMoodHint(EntityUid uid, SchizophreniaComponent psychosis)
    {
        if (psychosis.Stage >= SchizophreniaStage.Active)
            RaiseLocalEvent(uid, new MoodEffectEvent("PsychoticDestabilization"));
        else
            RaiseLocalEvent(uid, new MoodRemoveEffectEvent("PsychoticDestabilization"));
    }

    public void SetSeverity(EntityUid uid, float severity, SchizophreniaAcquiredFlags flags = SchizophreniaAcquiredFlags.None, SchizophreniaComponent? psychosis = null)
    {
        if (!Resolve(uid, ref psychosis, false))
            psychosis = EnsureComp<SchizophreniaComponent>(uid);

        psychosis.Severity = Math.Clamp(severity, MinSeverity, MaxSeverity);

        if (flags != SchizophreniaAcquiredFlags.None)
            psychosis.AcquiredFlags |= flags;

        psychosis.Stage = SchizophreniaRules.SeverityToStage(psychosis.Severity);
        psychosis.Remission = psychosis.Stage == SchizophreniaStage.Remission;
        Dirty(uid, psychosis);
    }

    public void AdjustSeverity(EntityUid uid,
        float amount,
        SchizophreniaAcquiredFlags flags = SchizophreniaAcquiredFlags.None,
        bool incident = false,
        SchizophreniaComponent? psychosis = null)
    {
        if (MathF.Abs(amount) <= 0.001f)
            return;

        if (!Resolve(uid, ref psychosis, false))
        {
            if (!AllowNonTraitAcquisition)
                return;

            if (amount <= 0f)
                return;

            psychosis = EnsureComp<SchizophreniaComponent>(uid);
            psychosis.Severity = AcquisitionSeverity;
        }

        var oldSeverity = psychosis.Severity;
        psychosis.Severity = Math.Clamp(oldSeverity + amount, MinSeverity, MaxSeverity);

        if (flags != SchizophreniaAcquiredFlags.None)
            psychosis.AcquiredFlags |= flags;

        if (incident && psychosis.Severity > oldSeverity)
            psychosis.LastIncident = _timing.CurTime;

        psychosis.Stage = SchizophreniaRules.SeverityToStage(psychosis.Severity);
        psychosis.Remission = psychosis.Stage == SchizophreniaStage.Remission;
        Dirty(uid, psychosis);
    }

    public void SetSuppression(EntityUid uid, TimeSpan duration, SchizophreniaComponent? psychosis = null)
    {
        if (!Resolve(uid, ref psychosis, false))
            return;

        var until = _timing.CurTime + duration;
        if (until <= psychosis.SuppressionUntil)
            return;

        psychosis.SuppressionUntil = until;
        Dirty(uid, psychosis);
    }

    public void ClearSuppression(EntityUid uid, SchizophreniaComponent? psychosis = null)
    {
        if (!Resolve(uid, ref psychosis, false))
            return;

        if (psychosis.SuppressionUntil == TimeSpan.Zero)
            return;

        psychosis.SuppressionUntil = TimeSpan.Zero;
        Dirty(uid, psychosis);
    }

    public string GetRecommendedProtocol(EntityUid uid, SchizophreniaComponent? psychosis = null)
    {
        if (!Resolve(uid, ref psychosis, false))
            return SchizophreniaRules.StageToProtocol(SchizophreniaStage.Remission);

        return SchizophreniaRules.StageToProtocol(psychosis.Stage);
    }

    public void GetBrainWaveMarkers(EntityUid uid,
        out float thetaDrift,
        out float gammaSpikes,
        out float coherenceDrop,
        out float noiseIndex,
        out float stressConductivity,
        SchizophreniaComponent? psychosis = null,
        MoodComponent? mood = null)
    {
        var severity = 0f;
        var stageAmp = 0f;

        if (Resolve(uid, ref psychosis, false))
        {
            severity = psychosis.Severity;
            stageAmp = psychosis.Stage switch
            {
                SchizophreniaStage.Break => 1.0f,
                SchizophreniaStage.Active => 0.7f,
                SchizophreniaStage.Prodromal => 0.35f,
                _ => 0.1f
            };
        }

        var stressLevel = 0f;
        if (Resolve(uid, ref mood, false))
            stressLevel = GetMoodStressLevel(mood.CurrentMoodThreshold);

        thetaDrift = Math.Clamp(10f + severity * 0.52f + stageAmp * 14f, 0f, 100f);
        gammaSpikes = Math.Clamp(8f + severity * 0.60f + stageAmp * 18f, 0f, 100f);
        coherenceDrop = Math.Clamp(6f + severity * 0.55f + stageAmp * 22f, 0f, 100f);
        noiseIndex = Math.Clamp(12f + severity * 0.68f + stageAmp * 16f, 0f, 100f);
        stressConductivity = Math.Clamp(18f + stressLevel * 12f + severity * 0.28f, 0f, 100f);
    }

    private float GetPsychosisChemLoad(EntityUid uid)
    {
        if (!TryComp<BloodstreamComponent>(uid, out var bloodstream))
            return 0f;

        if (!_solution.ResolveSolution(uid, bloodstream.ChemicalSolutionName, ref bloodstream.ChemicalSolution, out var solution))
            return 0f;

        var total = 0f;
        foreach (var reagent in PsychosisTriggerReagents)
        {
            if (!solution.TryGetReagentQuantity(new ReagentId(reagent, null), out var quantity))
                continue;

            total += quantity.Float();
        }

        return total;
    }

    private static float GetMoodStressLevel(MoodThreshold threshold)
    {
        if (threshold > MoodThreshold.Bad)
            return 0f;

        if (threshold == MoodThreshold.Dead)
            return 5f;

        return Math.Max(1f, (int) MoodThreshold.Bad - (int) threshold + 1f);
    }

    private static float GetAcquisitionMoodStressLevel(MoodThreshold threshold)
    {
        return threshold switch
        {
            MoodThreshold.Terrible => 0.65f,
            MoodThreshold.Horrible => 1.10f,
            MoodThreshold.Dead => 1.35f,
            _ => 0f
        };
    }

}
