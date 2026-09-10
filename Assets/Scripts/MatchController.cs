using System;
using System.Collections;
using UnityEngine;

// Owns the whole match now: turn sequencing, scoring, sudden death, rewind,
// AI shot decisions - the Dart GameController's job, ported here as part of
// the Unity-as-separate-Activity migration (see the plan). Drives the
// existing scene mechanics (BallController/KickerAnimatorBase/
// GoalkeeperAnimatorBase/PenaltyKickInput/GoalkeeperInput) directly via
// coroutines, and drives the in-match HUD (MatchHud, built by
// PrototypeSceneBuilder.BuildHud()) the same way - no GamePhase enum/
// reactive state machine on either side, just direct calls at the points
// that matter (turn start, shot resolved, rewind offered).
//
// GameManager stays as the low-level single-attempt scene API (kept for its
// own Editor keyboard testing loop, used when flutterControlled is false);
// this class is the new orchestrator, replacing FlutterBridge.cs (still
// present, unused by this flow - deleted in the Phase 4 cleanup once this is
// verified working).
public class MatchController : MonoBehaviour
{
    public GameManager gameManager;
    public BallController ball;
    public PenaltyKickInput penaltyKickInput;
    public GoalkeeperInput goalkeeperInput;
    public MatchHud hud;

    // Equipped cosmetics (ball/boots/pitch) applied once per match, and
    // jersey colors applied every turn - see BeginNewMatch/ApplyKitColorsForTurn.
    public BallSkinManager ballSkin;
    public BootsSkinManager kickerBoots;
    public BootsSkinManager keeperBoots;
    public PitchSkinManager pitchSkin;
    public TeamKitTint kickerKitTint;
    public TeamKitTint keeperKitTint;
    public WeatherController weather;

    // Score display on the stadium's big screen - see BuildJumbotronScoreboard.
    public JumbotronScoreboard jumbotron;

    MatchLaunchParams _launchParams;
    MatchState _state;
    AIOpponent _ai;
    bool _isTeam1Turn;
    int _matchGoalsCurve, _matchGoalsLob, _matchGoalsKnuckle;

    bool _rewindOfferedThisTurn;
    bool _awaitingRewindDecision;
    bool _rewindConsumedThisOffer;

    void Awake()
    {
        if (hud != null)
        {
            hud.OnRewindPressed += HandleRewindPressed;
        }
    }

    void Start()
    {
        string launchParamsJson = MatchResultSender.GetLaunchParams();
        if (string.IsNullOrEmpty(launchParamsJson))
        {
            // A pre-warm launch (MainActivity fires this at app open, before
            // any real match was requested - see UnityMatchActivity.kt) has
            // no launch params yet. Starting a match anyway with a default
            // MatchLaunchParams would crash the moment turn logic touches
            // its null team1/team2 - just sit idle instead, until the real
            // BeginNewMatch UnitySendMessage arrives via onNewIntent once
            // the player actually taps a level.
            Debug.Log("MatchController: no launch params yet (pre-warm) - waiting for a real match request.");
            return;
        }
        BeginNewMatch(launchParamsJson);
    }

