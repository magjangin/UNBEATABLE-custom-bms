using Arcade.Progression;
using MelonLoader;
using UnbeatableCustomMode;

[assembly: MelonInfo(typeof(UnbeatableModEntry), "UNBEATABLE Custom Mode", "0.1.0", "hwa")]
[assembly: MelonGame("D-CELL GAMES", "UNBEATABLE")]

namespace UnbeatableCustomMode;

public class UnbeatableModEntry : MelonMod
{
    public override void OnInitializeMelon()
    {
        // Unlocks whatever content is actually loaded (base roster + Deluxe Edition, menu
        // palettes, rhythm scenes). It cannot summon characters from DLC packs whose .dlc file
        // isn't present in StreamingAssets/DLC - that data simply isn't on disk to unlock.
        MainProgressionContainer.unlockAll = true;

        HwaSongLoader.ConvertAll(LoggerInstance);
    }
}
