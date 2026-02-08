using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class BuildWindowsDebug
{
    public static void Build()
    {
        string projectDir = Directory.GetCurrentDirectory();
        string output = Path.Combine(projectDir, "Builds", "WindowsClient_Debug", "Doudizhu_Debug.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development | BuildOptions.AllowDebugging
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException($"Debug build failed: {report.summary.result}");
        }
    }
}