    // Called once by Start() for the first match, and again via
    // UnityPlayer.UnitySendMessage("GameManager", "BeginNewMatch", json) for
    // every match after that - UnityMatchActivity.kt is deliberately reused
    // (never finish()ed/recreated, see its class doc for why), so this is how
    // a second/third/... match gets going without restarting the engine.
    public void BeginNewMatch(string launchParamsJson)
    {
        _launchParams = MatchLaunchParams.FromJson(launchParamsJson);
        MatchLocalization.Locale = _launchParams.locale;
        _state = new MatchState { RewindsRemaining = _launchParams.rewindsAvailableForThisMatch };
        _ai = new AIOpponent(_launchParams.aiIntelligence);
        _isTeam1Turn = true;
        _matchGoalsCurve = _matchGoalsLob = _matchGoalsKnuckle = 0;
        _rewindOfferedThisTurn = false;

        if (hud != null)
        {
            hud.ApplyLocalizedLabels();
            hud.SetScore(0, 0);
            jumbotron?.SetScore(0, 0);
            hud.SetRewindsRemaining(_state.RewindsRemaining);
            hud.SetPips(_state.Team1Results, _state.Team2Results);
            hud.ShowSuddenDeath(false);
            hud.HideResult();
            hud.HideRewindOffer();
            hud.ShowTurnPrompt(null);
        }

        // Equipped cosmetics from the shop - confirmed gap: nothing applied
        // these at all in the new Activity-based match flow, so shop
        // purchases (ball/boots/pitch skins) had no visible effect. Applied
        // once here; jersey colors are separate (ApplyKitColorsForTurn,
        // called per turn since the kicker/keeper roles swap teams).
        var skins = _launchParams.equippedSkins;
        if (skins != null)
        {
            if (!string.IsNullOrEmpty(skins.ball)) ballSkin?.ApplySkin(skins.ball);
            if (!string.IsNullOrEmpty(skins.boots))
            {
                kickerBoots?.ApplySkin(skins.boots);
                keeperBoots?.ApplySkin(skins.boots);
            }
            if (!string.IsNullOrEmpty(skins.pitch)) pitchSkin?.ApplySkin(skins.pitch);
        }

        // Weather is randomized every match, independent of the equipped
        // pitch skin above (that's the cosmetic ground/lighting mood; this
        // is the actual condition - clear/rain/snow) - each level playthrough
        // can look different even with the same skin equipped.
        if (weather != null)
        {
            float roll = UnityEngine.Random.value;
            var randomWeather = roll < 0.6f ? WeatherController.Weather.Sunny
                : roll < 0.8f ? WeatherController.Weather.Rain
                : WeatherController.Weather.Snow;
            weather.Apply(randomWeather);
        }

        Debug.Log($"MatchController: starting match, mode={_launchParams.matchMode}");

        if (gameManager == null || gameManager.kicker == null || gameManager.keeper == null || ball == null)
        {
            Debug.LogWarning("MatchController.BeginNewMatch: scene is missing kicker/keeper/ball references.");
            return;
        }

        gameManager.EnableFlutterControl();
        StopAllCoroutines();
        StartCoroutine(PlayTurnsUntilMatchEnds());
    }

    bool IsTeam1Human => true; // team1 is human-controlled in every mode.
    bool IsTeam2Human => _launchParams.matchMode == MatchMode.Multiplayer;

    IEnumerator PlayTurnsUntilMatchEnds()
    {
        while (true)
        {
            bool currentIsHuman = _isTeam1Turn ? IsTeam1Human : IsTeam2Human;
            yield return currentIsHuman ? PlayHumanShotTurn() : PlayAiShotTurn();

            if (_rewindOfferedThisTurn)
            {
                yield return OfferRewind();
                if (_rewindConsumedThisOffer)
                {
                    // Same team retries the undone shot - matches
                    // GameController.rewindLastShot() returning to
                    // playerShooting for the same team, not advancing.
                    continue;
                }
            }

            _state.InvalidateSnapshot();

            if (_state.CheckWinner())
            {
                FinishMatch(completed: true);
                yield break;
            }

            _isTeam1Turn = !_isTeam1Turn;
        }
    }

    // Pauses turn progression so the player can actually choose, mirroring
    // GameController's rewind popup - times out to "keep the result" after a
    // few seconds so a missed tap can't stall the match forever.
    const float RewindOfferSeconds = 5f;

    IEnumerator OfferRewind()
    {
        _rewindConsumedThisOffer = false;
        _awaitingRewindDecision = true;
        if (hud != null) hud.ShowRewindOffer(_state.RewindsRemaining);

        float deadline = Time.time + RewindOfferSeconds;
        int lastSecondShown = -1;
        while (_awaitingRewindDecision && Time.time < deadline)
        {
            int secondsLeft = Mathf.CeilToInt(deadline - Time.time);
            if (secondsLeft != lastSecondShown)
            {
                lastSecondShown = secondsLeft;
                if (hud != null) hud.SetRewindCountdown(secondsLeft);
            }
            yield return null;
        }

        _awaitingRewindDecision = false;
        _rewindOfferedThisTurn = false;
        if (hud != null) hud.HideRewindOffer();
    }

