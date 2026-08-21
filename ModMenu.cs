using BepInEx.Configuration;
using Shooterlatro.Game;
using Shooterlatro.Score;
using UnityEngine;

namespace LuckShotMod;

// A single IMGUI overlay window for tweaking every mod feature and seeing its current
// state at a glance. Toggled with its own hotkey (default F1) and shown automatically
// whenever the game's own pause menu is open, since there's no supported way to inject
// into that screen's actual UGUI hierarchy from outside the assembly.
public class ModMenu : MonoBehaviour
{
    private ConfigEntry<bool> _menuToggleKeyEnabled;
    private ConfigEntry<string> _toggleHotkey;

    private AimAssist _aimAssist;
    private Triggerbot _triggerbot;
    private InfiniteAmmo _infiniteAmmo;
    private InstantReload _instantReload;
    private FireRateModifier _fireRateModifier;
    private WinRound _winRound;

    private bool _manuallyOpen;
    private Rect _windowRect = new Rect(20f, 20f, 340f, 0f);

    private string _aimAssistHotkeyBuffer = "";
    private string _triggerbotHotkeyBuffer = "";
    private string _winRoundHotkeyBuffer = "";
    private string _menuHotkeyBuffer = "";
    private string _coinAmountBuffer = "500";

    public void Init(ConfigFile config, AimAssist aimAssist, Triggerbot triggerbot, InfiniteAmmo infiniteAmmo, InstantReload instantReload, FireRateModifier fireRateModifier, WinRound winRound)
    {
        _toggleHotkey = config.Bind("Menu", "ToggleHotkey", "F1",
            "Hotkey that opens/closes the mod menu. The menu also always shows while the game is paused.");
        _menuToggleKeyEnabled = config.Bind("Menu", "Enabled", true,
            "Master switch for the mod menu itself.");

        _aimAssist = aimAssist;
        _triggerbot = triggerbot;
        _infiniteAmmo = infiniteAmmo;
        _instantReload = instantReload;
        _fireRateModifier = fireRateModifier;
        _winRound = winRound;

        _aimAssistHotkeyBuffer = _aimAssist.Hotkey.Value;
        _triggerbotHotkeyBuffer = _triggerbot.Hotkey.Value;
        _winRoundHotkeyBuffer = _winRound.Hotkey.Value;
        _menuHotkeyBuffer = _toggleHotkey.Value;
    }

    private void Update()
    {
        if (!_menuToggleKeyEnabled.Value)
        {
            return;
        }

        if (Hotkeys.WasPressedThisFrame(_toggleHotkey.Value))
        {
            _manuallyOpen = !_manuallyOpen;
        }
    }

    private bool IsVisible => _menuToggleKeyEnabled.Value && (_manuallyOpen || SettingsMenuManager.IsPaused);

    private void OnGUI()
    {
        if (!IsVisible)
        {
            return;
        }

        _windowRect = GUILayout.Window(GetInstanceID(), _windowRect, DrawWindow, "LuckShotMod");
    }

    private void DrawWindow(int windowId)
    {
        GUILayout.Label($"Round active: {(IsRoundActive() ? "yes" : "no")}");
        GUILayout.Space(6f);

        DrawAimAssistSection();
        GUILayout.Space(8f);
        DrawTriggerbotSection();
        GUILayout.Space(8f);
        DrawInfiniteAmmoSection();
        GUILayout.Space(8f);
        DrawInstantReloadSection();
        GUILayout.Space(8f);
        DrawFireRateSection();
        GUILayout.Space(8f);
        DrawRainbowBulletsSection();
        GUILayout.Space(8f);
        DrawWinRoundSection();
        GUILayout.Space(8f);
        DrawCurrencySection();
        GUILayout.Space(8f);
        DrawMenuSection();

        GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
    }

    private void DrawAimAssistSection()
    {
        GUILayout.Label("Aim Assist", GUI.skin.box);
        _aimAssist.Enabled.Value = GUILayout.Toggle(_aimAssist.Enabled.Value, "Enabled");
        _aimAssist.AlwaysOn.Value = GUILayout.Toggle(_aimAssist.AlwaysOn.Value, "Always on (ignores hotkey below)");
        _aimAssist.IgnoreChronoTargets.Value = GUILayout.Toggle(_aimAssist.IgnoreChronoTargets.Value, "Ignore Chrono (time) targets");
        DrawHotkeyRow("Hotkey", ref _aimAssistHotkeyBuffer, _aimAssist.Hotkey);

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Turn rate: {_aimAssist.TurnRateDegreesPerSecond.Value:0}°/s", GUILayout.Width(160f));
        _aimAssist.TurnRateDegreesPerSecond.Value = GUILayout.HorizontalSlider(_aimAssist.TurnRateDegreesPerSecond.Value, 60f, 1000f);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Deadzone: {_aimAssist.DeadzoneDegrees.Value:0.00}°", GUILayout.Width(160f));
        _aimAssist.DeadzoneDegrees.Value = GUILayout.HorizontalSlider(_aimAssist.DeadzoneDegrees.Value, 0f, 2f);
        GUILayout.EndHorizontal();
    }

