using System;
using System.Collections;
using UnityEngine;

// The single entry/exit point between Flutter and this scene. Lives on the
// same "GameManager" GameObject as GameManager.cs so Flutter's
// sendToUnity("GameManager", methodName, ...) calls land here without
// needing a second object name on the Dart side.
//
// Flutter -> Unity (package:flutter_embed_unity's sendToUnity, a plain
// SendMessage-style call - each method takes exactly one string):
//   sendToUnity("GameManager", "SetMode", "shoot" | "defend")
//   sendToUnity("GameManager", "SetSkins", '{"ball":"ball_flame","boots":"boots_neon","pitch":"pitch_night"}')
//     - any field can be omitted/empty to leave that skin as-is.
//   sendToUnity("GameManager", "PlayShot", '{"direction":0,"power":72,"effect":"curve","keeperDirection":1,"goalScored":true}')
//     - direction/keeperDirection: 0=left, 1=center, 2=right (matches
//       lib/constants.dart's ShotDirection). power: 0-100. effect: "normal"
//       | "curve" | "lob" | "knuckle". goalScored is the outcome Flutter's
//       GameController already decided (see game_controller.dart's
//       _handleShotResult) - this only replays it, it never re-decides.
//
// Unity -> Flutter (EmbedUnity's onMessageFromUnity, via SendToFlutter.cs),
// one JSON string per event:
//   {"event":"ready"} - sent once, when the scene has finished building and
//     is safe to send SetMode/SetSkins to. EmbedUnity exposes no "Unity
//     created" callback of its own, so Flutter waits for this instead of
//     guessing with a timer.
//   {"event":"result","outcome":"goal"|"save"|"miss"} - once per attempt.
//
// This never touches GameManager/BallController's own logic - it only adds
// a second, independent subscriber to BallController's existing events and
// a couple of public setters, so the prototype keeps working identically
// with the "G"/number/function keys when tested outside Flutter.
public class FlutterBridge : MonoBehaviour
{
    public GameManager gameManager;
    public BallController ball;
    public BallSkinManager ballSkin;
    public BootsSkinManager kickerBoots;
    public BootsSkinManager keeperBoots;
    public PitchSkinManager pitchSkin;
    public PenaltyKickInput penaltyKickInput;

    void Start()
    {
        SendToFlutter.Send(JsonUtility.ToJson(new ReadyMessage { @event = "ready" }));
    }

    void OnEnable()
    {
        if (ball == null) return;
        ball.OnGoalScored += HandleGoal;
        ball.OnShotBlocked += HandleBlocked;
        ball.OnShotMissed += HandleMissed;
    }

    void OnDisable()
    {
        if (ball == null) return;
        ball.OnGoalScored -= HandleGoal;
        ball.OnShotBlocked -= HandleBlocked;
        ball.OnShotMissed -= HandleMissed;
    }

    void HandleGoal() => SendResult("goal");
    void HandleBlocked() => SendResult("save");
    void HandleMissed() => SendResult("miss");

    void SendResult(string outcome)
    {
        var payload = new ResultMessage { @event = "result", outcome = outcome };
        SendToFlutter.Send(JsonUtility.ToJson(payload));
    }

    /// Flutter calls this once per attempt, before the kicker/keeper
    /// approach animation starts, to pick which side the player controls.
    /// Also swaps the active camera/listener (kicker view vs goalkeeper
    /// view) so a defending turn is actually seen from the goalkeeper's
    /// point of view, matching HappyGoal's real turn alternation.
    public void SetMode(string mode)
    {
        if (gameManager == null) return;
        gameManager.mode = mode == "defend"
            ? GameManager.ControlMode.PlayerDefends
            : GameManager.ControlMode.PlayerShoots;
        gameManager.ApplyCameraForMode();
    }

    /// Flutter calls this with the player's currently-equipped cosmetics
    /// (CosmeticItem ids from lib/models/cosmetic_item.dart). Empty/missing
    /// fields leave that skin unchanged.
    public void SetSkins(string json)
    {
        SkinsMessage skins;
        try
        {
            skins = JsonUtility.FromJson<SkinsMessage>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"FlutterBridge.SetSkins: could not parse '{json}' - {e.Message}");
            return;
        }

