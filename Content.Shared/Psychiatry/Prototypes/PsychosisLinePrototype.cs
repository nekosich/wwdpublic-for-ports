using System.Collections.Generic;
using Content.Shared.Psychiatry;
using Robust.Shared.Prototypes;

namespace Content.Shared.Psychiatry.Prototypes;

[Prototype("psychosisLine")]
public sealed partial class PsychosisLinePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("incidentType", required: true)]
    public PsychosisIncidentType IncidentType;

    [DataField("locKey", required: true)]
    public string LocKey = default!;

    [DataField("tags")]
    public HashSet<string> Tags = new();

    [DataField("weight")]
    public float Weight = 1f;
}
