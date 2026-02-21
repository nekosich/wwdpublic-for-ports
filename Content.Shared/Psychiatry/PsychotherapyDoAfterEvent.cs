using System;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Psychiatry;

[Serializable, NetSerializable]
public sealed partial class PsychotherapyDoAfterEvent : DoAfterEvent
{
    [DataField]
    public string Protocol = string.Empty;

    [DataField]
    public float Confidence;

    private PsychotherapyDoAfterEvent()
    {
    }

    public PsychotherapyDoAfterEvent(string protocol, float confidence)
    {
        Protocol = protocol;
        Confidence = confidence;
    }

    public override DoAfterEvent Clone() => this;
}
