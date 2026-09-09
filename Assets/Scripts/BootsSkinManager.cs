using UnityEngine;

// Recolors this character's shoes (the "Ch38_Shoes" child renderer) to match
// a purchased shop skin. Confirmed via Happygoal > 4) Inspect Character
// Renderers that Ch38_Shoes is its own renderer sharing the same base
// material as Ch38_Shirt/Socks/Body, so it tints independently the same way
// TintCharacterKit already tints those in PrototypeSceneBuilder. Skin ids
// match CosmeticItem.catalog on the Flutter side
// (lib/models/cosmetic_item.dart).
//
// Press F5-F7 in Play mode to test each skin without the bridge wired up yet.
public class BootsSkinManager : MonoBehaviour
{
    [System.Serializable]
    public class Skin
    {
        public string id;
        public Color tint = Color.white;
    }

    public string shoesChildName = "Ch38_Shoes";

    public Skin[] skins =
    {
        // Matches the procedural fallback rig's boot color (see BuildLeg in
        // PrototypeSceneBuilder) so both character paths agree on "default".
        new Skin { id = "boots_turf", tint = new Color(0.08f, 0.08f, 0.1f) },
        new Skin { id = "boots_lightning", tint = new Color(1f, 0.8f, 0.2f) },
        new Skin { id = "boots_neon", tint = new Color(0.2f, 0.95f, 0.75f) },
    };

    public KeyCode[] testKeys = { KeyCode.F5, KeyCode.F6, KeyCode.F7 };

    Material instanceMat;
    string currentId;

    void Awake()
    {
        var shoes = FindDeepChild(transform, shoesChildName);
        if (shoes == null)
        {
            Debug.LogWarning($"BootsSkinManager: no child named '{shoesChildName}' found under {name}.");
            enabled = false;
            return;
        }

        var renderer = shoes.GetComponent<Renderer>();
        if (renderer == null || renderer.sharedMaterial == null)
        {
            Debug.LogWarning($"BootsSkinManager: '{shoesChildName}' under {name} has no renderer/material.");
            enabled = false;
            return;
        }

        instanceMat = new Material(renderer.sharedMaterial);
        renderer.sharedMaterial = instanceMat;
        ApplySkin("boots_turf");
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
        Debug.LogWarning($"BootsSkinManager: unknown skin id '{skinId}'.");
    }

    static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }
}
