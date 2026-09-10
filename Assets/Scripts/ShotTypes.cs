using System;

// Mirrors lib/models/game_state.dart's ShotDirection/ShotEffect/PenaltySettings
// and ShotData exactly - the C# side of the match-rules port (see the
// Unity-as-separate-Activity plan).
public static class ShotDirection
{
    public const int Left = 0;
    public const int Center = 1;
    public const int Right = 2;

    // 0=left, 1=center, 2=right -> world X offset from the goal's centre, at
    // the middle of each third of the 7.32m-wide goal. Matches
    // FlutterBridge.ZoneToX exactly (kept there too until the Phase 4
    // cleanup removes FlutterBridge for good).
    public static float ToWorldX(int zone)
    {
        switch (zone)
        {
            case Left: return -2.44f;
            case Right: return 2.44f;
            default: return 0f;
        }
    }
}

public static class ShotEffect
{
    public const string Normal = "normal";
    public const string Curve = "curve";
    public const string Lob = "lob";
    public const string Knuckle = "knuckle";
}

public static class PenaltySettings
{
    public const int ShotsPerTeam = 5;
}

[Serializable]
public class ShotData
{
    public int direction;
    public int power;
    public string effect;
    public int goalkeeperDirection;
    public bool isGoal;
}
