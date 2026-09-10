using System.Collections.Generic;
using UnityEngine;

// Line-for-line port of lib/models/ai_opponent.dart's AIOpponent - direction/
// power/effect heuristics weighted by `intelligence` (0=random, 1=perfect).
// Per the Unity-as-separate-Activity plan, gets a FRESH instance every match
// (no cross-tournament persistence, unlike the Dart original - the user
// picked this deliberately as a simplification).
//
// `adjustedIntelligence` (computed, ramps up after 20/40 shots) is dead code
// in the Dart source - it's assigned but never read by anything below it.
// Ported faithfully rather than "fixed", matching the plan's explicit
// decision to preserve known Dart quirks rather than silently changing
// behavior during an architecture migration.
//
// predictGoalkeeperMove() is NOT ported - confirmed dead code in Dart too
// (GameState.getAIDecision() never calls it).
public class AIOpponent
{
    public readonly float intelligence;

    int _previousDirection = -1;
    int _consecutiveSameDirection = 0;
    readonly List<string> _recentEffects = new List<string>();
    int _shotsTaken = 0;
    bool _lastShotWasGoal = false;

    public AIOpponent(float intelligence = 0.6f)
    {
        this.intelligence = intelligence;
    }

    public void TakeShot(out int direction, out int power, out string effect)
    {
        _shotsTaken++;

        // ---- DIRECTION ----
        float directionChoice = Random.value;
        float adjustedIntelligence = intelligence;
        if (_shotsTaken > 20) adjustedIntelligence = Mathf.Min(1f, intelligence + 0.2f);
        if (_shotsTaken > 40) adjustedIntelligence = Mathf.Min(1f, intelligence + 0.3f);
        // adjustedIntelligence is intentionally unused below it - see class doc.
        _ = adjustedIntelligence;

        if (_previousDirection != -1 && _consecutiveSameDirection >= 1 && Random.value < intelligence * 0.9f)
        {
            var otherDirections = new List<int> { 0, 1, 2 };
            otherDirections.Remove(_previousDirection);
            direction = otherDirections[Random.Range(0, otherDirections.Count)];
        }
        else if (directionChoice < intelligence * 0.9f)
        {
            direction = Random.value < 0.5f ? ShotDirection.Left : ShotDirection.Right;
        }
        else if (directionChoice < intelligence)
        {
            var directions = new List<int> { ShotDirection.Left, ShotDirection.Right };
            if (Random.value > 0.8f) directions.Add(ShotDirection.Center);
            direction = directions[Random.Range(0, directions.Count)];
        }
        else
        {
            direction = Random.value < 0.5f
                ? ShotDirection.Center
                : (Random.value < 0.5f ? ShotDirection.Left : ShotDirection.Right);
        }

        if (direction == _previousDirection)
        {
            _consecutiveSameDirection++;
        }
        else
        {
            _previousDirection = direction;
            _consecutiveSameDirection = 0;
        }

        // ---- POWER ----
        if (Random.value < intelligence * 0.9f)
        {
            power = 70 + Random.Range(0, 25);
            if (Random.value < intelligence * 0.4f)
            {
                power = 85 + Random.Range(0, 15);
            }
        }
        else if (_lastShotWasGoal && Random.value < intelligence * 0.7f)
        {
            power = 65 + Random.Range(0, 30);
        }
        else
        {
            power = 40 + Random.Range(0, 40);
        }

        // ---- EFFECT ----
        float effectChoice = Random.value;
        if (effectChoice < intelligence * 0.6f)
        {
            float specialEffect = Random.value;

            var availableEffects = new List<string> { ShotEffect.Curve, ShotEffect.Lob, ShotEffect.Knuckle };
            availableEffects.RemoveAll(e => _recentEffects.Contains(e));
            if (availableEffects.Count == 0)
            {
                availableEffects = new List<string> { ShotEffect.Curve, ShotEffect.Lob, ShotEffect.Knuckle };
            }

            if (specialEffect < 0.5f) effect = ShotEffect.Knuckle;
            else if (specialEffect < 0.8f) effect = ShotEffect.Curve;
            else effect = ShotEffect.Lob;
        }
        else
        {
            effect = ShotEffect.Normal;
        }

        _recentEffects.Add(effect);
        if (_recentEffects.Count > 2) _recentEffects.RemoveAt(0);
    }

    public void SetLastShotResult(bool wasGoal)
    {
        _lastShotWasGoal = wasGoal;
    }
}
