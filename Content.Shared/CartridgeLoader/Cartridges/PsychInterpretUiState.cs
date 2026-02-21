using System;
using System.Collections.Generic;
using Robust.Shared.Serialization;

namespace Content.Shared.CartridgeLoader.Cartridges;

[Serializable, NetSerializable]
public sealed class PsychInterpretUiState : BoundUserInterfaceState
{
    public readonly string PatternCode;
    public readonly float Confidence;
    public readonly string ProtocolHint;
    public readonly string Notes;
    public readonly BrainWaveScanRecord? LastScan;
    public readonly List<string> SelectedSymptoms;
    public readonly List<string> SymptomOptions;

    public PsychInterpretUiState(
        string patternCode,
        float confidence,
        string protocolHint,
        string notes,
        BrainWaveScanRecord? lastScan,
        List<string> selectedSymptoms,
        List<string> symptomOptions)
    {
        PatternCode = patternCode;
        Confidence = confidence;
        ProtocolHint = protocolHint;
        Notes = notes;
        LastScan = lastScan;
        SelectedSymptoms = selectedSymptoms;
        SymptomOptions = symptomOptions;
    }
}

[Serializable, NetSerializable]
public sealed class PsychInterpretUiMessageEvent : CartridgeMessageEvent
{
    public readonly List<string> Symptoms;

    public PsychInterpretUiMessageEvent(List<string> symptoms)
    {
        Symptoms = symptoms;
    }
}
