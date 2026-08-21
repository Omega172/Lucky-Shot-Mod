using System.Collections;
using HarmonyLib;
using Shooterlatro.Core;
using Shooterlatro.Game;
using Shooterlatro.Score;
using Shooterlatro.Shop;
using Shooterlatro.UI.RunInfo;
using Shooterlatro.Weapons;
using UnityEngine;

namespace LuckShotMod;

// The shop's Run Info > Bullets tab only ever builds Holo entries as sellable
// (RunInfoPresentationBuilder.BuildShopItem hardcodes Sellable = false for every
// other kind, and RunInfoPanelViewController.OnSellShot bails out on anything
// without a HoloSlotIndex). These two patches extend that same sell button to
// bullets: special bullets sell for half their shop cost; normal (plain ammo)
// bullets sell for $0 - selling one just shrinks your magazine capacity by one,
// via Weapon.TryDestroyPersistentBulletOnce's own normal-bullet handling.
[HarmonyPatch(typeof(RunInfoPanelViewController), "BuildShopPresentation")]
internal static class RunInfoPanelViewController_BuildShopPresentation_Patch
{
    private static void Postfix(RunInfoItemKind kind, ScriptableObject source, RunInfoItemPresentation __result)
    {
        if (kind != RunInfoItemKind.Bullet || __result == null)
        {
            return;
        }

        if (source is not BulletItemData bullet)
        {
            return;
        }

        __result.Sellable = true;
        __result.SellValue = GetSellValue(bullet);
    }

    internal static int GetSellValue(BulletItemData bullet)
    {
        if (bullet.isNormalBullet)
        {
            return 0;
        }
        float cost = bullet.shopItem != null ? bullet.shopItem.cost : 0f;
        return Mathf.Max(0, Mathf.FloorToInt(cost * 0.5f));
    }
}

[HarmonyPatch(typeof(RunInfoPanelViewController), "OnSellShot")]
internal static class RunInfoPanelViewController_OnSellShot_Patch
{
    // Runs before the original, which only knows how to sell holos (it checks
    // HoloSlotIndex >= 0 and otherwise no-ops). Returning false skips the original
    // entirely once we've handled - or deliberately rejected - a bullet sale ourselves.
    private static bool Prefix(RunInfoPanelViewController __instance, ShotHitContext context)
    {
        if (!__instance.CanTriggerSell())
        {
            return true;
        }

        Traverse instanceTraverse = Traverse.Create(__instance);
        RunInfoItemPresentation selected = instanceTraverse.Method("GetSelectedPresentation").GetValue<RunInfoItemPresentation>();
        if (selected == null || selected.Kind != RunInfoItemKind.Bullet)
        {
            return true;
        }

        if (selected.SourceAsset is not BulletItemData bullet)
        {
            return false;
        }

        BulletModifierFlags modifiers = ResolveSelectedModifiers(instanceTraverse, selected, bullet);

        Weapon weapon = Weapon.Instance;
        if (weapon == null || !weapon.TryDestroyPersistentBulletOnce(bullet, modifiers))
        {
            return false;
        }

        int sellValue = RunInfoPanelViewController_BuildShopPresentation_Patch.GetSellValue(bullet);
        if (sellValue > 0 && ScoreManager.Instance != null)
        {
            ScoreManager.Instance.AddCurrency(sellValue, RoundCurrencySource.Bullet);
        }

        RunInfoPanelView panelView = instanceTraverse.Field("panelView").GetValue<RunInfoPanelView>();
        if (panelView != null)
        {
            panelView.PlaySellHitFeedback(context);
        }

        instanceTraverse.Field("_selectedLocalIndex").SetValue(-1);
        instanceTraverse.Method("RefreshAndRender", true, false).GetValue();

        return false;
    }

    // The presentation itself doesn't carry which modifier-combo (Fragile/Giga/Holo) it
    // represents, but it was built from _bulletCountBuffer[AbsoluteIndex] this same refresh,
    // so we can recover it from there.
    private static BulletModifierFlags ResolveSelectedModifiers(Traverse instanceTraverse, RunInfoItemPresentation selected, BulletItemData bullet)
    {
        IList buffer = instanceTraverse.Field("_bulletCountBuffer").GetValue<IList>();
        if (buffer == null || selected.AbsoluteIndex < 0 || selected.AbsoluteIndex >= buffer.Count)
        {
            return BulletModifierFlags.None;
        }

        Traverse entryTraverse = Traverse.Create(buffer[selected.AbsoluteIndex]);
        BulletItemData entryBullet = entryTraverse.Field("Bullet").GetValue<BulletItemData>();
        return entryBullet == bullet ? entryTraverse.Field("Modifiers").GetValue<BulletModifierFlags>() : BulletModifierFlags.None;
    }
}
