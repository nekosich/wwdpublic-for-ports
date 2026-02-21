using System;
using Content.Server.Psychiatry;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server.EntityEffects.Effects.Psychiatry;

[UsedImplicitly]
public sealed partial class SuppressPsychosisEpisodesEffect : EntityEffect
{
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;

    [DataField]
    public float Duration = 20f;

    [DataField]
    public bool ScaleByQuantity = true;

    [DataField]
    public float SeverityAdjustment;

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (!args.EntityManager.HasComponent<Content.Shared.Psychiatry.SchizophreniaComponent>(args.TargetEntity))
            return;

        var suppressionDuration = Duration;
        var severityDelta = SeverityAdjustment;

        if (ScaleByQuantity && args is EntityEffectReagentArgs reagentArgs)
        {
            suppressionDuration *= reagentArgs.Scale.Float();
            severityDelta *= reagentArgs.Scale.Float();
        }

        var psychosis = args.EntityManager.System<SchizophreniaSystem>();
        psychosis.SetSuppression(args.TargetEntity, TimeSpan.FromSeconds(Math.Max(0.5f, suppressionDuration)));

        if (Math.Abs(severityDelta) > 0.001f)
            psychosis.AdjustSeverity(args.TargetEntity, severityDelta);
    }
}
