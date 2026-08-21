using BepInEx.Configuration;
using HarmonyLib;
using Shooterlatro.Shop;

namespace LuckShotMod;

// Config-only holder (no per-frame logic needed) for the "every holo in the shop is Hacked"
// toggle. The actual behavior lives in the Harmony patch below.
public static class AllHolosHacked
{
    public static ConfigEntry<bool> Enabled { get; private set; }

    public static void Init(ConfigFile config)
    {
        Enabled = config.Bind("AllHolosHacked", "Enabled", false,
            "Every holo that appears in the shop gets the Hacked modifier (0 capacity slot cost).");
    }
}

// ShopItemExtensions rolls a holo's modifiers via HoloModifierService.RollForHolo (which only
// has a small natural chance of Hacked) and immediately applies them via ApplyToShopItem - the
// single choke point every holo shop item passes through, regardless of which of the two call
// sites (CrateManager, HoloItemManager) originally rolled it. Postfixing that method and forcing
// isHacked = true there is simpler and safer than patching RollForHolo itself, since it doesn't
// have to duplicate RollForHolo's own branching (forceClean vs normal vs StackedDifficulty).
// AppendModifierKeywords is re-run afterward so the shop tooltip actually shows the Hacked
// keyword - it was already called once inside ApplyToShopItem, but that happened before we set
// isHacked here, and TryAddKeyword is idempotent so re-running it is harmless.
[HarmonyPatch(typeof(HoloModifierService), nameof(HoloModifierService.ApplyToShopItem))]
internal static class HoloModifierService_ApplyToShopItem_Patch
{
    private static void Postfix(ShopItem item)
    {
        if (AllHolosHacked.Enabled?.Value != true)
        {
            return;
        }

        if (item == null || item.type != ShopManager.ShopItemType.Holo || item.holoModifier == null)
        {
            return;
        }

        if (item.holoModifier.isHacked)
        {
            return;
        }

        item.holoModifier.isHacked = true;
        HoloModifierService.AppendModifierKeywords(item);
    }
}