    void HandleRewindPressed()
    {
        if (!_awaitingRewindDecision || !_state.CanRewind) return;
        _state.RewindToLastShot();
        _rewindConsumedThisOffer = true;
        _awaitingRewindDecision = false;
        jumbotron?.SetScore(_state.Team1Score, _state.Team2Score);
        if (hud != null)
        {
            hud.SetScore(_state.Team1Score, _state.Team2Score);
            hud.SetRewindsRemaining(_state.RewindsRemaining);
            hud.SetPips(_state.Team1Results, _state.Team2Results);
            hud.ShowSuddenDeath(_state.IsSuddenDeathActive);
            hud.SetRoundText(BuildRoundText());
            hud.HideResult();
        }
    }

    // ---- Human turn: swipe captured directly on the Unity view
    // (PenaltyKickInput.cs) - see ReportShotAttempt for the other half. ----

    bool _waitingForShotAttempt;
    float _capturedTargetX;
    int _capturedPower;
    string _capturedEffect;

    IEnumerator PlayHumanShotTurn()
    {
        gameManager.mode = GameManager.ControlMode.PlayerShoots;
        gameManager.ApplyCameraForMode();
        ApplyKitColorsForTurn(_isTeam1Turn);
        if (hud != null) hud.HideResult();

        ball.ResetBall();
        gameManager.keeper.ResetKeeper();
        gameManager.kicker.ResetToStart();
        if (goalkeeperInput != null) goalkeeperInput.CancelWindow();
        if (penaltyKickInput != null) penaltyKickInput.InputEnabled = true;

        if (hud != null)
        {
            hud.ShowTurnPrompt(MatchLocalization.Get("your_turn_shoot"));
        }

        _waitingForShotAttempt = true;
        while (_waitingForShotAttempt) yield return null;

        if (hud != null) hud.ShowTurnPrompt(null);

        float targetX = _capturedTargetX;
        int power = _capturedPower;
        string effect = _capturedEffect;
        // Matches GameController.shoot(): when a human shoots, the opposing
        // goalkeeper's dive is pure chance, not a decision. The keeper still
        // dives to one of 3 zones (free aim is scoped to the shooter only).
        int keeperDirection = UnityEngine.Random.Range(0, 3);

        yield return ResolveAndPlayShot(targetX, power, effect, keeperDirection);
    }

    // Called by PenaltyKickInput.cs once the player's swipe is captured -
    // targetX is the exact continuous world-X the aim line showed (free
    // aim, not one of a few fixed zones), effect is detected from the
    // swipe's shape (see PenaltyKickInput.DetectEffect) rather than picked
    // from a button beforehand.
    public void ReportShotAttempt(float targetX, int power, string effect)
    {
        if (!_waitingForShotAttempt) return;
        _capturedTargetX = targetX;
        _capturedPower = power;
        _capturedEffect = effect;
        _waitingForShotAttempt = false;
    }

    // ---- AI turn: AI decides its shot upfront, human reacts with a swipe on
    // the goalkeeper (PenaltyKickInput's sibling, GoalkeeperInput.cs) while
    // the kick animation plays out - see ReportGoalkeeperChoice. ----

    bool _waitingForGoalkeeperChoice;
    int _capturedKeeperDirection;

    IEnumerator PlayAiShotTurn()
    {
        gameManager.mode = GameManager.ControlMode.PlayerDefends;
        gameManager.ApplyCameraForMode();
        ApplyKitColorsForTurn(_isTeam1Turn);
        if (hud != null) hud.HideResult();

        _ai.TakeShot(out int direction, out int power, out string effect);

        if (penaltyKickInput != null) penaltyKickInput.InputEnabled = false;
        ball.ResetBall();
        gameManager.keeper.ResetKeeper();
        gameManager.kicker.ResetToStart();

        if (hud != null) hud.ShowTurnPrompt(MatchLocalization.Get("ai_shooting"));

        yield return gameManager.kicker.ApproachRoutine();

        if (hud != null) hud.ShowTurnPrompt(MatchLocalization.Get("your_turn_defend"));

        // Open the reaction window now, before the ball even launches, so
        // the player gets the strike animation's wind-up as extra lead time
        // on top of the ball's own flight time - matches
        // GameManager.ApproachThenAiShoot's exact timing.
        _waitingForGoalkeeperChoice = true;
        if (goalkeeperInput != null)
        {
            goalkeeperInput.BeginReactionWindow(ShotDirection.ToWorldX(direction));
        }
        else
        {
            _capturedKeeperDirection = ShotDirection.Center;
            _waitingForGoalkeeperChoice = false;
        }

        // ReportGoalkeeperChoice (called from GoalkeeperInput.Resolve, itself
        // driven by Update()) resolves this before the kick animation below
        // finishes - the coroutines run concurrently on Unity's single
        // update loop, same as the already-working non-flutterControlled path.
        while (_waitingForGoalkeeperChoice) yield return null;

        if (hud != null) hud.ShowTurnPrompt(null);

        // AI still picks one of 3 zones - convert to the same continuous
        // world-X ResolveAndPlayShot now takes (shared with the free-aim
        // human path).
        yield return ResolveAndPlayShot(ShotDirection.ToWorldX(direction), power, effect, _capturedKeeperDirection);
    }

