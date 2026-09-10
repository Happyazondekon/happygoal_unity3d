using UnityEngine;

// Unity's half of the Activity-boundary bridge to
// android/app/src/main/kotlin/com/heyhappy/happygoal/UnityMatchResultBridge.kt -
// mirrors the exact reflection pattern Assets/FlutterEmbed/SendToFlutter/SendToFlutter.cs
// already used for the old PlatformView plugin, just pointed at our own small
// native class instead. This is what replaces FlutterBridge.cs / SendToFlutter.cs
// once MatchController fully owns the match (see the Unity-as-separate-Activity plan).
public static class MatchResultSender
{
    const string BridgeClass = "com.heyhappy.happygoal.UnityMatchResultBridge";

    /// Reads the MatchLaunchParams JSON this Activity was started with.
    /// Returns an empty string if none was provided (e.g. testing in the
    /// Editor, where this whole class is a no-op - see the #if guards below).
    public static string GetLaunchParams()
    {
#if UNITY_EDITOR
        return "";
#elif UNITY_ANDROID
        using (var bridge = new AndroidJavaClass(BridgeClass))
        {
            return bridge.CallStatic<string>("getLaunchParams");
        }
#else
        return "";
#endif
    }

    /// Hands the final MatchResult JSON back to MainActivity and closes this
    /// Activity - see UnityMatchResultBridge.reportMatchResult.
    public static void SendMatchResult(string json)
    {
#if UNITY_EDITOR
        Debug.Log("MatchResultSender - " + json);
#elif UNITY_ANDROID
        using (var bridge = new AndroidJavaClass(BridgeClass))
        {
            bridge.CallStatic("reportMatchResult", json);
        }
#endif
    }
}
