using Content.Client.UserInterface.Fragments;
using Content.Shared.CartridgeLoader.Cartridges;
using Robust.Client.UserInterface;

namespace Content.Client.CartridgeLoader.Cartridges;

public sealed partial class BrainWaveScannerUi : UIFragment
{
    private BrainWaveScannerUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new BrainWaveScannerUiFragment();
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not BrainWaveUiState cast)
            return;

        _fragment?.UpdateState(cast);
    }
}
