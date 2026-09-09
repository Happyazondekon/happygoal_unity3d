using UnityEngine;

// Recolors the ball to match a purchased shop skin. Multiplies a tint over
// the existing diffuse texture (Football_Diffuse.jpg, or the procedural
// pentagon pattern when that asset is missing) instead of swapping textures,
// so the panel-line detail stays legible under every skin. Skin ids match
// CosmeticItem.catalog on the Flutter side (lib/models/cosmetic_item.dart)
// so the eventual Flutter<->Unity bridge can pass the id straight through.
//
// Press F1-F4 in Play mode to test each skin without the bridge wired up yet.
public class BallSkinManager : MonoBehaviour
{
    [System.Serializable]
    public class Skin
    {
        public string id;
        public Color tint = Color.white;
    }

    public Skin[] skins =
    {
        new Skin { id = "ball_classic", tint = Color.white },
        new Skin { id = "ball_flame", tint = new Color(1f, 0.45f, 0.35f) },
        new Skin { id = "ball_gold", tint = new Color(1f, 0.85f, 0.35f) },
        new Skin { id = "ball_galaxy", tint = new Color(0.6f, 0.5f, 1f) },
    };

    public KeyCode[] testKeys =
        { KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4 };

    Renderer rend;
    Material instanceMat;
    string currentId;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        // Clone once so tinting this ball never touches the shared material
        // asset (or any other ball instance).
        instanceMat = new Material(rend.sharedMaterial);
        rend.sharedMaterial = instanceMat;
        ApplySkin("ball_classic");
    }

    void Update()
    {
        for (int i = 0; i < testKeys.Length && i < skins.Length; i++)
        {
            if (Input.GetKeyDown(testKeys[i])) ApplySkin(skins[i].id);
        }
    }

    public void ApplySkin(string skinId)
    {
        if (skinId == currentId) return;
        foreach (var skin in skins)
        {
            if (skin.id != skinId) continue;
            instanceMat.color = skin.tint;
            currentId = skinId;
            return;
        }
        Debug.LogWarning($"BallSkinManager: unknown skin id '{skinId}'.");
    }
}
