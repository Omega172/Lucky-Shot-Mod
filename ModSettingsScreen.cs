using System.Reflection;
using HarmonyLib;
using Shooterlatro.Game;
using Shooterlatro.Score;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LuckShotMod;

// Builds a real page in the game's own pause menu - a clone of the actual Settings screen,
// gutted and repopulated with our own rows - opened/closed via the game's own screen-transition
// methods (reflected, since they're private) so it animates identically to clicking "Settings".
//
// This only touches a *clone* of the settings screen, never the original, and only reflects
// into implementation details (private fields/methods, all read-only or generic) rather than
// mutating anything about how the real settings screen works.
public class ModSettingsScreen : MonoBehaviour
{
    private static readonly FieldInfo SettingsScreenField = AccessTools.Field(typeof(SettingsMenuManager), "settingsScreen");
    private static readonly FieldInfo GeneralOptionsRootField = AccessTools.Field(typeof(SettingsMenuManager), "generalOptionsRoot");
    private static readonly FieldInfo GraphicOptionsRootField = AccessTools.Field(typeof(SettingsMenuManager), "graphicOptionsRoot");
    private static readonly FieldInfo AudioOptionsRootField = AccessTools.Field(typeof(SettingsMenuManager), "audioOptionsRoot");
    private static readonly FieldInfo InputOptionsRootField = AccessTools.Field(typeof(SettingsMenuManager), "inputOptionsRoot");
    private static readonly FieldInfo GeneralOptionsButtonField = AccessTools.Field(typeof(SettingsMenuManager), "generalOptionsButton");
    private static readonly FieldInfo GraphicOptionsButtonField = AccessTools.Field(typeof(SettingsMenuManager), "graphicOptionsButton");
    private static readonly FieldInfo AudioOptionsButtonField = AccessTools.Field(typeof(SettingsMenuManager), "audioOptionsButton");
    private static readonly FieldInfo InputOptionsButtonField = AccessTools.Field(typeof(SettingsMenuManager), "inputOptionsButton");
    private static readonly FieldInfo SettingsBackButtonField = AccessTools.Field(typeof(SettingsMenuManager), "settingsBackButton");

    private static readonly MethodInfo OpenSubScreenFromPauseMethod =
        AccessTools.Method(typeof(SettingsMenuManager), "OpenSubScreenFromPause", new[] { typeof(UIScreen) });
    private static readonly MethodInfo BackToPauseMenuFromMethod =
        AccessTools.Method(typeof(SettingsMenuManager), "BackToPauseMenuFrom", new[] { typeof(UIScreen) });

    private AimAssist _aimAssist;
    private Triggerbot _triggerbot;
    private InfiniteAmmo _infiniteAmmo;
    private InstantReload _instantReload;
    private FireRateModifier _fireRateModifier;
    private WinRound _winRound;

    private UIScreen _screen;
    private RectTransform _container;
    private Toggle _toggleTemplate;
    private Slider _sliderTemplate;
    private TMP_InputField _inputFieldTemplate;
    private Button _buttonTemplate;
    private TMP_Text _labelTemplate;
    private bool _built;

    public void Init(AimAssist aimAssist, Triggerbot triggerbot, InfiniteAmmo infiniteAmmo, InstantReload instantReload, FireRateModifier fireRateModifier, WinRound winRound)
    {
        _aimAssist = aimAssist;
        _triggerbot = triggerbot;
        _infiniteAmmo = infiniteAmmo;
        _instantReload = instantReload;
        _fireRateModifier = fireRateModifier;
        _winRound = winRound;
    }

    // Called by PauseMenuIntegration's cloned "MODS" button.
    public void Open()
    {
        Plugin.Logger.LogInfo("[ModSettingsScreen] Open() called.");
        if (!_built)
        {
            bool built;
            try
            {
                built = TryBuild();
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError($"[ModSettingsScreen] TryBuild() threw: {ex}");
                built = false;
            }
            if (!built)
            {
                Plugin.Logger.LogWarning("[ModSettingsScreen] TryBuild() failed - see preceding log for which step. Not opening.");
                return;
            }
        }
        SettingsMenuManager settings = SettingsMenuManager.Instance;
        if (settings == null)
        {
            Plugin.Logger.LogWarning("[ModSettingsScreen] Open() aborted: SettingsMenuManager.Instance is null.");
            return;
        }
        if (OpenSubScreenFromPauseMethod == null)
        {
            Plugin.Logger.LogWarning("[ModSettingsScreen] Open() aborted: OpenSubScreenFromPauseMethod is null (reflection failed to find the method).");
            return;
        }
        Plugin.Logger.LogInfo("[ModSettingsScreen] Invoking OpenSubScreenFromPause.");
        try
        {
            OpenSubScreenFromPauseMethod.Invoke(settings, new object[] { _screen });
        }
        catch (System.Exception ex)
        {
            Plugin.Logger.LogError($"[ModSettingsScreen] OpenSubScreenFromPause threw: {ex}");
        }
    }

