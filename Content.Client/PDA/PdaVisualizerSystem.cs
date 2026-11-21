using Content.Shared.Light;
using Content.Shared.PDA;
using Robust.Client.GameObjects;

namespace Content.Client.PDA;

public sealed partial class PdaVisualizerSystem : VisualizerSystem<PdaVisualsComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, PdaVisualsComponent comp, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (args.TryGetData<string>(PdaVisuals.PdaType, out var pdaType))
            SpriteSystem.LayerSetRsiState((uid, args.Sprite), PdaVisualLayers.Base, pdaType);

        if (args.TryGetData<bool>(UnpoweredFlashlightVisuals.LightOn, out var isFlashlightOn))
            SpriteSystem.LayerSetVisible((uid, args.Sprite), PdaVisualLayers.Flashlight, isFlashlightOn);

        // _SE start
        if (args.TryGetData<bool>(PdaVisuals.IdCardInserted, out var isCardInserted))
        {
            SpriteSystem.LayerSetVisible((uid, args.Sprite), PdaVisualLayers.IdLight, isCardInserted);

            if (SpriteSystem.TryGetLayer((uid, args.Sprite), PdaVisualLayers.CardVisible, out _, false))
                SpriteSystem.LayerSetVisible((uid, args.Sprite), PdaVisualLayers.CardVisible, isCardInserted);
        }
        // _SE end
    }

    public enum PdaVisualLayers : byte
    {
        Base,
        Flashlight,
        IdLight,
        CardVisible, // _SE
    }
}
