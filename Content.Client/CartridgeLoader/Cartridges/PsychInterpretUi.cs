using Content.Client.UserInterface.Fragments;
using Content.Shared.CartridgeLoader;
using Content.Shared.CartridgeLoader.Cartridges;
using Robust.Client.UserInterface;

namespace Content.Client.CartridgeLoader.Cartridges;

public sealed partial class PsychInterpretUi : UIFragment
{
    private PsychInterpretUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new PsychInterpretUiFragment();
        _fragment.RunInterpretation += symptoms =>
        {
            userInterface.SendMessage(new CartridgeUiMessage(new PsychInterpretUiMessageEvent(symptoms)));
        };
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is not PsychInterpretUiState cast)
            return;

        _fragment?.UpdateState(cast);
    }
}
