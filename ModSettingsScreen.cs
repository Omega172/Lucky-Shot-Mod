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
        // Search inside generalOptionsRoot specifically, not the whole screen - a whole-screen
        // search can find the tab bar's own (much larger, differently-colored) label text
        // instead of an actual row label like "FOV"/"WEAPON POSITION".
        GameObject generalRootForLabel = GeneralOptionsRootField?.GetValue(settings) as GameObject;
        _labelTemplate = generalRootForLabel != null ? FindPlainLabel(generalRootForLabel.transform) : FindPlainLabel(original.transform);
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
        layout.spacing = 8f;
        layout.padding = new RectOffset(4, 4, 4, 4);
        // childControlHeight must be true, or each row keeps its GameObject-default
        // RectTransform size instead of the height our own LayoutElement asks for.
        layout.childControlHeight = true;
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

    // Matches the real settings screen's two-column look (label list on the left, controls on
    // the right, full row width) rather than tightly packing label+control together.
    private const float ColumnSplit = 0.52f;
    private const float LabelFontSize = 24f;
    private static readonly Color LabelColor = new Color(0.6f, 0.92f, 1f);
    private static readonly Color SectionColor = new Color(0.78f, 1f, 0.3f);

    private void AddSectionLabel(string text)
    {
        RectTransform row = CreateRow(44f);
        TMP_Text label = Instantiate(_labelTemplate, row);
        label.gameObject.name = "LuckShotMod_Section";
        StripLocalization(label);
        ConfigureLabelText(label, text, FontStyles.Bold, SectionColor);
        AnchorStretch(label.rectTransform, 0f, 1f, 0f);
    }

    private void AddToggleRow(string labelText, BepInEx.Configuration.ConfigEntry<bool> entry)
    {
        RectTransform row = CreateRow(38f);
        AddRowLabel(row, labelText);

        Toggle toggleTemplate = Instantiate(_toggleTemplate, row);
        toggleTemplate.gameObject.name = "LuckShotMod_Toggle";
        StripLocalization(toggleTemplate);
        // ReplaceToggle destroys toggleTemplate and returns a fresh component on the same
        // GameObject - everything after this must use the returned reference, not the
        // (now-destroyed) local we passed in.
        Toggle toggle = UiUtil.ReplaceToggle(toggleTemplate, entry.Value, value => entry.Value = value);
        AnchorFixed(toggle.GetComponent<RectTransform>(), ColumnSplit, 28f, 28f);
    }

    private void AddSliderRow(string labelText, float min, float max, BepInEx.Configuration.ConfigEntry<float> entry)
    {
        RectTransform row = CreateRow(38f);
        AddRowLabel(row, labelText);

        Slider sliderTemplate = Instantiate(_sliderTemplate, row);
        sliderTemplate.gameObject.name = "LuckShotMod_Slider";
        StripLocalization(sliderTemplate);
        Slider slider = UiUtil.ReplaceSlider(sliderTemplate, min, max, entry.Value, value => entry.Value = value);
        AnchorStretch(slider.GetComponent<RectTransform>(), ColumnSplit, 0.98f, 0.3f);
    }

    private void AddHotkeyRow(string labelText, BepInEx.Configuration.ConfigEntry<string> entry)
    {
        RectTransform row = CreateRow(38f);
        AddRowLabel(row, labelText);

        TMP_InputField fieldTemplate = Instantiate(_inputFieldTemplate, row);
        fieldTemplate.gameObject.name = "LuckShotMod_InputField";
        StripLocalization(fieldTemplate);
        TMP_InputField field = null;
        field = UiUtil.ReplaceInputField(fieldTemplate, entry.Value, text =>
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
        AnchorFixed(field.GetComponent<RectTransform>(), ColumnSplit, 160f, 32f);
    }

    private void AddButtonRow(string labelText, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform row = CreateRow(40f);

        Button buttonTemplate = Instantiate(_buttonTemplate, row);
        buttonTemplate.gameObject.name = "LuckShotMod_Button";
        Button button = UiUtil.ReplaceButton(buttonTemplate, onClick);
        AnchorStretch(button.GetComponent<RectTransform>(), 0f, ColumnSplit, 0.05f);

        TMP_Text buttonLabel = button.GetComponentInChildren<TMP_Text>(true);
        if (buttonLabel != null)
        {
            StripLocalization(buttonLabel);
            buttonLabel.text = labelText;
        }
    }

    private RectTransform CreateRow(float height)
    {
        GameObject rowObject = new GameObject("LuckShotMod_Row", typeof(RectTransform));
        RectTransform rect = rowObject.GetComponent<RectTransform>();
        rect.SetParent(_container, false);
        LayoutElement element = rowObject.AddComponent<LayoutElement>();
        element.minHeight = height;
        element.preferredHeight = height;
        return rect;
    }

    private void AddRowLabel(RectTransform row, string text)
    {
        TMP_Text label = Instantiate(_labelTemplate, row);
        label.gameObject.name = "LuckShotMod_Label";
        StripLocalization(label);
        ConfigureLabelText(label, text, FontStyles.Normal, LabelColor);
        AnchorStretch(label.rectTransform, 0f, ColumnSplit - 0.02f, 0f);
    }

    private static void ConfigureLabelText(TMP_Text label, string text, FontStyles style, Color color)
    {
        label.text = text;
        label.fontStyle = style;
        label.color = color;
        label.fontSize = LabelFontSize;
        label.enableAutoSizing = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.alignment = TextAlignmentOptions.MidlineLeft;
    }

    // Stretches rect to fill [minX, maxX] of its parent's width, full height minus verticalPad
    // fraction top/bottom.
    private static void AnchorStretch(RectTransform rect, float minX, float maxX, float verticalPad)
    {
        rect.anchorMin = new Vector2(minX, verticalPad);
        rect.anchorMax = new Vector2(maxX, 1f - verticalPad);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    // Places rect at a fixed pixel size, left-anchored at columnX of its parent's width,
    // vertically centered.
    private static void AnchorFixed(RectTransform rect, float columnX, float width, float height)
    {
        rect.anchorMin = new Vector2(columnX, 0.5f);
        rect.anchorMax = new Vector2(columnX, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = Vector2.zero;
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