    private void Back()
    {
        SettingsMenuManager settings = SettingsMenuManager.Instance;
        if (settings != null)
        {
            BackToPauseMenuFromMethod?.Invoke(settings, new object[] { _screen });
        }
    }

    private bool TryBuild()
    {
        Plugin.Logger.LogInfo("[ModSettingsScreen] TryBuild() starting.");

        if (OpenSubScreenFromPauseMethod == null || BackToPauseMenuFromMethod == null)
        {
            Plugin.Logger.LogWarning($"[ModSettingsScreen] Abort: reflection failed to find OpenSubScreenFromPause ({OpenSubScreenFromPauseMethod != null}) or BackToPauseMenuFrom ({BackToPauseMenuFromMethod != null}).");
            return false;
        }

        SettingsMenuManager settings = SettingsMenuManager.Instance;
        if (settings == null)
        {
            Plugin.Logger.LogWarning("[ModSettingsScreen] Abort: SettingsMenuManager.Instance is null.");
            return false;
        }

        UIScreen original = SettingsScreenField?.GetValue(settings) as UIScreen;
        if (original == null)
        {
            Plugin.Logger.LogWarning($"[ModSettingsScreen] Abort: settingsScreen field {(SettingsScreenField == null ? "not found by reflection" : "resolved to null")}.");
            return false;
        }
        Plugin.Logger.LogInfo("[ModSettingsScreen] Found real settings screen, harvesting templates.");

        // Harvest reusable templates from the real screen before we start gutting the clone.
        _toggleTemplate = original.GetComponentInChildren<Toggle>(true);
        _sliderTemplate = original.GetComponentInChildren<Slider>(true);
        _inputFieldTemplate = original.GetComponentInChildren<TMP_InputField>(true);
        _buttonTemplate = original.GetComponentInChildren<Button>(true);
        _labelTemplate = FindPlainLabel(original.transform);
        if (_toggleTemplate == null || _sliderTemplate == null || _buttonTemplate == null || _labelTemplate == null)
        {
            Plugin.Logger.LogWarning($"[ModSettingsScreen] Abort: template harvest incomplete - toggle={_toggleTemplate != null}, slider={_sliderTemplate != null}, inputField={_inputFieldTemplate != null}, button={_buttonTemplate != null}, label={_labelTemplate != null}.");
            return false;
        }
        Plugin.Logger.LogInfo("[ModSettingsScreen] Templates harvested, cloning screen.");

        _screen = Instantiate(original, original.transform.parent);
        _screen.gameObject.name = "LuckShotMod_ModsScreen";
        _screen.gameObject.SetActive(false);
        Transform cloneRoot = _screen.transform;

        HideCounterpart(cloneRoot, GraphicOptionsRootField, settings);
        HideCounterpart(cloneRoot, AudioOptionsRootField, settings);
        HideCounterpart(cloneRoot, InputOptionsRootField, settings);
        HideCounterpart(cloneRoot, GeneralOptionsButtonField, settings);
        HideCounterpart(cloneRoot, GraphicOptionsButtonField, settings);
        HideCounterpart(cloneRoot, AudioOptionsButtonField, settings);
        HideCounterpart(cloneRoot, InputOptionsButtonField, settings);

        GameObject generalRootOriginal = GeneralOptionsRootField?.GetValue(settings) as GameObject;
        Transform containerTransform = generalRootOriginal != null ? UiUtil.FindChildByName(cloneRoot, generalRootOriginal.name) : null;
        if (containerTransform == null)
        {
            Plugin.Logger.LogWarning($"[ModSettingsScreen] Abort: could not find cloned counterpart of generalOptionsRoot (original name: '{generalRootOriginal?.name ?? "null"}').");
            Destroy(_screen.gameObject);
            _screen = null;
            return false;
        }
        Plugin.Logger.LogInfo($"[ModSettingsScreen] Found container '{containerTransform.name}', building rows.");
        containerTransform.gameObject.SetActive(true);
        _container = containerTransform as RectTransform;

        for (int i = containerTransform.childCount - 1; i >= 0; i--)
        {
            Destroy(containerTransform.GetChild(i).gameObject);
        }

        VerticalLayoutGroup layout = containerTransform.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = containerTransform.gameObject.AddComponent<VerticalLayoutGroup>();
        }
        layout.spacing = 6f;
        layout.childControlHeight = false;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childAlignment = TextAnchor.UpperLeft;
        ContentSizeFitter fitter = containerTransform.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = containerTransform.gameObject.AddComponent<ContentSizeFitter>();
        }
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Button backButtonOriginal = SettingsBackButtonField?.GetValue(settings) as Button;
        Transform backTransform = backButtonOriginal != null ? UiUtil.FindChildByName(cloneRoot, backButtonOriginal.gameObject.name) : null;
        Button backButtonClone = backTransform != null ? backTransform.GetComponent<Button>() : null;
        if (backButtonClone != null)
        {
            UiUtil.ReplaceButton(backButtonClone, Back);
        }
        else
        {
            Plugin.Logger.LogWarning($"[ModSettingsScreen] Could not find/rewire back button (original name: '{backButtonOriginal?.gameObject.name ?? "null"}') - screen will open but Back may not work.");
        }

        BuildRows();

        _built = true;
        Plugin.Logger.LogInfo("[ModSettingsScreen] TryBuild() succeeded.");
        return true;
    }

    private void BuildRows()
    {
        AddSectionLabel("Aim Assist");
        AddToggleRow("Enabled", _aimAssist.Enabled);
        AddToggleRow("Always On", _aimAssist.AlwaysOn);
        AddToggleRow("Ignore Chrono Targets", _aimAssist.IgnoreChronoTargets);
        AddHotkeyRow("Hotkey", _aimAssist.Hotkey);
        AddSliderRow("Turn Rate", 60f, 1000f, _aimAssist.TurnRateDegreesPerSecond);
        AddSliderRow("Deadzone", 0f, 2f, _aimAssist.DeadzoneDegrees);

        AddSectionLabel("Triggerbot");
        AddToggleRow("Enabled", _triggerbot.Enabled);
        AddToggleRow("Auto-fire With Aim Assist", _triggerbot.AutoFireWithAimAssist);
        AddHotkeyRow("Hotkey", _triggerbot.Hotkey);
        AddSliderRow("Precision", 0.005f, 0.15f, _triggerbot.ViewportThreshold);

        AddSectionLabel("Infinite Ammo");
        AddToggleRow("Enabled", _infiniteAmmo.Enabled);

        AddSectionLabel("Instant Reload");
        AddToggleRow("Enabled", _instantReload.Enabled);

        AddSectionLabel("Fire Rate Modifier");
        AddToggleRow("Enabled", _fireRateModifier.Enabled);
        AddSliderRow("Delta", -20f, 20f, _fireRateModifier.Delta);

        AddSectionLabel("Rainbow Bullets");
        AddToggleRow("All Effects On Every Shot", RainbowBullets.Enabled);

        AddSectionLabel("Win Round");
        AddToggleRow("Hotkey Enabled", _winRound.Enabled);
        AddHotkeyRow("Hotkey", _winRound.Hotkey);
        AddButtonRow("Win Round Now", _winRound.TryWinRound);

        AddSectionLabel("Currency");
        AddButtonRow("Add 500 Coins", () => AddCoins(500));
        AddButtonRow("Add 5000 Coins", () => AddCoins(5000));
    }

    private static void AddCoins(int amount)
    {
        ScoreManager scoreManager = ScoreManager.Instance;
        if (scoreManager != null)
        {
            scoreManager.AddCurrency(amount, RoundCurrencySource.Unspecified);
        }
    }

    private void AddSectionLabel(string text)
    {
        TMP_Text label = Instantiate(_labelTemplate, _container);
        label.gameObject.name = "LuckShotMod_Section";
        StripLocalization(label);
        label.text = text;
        label.fontStyle = FontStyles.Bold;
        SetRowHeight(label.rectTransform, 32f);
    }

    private void AddToggleRow(string labelText, BepInEx.Configuration.ConfigEntry<bool> entry)
    {
        RectTransform row = CreateRow();
        CreateLabel(row, labelText);

        Toggle toggle = Instantiate(_toggleTemplate, row);
        toggle.gameObject.name = "LuckShotMod_Toggle";
        StripLocalization(toggle);
        UiUtil.ReplaceToggle(toggle, entry.Value, value => entry.Value = value);
        SetControlWidth(toggle.GetComponent<RectTransform>(), 40f);
    }

    private void AddSliderRow(string labelText, float min, float max, BepInEx.Configuration.ConfigEntry<float> entry)
    {
        RectTransform row = CreateRow();
        CreateLabel(row, labelText);

        Slider slider = Instantiate(_sliderTemplate, row);
        slider.gameObject.name = "LuckShotMod_Slider";
        StripLocalization(slider);
        UiUtil.ReplaceSlider(slider, min, max, entry.Value, value => entry.Value = value);
        SetControlWidth(slider.GetComponent<RectTransform>(), 200f);
    }

    private void AddHotkeyRow(string labelText, BepInEx.Configuration.ConfigEntry<string> entry)
    {
        RectTransform row = CreateRow();
        CreateLabel(row, labelText);

        TMP_InputField field = Instantiate(_inputFieldTemplate, row);
        field.gameObject.name = "LuckShotMod_InputField";
        StripLocalization(field);
        UiUtil.ReplaceInputField(field, entry.Value, text =>
        {
            if (Hotkeys.IsValid(text))
            {
                entry.Value = text.Trim();
            }
            else
            {
                field.text = entry.Value;
            }
        });
        SetControlWidth(field.GetComponent<RectTransform>(), 140f);
    }

    private void AddButtonRow(string labelText, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform row = CreateRow();

        Button button = Instantiate(_buttonTemplate, row);
        button.gameObject.name = "LuckShotMod_Button";
        UiUtil.ReplaceButton(button, onClick);

        TMP_Text buttonLabel = button.GetComponentInChildren<TMP_Text>(true);
        if (buttonLabel != null)
        {
            StripLocalization(buttonLabel);
            buttonLabel.text = labelText;
        }
        SetControlWidth(button.GetComponent<RectTransform>(), 220f);
    }

    private RectTransform CreateRow()
    {
        GameObject rowObject = new GameObject("LuckShotMod_Row", typeof(RectTransform));
        rowObject.transform.SetParent(_container, false);
        HorizontalLayoutGroup layout = rowObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = 10f;
        layout.childControlHeight = true;
        layout.childControlWidth = false;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        RectTransform rect = rowObject.GetComponent<RectTransform>();
        SetRowHeight(rect, 30f);
        return rect;
    }

    private void CreateLabel(RectTransform row, string text)
    {
        TMP_Text label = Instantiate(_labelTemplate, row);
        label.gameObject.name = "LuckShotMod_Label";
        StripLocalization(label);
        label.text = text;
        label.fontStyle = FontStyles.Normal;
        SetControlWidth(label.rectTransform, 220f);
    }

    private static void SetRowHeight(RectTransform rect, float height)
    {
        LayoutElement element = rect.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>();
        element.minHeight = height;
        element.preferredHeight = height;
    }

    private static void SetControlWidth(RectTransform rect, float width)
    {
        LayoutElement element = rect.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>();
        element.minWidth = width;
        element.preferredWidth = width;
        element.flexibleWidth = 0f;
    }

    // Prevents a cloned label from getting its text stamped back to the original's localized
    // value the first time its GameObject actually enables (see PauseMenuIntegration for the
    // same issue with the Resume button's label).
    private static void StripLocalization(Component component)
    {
        foreach (Shooterlatro.UI.Localization.LocalizedTMPText localizedText in component.GetComponentsInChildren<Shooterlatro.UI.Localization.LocalizedTMPText>(true))
        {
            localizedText.ClearLocalization();
        }
    }

    private static TMP_Text FindPlainLabel(Transform root)
    {
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text != null && text.GetComponentInParent<Selectable>(true) == null)
            {
                return text;
            }
        }
        return null;
    }

    private static void HideCounterpart(Transform cloneRoot, FieldInfo field, SettingsMenuManager settings)
    {
        if (field == null)
        {
            return;
        }
        Component original = field.GetValue(settings) as Component;
        GameObject originalObject = original != null ? original.gameObject : field.GetValue(settings) as GameObject;
        if (originalObject == null)
        {
            return;
        }
        Transform counterpart = UiUtil.FindChildByName(cloneRoot, originalObject.name);
        if (counterpart != null)
        {
            counterpart.gameObject.SetActive(false);
        }
    }
}
