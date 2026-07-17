using System.Linq;
using System.Reflection;
using Arcade.UI.SongSelect;
using HarmonyLib;
using Rhythm;

namespace UnbeatableCustomMode;

/// <summary>
/// ArcadeSongDatabase.Awake() only sets its private "_loadCustomSongs" flag to true when the
/// game was launched with "-customsongs" (or in the editor). This patch forces that flag on
/// regardless of launch args, so the folders HwaSongLoader writes into CustomSongs are always
/// picked up by the game's own, otherwise-untouched loading code.
/// </summary>
[HarmonyPatch(typeof(ArcadeSongDatabase), "Awake")]
public static class ForceLoadCustomSongsPatch
{
    private static readonly FieldInfo CustomCategoryField =
        typeof(ArcadeSongDatabase).GetField("customCategory", BindingFlags.NonPublic | BindingFlags.Static);

    private static readonly PropertyInfo SelectableCategoriesProperty =
        typeof(ArcadeSongDatabase).GetProperty(nameof(ArcadeSongDatabase.SelectableCategories));

    public static void Postfix(ArcadeSongDatabase __instance, ref bool ____loadCustomSongs)
    {
        if (____loadCustomSongs) return;

        ____loadCustomSongs = true;

        var customCategory = (BeatmapIndex.Category)CustomCategoryField.GetValue(null);
        var categories = __instance.SelectableCategories.ToList();
        if (!categories.Contains(customCategory))
        {
            categories.Add(customCategory);
        }
        SelectableCategoriesProperty.SetValue(__instance, categories);

        __instance.LoadDatabase();
        __instance.RefreshSongList(true);
    }
}