        if (!string.IsNullOrEmpty(skins.ball) && ballSkin != null)
        {
            ballSkin.ApplySkin(skins.ball);
        }
        if (!string.IsNullOrEmpty(skins.boots))
        {
            if (kickerBoots != null) kickerBoots.ApplySkin(skins.boots);
            if (keeperBoots != null) keeperBoots.ApplySkin(skins.boots);
        }
        if (!string.IsNullOrEmpty(skins.pitch) && pitchSkin != null)
        {
            pitchSkin.ApplySkin(skins.pitch);
        }
    }

    /// Flutter calls this right as a human shooting turn begins - before
    /// GameController has decided anything about this attempt. Resets the
    /// scene to an idle "waiting for the player's swipe" pose and lets
    /// PenaltyKickInput capture that swipe natively (instead of the old
    /// Flutter-side swipe widget, which fought this full-screen embed for
    /// touch input - see game_screen.dart). The captured swipe is reported
    /// back via ReportShotAttempt; PlayShot below then arrives shortly
    /// after with the decided result to actually animate the kick.
    public void BeginPlayerShot()
    {
        if (gameManager == null || gameManager.kicker == null || gameManager.keeper == null || ball == null)
        {
            Debug.LogWarning("FlutterBridge.BeginPlayerShot: scene is missing kicker/keeper/ball references.");
            return;
        }

        gameManager.mode = GameManager.ControlMode.PlayerShoots;
        gameManager.EnableFlutterControl();
        gameManager.ApplyCameraForMode();
        StopAllCoroutines();

        ball.ResetBall();
        gameManager.keeper.ResetKeeper();
        gameManager.kicker.ResetToStart();

        if (penaltyKickInput != null) penaltyKickInput.InputEnabled = true;
    }

    /// PenaltyKickInput calls this (native swipe capture) once the player
    /// swipes during a BeginPlayerShot window - direction/power only, no
    /// outcome. Purely a relay to Flutter's GameController.
    public void ReportShotAttempt(int direction, int power)
    {
        var payload = new ShotAttemptMessage { @event = "shotAttempt", direction = direction, power = power };
        SendToFlutter.Send(JsonUtility.ToJson(payload));
    }

    /// Flutter calls this once GameController has already decided this
    /// attempt's direction/power/effect/keeper-dive and the goal/save
    /// outcome (see lib/screens/game_screen.dart) - this only replays that
    /// decision visually, it never re-decides anything itself.
    public void PlayShot(string json)
    {
        ShotMessage shot;
        try
        {
            shot = JsonUtility.FromJson<ShotMessage>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"FlutterBridge.PlayShot: could not parse '{json}' - {e.Message}");
            return;
        }

        if (gameManager == null || gameManager.kicker == null || gameManager.keeper == null || ball == null)
        {
            Debug.LogWarning("FlutterBridge.PlayShot: scene is missing kicker/keeper/ball references.");
            return;
        }

        gameManager.EnableFlutterControl();
        StopAllCoroutines();
        StartCoroutine(PlayShotRoutine(shot));
    }

    IEnumerator PlayShotRoutine(ShotMessage shot)
    {
        ball.ResetBall();
        gameManager.keeper.ResetKeeper();
        gameManager.kicker.ResetToStart();

        yield return gameManager.kicker.ApproachRoutine();
        yield return new WaitForSeconds(0.2f);

        float targetX = ZoneToX(shot.direction);
        float diveX = ZoneToX(shot.keeperDirection);
        float powerT = Mathf.Clamp01(shot.power / 100f);
        float heightY = shot.effect == "lob"
            ? Mathf.Lerp(1.6f, 2.1f, powerT)
            : Mathf.Lerp(0.35f, 1.3f, powerT);
        float power = Mathf.Lerp(0.75f, 1.15f, powerT);
        float curve = shot.effect == "curve" ? (targetX < 0f ? 0.3f : -0.3f) : 0f;

        Vector3 center = gameManager.goalCenterPoint != null
            ? gameManager.goalCenterPoint.position
            : ball.transform.position;
        Vector3 targetPoint = new Vector3(center.x + targetX, heightY, center.z);

        // The keeper always commits to a dive toward the direction Flutter
        // already picked for it - ScheduleBlock (not the dive itself) is
        // what makes this shot a save, so the two stay independent exactly
        // like a real penalty (a correctly-guessed dive can still miss the
        // ball; a wrongly-guessed one is what usually makes it a goal).
        gameManager.keeper.PlayDirectedDive(diveX);
        if (!shot.goalScored) ball.ScheduleBlock();

        yield return gameManager.kicker.PlayKick(() => ball.Shoot(targetPoint, power, curve));
    }

    // 0=left, 1=center, 2=right -> world X offset from the goal's centre,
    // at the middle of each third of the 7.32m-wide goal.
    static float ZoneToX(int zone)
    {
        switch (zone)
        {
            case 0: return -2.44f;
            case 2: return 2.44f;
            default: return 0f;
        }
    }

    [Serializable]
    class ReadyMessage
    {
        public string @event;
    }

    [Serializable]
    class ShotMessage
    {
        public int direction;
        public int power;
        public string effect;
        public int keeperDirection;
        public bool goalScored;
    }

    [Serializable]
    class ResultMessage
    {
        public string @event;
        public string outcome;
    }

    [Serializable]
    class ShotAttemptMessage
    {
        public string @event;
        public int direction;
        public int power;
    }

    [Serializable]
    class SkinsMessage
    {
        public string ball;
        public string boots;
        public string pitch;
    }
}
