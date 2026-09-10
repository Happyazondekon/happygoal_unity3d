using UnityEngine;

// Recolors this character's jersey (Ch38_Shirt + Ch38_Socks) to match
// whichever team is currently controlling it. PrototypeSceneBuilder's
// TintCharacterKit already clones a per-instance material onto both
// renderers at build time (so tinting one kicker/keeper never affects the
// other), so this just needs to mutate that existing clone's color - no new
// material instantiation needed, unlike BootsSkinManager which clones lazily
// itself.
//
// MatchController calls SetColor with the current shooting/defending team's
// color at the start of every turn (the kicker/keeper GameObjects are fixed
// roles in the scene, not tied to a specific team - whichever team is
// shooting this turn wears the kicker's kit, the other wears the keeper's).
public class TeamKitTint : MonoBehaviour
{
    public string shirtChildName = "Ch38_Shirt";
    public string socksChildName = "Ch38_Socks";

    Renderer shirtRenderer;
    Renderer socksRenderer;

    void Awake()
    {
        var shirt = FindDeepChild(transform, shirtChildName);
        var socks = FindDeepChild(transform, socksChildName);
        shirtRenderer = shirt != null ? shirt.GetComponent<Renderer>() : null;
        socksRenderer = socks != null ? socks.GetComponent<Renderer>() : null;
    }

    public void SetColor(Color color)
    {
        if (shirtRenderer != null && shirtRenderer.sharedMaterial != null) shirtRenderer.sharedMaterial.color = color;
        if (socksRenderer != null && socksRenderer.sharedMaterial != null) socksRenderer.sharedMaterial.color = color;
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
