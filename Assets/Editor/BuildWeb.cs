using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Build WebGL direto em /web (o que a Vercel publica). Também roda por linha de comando:
/// Unity -batchmode -quit -projectPath . -executeMethod BuildWeb.Gerar</summary>
public static class BuildWeb
{
    [MenuItem("Tools/Build WebGL em /web")]
    public static void Gerar()
    {
        // a página com o carregamento e os créditos (Assets/WebGLTemplates/TelaCheia), não a padrão da Unity
        PlayerSettings.WebGL.template = "PROJECT:TelaCheia";
        string[] cenas = EditorBuildSettings.scenes.Where(c => c.enabled).Select(c => c.path).ToArray();
        BuildReport r = BuildPipeline.BuildPlayer(cenas, "web", BuildTarget.WebGL, BuildOptions.None);
        Debug.Log($"[BuildWeb] {r.summary.result} — {r.summary.totalSize / (1024 * 1024)} MB, {r.summary.totalErrors} erro(s)");
        if (Application.isBatchMode && r.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
