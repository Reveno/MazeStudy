// Пакетні команди (Unity -batchmode -executeMethod ...):
//   MazeStudy.StudyBuild.Setup      — створити сцену Main (камера + StudyApp) і додати її до збірки
//   MazeStudy.StudyBuild.Build      — зібрати Build/MazeStudy.exe (Windows)
//   MazeStudy.StudyBuild.Benchmark  — повний експеримент → Results/raw.csv, Results/summary.csv
//   MazeStudy.StudyBuild.Verify     — перевірка коректності алгоритмів → Results/verification.txt
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MazeStudy
{
    public static class StudyBuild
    {
        const string ScenePath = "Assets/MazeStudy/Scenes/Main.unity";

        [MenuItem("MazeStudy/Створити сцену")]
        public static void Setup()
        {
            Directory.CreateDirectory("Assets/MazeStudy/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.white; cam.orthographic = true;
            new GameObject("MazeStudy").AddComponent<StudyApp>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.productName = "MazeStudy"; PlayerSettings.companyName = "Diploma";
            PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 800; PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            AssetDatabase.SaveAssets();
            Debug.Log("[MazeStudy] SETUP OK");
        }

        [MenuItem("MazeStudy/Зібрати програму")]
        public static void Build()
        {
            if (!File.Exists(ScenePath)) Setup();
            // текст вимірюваного коду – у збірку, щоб його можна було показати на вкладці «Код C#»
            Directory.CreateDirectory("Assets/MazeStudy/Resources");
            File.Copy("Assets/MazeStudy/Scripts/Core/Generators.cs", "Assets/MazeStudy/Resources/GeneratorsSource.txt", true);
            AssetDatabase.Refresh();
            PlayerSettings.runInBackground = true; // програма не ставиться на паузу, коли вікно не в фокусі (довгі експерименти, запити до LLM)
            var r = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Build/MazeStudy.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
            Debug.Log("[MazeStudy] BUILD " + r.summary.result);
        }

        // Збірка для Linux (x86_64): BuildLinux/MazeStudy.x86_64
        [MenuItem("MazeStudy/Зібрати програму (Linux)")]
        public static void BuildLinux()
        {
            if (!File.Exists(ScenePath)) Setup();
            Directory.CreateDirectory("Assets/MazeStudy/Resources");
            File.Copy("Assets/MazeStudy/Scripts/Core/Generators.cs", "Assets/MazeStudy/Resources/GeneratorsSource.txt", true);
            AssetDatabase.Refresh();
            PlayerSettings.runInBackground = true;
            var r = BuildPipeline.BuildPlayer(new[] { ScenePath }, "BuildLinux/MazeStudy.x86_64", BuildTarget.StandaloneLinux64, BuildOptions.None);
            Debug.Log("[MazeStudy] BUILD LINUX " + r.summary.result);
        }

        [MenuItem("MazeStudy/Експеримент (повний)")]
        public static void Benchmark()
        {
            // 5 незалежних серій по 30 запусків: середнє за 150 лабіринтами + перевірка, чи не міняються алгоритми місцями (stability.csv)
            var e = new Experiment { Sizes = new[] { 10, 25, 50, 100 }, Runs = 30, Repeats = 5 };
            e.Run();
            e.SaveCsv(Path.GetFullPath("Results"));
            Debug.Log("[MazeStudy] BENCHMARK OK: " + e.Raw.Count + " лабіринтів");
        }

        [MenuItem("MazeStudy/Перевірка алгоритмів")]
        public static void Verify()
        {
            Directory.CreateDirectory("Results");
            var text = Verification.Run();
            File.WriteAllText("Results/verification.txt", text, new System.Text.UTF8Encoding(true));
            Debug.Log("[MazeStudy] VERIFY OK " + text);
        }
    }
}
