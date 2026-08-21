using BepInEx.Configuration;
using Shooterlatro.Game;
using UnityEngine;

namespace LuckShotMod;

// Hotkey (default Mouse4) that instantly ends the current round counted as cleared/won,
// via the game's own GameManager.EndCurrentRound(true) - the same call used when a round's
// win condition is met, it just doesn't check whether it actually was.
//
// EndCurrentRound requires GameManager.Phase == GamePhase.Round exactly - IsRoundActive
// alone isn't enough. Pausing the game (which is how the mod menu is usually open when this
// gets clicked) flips Phase to GamePhase.Settings even though IsRoundActive stays true, so
// the call would silently no-op. This resumes first when paused, then waits for Phase to
// actually flip back to Round (SettingsMenuManager.Resume() closes the pause UI, which isn't
// necessarily instantaneous) before committing the win.
public class WinRound : MonoBehaviour
{
    public ConfigEntry<bool> Enabled { get; private set; }
    public ConfigEntry<string> Hotkey { get; private set; }

    private bool _pendingWin;

    public void Init(ConfigFile config)
    {
        Enabled = config.Bind("WinRound", "Enabled", true,
            "Enable the win-round hotkey.");
        Hotkey = config.Bind("WinRound", "Hotkey", "Mouse4",
            "Hotkey that instantly ends the current round as cleared/won. Examples: Mouse4, F2.");
    }

    private void Update()
    {
        if (_pendingWin)
        {
            TryCommitWin();
        }

        if (!Enabled.Value)
        {
            return;
        }

        if (Hotkeys.WasPressedThisFrame(Hotkey.Value))
        {
            Plugin.Logger.LogInfo($"[WinRound] Hotkey '{Hotkey.Value}' pressed.");
            TryWinRound();
        }
    }

    public void TryWinRound()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            Plugin.Logger.LogInfo("[WinRound] Aborted: GameManager.Instance is null.");
            return;
        }
        if (!gameManager.IsRoundActive)
        {
            Plugin.Logger.LogInfo($"[WinRound] Aborted: IsRoundActive=false (Phase={gameManager.Phase}).");
            return;
        }

        if (SettingsMenuManager.IsPaused)
        {
            Plugin.Logger.LogInfo("[WinRound] Paused - resuming first.");
            SettingsMenuManager.Instance?.Resume();
        }

        _pendingWin = true;
        TryCommitWin();
    }

    private void TryCommitWin()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null || !gameManager.IsRoundActive)
        {
            Plugin.Logger.LogInfo("[WinRound] Cancelled while pending: round no longer active.");
            _pendingWin = false;
            return;
        }

        if (gameManager.Phase != GamePhase.Round)
        {
            // Still waiting for the pause UI to finish closing.
            return;
        }

        Plugin.Logger.LogInfo("[WinRound] Calling GameManager.EndCurrentRound(true).");
        gameManager.EndCurrentRound(true);
        _pendingWin = false;
    }
}
