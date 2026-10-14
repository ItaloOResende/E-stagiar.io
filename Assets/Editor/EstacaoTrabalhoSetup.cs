using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// PC e estação de trabalho reutilizáveis (Tools > E-stagiar.io > Montar PC e estação de trabalho). Roda uma vez:
/// se o <c>Estacao_Trabalho.prefab</c> já existe, não faz nada.
///  - <c>PC_Escritorio.prefab</c> (Gameplay): é o antigo <c>Props/PH_Pc.prefab</c> (mesmo GUID), movido e refeito
///    a partir do PC já configurado da mesa 2 da <c>Office_Placeholder</c>: <c>Gabinete</c> (aninhado), <c>OF_Monitor</c>
///    (aninhado, com a <c>Tela</c>/<c>MonitorDisplay</c> e as âncoras <c>Porta3D_Monitor*</c>), <c>OF_Teclado</c>,
///    <c>OF_Mouse</c> e o <c>PontoEletrico</c> (aninhado). As referências (MonitorDisplay.Source, âncoras dos cabos 3D)
///    ficam dentro do prefab, então cada cópia tem o próprio estado. Começa funcional: peças fixas (todas) e HD com sistema.
///  - <c>Estacao_Trabalho.prefab</c> (Gameplay): <c>OF_Mesa</c>, <c>OF_Cadeira</c>, <c>PC_Escritorio</c> (aninhado) e <c>OF_Xicara</c>.
///  - Cena: mesa 2 vira <c>Estacao_2</c> (mantém o sorteio de peças que o gabinete tinha), mesa 1 vira <c>Estacao_1</c>
///    (no lugar do PC legado <c>PC_1</c>); saem <c>PC_1</c>, <c>PC_2</c> (desativado), <c>CaixaTeste_Pickup</c> e os
///    primitivos desativados das mesas.
///  - Assets sem uso (PH_pc.fbx e seus materiais/texturas, Pickup_CaixaTeste) são copiados para
///    <c>Art/_Removidos_2026-10-13/</c> (fora de Assets, com o .meta) e apagados, só se nada mais os referencia.
/// </summary>
public static class EstacaoTrabalhoSetup
{
    private const string ScenePath = "Assets/Scenes/Office_Placeholder.unity";
    private const string Gameplay = "Assets/Prefabs/Gameplay/";
    private const string Props = "Assets/Prefabs/Props/";
    private const string OldPcPath = Props + "PH_Pc.prefab";
    private const string PcPath = Gameplay + "PC_Escritorio.prefab";
    private const string StationPath = Gameplay + "Estacao_Trabalho.prefab";
    private const string BackupDir = "Art/_Removidos_2026-10-13/";
    private const float DeskHeight = 0.75f;

    private static readonly string[] Obsolete =
    {
        Gameplay + "Pickup_CaixaTeste.prefab",
        "Assets/Art/Models/PH_pc.fbx",
        "Assets/Art/Materials/PH_M_PC_Atlas.mat",
        "Assets/Art/Materials/PH_M_PC_TowerBack.mat",
        "Assets/Art/Materials/PH_M_PC_TowerFront.mat",
        "Assets/Art/Textures/PH_pc_atlas.png",
        "Assets/Art/Textures/PH_pc_back.png",
        "Assets/Art/Textures/PH_pc_front.png",
    };

