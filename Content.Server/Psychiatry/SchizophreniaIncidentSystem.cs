using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.Ghost;
using Content.Server.KillTracking;
using Content.Server.Psychiatry.Components;
using Content.Server.Roles;
using Content.Server.Roles.Jobs;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Psychiatry;
using Content.Shared.Psychiatry.Prototypes;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Psychiatry;

public sealed class SchizophreniaIncidentSystem : EntitySystem
{
    [Dependency] private readonly GhostSystem _ghost = default!;
    [Dependency] private readonly JobSystem _jobs = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly RoleSystem _roles = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<PsychosisIncidentType, List<PsychosisLinePrototype>> _linesByType = new();

    private static readonly TimeSpan PseudoAttackCooldown = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan ScreenStingerCooldown = TimeSpan.FromSeconds(14);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KillReportedEvent>(OnKillReported);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        RebuildLineCache();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<SchizophreniaComponent>();
        while (query.MoveNext(out var uid, out var psychosis))
        {
            if (psychosis.Stage == SchizophreniaStage.Remission ||
                now < psychosis.SuppressionUntil ||
                (TryComp<MobStateComponent>(uid, out var mobState) && mobState.CurrentState == MobState.Dead) ||
                !_player.TryGetSessionByEntity(uid, out var session))
            {
                continue;
            }

            var memory = EnsureComp<SchizophreniaMemoryComponent>(uid);

            if (memory.NextIncidentAt == TimeSpan.Zero)
                ScheduleNextIncident(memory, psychosis.Stage, now);

            if (now < memory.NextIncidentAt)
                continue;

            TriggerIncident(uid, psychosis, memory, session.Channel, now);
        }
    }

    private void OnKillReported(ref KillReportedEvent args)
    {
        if (args.Suicide ||
            !HasComp<ActorComponent>(args.Entity) ||
            !HasComp<HumanoidAppearanceComponent>(args.Entity) ||
            args.Primary is not KillPlayerSource killer ||
            !_player.TryGetSessionById(killer.PlayerId, out var killerSession) ||
            killerSession.AttachedEntity is not { Valid: true } killerEntity ||
            !HasComp<SchizophreniaComponent>(killerEntity) ||
            !_ghost.TryBuildPsychosisVictimSnapshot(args.Entity, out var snapshot))
        {
            return;
        }

        var memory = EnsureComp<SchizophreniaMemoryComponent>(killerEntity);
        PushVictimSnapshot(memory, snapshot);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<PsychosisLinePrototype>())
            return;

        RebuildLineCache();
    }

    private void TriggerIncident(
        EntityUid uid,
        SchizophreniaComponent psychosis,
        SchizophreniaMemoryComponent memory,
        INetChannel channel,
        TimeSpan now)
    {
        var context = BuildContext(uid, psychosis, memory);
        var type = PickIncidentType(psychosis.Stage, memory, now);

        if (type == null)
        {
            memory.NextIncidentAt = now + TimeSpan.FromSeconds(_random.NextFloat(2f, 4f));
            return;
        }

        PsychosisVictimSnapshot? victim = null;
        if (type == PsychosisIncidentType.MurderEcho)
            victim = PickVictimSnapshot(memory.VictimSnapshots, _random);

        var lineId = ResolveLinePrototypeId(PickLineId(type.Value, context), type.Value, psychosis.Stage);
        var phantomPrototype = ResolvePhantomPrototype(uid, type.Value, victim);
        var ev = new PsychosisIncidentEvent(
            GetNetEntity(uid),
            type.Value,
            lineId,
            phantomPrototype,
            context,
            victim,
            _random.Next());

        RaiseNetworkEvent(ev, channel);

        memory.LastIncidentByType[type.Value] = now;
        psychosis.LastIncident = now;
        Dirty(uid, psychosis);

        ScheduleNextIncident(memory, psychosis.Stage, now);
    }

    private string ResolvePhantomPrototype(
        EntityUid uid,
        PsychosisIncidentType type,
        PsychosisVictimSnapshot? victim)
    {
        if (type == PsychosisIncidentType.MurderEcho && victim is { } victimSnapshot)
            return victimSnapshot.ObserverPrototype;

        if (_ghost.TryGetVisualObserverPrototype(uid, out var observerPrototype))
            return observerPrototype;

        return "MobObserverVisualHumanoid";
    }

    private PsychosisIncidentContext BuildContext(
        EntityUid uid,
        SchizophreniaComponent psychosis,
        SchizophreniaMemoryComponent memory)
    {
        var speciesId = TryComp<HumanoidAppearanceComponent>(uid, out var humanoid)
            ? humanoid.Species.ToString()
            : null;

        string? jobId = null;
        var antag = false;

        if (_mind.TryGetMind(uid, out var mindId, out _))
        {
            if (_jobs.MindTryGetJobId(mindId, out var jobProto))
                jobId = jobProto?.ToString();

            antag = _roles.MindIsAntagonist(mindId);
        }

        return new PsychosisIncidentContext(
            speciesId,
            jobId,
            antag,
            memory.VictimSnapshots.Count,
            memory.VictimSnapshots.Count > 0,
            psychosis.Stage,
            ToSeverityBand(psychosis.Severity));
    }

    private PsychosisIncidentType? PickIncidentType(
        SchizophreniaStage stage,
        SchizophreniaMemoryComponent memory,
        TimeSpan now)
    {
        var weighted = new List<(PsychosisIncidentType Type, float Weight)>();
        var hasVictims = memory.VictimSnapshots.Count > 0;

        foreach (var type in Enum.GetValues<PsychosisIncidentType>())
        {
            var weight = GetIncidentWeight(stage, type, hasVictims);
            if (weight <= 0f || IsIncidentOnCooldown(memory, type, now))
                continue;

            weighted.Add((type, weight));
        }

        if (weighted.Count == 0)
            return null;

        var total = 0f;
        foreach (var (_, weight) in weighted)
        {
            total += MathF.Max(0.01f, weight);
        }

        var roll = _random.NextFloat() * total;
        foreach (var (type, weight) in weighted)
        {
            roll -= MathF.Max(0.01f, weight);
            if (roll <= 0f)
                return type;
        }

        return weighted[^1].Type;
    }

    public static bool IsIncidentOnCooldown(
        SchizophreniaMemoryComponent memory,
        PsychosisIncidentType type,
        TimeSpan now)
    {
        if (!memory.LastIncidentByType.TryGetValue(type, out var last))
            return false;

        var cooldown = GetIncidentCooldown(memory, type);

        return cooldown > TimeSpan.Zero && now - last < cooldown;
    }

    public static TimeSpan GetIncidentCooldown(
        SchizophreniaMemoryComponent memory,
        PsychosisIncidentType type)
    {
        return type switch
        {
            PsychosisIncidentType.MurderEcho => memory.MurderEchoCooldown,
            PsychosisIncidentType.PseudoAttack => PseudoAttackCooldown,
            PsychosisIncidentType.ScreenStinger => ScreenStingerCooldown,
            _ => TimeSpan.Zero
        };
    }

    public static PsychosisVictimSnapshot? PickVictimSnapshot(
        IReadOnlyList<PsychosisVictimSnapshot> snapshots,
        IRobustRandom random)
    {
        if (snapshots.Count == 0)
            return null;

        return snapshots[random.Next(snapshots.Count)];
    }

    private string? PickLineId(PsychosisIncidentType type, PsychosisIncidentContext context)
    {
        if (!_linesByType.TryGetValue(type, out var lines) || lines.Count == 0)
            return null;

        var stageTag = $"stage:{context.Stage.ToString().ToLowerInvariant()}";
        var speciesTag = context.SpeciesId == null
            ? null
            : $"species:{context.SpeciesId.ToLowerInvariant()}";
        var jobIdTag = context.JobId == null
            ? null
            : $"job:{context.JobId.ToLowerInvariant()}";
        var jobGroupTag = ResolveJobContextTag(context.JobId);

        var eligible = lines
            .Where(line => LineEligible(line, context, stageTag, speciesTag, jobIdTag, jobGroupTag))
            .ToList();

        if (eligible.Count == 0)
            return null;

        if (context.KillCount > 0)
        {
            var killer = SelectTagged(eligible, "killer:true");
            if (killer.Count > 0)
                return PickWeightedLine(killer)?.ID;
        }

        if (context.IsAntag)
        {
            var antag = SelectTagged(eligible, "antag:true");
            if (antag.Count > 0)
                return PickWeightedLine(antag)?.ID;
        }

        var stage = SelectTagged(eligible, stageTag);
        if (stage.Count > 0)
            return PickWeightedLine(stage)?.ID;

        if (jobGroupTag != null)
        {
            var job = SelectTagged(eligible, jobGroupTag);
            if (job.Count > 0)
                return PickWeightedLine(job)?.ID;
        }

        if (jobIdTag != null)
        {
            var jobSpecific = SelectTagged(eligible, jobIdTag);
            if (jobSpecific.Count > 0)
                return PickWeightedLine(jobSpecific)?.ID;
        }

        if (speciesTag != null)
        {
            var species = SelectTagged(eligible, speciesTag);
            if (species.Count > 0)
                return PickWeightedLine(species)?.ID;
        }

        var generic = SelectTagged(eligible, "generic");
        if (generic.Count > 0)
            return PickWeightedLine(generic)?.ID;

        return PickWeightedLine(eligible)?.ID;
    }

    public static string GetFallbackLinePrototypeId(PsychosisIncidentType type, SchizophreniaStage stage)
    {
        var safeStage = stage == SchizophreniaStage.Remission
            ? SchizophreniaStage.Prodromal
            : stage;

        return $"PsychosisFallback{type}{safeStage}";
    }

    public static string ResolveLinePrototypeId(
        string? selectedLineId,
        PsychosisIncidentType type,
        SchizophreniaStage stage)
    {
        return selectedLineId ?? GetFallbackLinePrototypeId(type, stage);
    }

    private static List<PsychosisLinePrototype> SelectTagged(List<PsychosisLinePrototype> source, string tag)
    {
        return source.Where(line => HasTag(line, tag)).ToList();
    }

    private PsychosisLinePrototype? PickWeightedLine(List<PsychosisLinePrototype> lines)
    {
        if (lines.Count == 0)
            return null;

        var total = 0f;
        foreach (var line in lines)
        {
            total += MathF.Max(0.01f, line.Weight);
        }

        var roll = _random.NextFloat() * total;
        foreach (var line in lines)
        {
            roll -= MathF.Max(0.01f, line.Weight);
            if (roll <= 0f)
                return line;
        }

        return lines[^1];
    }

    private static bool LineEligible(
        PsychosisLinePrototype line,
        PsychosisIncidentContext context,
        string stageTag,
        string? speciesTag,
        string? jobIdTag,
        string? jobGroupTag)
    {
        if (HasTag(line, "break-only") && context.Stage != SchizophreniaStage.Break)
            return false;

        if (HasTag(line, "killer:true") && context.KillCount <= 0)
            return false;

        if (HasTag(line, "antag:true") && !context.IsAntag)
            return false;

        var hasStageTags = false;
        var stageMatch = false;
        foreach (var tag in line.Tags)
        {
            if (!tag.StartsWith("stage:", StringComparison.OrdinalIgnoreCase))
                continue;

            hasStageTags = true;
            if (string.Equals(tag, stageTag, StringComparison.OrdinalIgnoreCase))
                stageMatch = true;
        }

        if (hasStageTags && !stageMatch)
            return false;

        var hasSpeciesTags = false;
        var speciesMatch = false;
        foreach (var tag in line.Tags)
        {
            if (!tag.StartsWith("species:", StringComparison.OrdinalIgnoreCase))
                continue;

            hasSpeciesTags = true;
            if (speciesTag != null && string.Equals(tag, speciesTag, StringComparison.OrdinalIgnoreCase))
                speciesMatch = true;
        }

        if (hasSpeciesTags && !speciesMatch)
            return false;

        var hasJobTags = false;
        var jobMatch = false;
        foreach (var tag in line.Tags)
        {
            if (!tag.StartsWith("job:", StringComparison.OrdinalIgnoreCase))
                continue;

            hasJobTags = true;
            if ((jobIdTag != null && string.Equals(tag, jobIdTag, StringComparison.OrdinalIgnoreCase)) ||
                (jobGroupTag != null && string.Equals(tag, jobGroupTag, StringComparison.OrdinalIgnoreCase)))
            {
                jobMatch = true;
            }
        }

        return !hasJobTags || jobMatch;
    }

    public static bool IsLineEligible(PsychosisLinePrototype line, PsychosisIncidentContext context)
    {
        var stageTag = $"stage:{context.Stage.ToString().ToLowerInvariant()}";
        var speciesTag = context.SpeciesId == null
            ? null
            : $"species:{context.SpeciesId.ToLowerInvariant()}";
        var jobIdTag = context.JobId == null
            ? null
            : $"job:{context.JobId.ToLowerInvariant()}";
        var jobGroupTag = ResolveJobContextTag(context.JobId);

        return LineEligible(line, context, stageTag, speciesTag, jobIdTag, jobGroupTag);
    }

    private static bool HasTag(PsychosisLinePrototype line, string tag)
    {
        foreach (var lineTag in line.Tags)
        {
            if (string.Equals(lineTag, tag, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void RebuildLineCache()
    {
        _linesByType.Clear();
        foreach (var type in Enum.GetValues<PsychosisIncidentType>())
        {
            _linesByType[type] = new List<PsychosisLinePrototype>();
        }

        foreach (var line in _prototype.EnumeratePrototypes<PsychosisLinePrototype>())
        {
            if (line.Weight <= 0f)
                continue;

            _linesByType[line.IncidentType].Add(line);
        }
    }

    private void ScheduleNextIncident(
        SchizophreniaMemoryComponent memory,
        SchizophreniaStage stage,
        TimeSpan now)
    {
        var (min, max) = GetIncidentIntervalSeconds(stage);
        memory.NextIncidentAt = now + TimeSpan.FromSeconds(_random.NextFloat(min, max));
    }

    public static void PushVictimSnapshot(
        SchizophreniaMemoryComponent memory,
        PsychosisVictimSnapshot snapshot)
    {
        if (memory.VictimMemoryLimit <= 0)
            return;

        while (memory.VictimSnapshots.Count >= memory.VictimMemoryLimit)
        {
            memory.VictimSnapshots.RemoveAt(0);
        }

        memory.VictimSnapshots.Add(snapshot);
    }

    public static (float MinSeconds, float MaxSeconds) GetIncidentIntervalSeconds(SchizophreniaStage stage)
    {
        return stage switch
        {
            SchizophreniaStage.Prodromal => (12f, 20f),
            SchizophreniaStage.Active => (6f, 11f),
            SchizophreniaStage.Break => (3.5f, 7f),
            _ => (30f, 45f)
        };
    }

    public static float GetIncidentWeight(
        SchizophreniaStage stage,
        PsychosisIncidentType type,
        bool hasVictimData)
    {
        return stage switch
        {
            SchizophreniaStage.Prodromal => type switch
            {
                PsychosisIncidentType.Whisper => 0.40f,
                PsychosisIncidentType.Directive => 0.22f,
                PsychosisIncidentType.Accusation => 0.25f,
                PsychosisIncidentType.ScreenStinger => 0.05f,
                PsychosisIncidentType.PseudoAttack => 0.08f,
                PsychosisIncidentType.MurderEcho => 0f,
                _ => 0f
            },
            SchizophreniaStage.Active => type switch
            {
                PsychosisIncidentType.Whisper => hasVictimData ? 0.16f : 0.23f,
                PsychosisIncidentType.Directive => hasVictimData ? 0.24f : 0.28f,
                PsychosisIncidentType.Accusation => hasVictimData ? 0.18f : 0.23f,
                PsychosisIncidentType.ScreenStinger => 0.08f,
                PsychosisIncidentType.PseudoAttack => hasVictimData ? 0.14f : 0.18f,
                PsychosisIncidentType.MurderEcho => hasVictimData ? 0.20f : 0f,
                _ => 0f
            },
            SchizophreniaStage.Break => type switch
            {
                PsychosisIncidentType.Whisper => hasVictimData ? 0.10f : 0.16f,
                PsychosisIncidentType.Directive => hasVictimData ? 0.20f : 0.28f,
                PsychosisIncidentType.Accusation => hasVictimData ? 0.13f : 0.20f,
                PsychosisIncidentType.ScreenStinger => hasVictimData ? 0.05f : 0.08f,
                PsychosisIncidentType.PseudoAttack => hasVictimData ? 0.17f : 0.28f,
                PsychosisIncidentType.MurderEcho => hasVictimData ? 0.35f : 0f,
                _ => 0f
            },
            _ => 0f
        };
    }

    public static PsychosisSeverityBand ToSeverityBand(float severity)
    {
        return severity switch
        {
            < 35f => PsychosisSeverityBand.Low,
            < 70f => PsychosisSeverityBand.Medium,
            _ => PsychosisSeverityBand.High
        };
    }

    public static string? ResolveJobContextTag(string? jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            return null;

        var job = jobId.ToLowerInvariant();
        if (job.Contains("captain") || job.Contains("chief") || job.Contains("head") || job.Contains("hos") ||
            job.Contains("hop") || job.Contains("quartermaster"))
        {
            return "job:command";
        }

        if (job.Contains("security") || job.Contains("warden") || job.Contains("detective") || job.Contains("brig"))
            return "job:sec";

        if (job.Contains("medical") || job.Contains("chemist") || job.Contains("paramedic") || job.Contains("coroner") ||
            job.Contains("psychologist") || job.Contains("virologist"))
        {
            return "job:med";
        }

        if (job.Contains("engineer") || job.Contains("atmos") || job.Contains("technical"))
            return "job:eng";

        if (job.Contains("scientist") || job.Contains("research") || job.Contains("roboticist") || job.Contains("geneticist"))
            return "job:science";

        if (job.Contains("cargo") || job.Contains("salvage") || job.Contains("mail"))
            return "job:cargo";

        if (job.Contains("service") || job.Contains("chef") || job.Contains("bartender") || job.Contains("janitor") ||
            job.Contains("chaplain") || job.Contains("botanist") || job.Contains("librarian") || job.Contains("clown") ||
            job.Contains("mime") || job.Contains("musician"))
        {
            return "job:service";
        }

        if (job.Contains("assistant"))
            return "job:assistant";

        return null;
    }
}
