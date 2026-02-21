using System;
using Robust.Shared.Serialization;

namespace Content.Shared.Psychiatry;

[Serializable, NetSerializable]
public enum SchizophreniaStage : byte
{
    Remission = 0,
    Prodromal = 1,
    Active = 2,
    Break = 3
}

[Flags]
[Serializable, NetSerializable]
public enum SchizophreniaAcquiredFlags : byte
{
    None = 0,
    TraitSpawn = 1 << 0,
    MoodStress = 1 << 1,
    Chemistry = 1 << 2,
    EventWave = 1 << 3
}
