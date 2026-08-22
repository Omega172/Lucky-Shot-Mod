using BepInEx.Configuration;
using HarmonyLib;
using Shooterlatro.Weapons;
using UnityEngine;

namespace LuckShotMod;

// Config-only holder (no per-frame logic needed) for the "no recoil" toggle. The actual
// behavior lives in the Harmony patch below.
public static class NoRecoil
{
    public static ConfigEntry<bool> Enabled { get; private set; }

    public static void Init(ConfigFile config)
    {
        Enabled = config.Bind("NoRecoil", "Enabled", false,
            "Removes the deterministic recoil kick pattern from every weapon. Spread/bloom (genuine per-shot accuracy randomness) is left untouched.");
    }
}

// This game's recoil never moves the camera - RecoilController.ResolveBallistics bends the
// fired bullet's direction using a deterministic pattern looked up per consecutive-shot chain
// index (see RecoilCompensation.cs), plus a separate spread/bloom component. The pattern lookup
// itself - WeaponRecoilProfileData.GetShotOffsetDegrees - is the single choke point every
// weapon's recoil pattern goes through (RecoilController.cs is its only caller), so forcing it
// to report zero there kills the kick everywhere at once without touching chain-index/pending-
// shot bookkeeping or the spread math, which both keep working exactly as before.
[HarmonyPatch(typeof(WeaponRecoilProfileData), nameof(WeaponRecoilProfileData.GetShotOffsetDegrees))]
internal static class WeaponRecoilProfileData_GetShotOffsetDegrees_Patch
{
    private static void Postfix(ref Vector2 __result)
    {
        if (NoRecoil.Enabled?.Value == true)
        {
            __result = Vector2.zero;
        }
    }
}