    [MenuItem("Tools/E-stagiar.io/Montar PC e estação de trabalho")]
    public static void Run()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(StationPath) != null)
        {
            Debug.Log("EstacaoTrabalhoSetup: Estacao_Trabalho.prefab já existe, nada a fazer");
            return;
        }
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // ---- PC configurado da mesa 2 (único gabinete e único monitor ligado a ele)
        ComputerCase pcCase = Single(Object.FindObjectsByType<ComputerCase>(FindObjectsInactive.Include, FindObjectsSortMode.None), "ComputerCase");
        MonitorDisplay display = Single(Object.FindObjectsByType<MonitorDisplay>(FindObjectsInactive.Include, FindObjectsSortMode.None), "MonitorDisplay");
        GameObject gab = pcCase.gameObject;
        if (display.Source != gab.GetComponent<ComputerPower>()) throw new System.Exception("MonitorDisplay não aponta para o Gabinete");
        // a Tela é um objeto acrescentado à instância do OF_Monitor: o root vem do pai dela
        GameObject monitor = PrefabUtility.GetOutermostPrefabInstanceRoot(display.transform.parent.gameObject);
        Transform desk2 = monitor.transform.parent;

        var cableView = gab.GetComponent<PcCableView3D>();
        Transform[] monitorAnchors = cableView.Anchors
            .Where(a => a.port == PcPort.MonitorPower || a.port == PcPort.MonitorVideo || a.port == PcPort.MonitorHdmi)
            .Select(a => a.anchor).ToArray();
        if (monitorAnchors.Length != 3 || monitorAnchors.Any(t => t == null)) throw new System.Exception("âncoras do monitor incompletas");
        GameObject plate = PrefabUtility.GetOutermostPrefabInstanceRoot(cableView.Anchors.First(a => a.port == PcPort.Outlet).anchor.gameObject);

        List<GameObject> all = Instances();
        GameObject keyboard = Single(all.Where(g => Source(g) == Props + "OF_Teclado.prefab" && g.transform.parent == desk2), "OF_Teclado");
        GameObject mouse = Single(all.Where(g => Source(g) == Props + "OF_Mouse.prefab" && g.transform.parent == desk2), "OF_Mouse");
        GameObject desk2Top = Single(all.Where(g => Source(g) == Props + "OF_Mesa.prefab" && g.transform.parent == desk2), "OF_Mesa (mesa 2)");
        GameObject desk1Top = Single(all.Where(g => Source(g) == Props + "OF_Mesa.prefab" && g != desk2Top), "OF_Mesa (mesa 1)");
        Transform desk1 = desk1Top.transform.parent;
        GameObject cup = Single(all.Where(g => Source(g) == Props + "OF_Xicara.prefab"), "OF_Xicara");
        GameObject chair2 = Nearest(all.Where(g => Source(g) == Props + "OF_Cadeira.prefab"), desk2Top.transform.position);
        GameObject chair1 = Nearest(all.Where(g => Source(g) == Props + "OF_Cadeira.prefab"), desk1Top.transform.position);
        if (chair1 == chair2) throw new System.Exception("uma cadeira por mesa esperada");

        // ---- objetos antigos/de teste (instâncias do PH_Pc legado e da caixa de teste)
        foreach (GameObject g in all.Where(g => Source(g) == OldPcPath || Source(g) == Gameplay + "Pickup_CaixaTeste.prefab"))
        {
            Debug.Log($"EstacaoTrabalhoSetup: removido da cena {g.name} ({Source(g)})");
            Object.DestroyImmediate(g);
        }

        // ---- PC_Escritorio: raiz no tampo da mesa, virada como a mesa
        var pc = new GameObject("PC_Escritorio");
        pc.transform.SetPositionAndRotation(desk2Top.transform.position + Vector3.up * DeskHeight, desk2Top.transform.rotation);
        pc.transform.SetParent(desk2, true);
        foreach (GameObject g in new[] { gab, monitor, keyboard, mouse, plate }) g.transform.SetParent(pc.transform, true);
        foreach (Transform a in monitorAnchors) a.SetParent(monitor.transform, true);
        // computador comum começa funcional: todas as peças, HD com sistema (o da mesa 2 volta a sortear, abaixo)
        var so = new SerializedObject(pcCase);
        so.FindProperty("randomizeComponents").boolValue = false;
        so.FindProperty("fixedComponents").intValue = (int)ComputerCase.AllComponents;
        so.FindProperty("startingHddHasOperatingSystem").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        string moved = AssetDatabase.MoveAsset(OldPcPath, PcPath); // mantém o GUID do PH_Pc
        if (!string.IsNullOrEmpty(moved)) throw new System.Exception("MoveAsset PH_Pc: " + moved);
        Save(pc, PcPath);

        // ---- Estacao_Trabalho: raiz na base da mesa
        var station = new GameObject("Estacao_Trabalho");
        station.transform.SetPositionAndRotation(desk2Top.transform.position, desk2Top.transform.rotation);
        station.transform.SetParent(desk2.parent, true);
        // xícara: mesma posição relativa à mesa que ela tinha na mesa 1
        Vector3 cupLocal = desk1Top.transform.InverseTransformPoint(cup.transform.position);
        Quaternion cupRot = Quaternion.Inverse(desk1Top.transform.rotation) * cup.transform.rotation;
        cup.transform.SetPositionAndRotation(desk2Top.transform.TransformPoint(cupLocal), desk2Top.transform.rotation * cupRot);
        foreach (GameObject g in new[] { desk2Top, chair2, pc, cup }) g.transform.SetParent(station.transform, true);
        Save(station, StationPath);
        GameObject stationAsset = AssetDatabase.LoadAssetAtPath<GameObject>(StationPath);

        station.name = "Estacao_2";
        station.transform.SetSiblingIndex(desk2.GetSiblingIndex());
        // a mesa 2 continua com o PC da manutenção como estava: peças sorteadas (override só nesta instância)
        so = new SerializedObject(station.GetComponentInChildren<ComputerCase>(true));
        so.FindProperty("randomizeComponents").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        var station1 = (GameObject)PrefabUtility.InstantiatePrefab(stationAsset, desk1.parent);
        station1.name = "Estacao_1";
        station1.transform.SetPositionAndRotation(desk1Top.transform.position, desk1Top.transform.rotation);
        station1.transform.SetSiblingIndex(desk1.GetSiblingIndex());

        // ---- mesas antigas: sobram a mesa 1 e a cadeira 1 (substituídas pela Estacao_1) e os primitivos desativados
        Object.DestroyImmediate(desk1Top);
        Object.DestroyImmediate(chair1);
        foreach (Transform desk in new[] { desk1, desk2 })
        {
            Transform active = desk.Cast<Transform>().FirstOrDefault(c => c.gameObject.activeSelf);
            if (active != null) throw new System.Exception($"{desk.name} ainda tem {active.name} ativo");
            Object.DestroyImmediate(desk.gameObject);
        }

        Validate(station);
        Validate(station1);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Validate(AssetDatabase.LoadAssetAtPath<GameObject>(PcPath), isStation: false);

        RemoveObsolete();
        AssetDatabase.SaveAssets();
        Debug.Log("EstacaoTrabalhoSetup: pronto");
    }

    // ------------------------------------------------------------------ prefabs
    private static void Save(GameObject root, string path)
    {
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction, out bool ok);
        if (!ok) throw new System.Exception("falha ao salvar " + path);
        Debug.Log($"EstacaoTrabalhoSetup: salvo {path} (GUID {AssetDatabase.AssetPathToGUID(path)})");
    }

    private static List<GameObject> Instances() =>
        Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Select(t => t.gameObject).Where(PrefabUtility.IsOutermostPrefabInstanceRoot).ToList();

    private static string Source(GameObject g) => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g);

    private static T Single<T>(IEnumerable<T> items, string what)
    {
        var list = items.ToList();
        if (list.Count != 1) throw new System.Exception($"esperado 1 {what} na cena, achados {list.Count}");
        return list[0];
    }

    private static GameObject Nearest(IEnumerable<GameObject> items, Vector3 p) =>
        items.OrderBy(g => (g.transform.position - p).sqrMagnitude).First();

    /// <summary>Um PC completo, com todas as referências dentro da própria raiz.</summary>
    private static void Validate(GameObject root, bool isStation = true)
    {
        var cases = root.GetComponentsInChildren<ComputerCase>(true);
        var displays = root.GetComponentsInChildren<MonitorDisplay>(true);
        var views = root.GetComponentsInChildren<PcCableView3D>(true);
        if (cases.Length != 1 || displays.Length != 1 || views.Length != 1)
            throw new System.Exception($"{root.name}: {cases.Length} gabinetes, {displays.Length} telas, {views.Length} cabos 3D");
        if (displays[0].Source != cases[0].GetComponent<ComputerPower>()) throw new System.Exception($"{root.name}: monitor ligado a outro PC");
        foreach (var a in views[0].Anchors)
            if (a.anchor == null || !a.anchor.IsChildOf(root.transform)) throw new System.Exception($"{root.name}: âncora {a.port} fora do PC");
        if (views[0].Anchors.Count != System.Enum.GetValues(typeof(PcPort)).Length) throw new System.Exception($"{root.name}: âncoras faltando");
        if (isStation && root.GetComponentsInChildren<Transform>(true).Count(t => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == PcPath && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) != 1)
            throw new System.Exception($"{root.name}: esperado 1 PC_Escritorio");
        Debug.Log($"EstacaoTrabalhoSetup: {root.name} ok");
    }

    // ------------------------------------------------------------------ assets sem uso
    private static void RemoveObsolete()
    {
        var candidates = new HashSet<string>(Obsolete.Where(p => File.Exists(p)));
        string[] users = AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/") && !candidates.Contains(p) &&
                        (p.EndsWith(".unity") || p.EndsWith(".prefab") || p.EndsWith(".mat") || p.EndsWith(".asset") || p.EndsWith(".fbx")))
            .ToArray();
        var used = new Dictionary<string, string>();
        foreach (string u in users)
            foreach (string d in AssetDatabase.GetDependencies(u, false))
                if (candidates.Contains(d) && !used.ContainsKey(d)) used[d] = u;
        foreach (string p in candidates)
        {
            if (used.TryGetValue(p, out string by))
            {
                Debug.LogWarning($"EstacaoTrabalhoSetup: mantido {p} (usado por {by})");
                continue;
            }
            string dst = BackupDir + p.Substring("Assets/".Length);
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            File.Copy(p, dst, true);
            File.Copy(p + ".meta", dst + ".meta", true);
            AssetDatabase.DeleteAsset(p);
            Debug.Log($"EstacaoTrabalhoSetup: removido {p} (cópia em {dst})");
        }
    }
}
