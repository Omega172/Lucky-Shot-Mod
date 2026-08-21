using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Shooterlatro.Core;
using Shooterlatro.Game;
using Shooterlatro.Gameplay;
using Shooterlatro.Player;
using Shooterlatro.Shop;
using Shooterlatro.Weapons;
using UnityEngine;

namespace LuckShotMod;

// While a round is active and either the hotkey (default right mouse) is held or AlwaysOn is
// set, smoothly steers the aim camera toward the highest-priority live target near the crosshair.
//
// Selection order:
//   1. Special targets about to disappear
//   2. Special targets
//   3. Normal targets about to disappear
//   4. Normal targets
// Within the best tier: normally the closest candidate to the crosshair wins, but if an
// explosive bullet is chambered, whichever candidate has the most other targets within its
// blast radius wins instead (distance still breaks ties).
public class AimAssist : MonoBehaviour
{
    // Default value order for a "which special is most worth chasing" ranking:
    // Chrono (more round time) and Reload (keeps you firing) affect whether you can
    // keep playing at all, so they lead. Overclock/Magic/Giga are the big score/DPS
    // swings. Shiny is economy (currency), Freeze is situational crowd control, and
    // Splitter mostly changes board state rather than rewarding you directly.
    private const string DefaultSpecialPriorityOrder = "Chrono,Reload,Overclock,Magic,Giga,Shiny,Freeze,Splitter";

    private static readonly HashSet<Target> AboutToDisappear = new HashSet<Target>();

    public ConfigEntry<bool> Enabled { get; private set; }
    public ConfigEntry<bool> AlwaysOn { get; private set; }
    public ConfigEntry<string> Hotkey { get; private set; }
    public ConfigEntry<float> TurnRateDegreesPerSecond { get; private set; }
    public ConfigEntry<float> DeadzoneDegrees { get; private set; }
    public ConfigEntry<bool> IgnoreChronoTargets { get; private set; }
    private ConfigEntry<string> _specialPriorityOrderConfig;

    private List<SpecialTargetType> _specialPriorityOrder;

    public void Init(ConfigFile config)
    {
        Enabled = config.Bind("AimAssist", "Enabled", true,
            "Enable aim assist while holding the hotkey during a round.");
        AlwaysOn = config.Bind("AimAssist", "AlwaysOn", false,
            "Engage aim assist continuously during a round, without needing to hold the hotkey.");
        Hotkey = config.Bind("AimAssist", "Hotkey", "Mouse2",
            "Hotkey held to engage aim assist. Examples: Mouse2, Mouse5, LeftAlt, F.");
        TurnRateDegreesPerSecond = config.Bind("AimAssist", "TurnRateDegreesPerSecond", 480f,
            "How fast the camera turns toward the chosen target, in degrees per second.");
        DeadzoneDegrees = config.Bind("AimAssist", "DeadzoneDegrees", 0.15f,
            "Angle within which the camera stops correcting, to avoid jitter once on target.");
        IgnoreChronoTargets = config.Bind("AimAssist", "IgnoreChronoTargets", false,
            "Never aim at Chrono (extra round time) targets - treat them as if they weren't there.");
        _specialPriorityOrderConfig = config.Bind("AimAssist", "SpecialTargetPriorityOrder", DefaultSpecialPriorityOrder,
            "Comma-separated SpecialTargetType names, most worth chasing first. " +
            "Valid values: Shiny, Magic, Giga, Reload, Splitter, Freeze, Chrono, Overclock.");

        _specialPriorityOrder = ParseSpecialPriorityOrder(_specialPriorityOrderConfig.Value);
    }

    private void OnEnable()
    {
        GameEvents.OnTargetSpawned += HandleTargetSpawned;
        GameEvents.OnTargetDisappear += HandleTargetDisappear;
        GameEvents.OnTargetKilled += HandleTargetKilled;
    }

    private void OnDisable()
    {
        GameEvents.OnTargetSpawned -= HandleTargetSpawned;
        GameEvents.OnTargetDisappear -= HandleTargetDisappear;
        GameEvents.OnTargetKilled -= HandleTargetKilled;
    }

    private static void HandleTargetSpawned(TargetSpawnedContext context)
    {
        // A freshly (re)spawned pooled target can't already be "about to disappear".
        Target target = ResolveTarget(context?.Target);
        if (target != null)
        {
            AboutToDisappear.Remove(target);
        }
    }

    private static void HandleTargetDisappear(TargetDisappearContext context)
    {
        Target target = ResolveTarget(context?.Target);
        if (target != null)
        {
            AboutToDisappear.Add(target);
        }
    }

    private static void HandleTargetKilled(TargetKilledContext context)
    {
        Target target = ResolveTarget(context?.Target);
        if (target != null)
        {
            AboutToDisappear.Remove(target);
        }
    }

