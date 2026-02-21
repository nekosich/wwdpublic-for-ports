using System;
using Content.Server.StationEvents.Events;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.StationEvents.Components;

[RegisterComponent, Access(typeof(MassHallucinationsRule))]
public sealed partial class MassHallucinationsRuleComponent : Component
{
    [DataField("startSeverityBoost")]
    public float StartSeverityBoost = 10f;

    [DataField("pulseSeverityBoost")]
    public float PulseSeverityBoost = 1.5f;

    [DataField("pulseInterval")]
    public float PulseInterval = 10f;

    [DataField("clearSuppressionOnStart")]
    public bool ClearSuppressionOnStart = true;

    [DataField("eventWaveFlag")]
    public bool EventWaveFlag = true;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextPulse;
}
