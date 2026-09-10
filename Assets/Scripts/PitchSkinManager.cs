using System;
using UnityEngine;

// Applies a purchased pitch/stadium skin as a lighting + ground-tint mood
// (no new stadium models needed - the recovered Football Arena stays the
// same mesh either way). This only changes what the CLEAR-weather baseline
// looks like; WeatherController's Rain/Snow states already have their own
// absolute look and are untouched, so a match can still rain on a night
// pitch. Skin ids match CosmeticItem.catalog on the Flutter side
// (lib/models/cosmetic_item.dart).
//
// Press 7/8/9 in Play mode to test each skin without the bridge wired up yet.
public class PitchSkinManager : MonoBehaviour
{
    [Serializable]
    public class Skin
    {
        public string id;
        public Color groundTint = Color.white;
        public float sunIntensityMultiplier = 1f;
        public Color sunColor = Color.white;
        public Color ambientSky;
        public Color ambientGround;
    }

    public Renderer pitchRenderer;
    public Light sun;
    public WeatherController weather;

    public Skin[] skins =
    {
        new Skin
        {
            id = "pitch_municipal",
            groundTint = Color.white,
            sunIntensityMultiplier = 1f,
            sunColor = Color.white,
            ambientSky = new Color(0.6f, 0.75f, 0.9f),
            ambientGround = new Color(0.25f, 0.3f, 0.2f),
        },
        new Skin
        {
            id = "pitch_night",
            groundTint = new Color(0.5f, 0.55f, 0.65f),
            sunIntensityMultiplier = 0.45f,
            sunColor = new Color(0.75f, 0.8f, 1f),
            ambientSky = new Color(0.12f, 0.15f, 0.28f),
            ambientGround = new Color(0.04f, 0.05f, 0.07f),
        },
        new Skin
        {
            id = "pitch_snow",
            groundTint = new Color(0.93f, 0.95f, 0.98f),
            sunIntensityMultiplier = 0.85f,
            sunColor = new Color(0.9f, 0.92f, 1f),
            ambientSky = new Color(0.75f, 0.78f, 0.85f),
            ambientGround = new Color(0.55f, 0.58f, 0.63f),
        },
    };

    public KeyCode municipalKey = KeyCode.Alpha7;
    public KeyCode nightKey = KeyCode.Alpha8;
    public KeyCode snowKey = KeyCode.Alpha9;

    Material pitchMatInstance;
    float baseSunIntensity;
    string currentId;

    void Awake()
    {
        if (pitchRenderer != null)
        {
            pitchMatInstance = new Material(pitchRenderer.sharedMaterial);
            pitchRenderer.sharedMaterial = pitchMatInstance;
        }
        if (sun != null) baseSunIntensity = sun.intensity;
    }

    void Start()
    {
        ApplySkin("pitch_municipal");
    }

    void Update()
    {
        if (Input.GetKeyDown(municipalKey)) ApplySkin("pitch_municipal");
        else if (Input.GetKeyDown(nightKey)) ApplySkin("pitch_night");
        else if (Input.GetKeyDown(snowKey)) ApplySkin("pitch_snow");
    }

    public void ApplySkin(string skinId)
    {
        if (skinId == currentId) return;

        Skin match = null;
        foreach (var skin in skins)
        {
            if (skin.id == skinId) { match = skin; break; }
        }
        if (match == null)
        {
            Debug.LogWarning($"PitchSkinManager: unknown skin id '{skinId}'.");
            return;
        }

        currentId = skinId;

        if (pitchMatInstance != null) pitchMatInstance.color = match.groundTint;
        if (sun != null)
        {
            sun.intensity = baseSunIntensity * match.sunIntensityMultiplier;
            sun.color = match.sunColor;
        }
        RenderSettings.ambientSkyColor = match.ambientSky;
        RenderSettings.ambientGroundColor = match.ambientGround;

        if (weather != null)
        {
            // The new mood becomes the "clear weather" baseline that
            // WeatherController.Apply(Sunny) reverts to.
            weather.RefreshSunnyDefaults();
            // "pitch_snow" is the one skin that's actual weather, not just a
            // lighting mood - make it genuinely snow rather than just tint
            // the ground white (confirmed gap: equipping it previously had
            // no visible snowfall at all).
            weather.Apply(skinId == "pitch_snow"
                ? WeatherController.Weather.Snow
                : WeatherController.Weather.Sunny);
        }
    }
}
