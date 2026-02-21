using System;
using Content.Server.Psychiatry;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.Psychiatry;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server.EntityEffects.Effects.Psychiatry;

[UsedImplicitly]
public sealed partial class AdjustPsychosisSeverityEffect : EntityEffect
{
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;

    [DataField]
    public float Amount = -1f;

    [DataField]
    public bool ScaleByQuantity = true;

    public override void Effect(EntityEffectBaseArgs args)
    {
        var amount = Amount;
        if (ScaleByQuantity && args is EntityEffectReagentArgs reagentArgs)
            amount *= reagentArgs.Scale.Float();

        if (Math.Abs(amount) <= 0.001f)
            return;

        var psychosisSystem = args.EntityManager.System<SchizophreniaSystem>();
        var flags = amount > 0f
            ? SchizophreniaAcquiredFlags.Chemistry
            : SchizophreniaAcquiredFlags.None;

        psychosisSystem.AdjustSeverity(args.TargetEntity, amount, flags, amount > 0f);
    }
}
