using System;
using System.Collections.Generic;

// The two JSON message shapes crossing the Activity boundary - see the
// "Data contract" section of the Unity-as-separate-Activity plan. Kept as
// plain [Serializable] classes for JsonUtility (no dynamic-map support, so
// the schema has to be this explicit).

public static class MatchMode
{
    public const string Solo = "solo";
    public const string Tournament = "tournament";
    public const string Hero = "hero";
    public const string Multiplayer = "multiplayer";
}

[Serializable]
public class TeamInfo
{
    public string name;
    public long colorArgb;
    public string flagAssetKey;
}

[Serializable]
public class SkinsInfo
{
    public string ball;
    public string boots;
    public string pitch;
}

[Serializable]
public class MatchLaunchParams
{
    public int schemaVersion = 1;
    public string matchMode = MatchMode.Solo;
    public int heroLevel = -1;
    public float aiIntelligence = 0.6f;
    public int rewindsAvailableForThisMatch = 0;
    public string locale = "fr";
    public TeamInfo team1;
    public TeamInfo team2;
    public SkinsInfo equippedSkins;

    public static MatchLaunchParams FromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return new MatchLaunchParams();
        try
        {
            return UnityEngine.JsonUtility.FromJson<MatchLaunchParams>(json);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning($"MatchLaunchParams.FromJson: could not parse '{json}' - {e.Message}");
            return new MatchLaunchParams();
        }
    }
}

[Serializable]
public class MatchResult
{
    public int schemaVersion = 1;
    public bool completed = true;
    public int team1Score;
    public int team2Score;
    public bool isSuddenDeathActive;
    public bool isUserWinner;
    public int rewindsUsed;
    public List<ShotData> team1ShotData = new List<ShotData>();
    public List<ShotData> team2ShotData = new List<ShotData>();
    public int matchGoalsCurve;
    public int matchGoalsLob;
    public int matchGoalsKnuckle;

    public string ToJson() => UnityEngine.JsonUtility.ToJson(this);
}
