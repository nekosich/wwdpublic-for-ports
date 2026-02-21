using System;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Psychiatry;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SchizophreniaComponent : Component
{
    [DataField("severity")]
    [AutoNetworkedField]
    public float Severity = 45f;

    [DataField("stage")]
    [AutoNetworkedField]
    public SchizophreniaStage Stage = SchizophreniaStage.Active;

    [DataField("remission")]
    [AutoNetworkedField]
    public bool Remission;

    [DataField("lastIncident", customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField]
    public TimeSpan LastIncident;

    [DataField("suppressionUntil", customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField]
    public TimeSpan SuppressionUntil;

    [DataField("acquiredFlags")]
    [AutoNetworkedField]
    public SchizophreniaAcquiredFlags AcquiredFlags = SchizophreniaAcquiredFlags.None;
}
