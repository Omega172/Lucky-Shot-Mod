using System.Reflection;
using HarmonyLib;
using Shooterlatro.Weapons;
using UnityEngine;

namespace LuckShotMod;

// Shared recoil-pattern math so AimAssist (which needs to know where to aim so recoil lands
// on target) and Triggerbot (which needs to know where a shot fired right now would actually
// go) agree on the same numbers instead of duplicating the reflection/rotation logic.
//
// This game's recoil never moves the camera - RecoilController bends the fired bullet's
// direction away from wherever the camera points, using a deterministic pattern
// (RecoilHorizontalDeg/VerticalDeg, looked up by consecutive-shot chain index) plus a random
// spread/bloom component. Only the deterministic pattern is compensated here; spread is
// genuine per-shot accuracy randomness, not something there's anything to "correct".
public static class RecoilCompensation
{
    private static readonly FieldInfo RecoilControllerField = AccessTools.Field(typeof(Weapon), "_recoilController");

    public static bool TryGetPatternDegrees(Weapon weapon, out float horizontalDeg, out float verticalDeg)
    {
        horizontalDeg = 0f;
        verticalDeg = 0f;

        RecoilController recoilController = weapon != null ? RecoilControllerField?.GetValue(weapon) as RecoilController : null;
        if (recoilController == null)
        {
            return false;
        }

        Transform aimReference = weapon.UnshakenAimReference;
        Vector3 baseDirection = aimReference != null ? aimReference.forward : weapon.transform.forward;

        // Read-only in effect: mirrors exactly what a real shot computes, and any pending-shot
        // bookkeeping it touches gets overwritten again before a real shot ever consumes it
        // (see RecoilController.ResolveBallistics / OnShotFired).
        RecoilBallisticsResult result = recoilController.ResolveBallistics(baseDirection, aimReference, Time.unscaledTime);
        horizontalDeg = result.RecoilHorizontalDeg;
        verticalDeg = result.RecoilVerticalDeg;
        return true;
    }

    // Forward transform - approximates RecoilController.ApplyHorizontalVertical: what a shot
    // fired along `direction` right now would actually travel along once recoil is applied.
    public static Vector3 ApplyPattern(Vector3 direction, Vector3 up, Vector3 right, float horizontalDeg, float verticalDeg)
    {
        Vector3 afterHorizontal = Quaternion.AngleAxis(horizontalDeg, up) * direction;
        return Quaternion.AngleAxis(-verticalDeg, right) * afterHorizontal;
    }

    // Approximate inverse of ApplyPattern: where to aim right now so that once the game's own
    // recoil is applied at fire time, the shot ends up along `direction`.
    public static Vector3 ApplyInversePattern(Vector3 direction, Vector3 up, Vector3 right, float horizontalDeg, float verticalDeg)
    {
        Vector3 afterVertical = Quaternion.AngleAxis(verticalDeg, right) * direction;
        return Quaternion.AngleAxis(-horizontalDeg, up) * afterVertical;
    }

    // Resolves the up/right axes the same way everywhere: the weapon's UnshakenAimReference
    // when available (matching what the real recoil math uses), else a given fallback transform.
    public static void ResolveAxes(Weapon weapon, Transform fallback, out Vector3 up, out Vector3 right)
    {
        Transform aimReference = weapon != null ? weapon.UnshakenAimReference : null;
        Transform axesSource = aimReference != null ? aimReference : fallback;
        up = axesSource.up;
        right = axesSource.right;
    }
}
