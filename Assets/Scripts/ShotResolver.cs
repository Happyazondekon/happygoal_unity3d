using UnityEngine;

// Decides whether a shot is a goal or a save. Originally a line-for-line
// port of GameState.calculateScoringChance() in lib/models/game_state.dart
// (the user explicitly chose this richer, continuous-probability formula
// over the simpler one GameController._handleShotResult() shipped with, as
// part of the Unity-as-separate-Activity migration - see the plan's
// "Formula decision" section for what that changed vs. before).
//
// Reworked for free-aim shooting: the shot is no longer one of 3 fixed
// zones, it's a continuous world-X position (the keeper's dive still is a
// zone - see the plan discussion on scoping this to the shooter only). The
// old binary "same zone or not" +-0.35 swing is now a smooth function of
// how far apart the shot and the dive actually land.
//
// shotPrecision is fixed at 1.0 here, matching every code path in the Dart
// codebase - nothing ever sets it to anything else, so the
// `chance *= precision*precision` term is a no-op and is left out entirely
// rather than hard-coded to a magic 1.0 multiply.
public static class ShotResolver
{
    // Matches the aim margin already used by PenaltyKickInput/GameManager/AI
    // (goalHalfWidth 3.66 minus a 0.3 safety margin) - the practical max
    // distance a shot or a keeper's dive zone ends up from centre.
    public const float MaxAimDistance = 3.36f;

    // How far a keeper's dive plausibly reaches for the visual save-vs-miss
    // decision (see MatchController.ResolveAndPlayShot) - separate from the
    // probability below, which stays smooth/continuous with no hard cutoff.
    public const float KeeperReachRadius = 1.1f;

    public static float CalculateScoringChance(float shotX, float keeperX, string effect, int power)
    {
        float chance = 0.4f;

        // Continuous replacement for the old zone match/mismatch +-0.35
        // swing: shot and dive landing on top of each other behaves like the
        // old "same zone" (-0.35); maximally far apart behaves like the old
        // "different zone" (+0.35), smoothly in between.
        float normalizedDistance = Mathf.Clamp01(Mathf.Abs(shotX - keeperX) / MaxAimDistance);
        chance += Mathf.Lerp(-0.35f, 0.35f, normalizedDistance);

        switch (effect)
        {
            case ShotEffect.Curve: chance += 0.12f; break;
            case ShotEffect.Knuckle: chance += 0.15f; break;
            case ShotEffect.Lob: chance += 0.08f; break;
        }

        if (power > 95) chance -= 0.7f;
        if (power > 85) chance += 0.08f;
        else if (power < 40) chance -= 0.25f;
        else if (power < 60) chance -= 0.1f;

        return Mathf.Clamp(chance, 0.03f, 0.9f);
    }

    public static bool IsGoal(float shotX, float keeperX, string effect, int power)
    {
        return Random.value < CalculateScoringChance(shotX, keeperX, effect, power);
    }
}
