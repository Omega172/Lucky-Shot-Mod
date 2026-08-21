using BepInEx.Configuration;
using Shooterlatro.Core;
using Shooterlatro.Weapons;
using UnityEngine;

namespace LuckShotMod;

// Speeds up the weapon's actual reload via Weapon.ModifyReloadSpeedPercent, rather than
// bypassing it entirely (see InfiniteAmmo, which keeps the magazine topped off every frame
// regardless of input). This still goes through the real reload trigger/animation/sequence -
// it just finishes almost instantly.
//
// ModifyReloadSpeedPercent composes multiplicatively onto Weapon._reloadSpeedMultiplier, and
// that multiplier resets to 1 whenever the weapon (re)initializes (new round, weapon swap) -
// same reset Weapon._fireRate gets. So, like FireRateModifier, this tracks exactly which
// multiplier factor it has currently applied and resyncs on those events, computing whatever
// percent delta is needed to move from the applied factor to the desired one (or back to 1
// to fully revert) instead of assuming any prior state.
public class InstantReload : MonoBehaviour
{
    private const float TargetMultiplier = 1000f;

    public ConfigEntry<bool> Enabled { get; private set; }

    private float _appliedMultiplierFactor = 1f;
    private Weapon _trackedWeapon;

    public void Init(ConfigFile config)
    {
        Enabled = config.Bind("InstantReload", "Enabled", false,
            "Speed up the weapon's normal reload so it completes almost instantly, instead of skipping it entirely.");
    }

    private void OnEnable()
    {
        GameEvents.OnRoundStarted += HandleRoundStarted;
    }

    private void OnDisable()
    {
        GameEvents.OnRoundStarted -= HandleRoundStarted;
        RevertIfPossible();
    }

    private void HandleRoundStarted(RoundStartedContext context)
    {
        _appliedMultiplierFactor = 1f;
    }

    private void Update()
    {
        Weapon weapon = Weapon.Instance;
        if (weapon != _trackedWeapon)
        {
            _trackedWeapon = weapon;
            _appliedMultiplierFactor = 1f;
        }

        if (weapon == null)
        {
            return;
        }

        float desiredFactor = Enabled.Value ? TargetMultiplier : 1f;
        ApplyFactor(weapon, desiredFactor);
    }

    private void ApplyFactor(Weapon weapon, float desiredFactor)
    {
        if (Mathf.Approximately(desiredFactor, _appliedMultiplierFactor))
        {
            return;
        }
        float ratio = desiredFactor / Mathf.Max(0.0001f, _appliedMultiplierFactor);
        weapon.ModifyReloadSpeedPercent((ratio - 1f) * 100f);
        _appliedMultiplierFactor = desiredFactor;
    }

    private void RevertIfPossible()
    {
        if (_trackedWeapon != null)
        {
            ApplyFactor(_trackedWeapon, 1f);
        }
    }
}