    // Called by GoalkeeperInput.cs once the player's dive is captured (or the
    // reaction window times out to center).
    public void ReportGoalkeeperChoice(int direction)
    {
        if (!_waitingForGoalkeeperChoice) return;
        _capturedKeeperDirection = direction;
        _waitingForGoalkeeperChoice = false;
    }

    // ---- Shared resolution + animation, once targetX/power/effect/keeper
    // are all known - mirrors FlutterBridge.PlayShotRoutine's exact target/
    // curve/height math and reset->approach->dive->kick sequencing. targetX
    // is a continuous world-X (free aim for the human path, one of the 3
    // zone centres for the AI path - see the call sites). ----

    IEnumerator ResolveAndPlayShot(float targetX, int power, string effect, int keeperDirection)
    {
        _state.SaveStateBeforeShot();

        float diveX = ShotDirection.ToWorldX(keeperDirection);
        bool isGoal = ShotResolver.IsGoal(targetX, diveX, effect, power);

        float powerT = Mathf.Clamp01(power / 100f);
        float heightY = effect == ShotEffect.Lob
            ? Mathf.Lerp(1.6f, 2.1f, powerT)
            : Mathf.Lerp(0.35f, 1.3f, powerT);
        float shotPower = Mathf.Lerp(0.75f, 1.15f, powerT);
        float curve = effect == ShotEffect.Curve ? (targetX < 0f ? 0.3f : -0.3f) : 0f;

        Vector3 center = gameManager.goalCenterPoint != null
            ? gameManager.goalCenterPoint.position
            : ball.transform.position;
        Vector3 targetPoint = new Vector3(center.x + targetX, heightY, center.z);

        gameManager.keeper.PlayDirectedDive(diveX);

        if (!isGoal)
        {
            if (Mathf.Abs(targetX - diveX) <= ShotResolver.KeeperReachRadius)
            {
                // Keeper actually dove within reach of the shot - a real save.
                ball.ScheduleBlock();
            }
            else
            {
                // Keeper dove nowhere near the shot, but ShotResolver still
                // rolled a miss (CalculateScoringChance caps below 100% even
                // on a perfect read - see the plan's "Formula decision").
                // Send the ball over the bar instead of triggering
                // ScheduleBlock, which would read as an impossible save from
                // a keeper who was nowhere near the ball's flight path.
                targetPoint.y = 2.44f + 0.9f;
            }
        }

        yield return gameManager.kicker.PlayKick(() => ball.Shoot(targetPoint, shotPower, curve));

        // Give the ball's own flight/deflection animation a moment to read
        // before the next turn resets everything - matches the ~1-2s pause
        // GameController used between a shot resolving and the next round.
        yield return new WaitForSeconds(1.2f);

        var shotData = new ShotData
        {
            // Free-aim continuous X, rounded to an int purely to match the
            // existing Dart-side ShotData.direction contract (int) - Dart
            // doesn't use this value for any decision, just carries it
            // through, so rounding loses nothing that mattered.
            direction = Mathf.RoundToInt(targetX),
            power = power,
            effect = effect,
            goalkeeperDirection = keeperDirection,
            isGoal = isGoal,
        };
        _state.RecordShotResult(_isTeam1Turn, isGoal, shotData);
        jumbotron?.SetScore(_state.Team1Score, _state.Team2Score);

        if (hud != null)
        {
            hud.ShowResult(MatchLocalization.GetResultText(isGoal, effect, power));
            hud.SetScore(_state.Team1Score, _state.Team2Score);
            hud.SetPips(_state.Team1Results, _state.Team2Results);
            hud.ShowSuddenDeath(_state.IsSuddenDeathActive);
            hud.SetRoundText(BuildRoundText());
        }

        if (_isTeam1Turn && isGoal)
        {
            switch (effect)
            {
                case ShotEffect.Curve: _matchGoalsCurve++; break;
                case ShotEffect.Lob: _matchGoalsLob++; break;
                case ShotEffect.Knuckle: _matchGoalsKnuckle++; break;
            }
        }

        if (!_isTeam1Turn) _ai.SetLastShotResult(isGoal);

        // Rewind is a player-only do-over, offered only when THIS turn's
        // result was bad for the player specifically - not "not a goal" in
        // general, which used to also fire when the opponent (team2) simply
        // missed their own shot (a good outcome for the player, nothing to
        // undo) and never fired when the opponent scored against the player
        // (isGoal true on team2's turn - the one case a player-side rewind
        // actually makes sense for, since it means the player just conceded).
        bool badOutcomeForPlayer = _isTeam1Turn ? !isGoal : isGoal;
        _rewindOfferedThisTurn = badOutcomeForPlayer && _state.CanRewind;
    }