    private void DrawTriggerbotSection()
    {
        GUILayout.Label("Triggerbot", GUI.skin.box);
        _triggerbot.Enabled.Value = GUILayout.Toggle(_triggerbot.Enabled.Value, "Enabled");
        DrawHotkeyRow("Hotkey", ref _triggerbotHotkeyBuffer, _triggerbot.Hotkey);
        _triggerbot.AutoFireWithAimAssist.Value = GUILayout.Toggle(_triggerbot.AutoFireWithAimAssist.Value,
            "Auto-fire whenever Aim Assist is on target (ignores triggerbot hotkey)");

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Precision: {_triggerbot.ViewportThreshold.Value:0.000}", GUILayout.Width(160f));
        _triggerbot.ViewportThreshold.Value = GUILayout.HorizontalSlider(_triggerbot.ViewportThreshold.Value, 0.005f, 0.15f);
        GUILayout.EndHorizontal();
    }

    private void DrawInfiniteAmmoSection()
    {
        GUILayout.Label("Infinite Ammo", GUI.skin.box);
        _infiniteAmmo.Enabled.Value = GUILayout.Toggle(_infiniteAmmo.Enabled.Value, "Enabled");
    }

    private void DrawInstantReloadSection()
    {
        GUILayout.Label("Instant Reload", GUI.skin.box);
        _instantReload.Enabled.Value = GUILayout.Toggle(_instantReload.Enabled.Value, "Enabled");
        GUILayout.Label("(Speeds up the normal reload instead of skipping it - use Infinite Ammo above to never reload at all.)");
    }

    private void DrawFireRateSection()
    {
        GUILayout.Label("Fire Rate Modifier", GUI.skin.box);
        _fireRateModifier.Enabled.Value = GUILayout.Toggle(_fireRateModifier.Enabled.Value, "Enabled");

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Delta: {_fireRateModifier.Delta.Value:+0.0;-0.0;0.0}", GUILayout.Width(120f));
        if (GUILayout.Button("-", GUILayout.Width(28f)))
        {
            _fireRateModifier.Decrement();
        }
        if (GUILayout.Button("+", GUILayout.Width(28f)))
        {
            _fireRateModifier.Increment();
        }
        if (GUILayout.Button("Reset", GUILayout.Width(56f)))
        {
            _fireRateModifier.ResetDelta();
        }
        GUILayout.EndHorizontal();

        float? currentFireRate = FireRateModifier.GetCurrentBaseFireRate();
        GUILayout.Label(currentFireRate.HasValue
            ? $"Weapon's current fire rate: {currentFireRate.Value:0.0}/s"
            : "Weapon's current fire rate: n/a (no weapon)");
    }

    private void DrawRainbowBulletsSection()
    {
        GUILayout.Label("Rainbow Bullets", GUI.skin.box);
        RainbowBullets.Enabled.Value = GUILayout.Toggle(RainbowBullets.Enabled.Value,
            "Every shot triggers all effects (Explosive, Ricochet, Clone, ShinyReward)");
    }

    private void DrawWinRoundSection()
    {
        GUILayout.Label("Win Round", GUI.skin.box);
        _winRound.Enabled.Value = GUILayout.Toggle(_winRound.Enabled.Value, "Hotkey enabled");
        DrawHotkeyRow("Hotkey", ref _winRoundHotkeyBuffer, _winRound.Hotkey);
        if (GUILayout.Button("Win Round Now"))
        {
            _winRound.TryWinRound();
        }
    }

    private void DrawCurrencySection()
    {
        GUILayout.Label("Currency", GUI.skin.box);
        ScoreManager scoreManager = ScoreManager.Instance;
        GUILayout.Label(scoreManager != null ? $"Current coins: {scoreManager.Currency}" : "Current coins: n/a");

        GUILayout.BeginHorizontal();
        _coinAmountBuffer = GUILayout.TextField(_coinAmountBuffer, GUILayout.Width(80f));
        if (GUILayout.Button("Add Coins") && scoreManager != null && int.TryParse(_coinAmountBuffer, out int amount) && amount != 0)
        {
            scoreManager.AddCurrency(amount, RoundCurrencySource.Unspecified);
        }
        GUILayout.EndHorizontal();
    }

    private void DrawMenuSection()
    {
        GUILayout.Label("Menu", GUI.skin.box);
        DrawHotkeyRow("Toggle hotkey", ref _menuHotkeyBuffer, _toggleHotkey);
        GUILayout.Label("(This window also always shows while the game is paused.)");
    }

    private static void DrawHotkeyRow(string label, ref string buffer, ConfigEntry<string> target)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(90f));
        buffer = GUILayout.TextField(buffer, GUILayout.Width(90f));
        bool valid = Hotkeys.IsValid(buffer);
        GUI.enabled = valid;
        if (GUILayout.Button("Set", GUILayout.Width(40f)))
        {
            target.Value = buffer.Trim();
        }
        GUI.enabled = true;
        GUILayout.Label(valid ? "" : "invalid", GUILayout.Width(50f));
        GUILayout.EndHorizontal();
    }

    private static bool IsRoundActive()
    {
        GameManager gameManager = GameManager.Instance;
        return gameManager != null && gameManager.IsRoundActive;
    }
}