    private static Target ResolveTarget(GameObject gameObject)
    {
        return gameObject != null ? gameObject.GetComponent<Target>() : null;
    }

    private void Update()
    {
        if (!Enabled.Value)
        {
            return;
        }

        GameManager gameManager = GameManager.Instance;
        if (gameManager == null || !gameManager.IsRoundActive)
        {
            return;
        }

        if (!AlwaysOn.Value && !Hotkeys.IsHeld(Hotkey.Value))
        {
            return;
        }

        Weapon weapon = Weapon.Instance;
        Camera aimCamera = weapon != null ? weapon.AimCamera : null;
        if (aimCamera == null)
        {
            return;
        }

        PlayerCameraController cameraController = PlayerCameraController.Instance;
        if (cameraController == null)
        {
            return;
        }

        if (TryGetBestTarget(aimCamera, out Vector3 targetPosition))
        {
            Vector3 aimPoint = ApplyRecoilCompensation(weapon, cameraController, targetPosition);
            cameraController.AutoAimTowardsWorldPoint(aimPoint, TurnRateDegreesPerSecond.Value, DeadzoneDegrees.Value);
            Triggerbot.Instance?.TryAutoFireForAimAssist();
        }
    }

    // The weapon's recoil is a deterministic pattern offset applied to the BULLET's direction
    // at fire time - it never moves the camera, so a perfectly centered crosshair can still
    // miss while a recoil pattern is active. We pre-rotate the aim point by the inverse of
    // that same offset (see RecoilCompensation) so the two roughly cancel out once the game
    // applies its own recoil to the shot.
    private static Vector3 ApplyRecoilCompensation(Weapon weapon, PlayerCameraController cameraController, Vector3 targetPosition)
    {
        if (!RecoilCompensation.TryGetPatternDegrees(weapon, out float horizontalDeg, out float verticalDeg))
        {
            return targetPosition;
        }
        if (Mathf.Approximately(horizontalDeg, 0f) && Mathf.Approximately(verticalDeg, 0f))
        {
            return targetPosition;
        }

        Transform origin = cameraController.transform;
        Vector3 targetDirection = targetPosition - origin.position;
        if (targetDirection.sqrMagnitude <= 1E-06f)
        {
            return targetPosition;
        }
        targetDirection.Normalize();

        RecoilCompensation.ResolveAxes(weapon, origin, out Vector3 up, out Vector3 right);
        Vector3 compensatedDirection = RecoilCompensation.ApplyInversePattern(targetDirection, up, right, horizontalDeg, verticalDeg);
        return origin.position + compensatedDirection * 100f;
    }

    private bool TryGetBestTarget(Camera aimCamera, out Vector3 position)
    {
        var activeTargets = Target.ActiveTargets;
        Vector2 crosshair = new Vector2(0.5f, 0.5f);

        // Pass 1: find the best (tier, specialRank) actually present on screen. This keeps
        // the same priority order regardless of what happens in pass 2 below.
        int bestTier = int.MaxValue;
        int bestSpecialRank = int.MaxValue;
        for (int i = 0; i < activeTargets.Count; i++)
        {
            Target candidate = activeTargets[i];
            if (!IsCandidate(candidate, aimCamera, out _))
            {
                continue;
            }

            bool isSpecial = candidate.HasAnySpecialTargetType;
            bool isDisappearing = AboutToDisappear.Contains(candidate);
            int tier = GetTier(isSpecial, isDisappearing);
            int specialRank = isSpecial ? GetSpecialRank(candidate) : int.MaxValue;

            if (tier < bestTier || (tier == bestTier && specialRank < bestSpecialRank))
            {
                bestTier = tier;
                bestSpecialRank = specialRank;
            }
        }

        if (bestTier == int.MaxValue)
        {
            position = default;
            return false;
        }

        // With an explosive bullet chambered, prefer whichever candidate in that best tier
        // has the most other live targets within blast radius, so the shot isn't wasted on
        // a single target when a cluster is available at the same priority.
        float explosionRadius = GetChamberedExplosionRadius();
        bool preferGrouping = explosionRadius > 0f;

        Target best = null;
        int bestGroupCount = -1;
        float bestSqrDistance = float.PositiveInfinity;

        for (int i = 0; i < activeTargets.Count; i++)
        {
            Target candidate = activeTargets[i];
            if (!IsCandidate(candidate, aimCamera, out Vector3 viewportPoint))
            {
                continue;
            }

            bool isSpecial = candidate.HasAnySpecialTargetType;
            bool isDisappearing = AboutToDisappear.Contains(candidate);
            if (GetTier(isSpecial, isDisappearing) != bestTier)
            {
                continue;
            }
            int specialRank = isSpecial ? GetSpecialRank(candidate) : int.MaxValue;
            if (specialRank != bestSpecialRank)
            {
                continue;
            }

            float sqrDistance = Vector2.SqrMagnitude(new Vector2(viewportPoint.x, viewportPoint.y) - crosshair);
            int groupCount = preferGrouping ? CountTargetsWithinPlanarRadius(activeTargets, candidate.transform.position, explosionRadius) : 0;

            bool better = preferGrouping
                ? (groupCount > bestGroupCount || (groupCount == bestGroupCount && sqrDistance < bestSqrDistance))
                : sqrDistance < bestSqrDistance;

            if (better)
            {
                best = candidate;
                bestGroupCount = groupCount;
                bestSqrDistance = sqrDistance;
            }
        }

        if (best == null)
        {
            position = default;
            return false;
        }

        position = best.transform.position;
        return true;
    }

