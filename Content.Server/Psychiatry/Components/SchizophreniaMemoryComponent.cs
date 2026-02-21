using System;
using System.Collections.Generic;
using Content.Shared.Psychiatry;

namespace Content.Server.Psychiatry.Components;

[RegisterComponent]
public sealed partial class SchizophreniaMemoryComponent : Component
{
    [DataField("victimMemoryLimit")]
    public int VictimMemoryLimit = 6;

    [DataField("murderEchoCooldown")]
    public TimeSpan MurderEchoCooldown = TimeSpan.FromSeconds(35);

    [DataField("nextIncidentAt")]
    public TimeSpan NextIncidentAt;

    [DataField("lastIncidentByType")]
    public Dictionary<PsychosisIncidentType, TimeSpan> LastIncidentByType = new();

    [DataField("victimSnapshots")]
    public List<PsychosisVictimSnapshot> VictimSnapshots = new();
}