    // Kicker/keeper are fixed roles in the scene (not tied to a specific
    // team) - whichever team is shooting this turn wears the kicker's kit,
    // the other wears the keeper's, so this needs to run every turn.
    void ApplyKitColorsForTurn(bool team1IsShooting)
    {
        var shooterTeam = team1IsShooting ? _launchParams.team1 : _launchParams.team2;
        var defenderTeam = team1IsShooting ? _launchParams.team2 : _launchParams.team1;
        if (shooterTeam != null) kickerKitTint?.SetColor(ArgbToColor(shooterTeam.colorArgb));
        if (defenderTeam != null) keeperKitTint?.SetColor(ArgbToColor(defenderTeam.colorArgb));
    }

    // Matches Flutter's Color.value packing (0xAARRGGBB). Alpha is forced
    // opaque - some predefined teams use Material named colors with baked-in
    // transparency (e.g. Colors.white70), which is a UI choice for chips/
    // backgrounds elsewhere, not something a jersey should ever inherit.
    static Color ArgbToColor(long argb)
    {
        uint v = (uint)argb;
        float r = ((v >> 16) & 0xFF) / 255f;
        float g = ((v >> 8) & 0xFF) / 255f;
        float b = (v & 0xFF) / 255f;
        return new Color(r, g, b, 1f);
    }

    // Port of GameHelpers.getRoundText().
    string BuildRoundText()
    {
        if (_state.IsSuddenDeathActive)
        {
            int round = _state.Team1SuddenDeathResults.Count + 1;
            return $"{MatchLocalization.Get("sudden_death")} {round}";
        }
        int regularRound = Mathf.Min(_state.Team1Shots + 1, PenaltySettings.ShotsPerTeam);
        return $"{regularRound}/{PenaltySettings.ShotsPerTeam}";
    }

    void FinishMatch(bool completed)
    {
        var result = new MatchResult
        {
            completed = completed,
            team1Score = _state.Team1Score,
            team2Score = _state.Team2Score,
            isSuddenDeathActive = _state.IsSuddenDeathActive,
            isUserWinner = _state.IsTeam1Winner() ?? false,
            rewindsUsed = _state.RewindsUsed,
            team1ShotData = _state.Team1ShotData,
            team2ShotData = _state.Team2ShotData,
            matchGoalsCurve = _matchGoalsCurve,
            matchGoalsLob = _matchGoalsLob,
            matchGoalsKnuckle = _matchGoalsKnuckle,
        };

        Debug.Log($"MatchController: match finished, {result.team1Score}-{result.team2Score}, userWon={result.isUserWinner}");
        MatchResultSender.SendMatchResult(result.ToJson());
    }
}
