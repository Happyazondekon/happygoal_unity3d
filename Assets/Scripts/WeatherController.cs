using UnityEngine;

// Switches lighting/fog/particles between three weather looks. Press 1/2/3
// to test in the Editor; Flutter will eventually drive this instead (match
// weather could be cosmetic, tied to a stadium, or random per match).
public class WeatherController : MonoBehaviour
{
    public enum Weather { Sunny, Rain, Snow }

    public Light sun;
    public ParticleSystem rain;
    public ParticleSystem snow;

    public KeyCode sunnyKey = KeyCode.Alpha1;
    public KeyCode rainKey = KeyCode.Alpha2;
    public KeyCode snowKey = KeyCode.Alpha3;

    float sunnyIntensity;
    Color sunnyLightColor;
    Color sunnySkyColor;
    Color sunnyGroundColor;
    bool cached;

    void Start()
    {
        CacheSunnyDefaults();
        Apply(Weather.Sunny);
    }

    void CacheSunnyDefaults()
    {
        if (cached) return;
        cached = true;
        if (sun != null)
        {
            sunnyIntensity = sun.intensity;
            sunnyLightColor = sun.color;
        }
        sunnySkyColor = RenderSettings.ambientSkyColor;
        sunnyGroundColor = RenderSettings.ambientGroundColor;
    }

    // Re-reads the current sun/ambient values as the new "Sunny" baseline -
    // called by PitchSkinManager after it applies a pitch skin's own mood,
    // so switching back to clear weather mid-match returns to that skin's
    // look instead of the very first values cached at Start().
    public void RefreshSunnyDefaults()
    {
        if (sun != null)
        {
            sunnyIntensity = sun.intensity;
            sunnyLightColor = sun.color;
        }
        sunnySkyColor = RenderSettings.ambientSkyColor;
        sunnyGroundColor = RenderSettings.ambientGroundColor;
        cached = true;
    }

    void Update()
    {
        if (Input.GetKeyDown(sunnyKey)) Apply(Weather.Sunny);
        else if (Input.GetKeyDown(rainKey)) Apply(Weather.Rain);
        else if (Input.GetKeyDown(snowKey)) Apply(Weather.Snow);
    }

    public void Apply(Weather weather)
    {
        CacheSunnyDefaults();

        if (rain != null)
        {
            if (weather == Weather.Rain) rain.Play(); else rain.Stop();
        }
        if (snow != null)
        {
            if (weather == Weather.Snow) snow.Play(); else snow.Stop();
        }

        switch (weather)
        {
            case Weather.Sunny:
                if (sun != null) { sun.intensity = sunnyIntensity; sun.color = sunnyLightColor; }
                RenderSettings.ambientSkyColor = sunnySkyColor;
                RenderSettings.ambientGroundColor = sunnyGroundColor;
                RenderSettings.fog = false;
                break;

            case Weather.Rain:
                if (sun != null) { sun.intensity = sunnyIntensity * 0.55f; sun.color = new Color(0.75f, 0.8f, 0.85f); }
                RenderSettings.ambientSkyColor = new Color(0.45f, 0.5f, 0.55f);
                RenderSettings.ambientGroundColor = new Color(0.2f, 0.22f, 0.24f);
                RenderSettings.fog = true;
                RenderSettings.fogColor = new Color(0.55f, 0.58f, 0.62f);
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = 15f;
                RenderSettings.fogEndDistance = 70f;
                break;

            case Weather.Snow:
                if (sun != null) { sun.intensity = sunnyIntensity * 0.75f; sun.color = new Color(0.9f, 0.92f, 1f); }
                RenderSettings.ambientSkyColor = new Color(0.75f, 0.78f, 0.85f);
                RenderSettings.ambientGroundColor = new Color(0.6f, 0.63f, 0.68f);
                RenderSettings.fog = true;
                RenderSettings.fogColor = new Color(0.85f, 0.87f, 0.92f);
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogStartDistance = 20f;
                RenderSettings.fogEndDistance = 90f;
                break;
        }
    }
}
