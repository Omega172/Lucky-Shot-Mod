using BepInEx.Configuration;
using Shooterlatro.Game;
using Shooterlatro.Gameplay;
using Shooterlatro.Weapons;
using UnityEngine;

namespace LuckShotMod;

// Fires automatically whenever the crosshair (screen center) is over a live target.
// Two independent ways to trigger it:
//   1. Its own hotkey (default Mouse5) - hold it, look at a target, it fires.
//   2. AimAssist's "auto fire" toggle, which calls TryAutoFireForAimAssist() every frame
//      AimAssist is actively engaged, completely ignoring this component's own hotkey.
public class Triggerbot : MonoBehaviour
{
    public static Triggerbot Instance { get; private set; }

    public ConfigEntry<bool> Enabled { get; private set; }
    public ConfigEntry<string> Hotkey { get; private set; }
    public ConfigEntry<float> ViewportThreshold { get; private set; }
    public ConfigEntry<bool> AutoFireWithAimAssist { get; private set; }

    public void Init(ConfigFile config)
    {
        Enabled = config.Bind("Triggerbot", "Enabled", false,
            "Enable the triggerbot: while the hotkey is held and the crosshair is over a live target, fire automatically.");
        Hotkey = config.Bind("Triggerbot", "Hotkey", "Mouse5",
            "Hotkey held to trigger auto-fire. Examples: Mouse5, Mouse4, Mouse2, LeftAlt, F.");
        ViewportThreshold = config.Bind("Triggerbot", "ViewportThreshold", 0.03f,
            "How close to dead-center (0-0.5, fraction of screen) a target must be to count as \"under the crosshair\".");
        AutoFireWithAimAssist = config.Bind("Triggerbot", "AutoFireWithAimAssist", false,
            "While enabled, fire automatically whenever AimAssist is actively aimed at a target - independent of this component's own hotkey.");
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (!Enabled.Value)
        {
            return;
        }

        if (!IsRoundActive())
        {
            return;
        }

        if (Hotkeys.IsHeld(Hotkey.Value))
        {
            TryFireIfCrosshairOnTarget();
        }
    }

    // Called by AimAssist - deliberately does not check this.Hotkey.
    public void TryAutoFireForAimAssist()
    {
        if (!AutoFireWithAimAssist.Value || !IsRoundActive())
        {
            return;
        }
        TryFireIfCrosshairOnTarget();
    }

    private static bool IsRoundActive()
    {
        GameManager gameManager = GameManager.Instance;
        return gameManager != null && gameManager.IsRoundActive;
    }

    private void TryFireIfCrosshairOnTarget()
    {
        Weapon weapon = Weapon.Instance;
        Camera aimCamera = weapon != null ? weapon.AimCamera : null;
        if (weapon == null || aimCamera == null)
        {
            return;
        }

        if (IsCrosshairOverTarget(weapon, aimCamera))
        {
            weapon.DebugTryFireOnce();
        }
    }

    private bool IsCrosshairOverTarget(Weapon weapon, Camera aimCamera)
    {
        var activeTargets = Target.ActiveTargets;
        Vector2 crosshair = ResolvePredictedCrosshair(weapon, aimCamera);
        float thresholdSqr = ViewportThreshold.Value * ViewportThreshold.Value;

        for (int i = 0; i < activeTargets.Count; i++)
        {
            Target candidate = activeTargets[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy || !candidate.IsAlive)
            {
                continue;
            }

            Vector3 viewportPoint = aimCamera.WorldToViewportPoint(candidate.transform.position);
            if (viewportPoint.z <= 0f)
            {
                continue;
            }

            float sqrDistance = Vector2.SqrMagnitude(new Vector2(viewportPoint.x, viewportPoint.y) - crosshair);
            if (sqrDistance <= thresholdSqr)
            {
                return true;
            }
        }

        return false;
    }

    // Screen center is where the camera points, not where a shot fired right now would
    // actually go - this game's recoil bends the bullet's direction away from the camera
    // without moving it. We forward-apply the weapon's current recoil pattern to the camera's
    // forward direction to predict the real shot direction, then use its viewport position as
    // the effective "crosshair" instead of the raw (0.5, 0.5) screen center.
    private static Vector2 ResolvePredictedCrosshair(Weapon weapon, Camera aimCamera)
    {
        Vector2 screenCenter = new Vector2(0.5f, 0.5f);
        if (!RecoilCompensation.TryGetPatternDegrees(weapon, out float horizontalDeg, out float verticalDeg))
        {
            return screenCenter;
        }
        if (Mathf.Approximately(horizontalDeg, 0f) && Mathf.Approximately(verticalDeg, 0f))
        {
            return screenCenter;
        }

        Transform origin = aimCamera.transform;
        RecoilCompensation.ResolveAxes(weapon, origin, out Vector3 up, out Vector3 right);
        Vector3 predictedDirection = RecoilCompensation.ApplyPattern(origin.forward, up, right, horizontalDeg, verticalDeg);

        Vector3 predictedViewportPoint = aimCamera.WorldToViewportPoint(origin.position + predictedDirection * 100f);
        if (predictedViewportPoint.z <= 0f)
        {
            return screenCenter;
        }
        return new Vector2(predictedViewportPoint.x, predictedViewportPoint.y);
    }
}
