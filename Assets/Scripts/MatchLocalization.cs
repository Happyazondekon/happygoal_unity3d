using System.Collections.Generic;

// Unity's own small FR/EN string table for in-match UI text - the user
// deliberately chose this over passing pre-localized strings down from
// Flutter per launch (see the Unity-as-separate-Activity plan's localization
// decision). Ported from the exact strings game_screen.dart/
// GameController.getResultText() use today.
public static class MatchLocalization
{
    public const string Curve = "curve";
    public const string Lob = "lob";
    public const string Knuckle = "knuckle";
    public const string Weak = "weak";
    public const string Generic = "generic";

    static readonly Dictionary<string, Dictionary<string, string>> Strings = new Dictionary<string, Dictionary<string, string>>
    {
        ["fr"] = new Dictionary<string, string>
        {
            ["goal_lob"] = "BUUT ! Une panenka magnifique !",
            ["goal_curve"] = "BUUT ! Quelle frappe enroulée !",
            ["goal_knuckle"] = "BUUT ! Frappe enroulée imparable !",
            ["goal_weak"] = "BUUT ! Le gardien n'a rien pu faire !",
            ["goal_generic"] = "BUUT !",
            ["saved_weak"] = "ARRÊT ! Tir trop faible...",
            ["saved_generic"] = "ARRÊT du gardien !",
            ["sudden_death"] = "MORT SUBITE",
            ["ai_shooting"] = "L'adversaire tire...",
            ["your_turn_shoot"] = "GLISSE POUR TIRER",
            ["your_turn_defend"] = "GLISSE POUR PLONGER",
            ["effect_normal"] = "Normal",
            ["effect_curve"] = "Effet",
            ["effect_lob"] = "Lob",
            ["effect_knuckle"] = "Enroulé",
            ["rewind_offer"] = "Rembobiner ?",
        },
        ["en"] = new Dictionary<string, string>
        {
            ["goal_lob"] = "GOAL! A beautiful panenka!",
            ["goal_curve"] = "GOAL! What a curved strike!",
            ["goal_knuckle"] = "GOAL! Unstoppable knuckleball!",
            ["goal_weak"] = "GOAL! The keeper couldn't do anything!",
            ["goal_generic"] = "GOAL!",
            ["saved_weak"] = "SAVED! Too weak a shot...",
            ["saved_generic"] = "SAVED by the keeper!",
            ["sudden_death"] = "SUDDEN DEATH",
            ["ai_shooting"] = "Opponent is shooting...",
            ["your_turn_shoot"] = "SWIPE TO SHOOT",
            ["your_turn_defend"] = "SWIPE TO DIVE",
            ["effect_normal"] = "Normal",
            ["effect_curve"] = "Curve",
            ["effect_lob"] = "Lob",
            ["effect_knuckle"] = "Knuckle",
            ["rewind_offer"] = "Rewind?",
        },
    };

    public static string Locale = "fr";

    public static string Get(string key)
    {
        var table = Strings.TryGetValue(Locale, out var t) ? t : Strings["fr"];
        return table.TryGetValue(key, out var value) ? value : key;
    }

    // Port of GameController.getResultText()'s selection order: on a goal,
    // lob > curve > knuckle > weak(<30 power) > generic; on a miss,
    // weak(<20 power) > generic.
    public static string GetResultText(bool isGoal, string effect, int power)
    {
        if (isGoal)
        {
            if (effect == ShotEffect.Lob) return Get("goal_lob");
            if (effect == ShotEffect.Curve) return Get("goal_curve");
            if (effect == ShotEffect.Knuckle) return Get("goal_knuckle");
            if (power < 30) return Get("goal_weak");
            return Get("goal_generic");
        }
        else
        {
            if (power < 20) return Get("saved_weak");
            return Get("saved_generic");
        }
    }

    public static string EffectLabel(string effect)
    {
        switch (effect)
        {
            case ShotEffect.Curve: return Get("effect_curve");
            case ShotEffect.Lob: return Get("effect_lob");
            case ShotEffect.Knuckle: return Get("effect_knuckle");
            default: return Get("effect_normal");
        }
    }
}
