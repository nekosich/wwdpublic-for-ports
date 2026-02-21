using System;

namespace Content.Shared.Psychiatry;

public static class SchizophreniaRules
{
    public static SchizophreniaStage SeverityToStage(float severity)
    {
        return severity switch
        {
            < 20f => SchizophreniaStage.Remission,
            < 45f => SchizophreniaStage.Prodromal,
            < 75f => SchizophreniaStage.Active,
            _ => SchizophreniaStage.Break
        };
    }

    public static string StageToProtocol(SchizophreniaStage stage)
    {
        return stage switch
        {
            SchizophreniaStage.Break => "Protocol-BRK4",
            SchizophreniaStage.Active => "Protocol-ACT2",
            SchizophreniaStage.Prodromal => "Protocol-PR1",
            _ => "Protocol-RM0"
        };
    }
}
