#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Altera SOMENTE a cena exclusiva de tutorial e cria imagens opcionais a partir de assets do proprio jogo.</summary>
public static class TutorialKnowledgeSetup
{
    private const string ScenePath = "Assets/Scenes/Tutorial_Estagiar.unity";
    private const string IconsDir = "Assets/Resources/TutorialIcons";
    [MenuItem("Tools/E-stagiar.io/Tutorial/Instalar biblioteca sem alterar tablet")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorUtility.DisplayDialog("Biblioteca", "Saia de Play antes de instalar.", "OK"); return; }
        if (!File.Exists(ScenePath)) { EditorUtility.DisplayDialog("Biblioteca", "Crie/repare primeiro Tutorial_Estagiar.unity.", "OK"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string backup = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Tutorial_Estagiar.pre-biblioteca.backup.unity");
        File.Copy(ScenePath, backup, true);
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        TicketTabletPrototype tablet = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            tablet = root.GetComponentsInChildren<TicketTabletPrototype>(true).FirstOrDefault();
            if (tablet != null) break;
        }
        if (tablet == null) { Debug.LogError("[Biblioteca] Tablet nao encontrado. Cena original nao foi alterada."); return; }
        if (tablet.GetComponent<TutorialKnowledgeBase>() == null) tablet.gameObject.AddComponent<TutorialKnowledgeBase>();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Biblioteca] Instalada em " + ScenePath + ". Original Fase_01 e tablet do designer preservados. Backup: " + backup);
        EditorUtility.DisplayDialog("Biblioteca instalada", "Abra a cena Tutorial_Estagiar, Play, T e clique BIBLIOTECA (ou F1).\n\nAs imagens podem ser geradas pelo segundo comando do menu, sem alterar os prefabs originais.", "OK");
    }

    private static readonly Dictionary<string, string[]> Photos = new Dictionary<string, string[]> {
        {"gabinete",new[]{"Estacao_Trabalho","ComputerCase"}}, {"placa-mae",new[]{"placa_mae","placa mae","motherboard"}},
        {"fonte",new[]{"fonte","power supply"}}, {"ram",new[]{"ram"}}, {"ssd",new[]{"ssd","hd"}},
        {"cooler",new[]{"cooler","fan"}}, {"monitor",new[]{"monitor"}}, {"sistema",new[]{"pendrive"}},
        {"energia",new[]{"cabo_energia","power cable"}}, {"energia-monitor",new[]{"cabo_energia","power cable"}},
        {"hdmi",new[]{"hdmi"}}, {"vga",new[]{"vga"}}, {"rede",new[]{"cabo_rede","rj45"}},
        {"chave",new[]{"pickup_ferramenta","chave"}}, {"alicate",new[]{"pickup_alicate","alicate"}},
        {"pendrive",new[]{"pickup_pendrive","pendrive"}}, {"testador",new[]{"testador_caborede","testador_cabo"}},
        {"impressora",new[]{"impressora"}}, {"toner",new[]{"pickup_toner","toner_toon"}},
        {"resma",new[]{"pickup_resmapapel","resma_toon"}}, {"impressora-rede",new[]{"impressora"}},
        {"notebook",new[]{"notebook_toon","notebook_estacao"}}, {"ram-notebook",new[]{"notebook_ram"}},
        {"ssd-notebook",new[]{"notebook_ssd"}}, {"sistema-notebook",new[]{"notebook_estacao"}},
        {"rack",new[]{"rack"}}, {"crimpagem",new[]{"ui_screen_crimpagem","crimp"}},
        {"conectividade",new[]{"testador_caborede","testador_cabo"}}
    };

    // GetAssetPreview e assicrono: este menu usa tentativas agendadas durante a atualizacao do Editor.
    private static Queue<KeyValuePair<string,string>> pending;
    private static int attempts;
    private static int created;
    private static int missing;
    [MenuItem("Tools/E-stagiar.io/Tutorial/Gerar fotos dos itens do jogo")]
    public static void GeneratePhotos()
    {
        if (pending != null) { Debug.Log("[Biblioteca] Geracao de imagens ja em andamento."); return; }
        Directory.CreateDirectory(IconsDir);
        AssetDatabase.Refresh();
        string[] paths = AssetDatabase.FindAssets("t:Prefab").Concat(AssetDatabase.FindAssets("t:Model")).Concat(AssetDatabase.FindAssets("t:Texture2D")).Distinct()
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.StartsWith("Assets/") && !p.StartsWith(IconsDir) && !p.Contains(".meta"))
            .ToArray();
        pending = new Queue<KeyValuePair<string,string>>();
        foreach (var entry in Photos)
        {
            string file = IconsDir + "/" + entry.Key + ".png";
            if (File.Exists(file)) continue;
            string path = paths.FirstOrDefault(p => entry.Value.Any(token => Path.GetFileNameWithoutExtension(p).IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0));
            if (path != null) pending.Enqueue(new KeyValuePair<string, string>(entry.Key, path)); else missing++;
        }
        attempts = 0; created = 0;
        EditorApplication.update += Pump;
        Debug.Log("[Biblioteca] Preparando miniaturas de " + pending.Count + " itens com assets encontrados.");
    }
    private static void Pump()
    {
        if (pending == null) { EditorApplication.update -= Pump; return; }
        if (pending.Count == 0)
        {
            EditorApplication.update -= Pump; pending = null; AssetDatabase.Refresh();
            Debug.Log("[Biblioteca] Fotos criadas: " + created + "; sem imagem disponivel: " + missing + ". Os itens sem foto continuam funcionando com texto.");
            return;
        }
        var target = pending.Peek();
        var asset = AssetDatabase.LoadMainAssetAtPath(target.Value);
        Texture2D preview = asset != null ? AssetPreview.GetAssetPreview(asset) : null;
        if (preview == null && ++attempts < 80) return;
        try
        {
            if (preview != null)
            {
                var rt = RenderTexture.GetTemporary(200, 150, 0, RenderTextureFormat.ARGB32);
                var before = RenderTexture.active;
                Graphics.Blit(preview, rt); RenderTexture.active = rt;
                var copy = new Texture2D(200, 150, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0,0,200,150),0,0); copy.Apply();
                File.WriteAllBytes(IconsDir + "/" + target.Key + ".png", copy.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(copy);
                RenderTexture.active = before; RenderTexture.ReleaseTemporary(rt);
                created++;
            }
            else missing++;
        }
        catch (Exception error) { Debug.LogWarning("[Biblioteca] Foto nao criada: " + target.Key + " | " + error.Message); missing++; }
        pending.Dequeue(); attempts = 0;
    }
}
#endif
