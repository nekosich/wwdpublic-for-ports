using System;
using System.Collections.Generic;
using Content.Shared.Preferences;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Psychiatry;

[Serializable, NetSerializable]
public enum PsychosisIncidentType : byte
{
    Whisper,
    Directive,
    Accusation,
    MurderEcho,
    PseudoAttack,
    ScreenStinger
}

[Serializable, NetSerializable]
public enum PsychosisSeverityBand : byte
{
    Low,
    Medium,
    High
}

[Serializable, NetSerializable]
public sealed partial class PsychosisSnapshotItem
{
    public readonly string Slot;
    public readonly string PrototypeId;

    public PsychosisSnapshotItem(string slot, string prototypeId)
    {
        Slot = slot;
        PrototypeId = prototypeId;
    }
}

[Serializable, NetSerializable]
public sealed partial class PsychosisVictimSnapshot
{
    public readonly string VictimName;
    public readonly string ObserverPrototype;
    public readonly HumanoidCharacterProfile? Profile;
    public readonly List<PsychosisSnapshotItem> SnapshotSlots;

    public PsychosisVictimSnapshot(
        string victimName,
        string observerPrototype,
        HumanoidCharacterProfile? profile,
        List<PsychosisSnapshotItem> snapshotSlots)
    {
        VictimName = victimName;
        ObserverPrototype = observerPrototype;
        Profile = profile;
        SnapshotSlots = snapshotSlots;
    }
}

[Serializable, NetSerializable]
public sealed partial class PsychosisIncidentContext
{
    public readonly string? SpeciesId;
    public readonly string? JobId;
    public readonly bool IsAntag;
    public readonly int KillCount;
    public readonly bool HasVictimData;
    public readonly SchizophreniaStage Stage;
    public readonly PsychosisSeverityBand SeverityBand;

    public PsychosisIncidentContext(
        string? speciesId,
        string? jobId,
        bool isAntag,
        int killCount,
        bool hasVictimData,
        SchizophreniaStage stage,
        PsychosisSeverityBand severityBand)
    {
        SpeciesId = speciesId;
        JobId = jobId;
        IsAntag = isAntag;
        KillCount = killCount;
        HasVictimData = hasVictimData;
        Stage = stage;
        SeverityBand = severityBand;
    }
}

[Serializable, NetSerializable]
public sealed partial class PsychosisIncidentEvent : EntityEventArgs
{
    public readonly NetEntity Patient;
    public readonly PsychosisIncidentType Type;
    public readonly string? LineId;
    public readonly string? PhantomPrototype;
    public readonly PsychosisIncidentContext Context;
    public readonly PsychosisVictimSnapshot? Victim;
    public readonly int Seed;

    public PsychosisIncidentEvent(
        NetEntity patient,
        PsychosisIncidentType type,
        string? lineId,
        string? phantomPrototype,
        PsychosisIncidentContext context,
        PsychosisVictimSnapshot? victim,
        int seed)
    {
        Patient = patient;
        Type = type;
        LineId = lineId;
        PhantomPrototype = phantomPrototype;
        Context = context;
        Victim = victim;
        Seed = seed;
    }
}
