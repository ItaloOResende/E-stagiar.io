using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Cena de teste própria (Tools > E-stagiar.io > Criar cena de teste (escritório + chamados)), para a equipe não
/// disputar a <c>Office_Placeholder</c>:
///  - Prefabs (variantes do <c>Estacao_Trabalho</c>, que não muda): <c>Estacao_Trabalho_Direita</c> (gabinete, ponto
///    elétrico e xícara espelhados para a direita de quem senta), e para cada lado <c>Estacao_{Lado}_Funcional</c>
///    (PC 100% montado: cabos de energia, HDMI e rede ligados e PC ligado no sistema — <see cref="PcEstadoInicial"/>) e
///    <c>Estacao_{Lado}_Problema</c> (peças sorteadas pelo ComputerCase e cada cabo com 35% de chance de vir solto).
///  - Cena <c>Escritorio_Teste.unity</c>: cópia da <c>Office_Placeholder</c> só com o escritório de teste (sai o
///    escritório novo do Matheus e a UI de crimpagem dele, que continuam na Office_Placeholder), jogador de volta ao
///    escritório de teste, Estacao_1 = esquerda funcional, Estacao_2 = direita com problema, e os chamados da Fernanda
///    ligados (objeto <c>Chamados</c>: TicketManager + StageController + celular [C]; ponte "abrir gabinete" no PC com
///    problema).
/// Prefabs: sempre regravados. Cena: criada só se não existir (não sobrescreve trabalho feito nela).
/// Não mexe na Office_Placeholder, em shader nem nas configurações do projeto.
/// </summary>
public static class CenaTesteSetup
{
    private const string Gameplay = "Assets/Prefabs/Gameplay/";
    private const string BaseStation = Gameplay + "Estacao_Trabalho.prefab";
    private const string RightStation = Gameplay + "Estacao_Trabalho_Direita.prefab";
    private const string SourceScene = "Assets/Scenes/Office_Placeholder.unity";
    public const string TestScene = "Assets/Scenes/Escritorio_Teste.unity";
    private const string Ticket = "Assets/TicketsData/Ticket_ComputadorNaoLiga.asset";
    private const float ChanceCaboSoltoProblema = 0.35f;

    // Objetos do escritório novo (Matheus) que ficam só na Office_Placeholder.
    private static readonly string[] EscritorioNovo =
        { "Escritorio", "npc", "npc2", "mesa_grande", "Cube", "Cube (1)", "Cube (2)", "Cube (3)", "UI_RedeCrimpMinigame" };

    public static string StationPath(bool direita, bool funcional) =>
        Gameplay + $"Estacao_{(direita ? "Direita" : "Esquerda")}_{(funcional ? "Funcional" : "Problema")}.prefab";

    [MenuItem("Tools/E-stagiar.io/Criar cena de teste (escritório + chamados)")]
    public static void Run()
    {
        Prefabs();
        Cena();
        AssetDatabase.SaveAssets();
        Debug.Log("CenaTesteSetup: pronto");
    }

    // ------------------------------------------------------------------ prefabs
    [MenuItem("Tools/E-stagiar.io/Estações esquerda e direita (funcional e com problema)")]
    public static void Prefabs()
    {
        // direita: espelho da base no X local (gabinete, ponto elétrico e xícara)
        GameObject right = Instantiate(BaseStation);
        foreach (Transform t in new[] { Case(right).transform, Root(right.GetComponentInChildren<WallConnectionPoint>(true)), Cup(right) })
        {
            Vector3 p = t.localPosition;
            t.localPosition = new Vector3(-p.x, p.y, p.z);
            Vector3 e = t.localEulerAngles;
            t.localEulerAngles = new Vector3(e.x, -e.y, -e.z);
        }
        Save(right, RightStation);

        foreach (bool direita in new[] { false, true })
            foreach (bool funcional in new[] { true, false })
            {
                GameObject s = Instantiate(direita ? RightStation : BaseStation);
                ComputerCase pcCase = Case(s);
                var caseSo = new SerializedObject(pcCase);
                caseSo.FindProperty("randomizeComponents").boolValue = !funcional;
                caseSo.FindProperty("fixedComponents").intValue = (int)ComputerCase.AllComponents;
                caseSo.FindProperty("startingHddHasOperatingSystem").boolValue = true;
                caseSo.ApplyModifiedPropertiesWithoutUndo();

                var estado = pcCase.gameObject.AddComponent<PcEstadoInicial>();
                var so = new SerializedObject(estado);
                so.FindProperty("caboEnergiaPrefab").objectReferenceValue = Load(Gameplay + "Pickup_CaboEnergia.prefab");
                so.FindProperty("caboVideoPrefab").objectReferenceValue = Load(Gameplay + "Pickup_CaboHDMI.prefab");
                so.FindProperty("caboRedePrefab").objectReferenceValue = Load(Gameplay + "Pickup_CaboRede.prefab");
                so.FindProperty("chanceCaboSolto").floatValue = funcional ? 0f : ChanceCaboSoltoProblema;
                so.FindProperty("ligarAoIniciar").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                Save(s, StationPath(direita, funcional));
            }
    }

