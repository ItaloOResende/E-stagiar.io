#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Instala uma cena INDEPENDENTE. Nao modifica Fase_01, scripts, menus ou tablet da equipe.
public static class TutorialAutoSetup
{
    private const string BaseScene = "Assets/Scenes/Fase_01.unity";
    private const string TutorialScene = "Assets/Scenes/Tutorial_Estagiar.unity";
    private const string BaseTicket = "Assets/TicketsData/Gerados/pc-montagem-completa.asset";
    private const string TutorialTicket = "Assets/TicketsData/Tutorial/TutorialMontagemCompleta.asset";

    [MenuItem("Tools/E-stagiar.io/Tutorial/Criar cena independente")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new Exception("Saia do modo Play antes de instalar.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BaseScene) == null)
            throw new Exception("Cena nao encontrada: " + BaseScene);
        var source = AssetDatabase.LoadAssetAtPath<TicketDefinition>(BaseTicket);
        if (source == null || source.steps == null || source.steps.Count == 0)
            throw new Exception("Chamado de montagem completa nao encontrado: " + BaseTicket);

        // Nunca sobrescreve uma cena do tutorial que ja foi criada/editada.
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TutorialScene) != null)
        {
            Debug.LogWarning("[Tutorial] A cena ja existe. Nada foi sobrescrito: " + TutorialScene);
            EditorUtility.DisplayDialog("Tutorial", "A cena ja existe. Nenhum arquivo foi sobrescrito. Para recriar, faca backup e remova a cena manualmente.", "OK");
            return;
        }

        // Copia a cena antes de abri-la; a fase original permanece intacta.
        if (!AssetDatabase.CopyAsset(BaseScene, TutorialScene))
            throw new Exception("Nao foi possivel copiar a cena de origem.");
        AssetDatabase.Refresh();

        if (!Directory.Exists("Assets/TicketsData/Tutorial")) Directory.CreateDirectory("Assets/TicketsData/Tutorial");
        AssetDatabase.Refresh();
        var tutorial = AssetDatabase.LoadAssetAtPath<TicketDefinition>(TutorialTicket);
        if (tutorial == null)
        {
            tutorial = UnityEngine.Object.Instantiate(source);
            tutorial.name = "TutorialMontagemCompleta";
            AssetDatabase.CreateAsset(tutorial, TutorialTicket);
        }
        tutorial.ticketId = "pc-montagem-completa";
        tutorial.title = "Primeiro dia: monte um computador completo";
        tutorial.description = "Encontre o solicitante, leve o gabinete e monitor, instale todas as pecas, conecte os cabos, instale o sistema e teste.";
        tutorial.minimumStage = 0;
        tutorial.tutorialOnly = true;
        tutorial.slaSecondsOverride = 7200;
        tutorial.steps = source.steps.Select(s => new TicketStepDefinition { id = s.id, description = s.description, weight = s.weight }).ToList();
        EditorUtility.SetDirty(tutorial);

        var scene = EditorSceneManager.OpenScene(TutorialScene, OpenSceneMode.Single);
        var stage = FindInScene<StageController>(scene);
        var manager = FindInScene<TicketManager>(scene);
        var tablet = FindInScene<TicketTabletPrototype>(scene);
        if (stage == null || manager == null || tablet == null)
            throw new Exception("Cena de tutorial sem StageController, TicketManager ou TicketTabletPrototype. Cena original preservada.");

        Set(stage, "stageNumber", p => p.intValue = 0);
        Set(stage, "startAutomatically", p => p.boolValue = true);
        Set(stage, "showStartMenu", p => p.boolValue = false);
        Set(manager, "tutorialTicket", p => p.objectReferenceValue = tutorial);
        Set(manager, "requirePlayerToFinish", p => p.boolValue = true);
        Set(tablet, "toggleKey", p => p.intValue = (int)UnityEngine.InputSystem.Key.T);

        if (stage.GetComponent<TutorialGuide>() == null) stage.gameObject.AddComponent<TutorialGuide>();

        // Vincula o chamado a UMA estacao existente, sem criar / substituir prefab.
        var eligible = new List<PcTicketBridge>();
        foreach (var bridge in FindAll<PcTicketBridge>(scene))
        {
            var so = new SerializedObject(bridge);
            var ids = so.FindProperty("acceptedTicketIds");
            if (ids == null) continue;
            for (int i = 0; i < ids.arraySize; i++)
                if (ids.GetArrayElementAtIndex(i).stringValue == "pc-montagem-completa") { eligible.Add(bridge); break; }
        }
        var chosen = eligible.FirstOrDefault(b => {
            var e = StationEmployee.Of(b);
            return e != null && e.Occupied && e.employeeName == "Augusto";
        }) ?? eligible.FirstOrDefault(b => StationEmployee.Of(b) != null) ?? eligible.FirstOrDefault();
        if (chosen == null)
            throw new Exception("Nao encontrei uma estacao para montagem completa na cena copiada. Cena original intacta.");

        foreach (var bridge in eligible)
        {
            if (bridge == chosen) continue;
            var so = new SerializedObject(bridge);
            var ids = so.FindProperty("acceptedTicketIds");
            for (int i = ids.arraySize - 1; i >= 0; i--)
                if (ids.GetArrayElementAtIndex(i).stringValue == "pc-montagem-completa") ids.DeleteArrayElementAtIndex(i);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var employee = StationEmployee.Of(chosen);
        if (employee != null && employee.Occupied)
        {
            tutorial.requester = employee.employeeName;
            tutorial.department = employee.department;
            EditorUtility.SetDirty(tutorial);
        }

        // Somente a NOVA cena e o NOVO asset sao salvos.
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Nao foi possivel salvar a cena tutorial.");
        AssetDatabase.SaveAssets();
        Debug.Log("[Tutorial] Criado: " + TutorialScene + " | Solicitante: " + tutorial.requester);
        EditorUtility.DisplayDialog("Tutorial", "Cena independente criada! Abra Assets/Scenes/Tutorial_Estagiar.unity e pressione Play. As cenas e UIs existentes nao foram modificadas.", "OK");
    }

    private static void Set(UnityEngine.Object obj, string name, Action<SerializedProperty> value)
    {
        var so = new SerializedObject(obj);
        var prop = so.FindProperty(name);
        if (prop == null) throw new Exception("Campo nao encontrado na versao atual: " + obj.GetType().Name + "." + name);
        value(prop);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static T FindInScene<T>(Scene scene) where T : Component => FindAll<T>(scene).FirstOrDefault();
    private static IEnumerable<T> FindAll<T>(Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var component in root.GetComponentsInChildren<T>(true)) yield return component;
    }
}
#endif
