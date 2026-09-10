using UnityEditor;
using UnityEngine;

// One-shot entry point for running the scene rebuild + Android library
// export from the command line (Unity -batchmode -executeMethod), so the
// export doesn't depend on someone clicking through the Editor menus.
//
// Usage: -buildTarget Android -executeMethod CIBuildRunner.BuildAndExportAndroid
//        -exportPath <path to android/unityLibrary>
public static class CIBuildRunner
{
    public static void BuildAndExportAndroid()
    {
        // Development + script debugging: the Release/IL2CPP build strips
        // most engine startup logging, so a silent on-device hang (see the
        // "stuck black screen after 'Product Name' " investigation) leaves
        // almost no trace in logcat. Development mode keeps that logging so
        // the next test run actually shows where startup stalls.
        EditorUserBuildSettings.development = true;
        EditorUserBuildSettings.allowDebugging = true;

        Debug.Log("CIBuildRunner: rebuilding the prototype scene...");
        PrototypeSceneBuilder.BuildScene();

        Debug.Log("CIBuildRunner: exporting Android library...");
        ProjectExporterBatchmode.ExportProjectAndroid();

        Debug.Log("CIBuildRunner: done.");
    }
}
