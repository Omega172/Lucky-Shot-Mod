using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Shooterlatro.Core;
using Shooterlatro.Gameplay;

namespace LuckShotMod;

// Config-only holder (no per-frame logic needed) for the "every bullet triggers every
// effect" toggle. The actual behavior lives in the Harmony patch below.
public static class RainbowBullets
{
    public static ConfigEntry<bool> Enabled { get; private set; }

    public static void Init(ConfigFile config)
    {
        Enabled = config.Bind("RainbowBullets", "Enabled", false,
            "Every shot triggers all rainbow effects at once (Explosive, Ricochet, Clone, ShinyReward), regardless of what bullet is actually loaded.");
    }
}

// BulletEffectHandler.ProcessBulletEffect normally runs the loaded bullet's own single
// effect (plus a holo-granted overlay effect, if any). A real Rainbow bullet instead runs
// all four at once via the private TriggerRainbowAllEffects - but only when
// HoloMechanicRuntime.RainbowBulletsActivateAllEffectsEnabled() says so (normally gated by
// a specific charm). Rather than mutating the shared BulletItemData asset's effectType
// (which would corrupt it for everyone, permanently, since it's a ScriptableObject) this
// patch just calls that same private all-effects method directly for every real shot.
[HarmonyPatch(typeof(BulletEffectHandler), "ProcessBulletEffect")]
internal static class BulletEffectHandler_ProcessBulletEffect_Patch
{
    private static readonly MethodInfo TriggerRainbowAllEffectsMethod =
        AccessTools.Method(typeof(BulletEffectHandler), "TriggerRainbowAllEffects");

    private static bool Prefix(ShotHitContext context)
    {
        if (RainbowBullets.Enabled?.Value != true)
        {
            return true;
        }

        // Same early-outs the original uses: a propagated explosion hit or a ricochet's
        // subsequent hit must not re-trigger effects, or a single shot could chain into
        // an explosion-spawns-explosions runaway loop.
        if (context.isExplosionPropagation || context.isRicochet)
        {
            return true;
        }

        TriggerRainbowAllEffectsMethod?.Invoke(null, new object[] { context });
        return false;
    }
}
