using System;
using System.Collections.Generic;
using Content.Server.Psychiatry;
using Content.Server.Psychiatry.Components;
using Content.Shared.Psychiatry;
using Content.Shared.Psychiatry.Prototypes;
using NUnit.Framework;
using Robust.Shared.Random;

namespace Content.Tests.Server.Psychiatry;

[TestFixture]
public sealed class SchizophreniaIncidentSystemTests : ContentUnitTest
{
    [TestCase(SchizophreniaStage.Prodromal, PsychosisIncidentType.MurderEcho, false, 0f)]
    [TestCase(SchizophreniaStage.Active, PsychosisIncidentType.MurderEcho, true, 0.20f)]
    [TestCase(SchizophreniaStage.Break, PsychosisIncidentType.MurderEcho, true, 0.35f)]
    [TestCase(SchizophreniaStage.Prodromal, PsychosisIncidentType.PseudoAttack, false, 0.08f)]
    [TestCase(SchizophreniaStage.Active, PsychosisIncidentType.PseudoAttack, false, 0.18f)]
    [TestCase(SchizophreniaStage.Break, PsychosisIncidentType.PseudoAttack, false, 0.28f)]
    [TestCase(SchizophreniaStage.Active, PsychosisIncidentType.Directive, true, 0.24f)]
    [TestCase(SchizophreniaStage.Break, PsychosisIncidentType.Accusation, true, 0.13f)]
    public void IncidentWeightsMatchDesign(
        SchizophreniaStage stage,
        PsychosisIncidentType type,
        bool hasVictims,
        float expected)
    {
        Assert.That(SchizophreniaIncidentSystem.GetIncidentWeight(stage, type, hasVictims), Is.EqualTo(expected));
    }

    [Test]
    public void IncidentCooldownsAreAppliedByType()
    {
        var memory = new SchizophreniaMemoryComponent
        {
            MurderEchoCooldown = TimeSpan.FromSeconds(35)
        };

        var now = TimeSpan.FromSeconds(100);
        memory.LastIncidentByType[PsychosisIncidentType.MurderEcho] = TimeSpan.FromSeconds(70);
        memory.LastIncidentByType[PsychosisIncidentType.PseudoAttack] = TimeSpan.FromSeconds(95);
        memory.LastIncidentByType[PsychosisIncidentType.ScreenStinger] = TimeSpan.FromSeconds(90);
        memory.LastIncidentByType[PsychosisIncidentType.Whisper] = TimeSpan.FromSeconds(99);

        Assert.That(SchizophreniaIncidentSystem.IsIncidentOnCooldown(memory, PsychosisIncidentType.MurderEcho, now), Is.True);
        Assert.That(SchizophreniaIncidentSystem.IsIncidentOnCooldown(memory, PsychosisIncidentType.PseudoAttack, now), Is.True);
        Assert.That(SchizophreniaIncidentSystem.IsIncidentOnCooldown(memory, PsychosisIncidentType.ScreenStinger, now), Is.True);
        Assert.That(SchizophreniaIncidentSystem.IsIncidentOnCooldown(memory, PsychosisIncidentType.Whisper, now), Is.False);
    }

    [TestCase(SchizophreniaStage.Prodromal, 12f, 20f)]
    [TestCase(SchizophreniaStage.Active, 6f, 11f)]
    [TestCase(SchizophreniaStage.Break, 3.5f, 7f)]
    public void IncidentIntervalsMatchHardcorePacing(
        SchizophreniaStage stage,
        float expectedMin,
        float expectedMax)
    {
        var (min, max) = SchizophreniaIncidentSystem.GetIncidentIntervalSeconds(stage);
        Assert.That(min, Is.EqualTo(expectedMin));
        Assert.That(max, Is.EqualTo(expectedMax));
    }

    [TestCase(PsychosisIncidentType.Whisper, SchizophreniaStage.Prodromal, "PsychosisFallbackWhisperProdromal")]
    [TestCase(PsychosisIncidentType.Directive, SchizophreniaStage.Active, "PsychosisFallbackDirectiveActive")]
    [TestCase(PsychosisIncidentType.Accusation, SchizophreniaStage.Break, "PsychosisFallbackAccusationBreak")]
    [TestCase(PsychosisIncidentType.MurderEcho, SchizophreniaStage.Active, "PsychosisFallbackMurderEchoActive")]
    [TestCase(PsychosisIncidentType.PseudoAttack, SchizophreniaStage.Break, "PsychosisFallbackPseudoAttackBreak")]
    [TestCase(PsychosisIncidentType.ScreenStinger, SchizophreniaStage.Prodromal, "PsychosisFallbackScreenStingerProdromal")]
    public void FallbackPrototypeLineIdIsDeterministic(
        PsychosisIncidentType type,
        SchizophreniaStage stage,
        string expected)
    {
        Assert.That(SchizophreniaIncidentSystem.GetFallbackLinePrototypeId(type, stage), Is.EqualTo(expected));
    }

    [Test]
    public void ResolveLinePrototypeIdUsesFallbackWhenSelectorReturnsNull()
    {
        var lineId = SchizophreniaIncidentSystem.ResolveLinePrototypeId(
            null,
            PsychosisIncidentType.Directive,
            SchizophreniaStage.Active);

        Assert.That(lineId, Is.EqualTo("PsychosisFallbackDirectiveActive"));
    }