    private static GameObject Instantiate(string path)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(Load(path));
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        return go;
    }

    private static void Save(GameObject instance, string path)
    {
        instance.name = System.IO.Path.GetFileNameWithoutExtension(path);
        PrefabUtility.SaveAsPrefabAsset(instance, path, out bool ok);
        Object.DestroyImmediate(instance);
        if (!ok) throw new System.Exception("CenaTesteSetup: falha ao salvar " + path);
        Debug.Log("CenaTesteSetup: salvo " + path);
    }

    private static GameObject Load(string path)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null) throw new System.Exception("CenaTesteSetup: falta " + path);
        return go;
    }

    private static ComputerCase Case(GameObject station) => station.GetComponentsInChildren<ComputerCase>(true).Single();

    private static Transform Root(Component c)
    {
        if (c == null) throw new System.Exception("CenaTesteSetup: componente não encontrado na estação");
        GameObject r = PrefabUtility.GetNearestPrefabInstanceRoot(c.gameObject);
        return (r != null ? r : c.gameObject).transform;
    }

    private static Transform Cup(GameObject station) =>
        station.GetComponentsInChildren<Transform>(true).Single(t =>
            PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) &&
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == "Assets/Prefabs/Props/OF_Xicara.prefab");

    // ------------------------------------------------------------------ cena
    private static void Cena()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScene) != null)
        {
            Debug.LogWarning($"CenaTesteSetup: {TestScene} já existe — não foi alterada (apague-a para recriar).");
            return;
        }
        if (!AssetDatabase.CopyAsset(SourceScene, TestScene)) throw new System.Exception("CenaTesteSetup: falha ao copiar a cena");
        var scene = EditorSceneManager.OpenScene(TestScene, OpenSceneMode.Single);

        // escritório novo do Matheus fica só na Office_Placeholder
        foreach (GameObject g in AllObjects().Where(g => EscritorioNovo.Contains(g.name) && (g.transform.parent == null || g.name == "UI_RedeCrimpMinigame")).ToList())
        {
            if (g == null) continue;
            Debug.Log("CenaTesteSetup: removido " + g.name);
            Object.DestroyImmediate(g);
        }

        // jogador de volta ao escritório de teste
        PlayerMovement player = Object.FindObjectsByType<PlayerMovement>(FindObjectsInactive.Include).Single();
        player.transform.position = new Vector3(0f, player.transform.position.y, -0.5f);

        Replace("Estacao_1", StationPath(false, true));
        GameObject problema = Replace("Estacao_2", StationPath(true, false));

        // chamados (Fernanda)
        var chamados = new GameObject("Chamados");
        var manager = chamados.AddComponent<TicketManager>();
        var stage = chamados.AddComponent<StageController>();
        var tablet = chamados.AddComponent<TicketTabletPrototype>();
        var mso = new SerializedObject(manager);
        SerializedProperty catalog = mso.FindProperty("catalog");
        catalog.arraySize = 1;
        catalog.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<TicketDefinition>(Ticket)
            ?? throw new System.Exception("CenaTesteSetup: falta " + Ticket);
        mso.ApplyModifiedPropertiesWithoutUndo();
        var sso = new SerializedObject(stage);
        sso.FindProperty("ticketManager").objectReferenceValue = manager;
        sso.ApplyModifiedPropertiesWithoutUndo();
        var tso = new SerializedObject(tablet);
        tso.FindProperty("ticketManager").objectReferenceValue = manager;
        tso.FindProperty("stageController").objectReferenceValue = stage;
        tso.FindProperty("interactionDetector").objectReferenceValue =
            Object.FindObjectsByType<InteractionDetector>(FindObjectsInactive.Include).Single();
        SerializedProperty off = tso.FindProperty("disableWhileTabletOpen");
        var behaviours = new List<Behaviour> { player.GetComponentInChildren<PlayerLook>(true), player };
        if (behaviours[0] == null) behaviours[0] = Object.FindObjectsByType<PlayerLook>(FindObjectsInactive.Include).Single();
        off.arraySize = behaviours.Count;
        for (int i = 0; i < behaviours.Count; i++) off.GetArrayElementAtIndex(i).objectReferenceValue = behaviours[i];
        tso.ApplyModifiedPropertiesWithoutUndo();

        var bridge = Case(problema).gameObject.AddComponent<ComputerCaseTicketBridge>();
        var bso = new SerializedObject(bridge);
        bso.FindProperty("ticketManager").objectReferenceValue = manager;
        bso.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("CenaTesteSetup: cena " + TestScene + " criada");
    }

    private static List<GameObject> AllObjects() =>
        Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Select(t => t.gameObject).ToList();

    // Troca a instância de estação pelo prefab novo, no mesmo lugar. Referências de fora para dentro dela (ex.: pontos de
    // interesse do supervisor) passam para o objeto equivalente (mesmo caminho e tipo) na estação nova; sem equivalente, recusa.
    private static GameObject Replace(string name, string prefabPath)
    {
        GameObject old = AllObjects().Single(g => g.name == name && PrefabUtility.IsOutermostPrefabInstanceRoot(g));
        var fresh = (GameObject)PrefabUtility.InstantiatePrefab(Load(prefabPath), old.transform.parent);
        fresh.transform.SetPositionAndRotation(old.transform.position, old.transform.rotation);
        fresh.transform.SetSiblingIndex(old.transform.GetSiblingIndex());
        fresh.name = name;

        foreach (GameObject g in AllObjects().Where(g => !g.transform.IsChildOf(old.transform) && !g.transform.IsChildOf(fresh.transform)))
            foreach (Component c in g.GetComponents<Component>())
            {
                if (c == null || c is Transform) continue;
                var so = new SerializedObject(c);
                SerializedProperty it = so.GetIterator();
                bool changed = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                    Object target = it.objectReferenceValue;
                    GameObject tgo = target is Component tc ? tc.gameObject : target as GameObject;
                    if (tgo == null || !tgo.transform.IsChildOf(old.transform)) continue;
                    Object mapped = Equivalent(old.transform, fresh.transform, target);
                    if (mapped == null) throw new System.Exception($"CenaTesteSetup: {g.name}.{c.GetType().Name}.{it.propertyPath} aponta para {tgo.name} em {name}, sem equivalente");
                    it.objectReferenceValue = mapped;
                    changed = true;
                    Debug.Log($"CenaTesteSetup: {g.name}.{c.GetType().Name}.{it.propertyPath} -> {name}/{tgo.name} novo");
                }
                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }
        Object.DestroyImmediate(old);
        Debug.Log($"CenaTesteSetup: {name} -> {System.IO.Path.GetFileName(prefabPath)}");
        return fresh;
    }

    private static Object Equivalent(Transform oldRoot, Transform newRoot, Object target)
    {
        Transform t = target is Component c ? c.transform : ((GameObject)target).transform;
        var path = new List<string>();
        for (Transform p = t; p != oldRoot; p = p.parent) path.Insert(0, p.name);
        Transform n = newRoot;
        foreach (string part in path)
        {
            n = n.Cast<Transform>().FirstOrDefault(ch => ch.name == part);
            if (n == null) return null;
        }
        if (target is GameObject) return n.gameObject;
        var comps = t.GetComponents(target.GetType());
        var news = n.GetComponents(target.GetType());
        int i = System.Array.IndexOf(comps, target);
        return i >= 0 && i < news.Length ? news[i] : null;
    }
}
