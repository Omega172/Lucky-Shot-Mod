using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace LuckShotMod;

// Parses simple binding strings ("Mouse2", "Mouse5", "F1", "LeftShift", ...) so mod
// features can expose a rebindable hotkey via a plain BepInEx string config entry
// instead of a hardcoded key.
public static class Hotkeys
{
    public static bool IsHeld(string binding)
    {
        ButtonControl control = Resolve(binding);
        return control != null && control.isPressed;
    }

    public static bool WasPressedThisFrame(string binding)
    {
        ButtonControl control = Resolve(binding);
        return control != null && control.wasPressedThisFrame;
    }

    // Returns null (never matches) for an empty/unrecognized binding instead of throwing,
    // so a bad value typed into the menu just disables that hotkey rather than crashing.
    private static ButtonControl Resolve(string binding)
    {
        if (string.IsNullOrWhiteSpace(binding))
        {
            return null;
        }

        string trimmed = binding.Trim();

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            switch (trimmed.ToLowerInvariant())
            {
                case "mouse1":
                case "mouseleft":
                    return mouse.leftButton;
                case "mouse2":
                case "mouseright":
                    return mouse.rightButton;
                case "mouse3":
                case "mousemiddle":
                    return mouse.middleButton;
                case "mouse4":
                case "mouseback":
                    return mouse.backButton;
                case "mouse5":
                case "mouseforward":
                    return mouse.forwardButton;
            }
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && Enum.TryParse(trimmed, true, out Key key))
        {
            return keyboard[key];
        }

        return null;
    }

    // Used by the menu to validate a binding the user typed in before accepting it.
    public static bool IsValid(string binding)
    {
        if (string.IsNullOrWhiteSpace(binding))
        {
            return false;
        }
        string trimmed = binding.Trim();
        switch (trimmed.ToLowerInvariant())
        {
            case "mouse1":
            case "mouseleft":
            case "mouse2":
            case "mouseright":
            case "mouse3":
            case "mousemiddle":
            case "mouse4":
            case "mouseback":
            case "mouse5":
            case "mouseforward":
                return true;
        }
        return Enum.TryParse(trimmed, true, out Key _);
    }
}
