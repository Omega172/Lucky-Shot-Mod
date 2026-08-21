using BepInEx.Configuration;
using Shooterlatro.Game;
using Shooterlatro.Weapons;
using UnityEngine;

namespace LuckShotMod;

// While enabled and a round is active, keeps the weapon topped off every frame instead
// of waiting out the normal reload duration - functionally infinite ammo, not just a
// faster reload. Weapon.PerformInstantReloadWithEvents already no-ops when the magazine
// is full and not mid-reload, so calling it every frame is safe.
public class InfiniteAmmo : MonoBehaviour
{
    public static InfiniteAmmo Instance { get; private set; }

    public ConfigEntry<bool> Enabled { get; private set; }

    public void Init(ConfigFile config)
    {
        Enabled = config.Bind("InfiniteAmmo", "Enabled", false,
            "Keep the weapon's magazine instantly topped off, skipping reload time entirely - effectively infinite ammo.");
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

        GameManager gameManager = GameManager.Instance;
        if (gameManager == null || !gameManager.IsRoundActive)
        {
            return;
        }

        Weapon weapon = Weapon.Instance;
        if (weapon == null)
        {
            return;
        }

        weapon.PerformInstantReloadWithEvents();
    }
}
