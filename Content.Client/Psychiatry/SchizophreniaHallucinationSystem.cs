using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.Humanoid;
using Content.Shared.Inventory;
using Content.Client.Psychiatry.Overlays;
using Content.Shared.Humanoid;
using Content.Shared.Psychiatry.Prototypes;
using Content.Shared.Psychiatry;
using Content.Shared.Popups;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client.Psychiatry;

public sealed class SchizophreniaHallucinationSystem : EntitySystem
{
    private sealed class LocalPhantom
    {
        public EntityUid Uid;
        public TimeSpan DespawnAt;
        public float MoveSpeed;
        public bool Lunge;
        public bool Impacted;
        public Vector2 DriftVector;
        public string? Speech;
        public int RemainingSpeechBursts;
        public TimeSpan NextSpeechAt;
    }

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private readonly List<LocalPhantom> _phantoms = new();
    private readonly SoundSpecifier _whispers = new SoundCollectionSpecifier("PsychosisWhispers");
    private readonly SoundSpecifier _accusations = new SoundCollectionSpecifier("PsychosisAccusations");
    private readonly SoundSpecifier _attack = new SoundCollectionSpecifier("PsychosisPseudoAttack");
    private readonly SoundSpecifier _stinger = new SoundCollectionSpecifier("PsychosisStingers");

    private PsychosisOverlay _overlay = default!;
    private PsychosisStingerOverlay _stingerOverlay = default!;
    private bool _overlayEnabled;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new PsychosisOverlay();
        _stingerOverlay = new PsychosisStingerOverlay();

        SubscribeLocalEvent<SchizophreniaComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SchizophreniaComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SchizophreniaComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<SchizophreniaComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeNetworkEvent<PsychosisIncidentEvent>(OnPsychosisIncident);
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
        UpdatePhantomMotion(local, frameTime);

