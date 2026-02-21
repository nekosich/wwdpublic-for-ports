using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.Psychiatry.Overlays;
using Content.Shared.Psychiatry;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client.Psychiatry;

public sealed class SchizophreniaHallucinationSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private readonly List<(EntityUid Uid, TimeSpan DespawnAt)> _phantoms = new();
    private readonly SoundSpecifier _voices = new SoundCollectionSpecifier("PsychosisVoices");

    private PsychosisOverlay _overlay = default!;
    private TimeSpan _nextIncident;
    private bool _overlayEnabled;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new PsychosisOverlay();

        SubscribeLocalEvent<SchizophreniaComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SchizophreniaComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SchizophreniaComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<SchizophreniaComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        if (_player.LocalEntity is not { } local || !TryComp(local, out SchizophreniaComponent? psychosis))
        {
            ClearPhantoms();
            return;
        }

        CleanupPhantoms(local);

        if (_timing.CurTime < psychosis.SuppressionUntil || psychosis.Stage == SchizophreniaStage.Remission)
            return;

        if (_timing.CurTime < _nextIncident)
            return;

        TriggerIncident(local, psychosis);
        ScheduleNextIncident(psychosis);
    }

    private void OnInit(EntityUid uid, SchizophreniaComponent component, ComponentInit args)
    {
        if (_player.LocalEntity != uid)
            return;

        EnableOverlay();
        ScheduleNextIncident(component);
    }

    private void OnShutdown(EntityUid uid, SchizophreniaComponent component, ComponentShutdown args)
    {
        if (_player.LocalEntity != uid)
            return;

        DisableOverlay();
        ClearPhantoms();
    }

    private void OnPlayerAttached(EntityUid uid, SchizophreniaComponent component, LocalPlayerAttachedEvent args)
    {
        EnableOverlay();
        ScheduleNextIncident(component);
    }

    private void OnPlayerDetached(EntityUid uid, SchizophreniaComponent component, LocalPlayerDetachedEvent args)
    {
        DisableOverlay();
        ClearPhantoms();
    }

    private void EnableOverlay()
    {
        if (_overlayEnabled)
            return;

        _overlays.AddOverlay(_overlay);
        _overlayEnabled = true;
    }

    private void DisableOverlay()
    {
        if (!_overlayEnabled)
            return;

        _overlays.RemoveOverlay(_overlay);
        _overlayEnabled = false;
    }

    private void TriggerIncident(EntityUid local, SchizophreniaComponent psychosis)
    {
        PlayVoiceIncident(local, psychosis.Stage);
        TrySpawnPhantom(local, psychosis.Stage);

        var burst = psychosis.Stage switch
        {
            SchizophreniaStage.Prodromal => 0.025f,
            SchizophreniaStage.Active => 0.050f,
            _ => 0.080f
        };

        _overlay.TriggerBurst(burst);
    }

    private void PlayVoiceIncident(EntityUid local, SchizophreniaStage stage)
    {
        var radius = stage switch
        {
            SchizophreniaStage.Prodromal => 3.5f,
            SchizophreniaStage.Active => 5.5f,
            _ => 7.5f
        };

        var offset = new Vector2(_random.NextFloat(-radius, radius), _random.NextFloat(-radius, radius));
        var coordinates = Transform(local).Coordinates.Offset(offset);

        var audioParams = AudioParams.Default
            .WithVolume(stage == SchizophreniaStage.Break ? -4f : -7f)
            .WithPitchScale(_random.NextFloat(0.8f, 1.25f));

        _audio.PlayStatic(_voices, local, coordinates, audioParams);
    }

    private void TrySpawnPhantom(EntityUid local, SchizophreniaStage stage)
    {
        var chance = stage switch
        {
            SchizophreniaStage.Prodromal => 0.25f,
            SchizophreniaStage.Active => 0.65f,
            _ => 0.9f
        };

        if (!_random.Prob(chance))
            return;

        var maxPhantoms = stage == SchizophreniaStage.Break ? 2 : 1;
        if (_phantoms.Count >= maxPhantoms)
            return;

        var distance = _random.NextFloat(2.5f, stage == SchizophreniaStage.Break ? 6.5f : 4.8f);
        var angle = _random.NextFloat(0f, MathF.Tau);
        var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
        var coords = Transform(local).Coordinates.Offset(offset);

        var phantom = Spawn("PsychosisPhantom", coords);
        if (TryComp(phantom, out SpriteComponent? sprite))
            sprite.Color = new Color(0.95f, 0.95f, 1f, _random.NextFloat(0.32f, 0.68f));

        var lifeTime = stage switch
        {
            SchizophreniaStage.Prodromal => _random.NextFloat(2.2f, 4.2f),
            SchizophreniaStage.Active => _random.NextFloat(3.2f, 5.8f),
            _ => _random.NextFloat(4.3f, 7.2f)
        };

        _phantoms.Add((phantom, _timing.CurTime + TimeSpan.FromSeconds(lifeTime)));
    }

    private void ScheduleNextIncident(SchizophreniaComponent psychosis)
    {
        var seconds = psychosis.Stage switch
        {
            SchizophreniaStage.Prodromal => _random.NextFloat(16f, 30f),
            SchizophreniaStage.Active => _random.NextFloat(8f, 16f),
            SchizophreniaStage.Break => _random.NextFloat(4f, 8f),
            _ => _random.NextFloat(40f, 60f)
        };

        _nextIncident = _timing.CurTime + TimeSpan.FromSeconds(seconds);
    }

    private void CleanupPhantoms(EntityUid local)
    {
        var playerXform = Transform(local);
        var playerMap = playerXform.MapID;
        var playerPos = playerXform.MapPosition.Position;

        for (var i = _phantoms.Count - 1; i >= 0; i--)
        {
            var phantom = _phantoms[i];
            if (!Exists(phantom.Uid))
            {
                _phantoms.RemoveAt(i);
                continue;
            }

            var phantomXform = Transform(phantom.Uid);
            var delete = _timing.CurTime >= phantom.DespawnAt;

            if (!delete && playerMap == phantomXform.MapID)
            {
                var distanceSq = (playerPos - phantomXform.MapPosition.Position).LengthSquared();
                delete = distanceSq > 144f;
            }

            if (!delete)
                continue;

            Del(phantom.Uid);
            _phantoms.RemoveAt(i);
        }
    }

    private void ClearPhantoms()
    {
        foreach (var phantom in _phantoms)
        {
            if (Exists(phantom.Uid))
                Del(phantom.Uid);
        }

        _phantoms.Clear();
    }
}
