using Content.Shared.CartridgeLoader.Cartridges;
using System.Collections.Generic;
using Robust.Shared.Audio;

namespace Content.Server.CartridgeLoader.Cartridges;

[RegisterComponent, Access(typeof(BrainWaveScannerCartridgeSystem))]
public sealed partial class BrainWaveScannerCartridgeComponent : Component
{
    [DataField]
    public int MaxSavedScans = 8;

    [DataField]
    public SoundSpecifier ScanSound = new SoundCollectionSpecifier("PsychosisScanner");

    [DataField]
    public List<BrainWaveScanRecord> Scans = new();
}