    private bool IsCandidate(Target candidate, Camera aimCamera, out Vector3 viewportPoint)
    {
        if (IgnoreChronoTargets.Value && candidate != null && candidate.HasSpecialTargetType(SpecialTargetType.Chrono))
        {
            viewportPoint = default;
            return false;
        }
        return IsSelectable(candidate, aimCamera, out viewportPoint);
    }

    private static bool IsSelectable(Target candidate, Camera aimCamera, out Vector3 viewportPoint)
    {
        viewportPoint = default;
        if (candidate == null || !candidate.gameObject.activeInHierarchy || !candidate.IsAlive)
        {
            return false;
        }
        viewportPoint = aimCamera.WorldToViewportPoint(candidate.transform.position);
        return viewportPoint.z > 0f;
    }

    // Mirrors BulletEffectHandler's own explosion overlap check: planar (XY) distance, ignoring depth.
    private static int CountTargetsWithinPlanarRadius(IReadOnlyList<Target> targets, Vector3 center, float radius)
    {
        float radiusSqr = radius * radius;
        int count = 0;
        for (int i = 0; i < targets.Count; i++)
        {
            Target target = targets[i];
            if (target == null || !target.gameObject.activeInHierarchy || !target.IsAlive)
            {
                continue;
            }
            Vector3 p = target.transform.position;
            float dx = p.x - center.x;
            float dy = p.y - center.y;
            if (dx * dx + dy * dy <= radiusSqr)
            {
                count++;
            }
        }
        return count;
    }

    // Radius of the bullet that's actually about to fire (magazine slot 0), or 0 if it isn't explosive.
    // Doubled for a Giga-modified bullet to match BulletEffectHandler.TriggerExplosion.
    private static float GetChamberedExplosionRadius()
    {
        Weapon weapon = Weapon.Instance;
        if (weapon == null)
        {
            return 0f;
        }
        BulletInstanceInfo info = weapon.GetBulletInfoAt(0);
        BulletItemData bullet = info.Bullet;
        if (bullet == null || bullet.effectType != BulletEffectType.Explosive)
        {
            return 0f;
        }
        float radius = Mathf.Max(0f, bullet.explosionRadius);
        return info.IsGiga ? radius * 2f : radius;
    }

    // Lower is higher priority: special+disappearing, special, normal+disappearing, normal.
    private static int GetTier(bool isSpecial, bool isDisappearing)
    {
        if (isSpecial)
        {
            return isDisappearing ? 0 : 1;
        }
        return isDisappearing ? 2 : 3;
    }

    private int GetSpecialRank(Target target)
    {
        // Reload targets are useless while InfiniteAmmo is keeping the magazine topped off
        // anyway, so rank them below every other special type instead of by their configured
        // priority - still worth chasing over a normal target, just last in line among specials.
        bool deprioritizeReload = InfiniteAmmo.Instance != null && InfiniteAmmo.Instance.Enabled.Value;

        int best = int.MaxValue;
        for (int i = 0; i < _specialPriorityOrder.Count; i++)
        {
            SpecialTargetType type = _specialPriorityOrder[i];
            if (!target.HasSpecialTargetType(type))
            {
                continue;
            }
            int rank = (deprioritizeReload && type == SpecialTargetType.Reload) ? _specialPriorityOrder.Count : i;
            if (rank < best)
            {
                best = rank;
            }
        }
        return best;
    }

    private static List<SpecialTargetType> ParseSpecialPriorityOrder(string configValue)
    {
        var order = new List<SpecialTargetType>();
        if (!string.IsNullOrWhiteSpace(configValue))
        {
            string[] parts = configValue.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                if (Enum.TryParse(parts[i].Trim(), true, out SpecialTargetType parsed)
                    && parsed != SpecialTargetType.None
                    && !order.Contains(parsed))
                {
                    order.Add(parsed);
                }
            }
        }

        // Append any special types the config omitted so every type is still ranked.
        foreach (SpecialTargetType type in Enum.GetValues(typeof(SpecialTargetType)))
        {
            if (type != SpecialTargetType.None && !order.Contains(type))
            {
                order.Add(type);
            }
        }

        return order;
    }
}
