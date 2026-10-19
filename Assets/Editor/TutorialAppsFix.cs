#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TutorialAppsFix
{
    private const string PathScene = "Assets/Scenes/Tutorial_Estagiar.unity";
    [MenuItem("Tools/E-stagiar.io/Tutorial/CORRIGIR aplicativos dentro do tablet")]
    public static void Fix()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorUtility.DisplayDialog("Tutorial", "Pare o Play antes.", "OK"); return; }
        if (!File.Exists(PathScene))
        { Debug.LogError("[Tutorial] Cena nao encontrada: " + PathScene); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string backup = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "Tutorial_Estagiar.antes-apps.unity");
        File.Copy(PathScene, backup, true);
        Scene scene = EditorSceneManager.OpenScene(PathScene, OpenSceneMode.Single);
        TicketTabletPrototype tablet = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<TicketTabletPrototype>(true))
            .FirstOrDefault();
        if (tablet == null) { Debug.LogError("[Tutorial] TicketTabletPrototype nao encontrado."); return; }
        if (tablet.GetComponent<TutorialKnowledgeBase>() == null)
            tablet.gameObject.AddComponent<TutorialKnowledgeBase>();
        // Somente no tutorial: T abre e fecha o tablet.
        var obj = new SerializedObject(tablet);
        var prop = obj.FindProperty("toggleKey");
        if (prop != null) { prop.intValue = (int)UnityEngine.InputSystem.Key.T; obj.ApplyModifiedProperties(); }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Tutorial] Apps vinculados ao tablet da cena tutorial. Backup: " + backup);
        EditorUtility.DisplayDialog("Apps instalados", "Abra Tutorial_Estagiar, Play, T e clique APPS / GUIA no proprio tablet. F1 tambem abre o guia. As telas originais nao foram substituidas.", "OK");
    }
}
#endif
