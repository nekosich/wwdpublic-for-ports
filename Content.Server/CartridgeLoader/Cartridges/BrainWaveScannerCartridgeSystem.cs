using System;
using System.Collections.Generic;
using Content.Server.Psychiatry;
using Content.Shared.CartridgeLoader;
using Content.Shared.CartridgeLoader.Cartridges;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CartridgeLoader.Cartridges;

public sealed class BrainWaveScannerCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly SchizophreniaSystem _schizophrenia = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BrainWaveScannerCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<BrainWaveScannerCartridgeComponent, CartridgeAfterInteractEvent>(OnAfterInteract);
    }

    private void OnUiReady(EntityUid uid, BrainWaveScannerCartridgeComponent component, CartridgeUiReadyEvent args)
    {
        UpdateUiState(uid, args.Loader, component);
    }

    private void OnAfterInteract(EntityUid uid, BrainWaveScannerCartridgeComponent component, CartridgeAfterInteractEvent args)
    {
        if (args.InteractEvent.Handled
            || !args.InteractEvent.CanReach
            || args.InteractEvent.Target is not { } target)
        {
            return;
        }

        _schizophrenia.GetBrainWaveMarkers(
            target,
            out var thetaDrift,
            out var gammaSpikes,
            out var coherenceDrop,
            out var noiseIndex,
            out var stressConductivity);

        // Adds slight fluctuations so repeated scans are not perfectly identical.
        thetaDrift = AddSensorJitter(thetaDrift);
        gammaSpikes = AddSensorJitter(gammaSpikes);
        coherenceDrop = AddSensorJitter(coherenceDrop);
        noiseIndex = AddSensorJitter(noiseIndex);
        stressConductivity = AddSensorJitter(stressConductivity);

        if (component.Scans.Count >= component.MaxSavedScans)
            component.Scans.RemoveAt(0);

        component.Scans.Add(
            new BrainWaveScanRecord(
                GetNetEntity(target),
                Name(target),
                thetaDrift,
                gammaSpikes,
                coherenceDrop,
                noiseIndex,
                stressConductivity,
                _timing.CurTime));

        _audio.PlayEntity(component.ScanSound, args.InteractEvent.User, target);
        _popup.PopupCursor(
            Loc.GetString("brain-wave-scanner-scan", ("target", Name(target))),
            args.InteractEvent.User,
            PopupType.SmallCaution);

        args.InteractEvent.Handled = true;
        UpdateUiState(uid, args.Loader, component);
    }

    private void UpdateUiState(EntityUid uid, EntityUid loaderUid, BrainWaveScannerCartridgeComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var thetaDrift = 0f;
        var gammaSpikes = 0f;
        var coherenceDrop = 0f;
        var noiseIndex = 0f;
        var stressConductivity = 0f;

        if (component.Scans.Count > 0)
        {
            var latest = component.Scans[^1];
            thetaDrift = latest.ThetaDrift;
            gammaSpikes = latest.GammaSpikes;
            coherenceDrop = latest.CoherenceDrop;
            noiseIndex = latest.NoiseIndex;
            stressConductivity = latest.StressConductivity;
        }

        var state = new BrainWaveUiState(
            new List<BrainWaveScanRecord>(component.Scans),
            thetaDrift,
            gammaSpikes,
            coherenceDrop,
            noiseIndex,
            stressConductivity);

        _cartridgeLoader.UpdateCartridgeUiState(loaderUid, state);
    }

    private float AddSensorJitter(float value)
    {
        return Math.Clamp(value + _random.NextFloat(-2.5f, 2.5f), 0f, 100f);
    }
}
