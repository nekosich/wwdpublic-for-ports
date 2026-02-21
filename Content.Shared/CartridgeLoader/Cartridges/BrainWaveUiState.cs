using System;
using System.Collections.Generic;
using Robust.Shared.Serialization;

namespace Content.Shared.CartridgeLoader.Cartridges;

[Serializable, NetSerializable]
public sealed class BrainWaveUiState : BoundUserInterfaceState
{
    public readonly List<BrainWaveScanRecord> Scans;

    public readonly float ThetaDrift;
    public readonly float GammaSpikes;
    public readonly float CoherenceDrop;
    public readonly float NoiseIndex;
    public readonly float StressConductivity;

    public BrainWaveUiState(
        List<BrainWaveScanRecord> scans,
        float thetaDrift,
        float gammaSpikes,
        float coherenceDrop,
        float noiseIndex,
        float stressConductivity)
    {
        Scans = scans;
        ThetaDrift = thetaDrift;
        GammaSpikes = gammaSpikes;
        CoherenceDrop = coherenceDrop;
        NoiseIndex = noiseIndex;
        StressConductivity = stressConductivity;
    }
}

[Serializable, NetSerializable]
public sealed partial class BrainWaveScanRecord
{
    public readonly NetEntity Subject;
    public readonly string SubjectName;
    public readonly float ThetaDrift;
    public readonly float GammaSpikes;
    public readonly float CoherenceDrop;
    public readonly float NoiseIndex;
    public readonly float StressConductivity;
    public readonly TimeSpan Timestamp;

    public BrainWaveScanRecord(
        NetEntity subject,
        string subjectName,
        float thetaDrift,
        float gammaSpikes,
        float coherenceDrop,
        float noiseIndex,
        float stressConductivity,
        TimeSpan timestamp)
    {
        Subject = subject;
        SubjectName = subjectName;
        ThetaDrift = thetaDrift;
        GammaSpikes = gammaSpikes;
        CoherenceDrop = coherenceDrop;
        NoiseIndex = noiseIndex;
        StressConductivity = stressConductivity;
        Timestamp = timestamp;
    }
}
