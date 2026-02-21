using System;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Psychiatry;
using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Psychiatry;

namespace Content.Server.StationEvents.Events;

public sealed class MassHallucinationsRule : StationEventSystem<MassHallucinationsRuleComponent>
{
    [Dependency] private readonly SchizophreniaSystem _schizophrenia = default!;

    protected override void Started(EntityUid uid, MassHallucinationsRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        component.NextPulse = Timing.CurTime + TimeSpan.FromSeconds(Math.Max(1f, component.PulseInterval));
        var flags = component.EventWaveFlag
            ? SchizophreniaAcquiredFlags.EventWave
            : SchizophreniaAcquiredFlags.None;

        var query = EntityQueryEnumerator<MindContainerComponent>();
        while (query.MoveNext(out var ent, out _))
        {
            EnsureComp<MassHallucinationsComponent>(ent);

            if (component.ClearSuppressionOnStart)
                _schizophrenia.ClearSuppression(ent);

            _schizophrenia.AdjustSeverity(ent, component.StartSeverityBoost, flags, incident: true);
        }
    }

    protected override void Ended(EntityUid uid, MassHallucinationsRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        var query = EntityQueryEnumerator<MassHallucinationsComponent>();
        while (query.MoveNext(out var ent, out _))
        {
            RemComp<MassHallucinationsComponent>(ent);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var eventQuery = EntityQueryEnumerator<MassHallucinationsRuleComponent, GameRuleComponent>();
        while (eventQuery.MoveNext(out var eventUid, out var component, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(eventUid, gameRule) || Timing.CurTime < component.NextPulse)
                continue;

            component.NextPulse = Timing.CurTime + TimeSpan.FromSeconds(Math.Max(1f, component.PulseInterval));
            PulseSeverity(component);
        }
    }

    private void PulseSeverity(MassHallucinationsRuleComponent component)
    {
        var flags = component.EventWaveFlag
            ? SchizophreniaAcquiredFlags.EventWave
            : SchizophreniaAcquiredFlags.None;

        var query = EntityQueryEnumerator<MassHallucinationsComponent>();
        while (query.MoveNext(out var ent, out _))
        {
            _schizophrenia.AdjustSeverity(ent, component.PulseSeverityBoost, flags, incident: true);
        }
    }
}
