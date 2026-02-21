using System.Collections.Generic;
using Content.Shared.CartridgeLoader.Cartridges;

namespace Content.Server.CartridgeLoader.Cartridges;

[RegisterComponent, Access(typeof(PsychInterpretCartridgeSystem), typeof(Content.Server.Psychiatry.PsychotherapySystem))]
public sealed partial class PsychInterpretCartridgeComponent : Component
{
    [DataField]
    public List<string> SymptomOptions = new()
    {
        "auditory_whispers",
        "visual_phantoms",
        "derealization",
        "paranoid_dread",
        "thought_fragmentation"
    };

    [DataField]
    public List<string> SelectedSymptoms = new();

    [DataField]
    public string PatternCode = "P-RM0";

    [DataField]
    public float Confidence = 0f;

    [DataField]
    public string ProtocolHint = "Protocol-RM0";

    [DataField]
    public string Notes = string.Empty;

    [DataField]
    public BrainWaveScanRecord? LastScan;
}
