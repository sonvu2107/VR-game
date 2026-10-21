using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

[InitializeOnLoad]
public static class LanNameSmokeBuild
{
    static LanNameSmokeBuild() { EditorApplication.update += Tick; }
    private static void Tick()
    {
        const string request = "Temp/LanNames.build";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = "Build/LanNames/TheLabyrinth.exe", target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development });
        File.WriteAllText("Temp/LanNames.build-result", report.summary.result + " errors=" + report.summary.totalErrors);
    }
}
