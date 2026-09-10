using System;
using UnityEngine;
using UnityEngine.UI;

// The in-match HUD - score/round bar, result banner, rewind offer,
// AI-turn indicator. Effect is no longer picked here - it's detected from
// the shot swipe's shape (see PenaltyKickInput.DetectEffect). Net-new for
// the Unity-as-separate-Activity
// migration (see the plan's "Unity in-match UI" section) - nothing like this
// existed in the project before (confirmed: zero Canvas/UI usage anywhere).
// Built procedurally by PrototypeSceneBuilder.BuildHud(), matching how the
// rest of the scene is assembled, and driven by MatchController the same way
// it drives BallController/KickerAnimatorBase/GoalkeeperAnimatorBase.
//
// Legacy UnityEngine.UI (not TextMeshPro): adding the TMP package would need
// a network fetch + "Import TMP Essential Resources" step that isn't
// reliable in the batch-mode CI export this project depends on - legacy Text
// needs neither and was the pragmatic choice here.
public class MatchHud : MonoBehaviour
{
    public Text scoreText;
    public Text roundText;
    public Text suddenDeathText;
    public Image[] team1Pips;
    public Image[] team2Pips;

    public GameObject resultBannerPanel;
    public Text resultText;

    public GameObject rewindPanel;
    public Button rewindButton;
    public Text rewindButtonLabel;
    public Text rewindCountText;

    // Persistent "how many rewinds do I have left" badge, always visible in
    // the scoreboard (left side) - distinct from rewindPanel above, which is
    // the transient offer popup shown only right after a miss.
    public Text rewindBadgeCountText;

    public GameObject turnPromptPanel;
    public Text turnPromptText;

    public event Action OnRewindPressed;

    static readonly Color PipEmpty = new Color(1f, 1f, 1f, 0.2f);
    static readonly Color PipGoal = HgColor("#4CAF50");
    static readonly Color PipMiss = HgColor("#DC143C");

    // Built at Editor-export time (fixed locale, whatever MatchLocalization.
    // Locale happened to default to) - MatchController calls this again
    // right after setting MatchLocalization.Locale from the real
    // MatchLaunchParams, so labels always match the match's actual locale.
    public void ApplyLocalizedLabels()
    {
        if (rewindButtonLabel != null) rewindButtonLabel.text = MatchLocalization.Get("rewind_offer");
        if (suddenDeathText != null) suddenDeathText.text = MatchLocalization.Get("sudden_death");
    }

    void Awake()
    {
        if (rewindButton != null) rewindButton.onClick.AddListener(() => OnRewindPressed?.Invoke());

        HideResult();
        ShowRewindOffer(0);
        ShowTurnPrompt(null);
        ShowSuddenDeath(false);
    }

    public void SetScore(int team1Score, int team2Score)
    {
        if (scoreText != null) scoreText.text = $"{team1Score} - {team2Score}";
    }

    public void SetRewindsRemaining(int count)
    {
        if (rewindBadgeCountText != null) rewindBadgeCountText.text = count.ToString();
    }

    public void SetRoundText(string text)
    {
        if (roundText != null) roundText.text = text;
    }

    public void ShowSuddenDeath(bool show)
    {
        if (suddenDeathText != null) suddenDeathText.gameObject.SetActive(show);
    }

    public void SetPips(System.Collections.Generic.List<bool> team1Results, System.Collections.Generic.List<bool> team2Results)
    {
        SetPipRow(team1Pips, team1Results);
        SetPipRow(team2Pips, team2Results);
    }

    static void SetPipRow(Image[] pips, System.Collections.Generic.List<bool> results)
    {
        if (pips == null) return;
        for (int i = 0; i < pips.Length; i++)
        {
            if (pips[i] == null) continue;
            pips[i].color = i >= results.Count ? PipEmpty : (results[i] ? PipGoal : PipMiss);
        }
    }

    public void ShowResult(string text)
    {
        if (resultBannerPanel != null) resultBannerPanel.SetActive(true);
        if (resultText != null) resultText.text = text;
    }

    public void HideResult()
    {
        if (resultBannerPanel != null) resultBannerPanel.SetActive(false);
    }

    public void ShowRewindOffer(int rewindsRemaining)
    {
        bool show = rewindsRemaining > 0;
        if (rewindPanel != null) rewindPanel.SetActive(show);
        if (rewindCountText != null) rewindCountText.text = rewindsRemaining.ToString();
        if (rewindButtonLabel != null) rewindButtonLabel.text = MatchLocalization.Get("rewind_offer");
    }

    public void HideRewindOffer() => ShowRewindOffer(0);

    // Ticks the button label itself (e.g. "Rewind? (5)" -> "(4)" -> ...)
    // while the offer is up, so the player can actually see the decision
    // window closing instead of the popup just silently vanishing - the
    // Score Hero-style "press within N seconds or lose it" countdown.
    public void SetRewindCountdown(int secondsLeft)
    {
        if (rewindButtonLabel != null)
            rewindButtonLabel.text = $"{MatchLocalization.Get("rewind_offer")} ({secondsLeft})";
    }

    public void ShowTurnPrompt(string text)
    {
        if (turnPromptPanel != null) turnPromptPanel.SetActive(!string.IsNullOrEmpty(text));
        if (turnPromptText != null) turnPromptText.text = text ?? "";
    }

    public static Color HgColor(string hex)
    {
        return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }
}
