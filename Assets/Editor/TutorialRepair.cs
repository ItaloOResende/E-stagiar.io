#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Repara apenas a cena independente do tutorial. Nao modifica cenas/prefabs originais.</summary>
public static class TutorialRepair
{
    private const string ScenePath = "Assets/Scenes/Tutorial_Estagiar.unity";
    private const string SourcePath = "Assets/Scenes/Fase_01.unity";
    private const string SourceTicket = "Assets/TicketsData/Gerados/pc-montagem-completa.asset";
    private const string DestinationTicket = "Assets/TicketsData/Tutorial/TutorialMontagemCompleta.asset";

    [MenuItem("Tools/E-stagiar.io/Tutorial/REPARAR cena existente")]
    public static void Repair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) {
            EditorUtility.DisplayDialog("Tutorial", "Saia do Play antes de reparar.", "OK"); return;
        }
        if (!File.Exists(SourcePath)) { Fail("Cena-base nao encontrada: " + SourcePath); return; }
        if (!File.Exists(SourceTicket)) { Fail("Chamado-base nao encontrado: " + SourceTicket); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        try
        {
            if (!File.Exists(ScenePath))
            {
                if (!AssetDatabase.CopyAsset(SourcePath, ScenePath)) throw new Exception("Falha ao criar a copia da cena.");
                AssetDatabase.Refresh();
            }
            // Salva copia de seguranca local fora de Assets e fora do Git.
            var backup = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Tutorial_Estagiar.backup.unity");
            File.Copy(ScenePath, backup, true);
            var source = AssetDatabase.LoadMainAssetAtPath(SourceTicket);
            if (source == null) throw new Exception("Asset do chamado original nao foi carregado.");
            Directory.CreateDirectory("Assets/TicketsData/Tutorial");
            AssetDatabase.Refresh();
            var tutorial = AssetDatabase.LoadMainAssetAtPath(DestinationTicket);
            if (tutorial == null)
            {
                tutorial = UnityEngine.Object.Instantiate(source);
                tutorial.name = "TutorialMontagemCompleta";
                AssetDatabase.CreateAsset(tutorial, DestinationTicket);
            }
            TrySet(tutorial, "ticketId", p => p.stringValue = "pc-montagem-completa");
            TrySet(tutorial, "minimumStage", p => p.intValue = 0);
            TrySet(tutorial, "tutorialOnly", p => p.boolValue = true);
            TrySet(tutorial, "title", p => p.stringValue = "Primeiro dia - Montagem completa");
            TrySet(tutorial, "description", p => p.stringValue = "Encontre o solicitante, monte o PC vazio, conecte cabos, instale o sistema e teste.");
            EditorUtility.SetDirty(tutorial);

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Component stage = FindComponent(scene, "StageController");
            Component manager = FindComponent(scene, "TicketManager");
            Component tablet = FindComponent(scene, "TicketTabletPrototype");
            if (stage == null || manager == null || tablet == null)
                throw new Exception("Faltam componentes na cena copiada: StageController="+(stage!=null)+", TicketManager="+(manager!=null)+", TicketTabletPrototype="+(tablet!=null));

            bool stageOk = TrySet(stage, "stageNumber", p => p.intValue = 0);
            bool startOk = TrySet(stage, "startAutomatically", p => p.boolValue = true);
            TrySet(stage, "showStartMenu", p => p.boolValue = false);
            bool assetOk = TrySet(manager, "tutorialTicket", p => p.objectReferenceValue = tutorial);
            TrySet(manager, "requirePlayerToFinish", p => p.boolValue = true);
            TrySet(tablet, "toggleKey", p => p.intValue = (int)UnityEngine.InputSystem.Key.T);

            Type guideType = FindType("TutorialGuide");
            if (guideType == null) throw new Exception("Nao encontrei TutorialGuide.cs compilado. Verifique erros vermelhos no Console do Unity.");
            if (manager.gameObject != stage.gameObject)
                throw new Exception("StageController e TicketManager nao estao no mesmo objeto. O TutorialGuide atual exige ambos no mesmo GameObject.");
            if (manager.GetComponent(guideType) == null) manager.gameObject.AddComponent(guideType);

            if (!stageOk || !startOk || !assetOk)
                throw new Exception("A versao atual mudou campos essenciais. Veja os avisos [Tutorial Repair] no Console.");

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Falha ao salvar a cena de tutorial.");
            AssetDatabase.SaveAssets();
            Debug.Log("[Tutorial Repair] OK: cena="+ScenePath+" | tutorial="+DestinationTicket+" | objetos="+manager.gameObject.name+" | backup="+backup);
            EditorUtility.DisplayDialog("Tutorial reparado", "Cena reparada e salva! Com a cena Tutorial_Estagiar aberta, pressione Play. Se nao aparecer o painel PRIMEIRO DIA, abra Window > General > Console e confira os erros vermelhos.", "OK");
        }
        catch (Exception ex) { Fail(ex.ToString()); }
    }

    private static void Fail(string message)
    {
        Debug.LogError("[Tutorial Repair] " + message);
        EditorUtility.DisplayDialog("Falha no reparo do tutorial", message, "OK");
    }
    private static bool TrySet(UnityEngine.Object obj, string name, Action<SerializedProperty> assign)
    {
        var serialized = new SerializedObject(obj);
        var property = serialized.FindProperty(name);
        if (property == null) { Debug.LogWarning("[Tutorial Repair] Campo nao encontrado: " + obj.GetType().Name + "." + name); return false; }
        assign(property); serialized.ApplyModifiedPropertiesWithoutUndo(); return true;
    }
    private static Component FindComponent(Scene scene, string typeName)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var c in root.GetComponentsInChildren<Component>(true))
                if (c != null && c.GetType().Name == typeName) return c;
        return null;
    }
    private static Type FindType(string typeName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = null;
            try { t = assembly.GetTypes().FirstOrDefault(x => x.Name == typeName && typeof(Component).IsAssignableFrom(x)); }
            catch (System.Reflection.ReflectionTypeLoadException) { continue; }
            if (t != null) return t;
        }
        return null;
    }
}
#endif
