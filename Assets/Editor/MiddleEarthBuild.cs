using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MiddleEarthBuild
{
    private const string ScenePath = "Assets/Scenes/Game.unity";
    private static string ProjectRoot
    {
        get { return Directory.GetParent(Application.dataPath).FullName; }
    }

    [MenuItem("Путь Кольца/Создать игровую сцену")]
    public static void CreateScene()
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Scenes"));
        AssetDatabase.Refresh();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject root = new GameObject("Game / Путь Кольца");
        root.AddComponent<MiddleEarthGame>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Selection.activeGameObject = root;
        Debug.Log("Игровая сцена создана: " + ScenePath);
    }

    [MenuItem("Путь Кольца/Собрать Android APK")]
    public static void BuildAndroid()
    {
        if (!File.Exists(Path.Combine(ProjectRoot, ScenePath))) CreateScene();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        PlayerSettings.companyName = "Fan Prototype";
        PlayerSettings.productName = "Путь Кольца";
        PlayerSettings.bundleVersion = "0.1.0";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.fanprototype.putkoltsa");
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        EditorUserBuildSettings.buildAppBundle = false;

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
            !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
        {
            throw new Exception("Не удалось переключить платформу на Android. Проверьте Android Build Support.");
        }

        Directory.CreateDirectory(Path.Combine(ProjectRoot, "Builds"));
        string path = Path.Combine(ProjectRoot, "Builds/PutKoltsa.apk");
        BuildReport report = BuildPipeline.BuildPlayer(
            new[] { ScenePath }, path, BuildTarget.Android, BuildOptions.None);
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("Сборка APK не удалась. Проверьте Console и Editor.log.");
        Debug.Log("APK готов: " + path);
    }
}
