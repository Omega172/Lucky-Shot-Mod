using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LuckShotMod;

// Shared helpers for cloning pieces of the game's own UGUI and repurposing them.
//
// Cloned interactive controls (Button/Toggle/Slider/TMP_InputField) come with their original
// click/value-changed behavior still wired as *persistent* listeners - Unity's own docs are
// explicit that UnityEvent.RemoveAllListeners() only clears runtime-added listeners, leaving
// Editor-wired persistent ones in place. So instead of trying to clear a cloned control's
// event, these helpers destroy the component and add a fresh one, manually carrying over every
// visual/wiring field that lived on the destroyed instance (nothing else references those
// fields, so they'd otherwise be silently lost) before attaching our own listener.
public static class UiUtil
{
    public static Transform FindChildByName(Transform root, string name)
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

    // Destroys a component of type T on go, if present. Used to remove game-specific companion
    // scripts (e.g. [RequireComponent(typeof(Toggle))] wrappers) that would otherwise block
    // destroying the control component they depend on.
    private static void DestroyIfPresent<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        if (component != null)
        {
            Object.DestroyImmediate(component);
        }
    }

    public static Button ReplaceButton(Button old, UnityAction onClick)
    {
        GameObject go = old.gameObject;
        Graphic targetGraphic = old.targetGraphic;
        Selectable.Transition transition = old.transition;
        ColorBlock colors = old.colors;
        SpriteState spriteState = old.spriteState;
        Navigation navigation = old.navigation;

        Object.DestroyImmediate(old);

        Button fresh = go.AddComponent<Button>();
        fresh.targetGraphic = targetGraphic;
        fresh.transition = transition;
        fresh.colors = colors;
        fresh.spriteState = spriteState;
        fresh.navigation = navigation;
        fresh.onClick.AddListener(onClick);
        return fresh;
    }

    public static Toggle ReplaceToggle(Toggle old, bool initialValue, UnityAction<bool> onValueChanged)
    {
        GameObject go = old.gameObject;
        Graphic targetGraphic = old.targetGraphic;
        Graphic checkGraphic = old.graphic;
        ToggleGroup group = old.group;
        Selectable.Transition transition = old.transition;
        ColorBlock colors = old.colors;
        SpriteState spriteState = old.spriteState;
        Navigation navigation = old.navigation;

        // Same [RequireComponent(typeof(Toggle))] problem as SliderValueInput has for
        // TMP_InputField - a few of the settings screen's toggles have one of these game-
        // specific companion scripts attached, which blocks destroying the Toggle unless
        // it's removed first.
        DestroyIfPresent<Shooterlatro.UI.InvertedAimToggle>(go);
        DestroyIfPresent<Shooterlatro.UI.ControllerAimAssistToggle>(go);
        DestroyIfPresent<Shooterlatro.UI.NextBulletIndicatorToggle>(go);

        Object.DestroyImmediate(old);

        Toggle fresh = go.AddComponent<Toggle>();
        fresh.targetGraphic = targetGraphic;
        fresh.graphic = checkGraphic;
        fresh.group = group;
        fresh.transition = transition;
        fresh.colors = colors;
        fresh.spriteState = spriteState;
        fresh.navigation = navigation;
        fresh.isOn = initialValue;
        fresh.onValueChanged.AddListener(onValueChanged);
        return fresh;
    }

    public static Slider ReplaceSlider(Slider old, float min, float max, float initialValue, UnityAction<float> onValueChanged)
    {
        GameObject go = old.gameObject;
        Graphic targetGraphic = old.targetGraphic;
        RectTransform fillRect = old.fillRect;
        RectTransform handleRect = old.handleRect;
        Slider.Direction direction = old.direction;
        Selectable.Transition transition = old.transition;
        ColorBlock colors = old.colors;
        SpriteState spriteState = old.spriteState;
        Navigation navigation = old.navigation;

        Object.DestroyImmediate(old);

        Slider fresh = go.AddComponent<Slider>();
        fresh.targetGraphic = targetGraphic;
        fresh.fillRect = fillRect;
        fresh.handleRect = handleRect;
        fresh.direction = direction;
        fresh.transition = transition;
        fresh.colors = colors;
        fresh.spriteState = spriteState;
        fresh.navigation = navigation;
        fresh.minValue = min;
        fresh.maxValue = max;
        fresh.wholeNumbers = false;
        fresh.value = initialValue;
        fresh.onValueChanged.AddListener(onValueChanged);
        return fresh;
    }

    public static TMP_InputField ReplaceInputField(TMP_InputField old, string initialText, UnityAction<string> onEndEdit)
    {
        GameObject go = old.gameObject;
        Graphic targetGraphic = old.targetGraphic;
        TMP_Text textComponent = old.textComponent;
        RectTransform textViewport = old.textViewport;
        Graphic placeholder = old.placeholder;
        Selectable.Transition transition = old.transition;
        ColorBlock colors = old.colors;
        SpriteState spriteState = old.spriteState;
        Navigation navigation = old.navigation;
        TMP_InputField.ContentType contentType = old.contentType;

        // The settings screen's input fields are all paired with a SliderValueInput companion
        // script that has [RequireComponent(typeof(TMP_InputField))] - Unity refuses to destroy
        // a component another one on the same GameObject requires, so that has to go first.
        DestroyIfPresent<Shooterlatro.UI.SliderValueInput>(go);

        Object.DestroyImmediate(old);

        TMP_InputField fresh = go.AddComponent<TMP_InputField>();
        fresh.targetGraphic = targetGraphic;
        fresh.textViewport = textViewport;
        fresh.textComponent = textComponent;
        fresh.placeholder = placeholder;
        fresh.transition = transition;
        fresh.colors = colors;
        fresh.spriteState = spriteState;
        fresh.navigation = navigation;
        fresh.contentType = contentType;
        fresh.text = initialText;
        fresh.onEndEdit.AddListener(onEndEdit);
        return fresh;
    }
}
