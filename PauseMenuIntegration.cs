using System.Reflection;
using HarmonyLib;
using Shooterlatro.UI.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LuckShotMod;

// Best-effort attempt to add a real button into the game's own pause menu (a clone of the
// existing "Resume" button, for matching style/layout/animation) that opens ModMenu.
//
// SettingsMenuManager's pause screen is entirely private with no public API for adding
// entries, so this relies on reflecting one private field (pauseMenuScreen) and searching
// its hierarchy by GameObject name (the same technique the game's own code uses internally,
// e.g. ResolveExitToMenuOption searches for a child literally named "ExitToMenu"). If any of
// this breaks on a game update, injection just silently fails - the F1 hotkey and
// pause-triggered ModMenu overlay keep working regardless, this is purely additive.
public class PauseMenuIntegration : MonoBehaviour
{
    private const string InjectedButtonName = "LuckShotMod_OpenMenuButton";
    private const string ResumeButtonName = "Resume";

    private static readonly FieldInfo PauseMenuScreenField = AccessTools.Field(typeof(SettingsMenuManager), "pauseMenuScreen");

    private ModMenu _modMenu;
    private bool _injected;

    public void Init(ModMenu modMenu)
    {
        _modMenu = modMenu;
    }

    private void Update()
    {
        if (!_injected)
        {
            TryInject();
        }
    }

    private void TryInject()
    {
        UIScreen pauseScreen = ResolvePauseScreen();
        if (pauseScreen == null)
        {
            return;
        }

        Transform resumeTransform = FindChildByName(pauseScreen.transform, ResumeButtonName);
        if (resumeTransform == null)
        {
            return;
        }

        if (resumeTransform.parent != null && resumeTransform.parent.Find(InjectedButtonName) != null)
        {
            _injected = true;
            return;
        }

        Button template = resumeTransform.GetComponent<Button>() ?? resumeTransform.GetComponentInChildren<Button>(true);
        if (template == null)
        {
            return;
        }

        Button clone = Instantiate(template, template.transform.parent);
        GameObject cloneObject = clone.gameObject;
        cloneObject.name = InjectedButtonName;
        ReplaceOnClickWithFreshButton(clone);

        TMP_Text label = cloneObject.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            // The real Resume label has a LocalizedTMPText component that re-stamps its text
            // from a localization key on OnEnable/language-change - which happens *after* we
            // set our own text here, since the whole pause screen (and this freshly cloned
            // child with it) starts inactive and only actually enables the first time the
            // player opens pause, silently reverting "MODS" back to "Resume" moments later.
            // ClearLocalization() makes ApplyLocalization() a permanent no-op going forward.
            LocalizedTMPText localizedText = label.GetComponent<LocalizedTMPText>();
            if (localizedText != null)
            {
                localizedText.ClearLocalization();
            }
            label.text = "MODS";
        }

        cloneObject.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);
        _injected = true;
    }

    // Button.onClick.RemoveAllListeners() only clears runtime (AddListener) listeners - it
    // deliberately leaves Editor-wired *persistent* listeners in place (that's Unity's own
    // documented behavior), and the real Resume button's actual resume action is exactly that
    // kind of listener. So instead of trying to clear the cloned onClick, replace the whole
    // Button component with a fresh one (guaranteed-empty onClick), carrying over the visual
    // interaction state so it still looks and feels like a real button.
    private void ReplaceOnClickWithFreshButton(Button oldButton)
    {
        GameObject targetObject = oldButton.gameObject;
        Graphic targetGraphic = oldButton.targetGraphic;
        Selectable.Transition transition = oldButton.transition;
        ColorBlock colors = oldButton.colors;
        SpriteState spriteState = oldButton.spriteState;
        Navigation navigation = oldButton.navigation;

        DestroyImmediate(oldButton);

        Button freshButton = targetObject.AddComponent<Button>();
        freshButton.targetGraphic = targetGraphic;
        freshButton.transition = transition;
        freshButton.colors = colors;
        freshButton.spriteState = spriteState;
        freshButton.navigation = navigation;
        freshButton.onClick.AddListener(_modMenu.ToggleOpen);
    }

    private static UIScreen ResolvePauseScreen()
    {
        if (PauseMenuScreenField == null)
        {
            return null;
        }
        SettingsMenuManager settings = SettingsMenuManager.Instance;
        return settings != null ? PauseMenuScreenField.GetValue(settings) as UIScreen : null;
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != null && child.name == name)
            {
                return child;
            }
        }
        return null;
    }
}
