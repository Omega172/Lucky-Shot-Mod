using BepInEx.Configuration;
using Shooterlatro.Core;
using Shooterlatro.Weapons;
using UnityEngine;

namespace LuckShotMod;

// Applies an adjustable delta on top of the weapon's fire rate via the game's own
// Weapon.ModifyFireRate(delta), and keeps it perfectly revertible: toggling Enabled off
// (or the delta changing) re-derives exactly how much to add/remove from the weapon's
// current _fireRate rather than assuming any prior state, so it can never drift.
//
// Weapon._fireRate gets reset to the weapon asset's base value whenever a weapon is
// (re)initialized (new round, weapon swap), which would silently strand our applied delta.
// Subscribing to GameEvents.OnRoundStarted and tracking the Weapon instance identity lets
// us notice that reset and reapply cleanly instead of stacking on top of a stale amount.
public class FireRateModifier : MonoBehaviour
{
    private const float Step = 0.5f;

    public ConfigEntry<bool> Enabled { get; private set; }
    public ConfigEntry<float> Delta { get; private set; }

    private float _appliedDelta;
    private Weapon _trackedWeapon;

    public void Init(ConfigFile config)
    {
        Enabled = config.Bind("FireRateModifier", "Enabled", false,
            "Apply the fire rate delta below to the weapon. Turning this off fully reverts it.");
        Delta = config.Bind("FireRateModifier", "Delta", 0f,
            "Shots/second added to (or removed from, if negative) the weapon's base fire rate while enabled.");
    }

    private void OnEnable()
    {
        GameEvents.OnRoundStarted += HandleRoundStarted;
    }

    private void OnDisable()
    {
        GameEvents.OnRoundStarted -= HandleRoundStarted;
        // Best-effort revert on disable so an unloaded/disabled mod doesn't leave a stuck boost.
        RevertIfPossible();
    }

    private void HandleRoundStarted(RoundStartedContext context)
    {
        // The weapon just reinitialized its base fire rate; our tracked delta no longer applies.
        _appliedDelta = 0f;
    }

    public void Increment() => Delta.Value += Step;

    public void Decrement() => Delta.Value -= Step;

    public void ResetDelta() => Delta.Value = 0f;

    private void Update()
    {
        Weapon weapon = Weapon.Instance;
        if (weapon != _trackedWeapon)
        {
            // Different weapon instance than the one our delta was applied to - it has its
            // own fresh _fireRate, so our bookkeeping no longer applies to it.
            _trackedWeapon = weapon;
            _appliedDelta = 0f;
        }

        if (weapon == null)
        {
            return;
        }

        float desiredDelta = Enabled.Value ? Delta.Value : 0f;
        if (!Mathf.Approximately(desiredDelta, _appliedDelta))
        {
            weapon.ModifyFireRate(desiredDelta - _appliedDelta);
            _appliedDelta = desiredDelta;
        }
    }

    private void RevertIfPossible()
    {
        if (_trackedWeapon != null && !Mathf.Approximately(_appliedDelta, 0f))
        {
            _trackedWeapon.ModifyFireRate(-_appliedDelta);
            _appliedDelta = 0f;
        }
    }

    // For the menu to show the weapon's live, post-modifier fire rate.
    public static float? GetCurrentBaseFireRate()
    {
        Weapon weapon = Weapon.Instance;
        return weapon != null ? weapon.CaptureRunSaveData().fireRate : (float?)null;
    }
}
