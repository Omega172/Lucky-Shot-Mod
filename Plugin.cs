using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using HarmonyLib;

namespace LuckShotMod;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;

    private Harmony _harmony;

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        AimAssist aimAssist = gameObject.AddComponent<AimAssist>();
        aimAssist.Init(Config);

        Triggerbot triggerbot = gameObject.AddComponent<Triggerbot>();
        triggerbot.Init(Config);

        InfiniteAmmo infiniteAmmo = gameObject.AddComponent<InfiniteAmmo>();
        infiniteAmmo.Init(Config);

        InstantReload instantReload = gameObject.AddComponent<InstantReload>();
        instantReload.Init(Config);

        FireRateModifier fireRateModifier = gameObject.AddComponent<FireRateModifier>();
        fireRateModifier.Init(Config);

        RainbowBullets.Init(Config);

        WinRound winRound = gameObject.AddComponent<WinRound>();
        winRound.Init(Config);

        ModSettingsScreen modSettingsScreen = gameObject.AddComponent<ModSettingsScreen>();
        modSettingsScreen.Init(aimAssist, triggerbot, infiniteAmmo, instantReload, fireRateModifier, winRound);

        gameObject.AddComponent<PauseMenuIntegration>().Init(modSettingsScreen);

        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll();
    }
}
