using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
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
            options = BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.DetailedBuildReport
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException($"Debug build failed: {report.summary.result}");
        }

        string outputDir = Path.GetDirectoryName(output)!;
        string[] pdbFiles = Directory.Exists(outputDir) ? Directory.GetFiles(outputDir, "*.pdb", SearchOption.TopDirectoryOnly) : new string[0];
        UnityEngine.Debug.Log($"Debug build output: {output}");
        UnityEngine.Debug.Log($"Debug symbols(.pdb) count: {pdbFiles.Length}");
        if (pdbFiles.Length > 0)
        {
            UnityEngine.Debug.Log($"Debug symbols sample: {Path.GetFileName(pdbFiles.First())}");
        }
    }
}
