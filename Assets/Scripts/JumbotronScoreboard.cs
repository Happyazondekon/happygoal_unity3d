using UnityEngine;
using UnityEngine.UI;

// Live score rendered on world-space Canvases mounted on the stadium's big
// screen meshes (Football Arena.fbx's "Scoreboards"/"Scoreboards.001"
// children - one per stand, so both get a canvas since which one ends up in
// the match cameras' view isn't known ahead of time) - see
// PrototypeSceneBuilder.BuildJumbotronScoreboard. Kept separate from
// MatchHud (the 2D screen-space overlay) since this one lives in 3D world
// space on the arena model instead.
public class JumbotronScoreboard : MonoBehaviour
{
    public Text[] scoreTexts = new Text[0];

    public void SetScore(int team1Score, int team2Score)
    {
        string text = $"{team1Score} - {team2Score}";
        foreach (var t in scoreTexts)
        {
            if (t != null) t.text = text;
        }
    }
}