        if (_timing.CurTime < psychosis.SuppressionUntil || psychosis.Stage == SchizophreniaStage.Remission)
            return;
    }

    private void OnInit(EntityUid uid, SchizophreniaComponent component, ComponentInit args)
    {
        if (_player.LocalEntity != uid)
            return;

        EnableOverlay();
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
        _overlays.AddOverlay(_stingerOverlay);
        _overlayEnabled = true;
    }

    private void DisableOverlay()
    {
        if (!_overlayEnabled)
            return;

        _overlays.RemoveOverlay(_overlay);
        _overlays.RemoveOverlay(_stingerOverlay);
        _overlayEnabled = false;
    }

    private void OnPsychosisIncident(PsychosisIncidentEvent ev)
    {
        if (_player.LocalEntity is not { } local || GetNetEntity(local) != ev.Patient)
            return;

        var text = ResolveIncidentText(ev);
        var burst = ev.Context.Stage switch
        {
            SchizophreniaStage.Prodromal => 0.025f,
            SchizophreniaStage.Active => 0.05f,
            _ => 0.08f
        };

        switch (ev.Type)
        {
            case PsychosisIncidentType.Whisper:
                PlayIncidentSound(_whispers, local, ev.Context.Stage);
                SpawnPhantom(local, ev, text, lunge: false);
                break;
            case PsychosisIncidentType.Directive:
            case PsychosisIncidentType.Accusation:
                PlayIncidentSound(_accusations, local, ev.Context.Stage);
                SpawnPhantom(local, ev, text, lunge: false);
                if (ev.Context.Stage >= SchizophreniaStage.Active && _random.Prob(0.35f))
                    _stingerOverlay.Trigger(text, 0.65f);
                break;
            case PsychosisIncidentType.MurderEcho:
                PlayIncidentSound(_accusations, local, ev.Context.Stage, extraPitch: true);
                SpawnPhantom(local, ev, text, lunge: false);
                burst += 0.02f;
                break;
            case PsychosisIncidentType.PseudoAttack:
                PlayIncidentSound(_attack, local, ev.Context.Stage);
                SpawnPhantom(local, ev, text, lunge: true);
                burst += 0.03f;
                break;
            case PsychosisIncidentType.ScreenStinger:
                PlayIncidentSound(_stinger, local, ev.Context.Stage);
                _stingerOverlay.Trigger(text, 1f);
                burst += 0.04f;
                break;
        }

        _overlay.TriggerBurst(burst);
    }

    private string ResolveIncidentText(PsychosisIncidentEvent ev)
    {
        var victim = ev.Victim?.VictimName ?? Loc.GetString("psychosis-hallucination-victim-unknown");
        if (ev.LineId != null && _prototype.TryIndex<PsychosisLinePrototype>(ev.LineId, out var line))
            return Loc.GetString(line.LocKey, ("victim", victim), ("killCount", ev.Context.KillCount));

        var fallback = GetFallbackTextLocKey(ev.Type, ev.Context.Stage);
        return Loc.GetString(fallback, ("victim", victim), ("killCount", ev.Context.KillCount));
    }

    private void PlayIncidentSound(SoundSpecifier sound, EntityUid local, SchizophreniaStage stage, bool extraPitch = false)
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
            .WithPitchScale(extraPitch ? _random.NextFloat(0.7f, 1.35f) : _random.NextFloat(0.82f, 1.22f));

        _audio.PlayStatic(sound, local, coordinates, audioParams);
    }

    private void SpawnPhantom(EntityUid local, PsychosisIncidentEvent ev, string? speech, bool lunge)
    {
        var maxPhantoms = ev.Context.Stage == SchizophreniaStage.Break ? 2 : 1;
        while (_phantoms.Count >= maxPhantoms)
        {
            var stale = _phantoms[0];
            if (Exists(stale.Uid))
                Del(stale.Uid);

            _phantoms.RemoveAt(0);
        }

        var distance = lunge
            ? _random.NextFloat(5.2f, 7.4f)
            : _random.NextFloat(2.8f, ev.Context.Stage == SchizophreniaStage.Break ? 6.4f : 4.8f);
        var angle = _random.NextFloat(0f, MathF.Tau);
        var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
        var coords = Transform(local).Coordinates.Offset(offset);

        var prototype = ResolvePhantomPrototype(ev);
        var phantom = Spawn(prototype, coords);

        if (ev.Victim is { } victim)
            ApplyVictimSnapshot(phantom, victim);

        if (TryComp(phantom, out SpriteComponent? sprite))
            sprite.Color = new Color(0.95f, 0.95f, 1f, _random.NextFloat(0.30f, 0.67f));

        var lifeTime = lunge ? _random.NextFloat(1.0f, 1.8f) : ev.Context.Stage switch
        {
            SchizophreniaStage.Prodromal => _random.NextFloat(2.2f, 4.2f),
            SchizophreniaStage.Active => _random.NextFloat(3.2f, 5.8f),
            _ => _random.NextFloat(4.3f, 7.2f)
        };

        if (!string.IsNullOrWhiteSpace(speech))
            _popup.PopupEntity(speech, phantom, PopupType.MediumCaution);

        _phantoms.Add(new LocalPhantom
        {
            Uid = phantom,
            DespawnAt = _timing.CurTime + TimeSpan.FromSeconds(lifeTime),
            MoveSpeed = lunge ? _random.NextFloat(8.5f, 10.5f) : _random.NextFloat(0.55f, 1.25f),
            DriftVector = _random.NextVector2(0.2f, 0.9f),
            Lunge = lunge,
            Speech = speech,
            RemainingSpeechBursts = string.IsNullOrWhiteSpace(speech) ? 0 : _random.Next(0, 2),
            NextSpeechAt = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(0.45f, 1.35f))
        });
    }

    private string ResolvePhantomPrototype(PsychosisIncidentEvent ev)
    {
        var prototype = ev.PhantomPrototype ?? ev.Victim?.ObserverPrototype ?? "MobObserverVisualHumanoid";
        if (_prototype.HasIndex<EntityPrototype>(prototype))
            return prototype;

        if (_prototype.HasIndex<EntityPrototype>("MobObserverVisualHumanoid"))
            return "MobObserverVisualHumanoid";

        if (_prototype.HasIndex<EntityPrototype>("PsychosisPhantom"))
            return "PsychosisPhantom";

        return "MobObserver";
    }

    private static string GetFallbackTextLocKey(PsychosisIncidentType type, SchizophreniaStage stage)
    {
        var stageTag = stage switch
        {
            SchizophreniaStage.Break => "break",
            SchizophreniaStage.Active => "active",
            _ => "prodromal"
        };

        var typeTag = type switch
        {
            PsychosisIncidentType.Whisper => "whisper",
            PsychosisIncidentType.Directive => "directive",
            PsychosisIncidentType.Accusation => "accusation",
            PsychosisIncidentType.MurderEcho => "murder-echo",
            PsychosisIncidentType.PseudoAttack => "pseudo-attack",
            PsychosisIncidentType.ScreenStinger => "screen-stinger",
            _ => "whisper"
        };

        return $"psychosis-line-fallback-{typeTag}-{stageTag}";
    }

    private void ApplyVictimSnapshot(EntityUid phantom, PsychosisVictimSnapshot snapshot)
    {
        if (snapshot.Profile != null && TryComp(phantom, out HumanoidAppearanceComponent? humanoid))
            _humanoid.LoadProfile(phantom, snapshot.Profile, humanoid, loadExtensions: false, generateLoadouts: false);

        if (snapshot.SnapshotSlots.Count == 0)
            return;

        var coordinates = Transform(phantom).Coordinates;
        foreach (var slot in snapshot.SnapshotSlots)
        {
            if (!_prototype.HasIndex<EntityPrototype>(slot.PrototypeId))
                continue;

            var item = Spawn(slot.PrototypeId, coordinates);
            if (!_inventory.TryEquip(phantom, item, slot.Slot, silent: true, force: true))
                Del(item);
        }
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

    private void UpdatePhantomMotion(EntityUid local, float frameTime)
    {
        var player = Transform(local).MapPosition.Position;
        var now = _timing.CurTime;

        foreach (var phantom in _phantoms)
        {
            if (!Exists(phantom.Uid))
                continue;

            var xform = Transform(phantom.Uid);
            var current = xform.MapPosition.Position;
            var toPlayer = player - current;
            var distance = toPlayer.Length();
            var dir = distance > 0.01f ? toPlayer / distance : Vector2.Zero;

            if (phantom.Lunge)
            {
                var next = current + dir * phantom.MoveSpeed * frameTime;
                xform.Coordinates = new EntityCoordinates(xform.MapUid!.Value, next);

                if (!phantom.Impacted && distance <= 1.3f)
                {
                    phantom.Impacted = true;
                    phantom.DespawnAt = now + TimeSpan.FromSeconds(0.20f);
                    _overlay.TriggerBurst(0.09f);
                    _stingerOverlay.Trigger(Loc.GetString("psychosis-hallucination-attack-stinger"), 0.8f);
                }

                continue;
            }

            var desired = dir * 0.70f + phantom.DriftVector * 0.30f;
            if (desired.LengthSquared() > 0.001f)
                desired = Vector2.Normalize(desired);

            var updated = current + desired * phantom.MoveSpeed * frameTime;
            xform.Coordinates = new EntityCoordinates(xform.MapUid!.Value, updated);

            if (phantom.RemainingSpeechBursts <= 0 ||
                string.IsNullOrWhiteSpace(phantom.Speech) ||
                now < phantom.NextSpeechAt)
            {
                continue;
            }

            _popup.PopupEntity(phantom.Speech, phantom.Uid, PopupType.SmallCaution);
            phantom.RemainingSpeechBursts--;
            phantom.NextSpeechAt = now + TimeSpan.FromSeconds(_random.NextFloat(0.8f, 1.6f));
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