    [Test]
    public void LineEligibilityRespectsContextTags()
    {
        var context = new PsychosisIncidentContext(
            speciesId: "human",
            jobId: "SecurityOfficer",
            isAntag: true,
            killCount: 2,
            hasVictimData: true,
            stage: SchizophreniaStage.Active,
            severityBand: PsychosisSeverityBand.High);

        var matching = new PsychosisLinePrototype
        {
            Tags = new HashSet<string> { "killer:true", "antag:true", "species:human", "job:sec", "stage:active" }
        };

        var wrongSpecies = new PsychosisLinePrototype
        {
            Tags = new HashSet<string> { "species:reptilian", "stage:active" }
        };

        var wrongStage = new PsychosisLinePrototype
        {
            Tags = new HashSet<string> { "stage:break" }
        };

        var killerOnly = new PsychosisLinePrototype
        {
            Tags = new HashSet<string> { "killer:true", "stage:active" }
        };

        Assert.That(SchizophreniaIncidentSystem.IsLineEligible(matching, context), Is.True);
        Assert.That(SchizophreniaIncidentSystem.IsLineEligible(wrongSpecies, context), Is.False);
        Assert.That(SchizophreniaIncidentSystem.IsLineEligible(wrongStage, context), Is.False);
        Assert.That(SchizophreniaIncidentSystem.IsLineEligible(killerOnly, context), Is.True);

        var noKillsContext = new PsychosisIncidentContext(
            context.SpeciesId,
            context.JobId,
            context.IsAntag,
            killCount: 0,
            hasVictimData: false,
            context.Stage,
            context.SeverityBand);

        Assert.That(SchizophreniaIncidentSystem.IsLineEligible(killerOnly, noKillsContext), Is.False);
    }

    [Test]
    public void MurderEchoVictimSelectionUsesWholePool()
    {
        var snapshots = new List<PsychosisVictimSnapshot>
        {
            MakeVictim("First"),
            MakeVictim("Middle"),
            MakeVictim("Last")
        };

        var random = new SequenceRandom(0, 2, 1);

        var pickA = SchizophreniaIncidentSystem.PickVictimSnapshot(snapshots, random);
        var pickB = SchizophreniaIncidentSystem.PickVictimSnapshot(snapshots, random);
        var pickC = SchizophreniaIncidentSystem.PickVictimSnapshot(snapshots, random);

        Assert.That(pickA?.VictimName, Is.EqualTo("First"));
        Assert.That(pickB?.VictimName, Is.EqualTo("Last"));
        Assert.That(pickC?.VictimName, Is.EqualTo("Middle"));
    }

    [Test]
    public void VictimMemoryRingBufferEvictsOldest()
    {
        var memory = new SchizophreniaMemoryComponent
        {
            VictimMemoryLimit = 2
        };

        SchizophreniaIncidentSystem.PushVictimSnapshot(memory, MakeVictim("Oldest"));
        SchizophreniaIncidentSystem.PushVictimSnapshot(memory, MakeVictim("Middle"));
        SchizophreniaIncidentSystem.PushVictimSnapshot(memory, MakeVictim("Newest"));

        Assert.That(memory.VictimSnapshots.Count, Is.EqualTo(2));
        Assert.That(memory.VictimSnapshots[0].VictimName, Is.EqualTo("Middle"));
        Assert.That(memory.VictimSnapshots[1].VictimName, Is.EqualTo("Newest"));
    }

    private static PsychosisVictimSnapshot MakeVictim(string name)
    {
        return new PsychosisVictimSnapshot(name, "MobObserverVisualHumanoid", null, new List<PsychosisSnapshotItem>());
    }

    private sealed class SequenceRandom(params int[] values) : IRobustRandom
    {
        private readonly Queue<int> _values = new(values);
        private readonly Random _fallback = new(42);

        public Random GetRandom()
        {
            return _fallback;
        }

        public void SetSeed(int seed)
        {
        }

        public float NextFloat()
        {
            return (float) _fallback.NextDouble();
        }

        public int Next()
        {
            return _fallback.Next();
        }

        public int Next(int maxValue)
        {
            if (_values.Count == 0)
                return _fallback.Next(maxValue);

            var value = _values.Dequeue();
            if (maxValue <= 0)
                return 0;

            return Math.Clamp(value, 0, maxValue - 1);
        }

        public int Next(int minValue, int maxValue)
        {
            if (maxValue <= minValue)
                return minValue;

            return minValue + Next(maxValue - minValue);
        }

        public double NextDouble()
        {
            return _fallback.NextDouble();
        }

        public TimeSpan Next(TimeSpan maxTime)
        {
            return Next(TimeSpan.Zero, maxTime);
        }

        public TimeSpan Next(TimeSpan minTime, TimeSpan maxTime)
        {
            if (maxTime <= minTime)
                return minTime;

            var range = maxTime - minTime;
            return minTime + TimeSpan.FromTicks((long) (range.Ticks * NextDouble()));
        }

        public void NextBytes(byte[] buffer)
        {
            _fallback.NextBytes(buffer);
        }
    }
}
