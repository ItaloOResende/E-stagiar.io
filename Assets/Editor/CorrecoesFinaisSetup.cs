using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using static OfficeGameplaySetup;

/// <summary>
/// Correções finais do gameplay na <c>Office_Placeholder</c>: Tools &gt; E-stagiar.io &gt; Escritório real: correções finais.
/// Não reconstrói a sala de TI nem as divisórias; mexe só no que está listado abaixo (relatório em
/// <c>Logs/CorrecoesFinais.txt</c>):
///  1. Telas de manutenção: os gabinetes apontavam para a <c>UI_Screen_Gabinete</c> da RAIZ da cena (fora de qualquer
///     Canvas: abria "invisível", só com o cursor de mão). Voltam a apontar para o PREFAB da tela, que o
///     <see cref="UIScreenManager"/> cria dentro do ScreenCanvas (como na Teste_Integrado). Monitor: clique direito
///     abre a manutenção do PC dele (<see cref="MonitorScreenInteractable"/>, no PC_Escritorio e no Monitor_Estoque).
///  2. Braço em 1ª pessoa com a pose da Teste_Integrado (à direita da tela). 3. Prompt só com ícones.
///  4. Plaquinhas apoiadas no tampo; mesas de cubículo com os pés no piso (tampo continua a 0,75 m).
///  5. Sala de TI: monitores decorativos trocados por monitores reais; armários com portas e reserva que se repõe ao
///     fechar (<see cref="DrawerContents"/> refillOnClose); equipamentos 3D no rack; mais cabos no gaveteiro.
///  6. Layout: 6 estações no setor 1 (as divisórias do usuário), 4 no setor 2 (área leste vazia), 3 impressoras,
///     Estacao_01…10, 2 NPCs (os dois modelos de NPC do projeto) e as demais estações identificadas pelo número;
///     NavMesh e ronda do supervisor nos dois setores e na sala de TI.
/// </summary>
public static class CorrecoesFinaisSetup
{
    private const string OfficePath = "Assets/Scenes/Office_Placeholder.unity";
    private const string TestePath = "Assets/Scenes/Teste_Integrado.unity";
    private const string Gameplay = "Assets/Prefabs/Gameplay/";
    private const string Props = "Assets/Prefabs/Props/";
    private const string MatDir = "Assets/Art/Materials/Mobiliario/";
    private const string LogPath = "Logs/CorrecoesFinais.txt";

    // setor 1 = a área das divisórias do usuário; setor 2 = área leste (x 42,7–59,1; z 3,8–33)
    private static readonly Rect Setor1 = Rect.MinMaxRect(13.2f, -7.6f, 40.7f, 11.6f);
    private static readonly Rect Setor2 = Rect.MinMaxRect(42.9f, 3.9f, 58.9f, 32.9f);
    private static readonly Rect SalaTI = Rect.MinMaxRect(30.4f, 12.3f, 40.3f, 16.8f);
    private static readonly Rect CorredorCentral = Rect.MinMaxRect(25.1f, 11.6f, 30.0f, 32.9f);
    private static readonly Rect CorredorNorte = Rect.MinMaxRect(25.1f, 26.0f, 42.8f, 32.9f);
    // portas e janelas (não bloquear): retângulos no piso, com folga
    private static readonly Rect[] Acessos =
    {
        Rect.MinMaxRect(12.5f, -6.6f, 14.6f, -3.6f), Rect.MinMaxRect(12.5f, 3.0f, 14.6f, 6.0f),        // portas oeste
        Rect.MinMaxRect(25.5f, -8.2f, 28.4f, -6.2f), Rect.MinMaxRect(36.2f, -8.2f, 39.1f, -6.2f),      // portas sul
        Rect.MinMaxRect(57.4f, 23.9f, 59.4f, 26.9f), Rect.MinMaxRect(41.5f, 31.3f, 44.4f, 33.4f),      // leste, norte
        Rect.MinMaxRect(34.6f, 11.0f, 37.2f, 12.3f),                                                    // porta da TI
    };

    // dois NPCs = os dois modelos de funcionário do projeto (o personagem do jogador e o chefe não entram)
    private static readonly (string modelo, string nome, string setor)[] Npcs =
    {
        ("Assets/Art/Models/character2.fbx", "Carmen", "Atendimento"),   // estação de desktop (chamados de PC e rack)
        ("Assets/Art/Models/npc2.fbx", "Paula", "Comercial"),            // estação de notebook (chamados de notebook)
    };

    private static List<string> L => OfficeGameplaySetup.log;

    [MenuItem("Tools/E-stagiar.io/Escritório real: correções finais")]
    public static void Run()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        OfficeGameplaySetup.log = new List<string>();
        try
        {
            GameObject monitorPrefab = MonitorPrefabs();
            GameObject gabReserva = GabineteReservaPrefab();
            GameObject armario = ArmarioReservaPrefab(monitorPrefab, gabReserva);

            Scene office = EditorSceneManager.OpenScene(OfficePath, OpenSceneMode.Single);
            Scene teste = EditorSceneManager.OpenScene(TestePath, OpenSceneMode.Additive);
            BracoDaTeste(teste, office);
            EditorSceneManager.CloseScene(teste, true);
            SceneManager.SetActiveScene(office);

            TelasDeManutencao(office);
            Prompt(office);
            PausaNaFase(office);
            SalaTIMonitores(office, monitorPrefab);
            Armarios(office, armario);
            Rack3D(office);
            EstoqueDeCabos(office);
            Layout(office);
            MesasNoPiso(office);
            Plaquinhas(office);
            Bake(office);
            Ronda(office);
            Validar(office);

            EditorSceneManager.MarkSceneDirty(office);
            EditorSceneManager.SaveScene(office);
            AssetDatabase.SaveAssets();
        }
        catch (System.Exception e)
        {
            L.Add("ERRO: " + e);
            Debug.LogException(e);
        }
        File.WriteAllLines(LogPath, L);
        Debug.Log("[CorrecoesFinais]\n" + string.Join("\n", L));
    }

    // ================================================================== prefabs

    // Monitor: clique direito = manutenção do PC dele. No PC_Escritorio (todas as estações) e no Monitor_Estoque.
    private static GameObject MonitorPrefabs()
    {
        var gab = AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + "Gabinete.prefab");
        Sprite icone = gab.GetComponent<UIScreenInteractable>().promptIcon;
        foreach (string path in new[] { Gameplay + "PC_Escritorio.prefab", Gameplay + "Monitor_Estoque.prefab" })
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (MonitorDisplay md in root.GetComponentsInChildren<MonitorDisplay>(true))
                {
                    GameObject mon = md.GetComponentInParent<PickupInteractable>(true).gameObject;
                    var msi = mon.GetComponent<MonitorScreenInteractable>() ?? mon.AddComponent<MonitorScreenInteractable>();
                    msi.interactionMessage = "Manutenção";
                    msi.promptIcon = icone;
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        L.Add("monitor: clique direito abre a manutenção do PC (PC_Escritorio e Monitor_Estoque)");
        return AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + "Monitor_Estoque.prefab");
    }

    // Gabinete de reserva: variante do Gabinete.prefab que começa FUNCIONAL (todas as peças, HD com sistema, sem sorteio).
    private static GameObject GabineteReservaPrefab()
    {
        string path = Gameplay + "Gabinete_Reserva.prefab";
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + "Gabinete.prefab"));
        inst.transform.position = Vector3.zero;
        MakeFunctional(inst.GetComponent<ComputerCase>());
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(inst, path);
        Object.DestroyImmediate(inst);
        L.Add("prefab Gabinete_Reserva (variante do Gabinete, funcional)");
        return prefab;
    }

    // Armário de reserva: mesmo visual do Armario_TI, com portas que giram, prateleiras e reserva que se repõe.
    private static GameObject ArmarioReservaPrefab(GameObject monitor, GameObject gabinete)
    {
        string fbx = "Assets/Art/Models/armario_reserva.fbx";
        AssetDatabase.ImportAsset(fbx, ImportAssetOptions.ForceUpdate);
        var mats = new Dictionary<string, Material>
        {
            { "Claro", AssetDatabase.LoadAssetAtPath<Material>(MatDir + "M_Mobiliario_Claro.mat") },
            { "Escuro", AssetDatabase.LoadAssetAtPath<Material>(MatDir + "M_Mobiliario_Escuro.mat") },
            { "Metal", AssetDatabase.LoadAssetAtPath<Material>(MatDir + "M_Mobiliario_Metal.mat") },
        };
        var root = new GameObject("Armario_Reserva");
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        model.name = "Visual";
        model.transform.SetParent(root.transform, false);
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            r.sharedMaterials = r.sharedMaterials.Select(m => m != null && mats.TryGetValue(m.name, out Material x) ? x : m).ToArray();
        Transform corpo = model.transform.Find("Corpo"), pe = model.transform.Find("Porta_E"), pd = model.transform.Find("Porta_D");
        Bounds cb = WorldBounds(corpo.gameObject);
        float frente = Mathf.Sign(WorldBounds(pe.gameObject).center.z - cb.center.z);   // +1: portas em +Z

        // colisores do corpo (oco): laterais, fundo, base, teto e as 2 prateleiras
        var col = new GameObject("Colisores").transform;
        col.SetParent(root.transform, false);
        void Box(Vector3 c, Vector3 s) { var b = col.gameObject.AddComponent<BoxCollider>(); b.center = c; b.size = s; }
        float zFundo = -frente * 0.23f;
        Box(new Vector3(-0.49f, 0.975f, 0f), new Vector3(0.02f, 1.95f, 0.48f));
        Box(new Vector3(0.49f, 0.975f, 0f), new Vector3(0.02f, 1.95f, 0.48f));
        Box(new Vector3(0f, 0.975f, zFundo - frente * 0.01f), new Vector3(0.96f, 1.95f, 0.02f));
        Box(new Vector3(0f, 0.04f, 0f), new Vector3(0.96f, 0.08f, 0.47f));
        Box(new Vector3(0f, 1.94f, 0f), new Vector3(1.0f, 0.04f, 0.5f));
        foreach (float y in new[] { 0.66f, 1.45f }) Box(new Vector3(0f, y + 0.0125f, -frente * 0.01f), new Vector3(0.96f, 0.025f, 0.45f));

        // portas: um pivô na dobradiça de cada uma (sem rotação), com colisor e OpenableInteractable (giro no Y)
        var interior = new GameObject("Interior").transform;
        interior.SetParent(root.transform, false);
        OpenableInteractable Porta(Transform mesh, string nome)
        {
            var piv = new GameObject(nome).transform;
            piv.SetParent(root.transform, false);
            piv.position = new Vector3(mesh.position.x, 0f, mesh.position.z);
            mesh.SetParent(piv, true);
            Bounds db = WorldBounds(mesh.gameObject);
            var bc = piv.gameObject.AddComponent<BoxCollider>();
            bc.center = piv.InverseTransformPoint(db.center);
            bc.size = new Vector3(db.size.x, db.size.y, Mathf.Max(0.025f, db.size.z * 0.6f));
            // o sentido que leva a porta para a FRENTE do armário
            float ang = 95f;
            piv.localRotation = Quaternion.Euler(0f, ang, 0f);
            float zMais = WorldBounds(mesh.gameObject).center.z;
            piv.localRotation = Quaternion.identity;
            if ((zMais - db.center.z) * frente < 0f) ang = -ang;
            var op = piv.gameObject.AddComponent<OpenableInteractable>();
            var so = new SerializedObject(op);
            so.FindProperty("mode").enumValueIndex = (int)OpenableInteractable.Mode.Rotate;
            so.FindProperty("localAxis").vector3Value = Vector3.up;
            so.FindProperty("openAmount").floatValue = ang;
            so.FindProperty("duration").floatValue = 0.5f;
            so.ApplyModifiedPropertiesWithoutUndo();
            op.interactionMessage = "Interagir";
            return op;
        }
        OpenableInteractable e = Porta(pe, "Porta_Esquerda"), d = Porta(pd, "Porta_Direita");
        foreach (var (a, b) in new[] { (e, d), (d, e) })
        {
            var so = new SerializedObject(a);
            SerializedProperty linked = so.FindProperty("linked");
            linked.arraySize = 1;
            linked.GetArrayElementAtIndex(0).objectReferenceValue = b;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // reserva: 2 monitores (um atrás do outro) no vão do meio, 1 gabinete deitado de lado no vão de baixo
        var dc = e.gameObject.AddComponent<DrawerContents>();
        var sd = new SerializedObject(dc);
        sd.FindProperty("contentsRoot").objectReferenceValue = interior;
        sd.FindProperty("refillOnClose").boolValue = true;
        sd.FindProperty("hideContentsWhenClosed").boolValue = true;
        SerializedProperty entries = sd.FindProperty("entries");
        entries.arraySize = 2;
        float yaw = frente > 0 ? 0f : 180f;
        // pivô do monitor = centro da base; frente/fundo do monitor: -0,105..+0,075 em Z local
        SetEntry(entries.GetArrayElementAtIndex(0), monitor, 2, new Vector3(0f, 0.6855f + 0.004f, -frente * 0.105f + frente * 0.0f - frente * 0.0f),
                 new Vector3(0f, yaw, 0f), new Vector3(0f, 0f, frente * 0.20f));
        var probe = (GameObject)PrefabUtility.InstantiatePrefab(gabinete);
        probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 90f, 0f));
        Bounds gb = WorldBounds(probe);
        Object.DestroyImmediate(probe);
        Vector3 alvo = new Vector3(0f, 0.08f + 0.004f, -frente * 0.005f);
        Vector3 pos = alvo - new Vector3(gb.center.x, gb.min.y, gb.center.z);
        SetEntry(entries.GetArrayElementAtIndex(1), gabinete.GetComponent<PickupInteractable>(), 1, pos, new Vector3(0f, 90f, 0f), Vector3.zero);
        sd.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Props + "Armario_Reserva.prefab");
        Object.DestroyImmediate(root);
        L.Add($"prefab Armario_Reserva: portas que giram ±95°, reserva 2 monitores + 1 gabinete (gabinete 0,30 x 0,52 x 0,65 m: " +
              $"só cabe 1 por armário, deitado de lado), repõe ao fechar; frente {(frente > 0 ? "+Z" : "-Z")}");
        return prefab;
    }

    private static void SetEntry(SerializedProperty e, Object prefab, int q, Vector3 pos, Vector3 euler, Vector3 step)
    {
        e.FindPropertyRelative("prefab").objectReferenceValue = prefab is GameObject g ? g.GetComponent<PickupInteractable>() : prefab;
        e.FindPropertyRelative("quantity").intValue = q;
        e.FindPropertyRelative("delivery").enumValueIndex = (int)DrawerContents.Delivery.PhysicalInDrawer;
        e.FindPropertyRelative("localPosition").vector3Value = pos;
        e.FindPropertyRelative("localEuler").vector3Value = euler;
        e.FindPropertyRelative("step").vector3Value = step;
    }

    // ================================================================== correções de cena

    private static void BracoDaTeste(Scene teste, Scene office)
    {
        foreach (string rel in new[] { "Main Camera/Arm_Personagem", "Main Camera/Arm_Personagem/Modelo", "Main Camera/HoldPoint", "Main Camera" })
        {
            Transform a = FindPath(teste, "Player/" + rel), b = FindPath(office, "Player/" + rel);
            if (a == null || b == null) { L.Add("braço: FALTA " + rel); continue; }
            if (b.localPosition == a.localPosition && b.localRotation == a.localRotation && b.localScale == a.localScale) continue;
            L.Add($"braço: {rel} {b.localPosition:F3}/{b.localEulerAngles:F1} → {a.localPosition:F3}/{a.localEulerAngles:F1} (como na Teste_Integrado)");
            b.localPosition = a.localPosition; b.localRotation = a.localRotation; b.localScale = a.localScale;
        }
    }

    private static Transform FindPath(Scene scene, string path)
    {
        string[] parts = path.Split('/');
        GameObject root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == parts[0]);
        Transform t = root != null ? root.transform : null;
        for (int i = 1; i < parts.Length && t != null; i++) t = t.Find(parts[i]);
        return t;
    }

    private static void TelasDeManutencao(Scene scene)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<UIScreen>("Assets/Prefabs/UI/UI_Screen_Gabinete.prefab");
        int n = 0;
        foreach (UIScreenInteractable u in Object.FindObjectsByType<UIScreenInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (u.screen == null || !u.screen.gameObject.scene.IsValid() || u.screen.GetComponentInParent<Canvas>(true) != null) continue;
            if (u.screen.GetComponent<ComputerScreenController>() == null) { L.Add($"TELA FORA DE CANVAS (não corrigida): {PathOf(u.transform)}"); continue; }
            var so = new SerializedObject(u);
            so.FindProperty("screen").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            n++;
        }
        L.Add($"telas: {n} gabinete(s) passaram a abrir o prefab UI_Screen_Gabinete dentro do ScreenCanvas " +
              "(a UI_Screen_Gabinete da raiz da cena está fora de Canvas e ficou sem uso, desligada)");
    }

    private static void Prompt(Scene scene)
    {
        foreach (InteractionPrompt p in Object.FindObjectsByType<InteractionPrompt>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(p);
            so.FindProperty("showLabels").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            L.Add("prompt: só ícones (sem textos ao lado)");
        }
    }

    private static void PausaNaFase(Scene scene)
    {
        StageController stage = Object.FindAnyObjectByType<StageController>(FindObjectsInactive.Include);
        // pausa oficial = PauseMenu (do Matheus); acha tablet, câmera e fase sozinho
        if (stage.GetComponent<PauseMenu>() == null) stage.gameObject.AddComponent<PauseMenu>();
        L.Add("menu de pausa (Esc) no objeto Chamados");
    }

    // ================================================================== sala de TI

    private static bool NaSalaTI(Vector3 p) => SalaTI.Contains(new Vector2(p.x, p.z));

    // Monitores decorativos (com tela, dentro de outros modelos ou soltos) na sala de TI → monitores reais.
    private static void SalaTIMonitores(Scene scene, GameObject monitorPrefab)
    {
        Transform estoque = Find(scene, "Sala_TI").Find("Estoque");
        Vector3 centro = new Vector3(SalaTI.center.x, 0f, SalaTI.center.y);
        int n = 0;
        var candidatos = new List<Transform>();
        foreach (MonitorDisplay md in Object.FindObjectsByType<MonitorDisplay>(FindObjectsSortMode.None))
            if (md.GetComponentInParent<PickupInteractable>() == null) candidatos.Add(md.transform.parent != null ? md.transform.parent : md.transform);
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && t.GetComponent<PickupInteractable>() == null
                && PrefabUtility.GetCorrespondingObjectFromOriginalSource(t.gameObject)?.name == "OF_Monitor") candidatos.Add(t);
        foreach (Transform dec in candidatos.Distinct())
        {
            if (!dec.gameObject.activeInHierarchy) continue;
            Bounds b = WorldBounds(dec.gameObject);
            if (!NaSalaTI(b.center) || b.size.magnitude > 1.6f) continue;
            Vector3 olhar = Flat(centro - b.center);
            olhar = Mathf.Abs(olhar.x) > Mathf.Abs(olhar.z) ? new Vector3(Mathf.Sign(olhar.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(olhar.z));
            var go = (GameObject)PrefabUtility.InstantiatePrefab(monitorPrefab, scene);
            go.name = "Monitor_Reserva_" + (++n);
            go.transform.SetParent(estoque, true);
            go.transform.rotation = Quaternion.LookRotation(olhar);
            go.transform.position = new Vector3(b.center.x, b.min.y, b.center.z);
            Bounds nb = WorldBounds(go);
            go.transform.position += new Vector3(b.center.x - nb.center.x, b.min.y + 0.003f - nb.min.y, b.center.z - nb.center.z);
            dec.gameObject.SetActive(false);
            L.Add($"monitor decorativo {PathOf(dec)} desligado; monitor real {go.name} no lugar ({go.transform.position:F2})");
        }
        if (n == 0) L.Add("nenhum monitor decorativo restante na sala de TI");
        // nomes únicos (rodar de novo acrescenta monitores com o mesmo nome base)
        int k = 0;
        foreach (Transform m in estoque.Cast<Transform>().Where(t => t.name.StartsWith("Monitor_Reserva")).OrderBy(t => t.position.z).ToList())
            m.name = "Monitor_Reserva_" + (++k);
    }

    private static void Armarios(Scene scene, GameObject prefab)
    {
        Transform moveis = Find(scene, "Sala_TI").Find("Moveis");
        var velhos = moveis.Cast<Transform>().Where(t => PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)?.name == "Armario_TI").ToList();
        foreach (Transform v in velhos)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.transform.SetParent(moveis, false);
            go.transform.SetPositionAndRotation(v.position, v.rotation);
            go.name = v.name;
            Object.DestroyImmediate(v.gameObject);
        }
        // portas abertas não podem bater em nada (puff, parede, outro armário)
        Physics.SyncTransforms();
        foreach (Transform a in moveis.Cast<Transform>().Where(t => PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject) == prefab))
            foreach (OpenableInteractable op in a.GetComponentsInChildren<OpenableInteractable>())
            {
                var so = new SerializedObject(op);
                float ang = so.FindProperty("openAmount").floatValue;
                foreach (float tentativa in new[] { 95f, 85f, 75f })
                {
                    op.transform.localRotation = Quaternion.Euler(0f, Mathf.Sign(ang) * tentativa, 0f);
                    Physics.SyncTransforms();
                    var bc = op.GetComponent<BoxCollider>();
                    bool bate = Physics.OverlapBox(bc.bounds.center, bc.bounds.extents * 0.95f, op.transform.rotation, ~0, QueryTriggerInteraction.Ignore)
                        .Any(c => !c.transform.IsChildOf(a) && c.bounds.max.y > 0.05f);
                    op.transform.localRotation = Quaternion.identity;
                    if (bate && tentativa > 75f) continue;
                    if (!Mathf.Approximately(Mathf.Abs(ang), tentativa))
                    {
                        so.FindProperty("openAmount").floatValue = Mathf.Sign(ang) * tentativa;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    L.Add($"{a.name}/{op.name}: abre {tentativa}°{(bate ? " (ENCOSTA EM ALGO)" : "")}");
                    break;
                }
            }
        L.Add($"armários de TI funcionais: {velhos.Count} trocado(s) pelo Armario_Reserva (mesma posição)");
    }

    private static Material MatCor(string nome, Color c, bool emissao = false)
    {
        Material m = Mat(nome, c);
        if (emissao)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        return m;
    }

    private static void Rack3D(Scene scene)
    {
        RedeEstacaoInteractable rack = Object.FindObjectsByType<RedeEstacaoInteractable>(FindObjectsSortMode.None)
            .FirstOrDefault(r => PrefabUtility.GetCorrespondingObjectFromSource(r.gameObject)?.name == "RackInternet");
        if (rack == null) { L.Add("FALTA RackInternet"); return; }
        Transform velho = rack.transform.Find("Equipamentos_3D");
        if (velho != null) Object.DestroyImmediate(velho.gameObject);
        string fbx = "Assets/Art/Models/rack_interno.fbx";
        AssetDatabase.ImportAsset(fbx, ImportAssetOptions.ForceUpdate);
        var mats = new Dictionary<string, Material>
        {
            { "Claro", AssetDatabase.LoadAssetAtPath<Material>(MatDir + "M_Mobiliario_Claro.mat") },
            { "Escuro", AssetDatabase.LoadAssetAtPath<Material>(MatDir + "M_Mobiliario_Escuro.mat") },
            { "Metal", AssetDatabase.LoadAssetAtPath<Material>(MatDir + "M_Mobiliario_Metal.mat") },
            { "Porta", MatCor("M_Rack_Contatos", new Color(0.80f, 0.66f, 0.30f)) },
            { "Etiqueta", MatCor("M_Rack_Etiqueta", new Color(0.95f, 0.95f, 0.92f)) },
            { "Led", MatCor("M_Rack_Led", new Color(0.1f, 0.1f, 0.1f), true) },
            { "Cabo", MatCor("M_Rack_CaboPatch", new Color(0.20f, 0.45f, 0.90f)) },
        };
        // frente do rack = lado do vidro
        Renderer vidro = rack.GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.name == "vidro");
        Bounds rb = WorldBounds(rack.gameObject);
        Vector3 frente = vidro != null ? Flat(vidro.bounds.center - rb.center).normalized : Flat(rack.transform.forward);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx), scene);
        go.name = "Equipamentos_3D";
        go.transform.SetParent(rack.transform, false);
        go.transform.rotation = Quaternion.LookRotation(frente);
        go.transform.position = new Vector3(rb.center.x, rb.min.y + 0.035f, rb.center.z) - frente * 0.0f;
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            r.sharedMaterials = r.sharedMaterials.Select(m => m != null && mats.TryGetValue(m.name, out Material x) ? x : m).ToArray();
        foreach (Collider c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        Bounds gb = WorldBounds(go);
        // centraliza no interior (o modelo tem a frente das portas em -Y do Blender: atrás do vidro)
        go.transform.position += new Vector3(rb.center.x - gb.center.x, 0f, rb.center.z - gb.center.z);
        var eq = go.AddComponent<RackEquipamento3D>();
        var so = new SerializedObject(eq);
        SerializedProperty cabos = so.FindProperty("cabos"), leds = so.FindProperty("leds");
        cabos.arraySize = 6; leds.arraySize = 6;
        for (int i = 0; i < 6; i++)
        {
            cabos.GetArrayElementAtIndex(i).objectReferenceValue = go.transform.Find("Cabo_" + (i + 1))?.gameObject;
            leds.GetArrayElementAtIndex(i).objectReferenceValue = go.transform.Find("Led_S" + (i + 1))?.GetComponent<Renderer>();
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        gb = WorldBounds(go);
        bool dentro = rb.Contains(gb.min + Vector3.one * 0.005f) && rb.Contains(gb.max - Vector3.one * 0.005f);
        L.Add($"rack: switch, patch panel (6 portas), organizador, trilhos, modem e nobreak dentro do RackInternet " +
              $"({gb.size.x:0.00} x {gb.size.y:0.00} x {gb.size.z:0.00} m, {(dentro ? "dentro da caixa" : "PASSA DA CAIXA")}); " +
              "patch cords e LEDs seguem a rede (RackEquipamento3D); sem colisores");
    }

    // Cabos no gaveteiro: mais unidades de energia e vídeo (os chamados podem tirar o cabo da estação), até onde cabe.
    private static void EstoqueDeCabos(Scene scene)
    {
        Transform g = Find(scene, "Gaveteiro_Cabos");
        if (g == null) { L.Add("FALTA Gaveteiro_Cabos"); return; }
        var alvo = new Dictionary<CableType, int> { { CableType.Power, 4 }, { CableType.Video, 2 }, { CableType.Hdmi, 2 }, { CableType.Network, 3 } };
        foreach (DrawerContents dc in g.GetComponentsInChildren<DrawerContents>(true))
        {
            var so = new SerializedObject(dc);
            SerializedProperty entries = so.FindProperty("entries");
            BoxCollider area = dc.StorageArea;
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty e = entries.GetArrayElementAtIndex(i);
                var p = e.FindPropertyRelative("prefab").objectReferenceValue as PickupInteractable;
                CableItem cable = p != null ? p.GetComponent<CableItem>() : null;
                if (cable == null || !alvo.TryGetValue(cable.Type, out int q)) continue;
                int atual = e.FindPropertyRelative("quantity").intValue;
                Vector3 pos0 = e.FindPropertyRelative("localPosition").vector3Value, step = e.FindPropertyRelative("step").vector3Value;
                int cabe = atual;
                for (int k = atual; k < q; k++)
                {
                    Vector3 local = pos0 + step * k;
                    Vector3 w = dc.GetComponent<OpenableInteractable>().MovingPart.TransformPoint(local);
                    bool ok = area == null || dc.StorageContains(w);
                    // não pode cair em cima das unidades de outra entrada
                    for (int j = 0; j < entries.arraySize && ok; j++)
                    {
                        if (j == i) continue;
                        SerializedProperty o = entries.GetArrayElementAtIndex(j);
                        Vector3 op0 = o.FindPropertyRelative("localPosition").vector3Value, ost = o.FindPropertyRelative("step").vector3Value;
                        for (int m = 0; m < o.FindPropertyRelative("quantity").intValue; m++)
                            if ((op0 + ost * m - local).magnitude < Mathf.Max(0.06f, step.magnitude * 0.9f)) { ok = false; break; }
                    }
                    if (!ok) break;
                    cabe = k + 1;
                }
                if (cabe != atual)
                {
                    e.FindPropertyRelative("quantity").intValue = cabe;
                    L.Add($"gaveteiro de cabos: {cable.Type} {atual} → {cabe}{(cabe < q ? $" (queria {q}; não cabe mais na gaveta)" : "")}");
                }
                else L.Add($"gaveteiro de cabos: {cable.Type} = {atual}{(atual < q ? " (sem espaço para mais)" : "")}");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ================================================================== layout

    private class Est { public Transform t; public bool notebook; public string label; }

    private static Vector3 V(float x, float z) => new Vector3(x, 0f, z);

    private static void Layout(Scene scene)
    {
        Transform grupo = Find(scene, "Estacoes_Trabalho");
        Transform s1 = grupo.Find("Setor_1") ?? NovoFilho(grupo, "Setor_1");
        Transform s2 = grupo.Find("Setor_2") ?? NovoFilho(grupo, "Setor_2");

        // estações atuais (raízes "Estacao*" com equipamento), por nome antigo ou novo
        var todas = StationRoots(scene);
        Transform Pega(params string[] nomes) => todas.FirstOrDefault(t => nomes.Contains(t.name));

        // modelo de cluster das divisórias do usuário: divisa (1) + estação sul (r180) + estação norte (r0)
        Transform div2 = Find(scene, "divisa (1)");
        Vector3 offSul = V(-1.98f, 0.13f), offNorte = V(-1.77f, 1.60f);
        Transform div3 = Find(scene, "Divisa_E");

        // setor 1: as 6 vagas das 3 divisórias (as estações que o usuário já pôs ficam onde estão)
        var setor1 = new List<(Transform t, string[] nomes, Vector3? pos, float yaw)>
        {
            (null, new[] { "Estacao_A_Desktop", "Estacao_01" }, null, 180f),
            (null, new[] { "Estacao_D_Notebook (1)", "Estacao_02" }, null, 0f),
            (null, new[] { "Estacao_Direita_Problema", "Estacao_03" }, null, 180f),
            (null, new[] { "Estacao_Direita_Problema (1)", "Estacao_04" }, null, 0f),
            (null, new[] { "Estacao_E_Desktop", "Estacao_05" }, div3 != null ? div3.position + offSul : (Vector3?)null, 180f),
            (null, new[] { "Estacao_Direita_Problema (2)", "Estacao_06" }, null, 0f),
        };
        int k = 0;
        var ordem = new List<Transform>();
        foreach (var (_, nomes, pos, yaw) in setor1)
        {
            Transform t = Pega(nomes);
            k++;
            if (t == null) { L.Add($"FALTA estação do setor 1 ({nomes[0]})"); continue; }
            if (pos.HasValue && Flat(t.position - pos.Value).magnitude > 0.05f)
            {
                L.Add($"{t.name}: {t.position:F2} r{t.eulerAngles.y:0} → {new Vector3(pos.Value.x, 0, pos.Value.z):F2} r{yaw:0} (vaga sul da 3ª divisória)");
                t.SetPositionAndRotation(new Vector3(pos.Value.x, 0f, pos.Value.z), Quaternion.Euler(0f, yaw, 0f));
            }
            t.SetParent(s1, true);
            ordem.Add(t);
        }

        // setor 2: dois clusters iguais aos do setor 1 (divisória + desktop ao sul + notebook ao norte)
        var divPrefab = div2 != null ? PrefabUtility.GetCorrespondingObjectFromSource(div2.gameObject) : null;
        var desktopPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + "Estacao_Direita_Problema.prefab");
        Transform divS2 = s2.Find("Divisorias") ?? NovoFilho(s2, "Divisorias");
        Vector3[] origens = { new Vector3(49.3f, div2 != null ? div2.position.y : 0.93f, 14.2f), new Vector3(56.6f, div2 != null ? div2.position.y : 0.93f, 14.2f) };
        string[][] nb = { new[] { "Estacao_C_Notebook", "Estacao_08" }, new[] { "Estacao_D_Notebook", "Estacao_10" } };
        string[][] dk = { new[] { "Estacao_07" }, new[] { "Estacao_09" } };
        for (int c = 0; c < 2; c++)
        {
            Vector3 o = origens[c];
            string dn = "Divisa_S2_" + (c + 1);
            Transform d = divS2.Find(dn);
            if (d == null && divPrefab != null)
            {
                d = ((GameObject)PrefabUtility.InstantiatePrefab(divPrefab, scene)).transform;
                d.name = dn;
                d.SetParent(divS2, false);
            }
            if (d != null)
            {
                d.SetPositionAndRotation(o, div2.rotation);
                d.localScale = div2.localScale;
            }
            Transform desk = Pega(dk[c]);
            if (desk == null)
            {
                desk = ((GameObject)PrefabUtility.InstantiatePrefab(desktopPrefab, scene)).transform;
                desk.name = dk[c][0];
            }
            desk.SetParent(s2, true);
            desk.SetPositionAndRotation(new Vector3(o.x, 0f, o.z) + offSul, Quaternion.Euler(0f, 180f, 0f));
            Transform n = Pega(nb[c]);
            if (n == null) { L.Add($"FALTA {nb[c][0]}"); continue; }
            n.SetParent(s2, true);
            n.SetPositionAndRotation(new Vector3(o.x, 0f, o.z) + offNorte, Quaternion.Euler(0f, 0f, 0f));
            ordem.Add(desk);
            ordem.Add(n);
        }

        // nomes Estacao_01..N e identificação
        for (int i = 0; i < ordem.Count; i++)
        {
            ordem[i].name = $"Estacao_{i + 1:00}";
            ordem[i].SetSiblingIndex(i % 6);
        }
        L.Add($"layout: {ordem.Count} estações — setor 1: {string.Join(", ", ordem.Where(t => t.parent == s1).Select(t => t.name))}; " +
              $"setor 2: {string.Join(", ", ordem.Where(t => t.parent == s2).Select(t => t.name))}");

        Impressoras(scene);
        Associar(scene, ordem);
    }

    private static Transform NovoFilho(Transform pai, string nome)
    {
        var t = new GameObject(nome).transform;
        t.SetParent(pai, false);
        return t;
    }

    // 3ª impressora no setor 2, encostada na parede oeste (x 42,7), voltada para o leste.
    private static void Impressoras(Scene scene)
    {
        Transform area = Find(scene, "Area_Impressoras");
        Transform s2 = area.Find("Setor_2") ?? NovoFilho(area, "Setor_2");
        var refBancada = area.Cast<Transform>().FirstOrDefault(t => PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)?.name == "Bancada_Impressoras");
        var refImp = area.Cast<Transform>().FirstOrDefault(t => t.GetComponent<ImpressoraEstacao>() != null);
        if (refBancada == null || refImp == null) { L.Add("FALTA bancada/impressora de referência"); return; }
        Quaternion rot = Quaternion.Euler(0f, 90f, 0f);              // frente (+Z local) para o leste
        Quaternion delta = rot * Quaternion.Inverse(refBancada.rotation);
        Vector3 basePos = new Vector3(43.05f, 0f, 22.0f);
        // copia o conjunto da área de impressão 1 (bancada, impressora, toner, resmas) com as mesmas posições relativas
        var conjunto = area.Cast<Transform>().Where(t => t != s2 && t.parent == area &&
            Flat(t.position - refBancada.position).magnitude < 1.2f && !t.name.EndsWith(")")).ToList();
        foreach (Transform t in conjunto)
        {
            string nome = t.name + "_S2";
            Transform c = s2.Find(nome);
            if (c == null)
            {
                // duplicata completa (como Ctrl+D: mantém o vínculo com o prefab, os componentes e as referências)
                Selection.activeGameObject = t.gameObject;
                Unsupported.DuplicateGameObjectsUsingPasteboard();
                c = Selection.activeGameObject != null && Selection.activeGameObject != t.gameObject ? Selection.activeGameObject.transform : null;
                if (c == null) { L.Add("não consegui duplicar " + t.name); continue; }
                c.name = nome;
                c.SetParent(s2, true);
            }
            c.SetPositionAndRotation(basePos + delta * (t.position - refBancada.position), delta * t.rotation);
        }
        int n = Object.FindObjectsByType<ImpressoraEstacao>(FindObjectsSortMode.None).Length;
        L.Add($"impressoras: {n} (3ª no setor 2, parede oeste, z 22)");
    }

    // Funcionários, NPCs, plaquinhas, pontes de chamado e portas do rack.
    private static void Associar(Scene scene, List<Transform> estacoes)
    {
        TicketManager tickets = Object.FindAnyObjectByType<TicketManager>(FindObjectsInactive.Include);
        ScoreKeeper score = tickets.GetComponent<ScoreKeeper>();
        string[] pcIds = Field<string[]>("PcTickets").Concat(Field<string[]>("RedePcTickets")).ToArray();
        string[] nbIds = Field<string[]>("NotebookTickets");
        var placa = AssetDatabase.LoadAssetAtPath<GameObject>(Props + "Placa_Mesa.prefab");

        // NPCs antigos (um por estação) saem: só dois personagens de funcionário existem no projeto
        foreach (Transform st in estacoes)
            foreach (Transform npc in st.Cast<Transform>().Where(c => c.name.StartsWith("NPC_Estacao")).ToList())
                Object.DestroyImmediate(npc.gameObject);

        // estação de cada NPC: Carmen no 1º desktop do setor 1 com divisória do Matheus (Estacao_03), Paula no 1º notebook do setor 2
        Transform estCarmen = estacoes.FirstOrDefault(t => t.name == "Estacao_03");
        Transform estPaula = estacoes.FirstOrDefault(t => t.GetComponentInChildren<NotebookEstacao>() != null && t.parent.name == "Setor_2");

        int porta = 0;
        foreach (Transform st in estacoes)
        {
            string label = "Estação " + st.name.Substring(st.name.Length - 2);
            var emp = st.GetComponent<StationEmployee>() ?? st.gameObject.AddComponent<StationEmployee>();
            emp.stationLabel = label;
            int npcIdx = st == estCarmen ? 0 : st == estPaula ? 1 : -1;
            emp.employeeName = npcIdx >= 0 ? Npcs[npcIdx].nome : "";
            emp.department = npcIdx >= 0 ? Npcs[npcIdx].setor : "";
            EditorUtility.SetDirty(emp);
            Transform npc = npcIdx >= 0 ? CriarNpc(st, Npcs[npcIdx].modelo, scene) : null;
            // plaquinha (posição corrigida depois, em Plaquinhas)
            if (st.Find("Placa_Mesa") == null && placa != null)
            {
                var p = (GameObject)PrefabUtility.InstantiatePrefab(placa, scene);
                p.name = "Placa_Mesa";
                p.transform.SetParent(st, false);
            }
            ComputerCase pc = st.GetComponentInChildren<ComputerCase>();
            if (pc != null)
            {
                MakeFunctional(pc);
                var wall = st.GetComponentInChildren<WallConnectionPoint>();
                bool temPorta = wall != null && porta < 6;
                string[] ids = temPorta ? pcIds : pcIds.Where(i => i != TestadorCabo.ChamadoRack).ToArray();
                Bridge<PcTicketBridge>(pc.gameObject, tickets, $"PC da {label}", ids, npc, so => so.FindProperty("scoreKeeper").objectReferenceValue = score);
                if (wall != null)
                {
                    var link = wall.GetComponent<PontoDeRedeRack>() ?? wall.gameObject.AddComponent<PontoDeRedeRack>();
                    var so = new SerializedObject(link);
                    so.FindProperty("portaRack").intValue = temPorta ? ++porta : 0;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                L.Add($"{st.name}: desktop · {(npc != null ? $"NPC {emp.employeeName}" : "sem NPC (plaquinha: " + label + ")")} · " +
                      $"{(temPorta ? $"porta {porta} do rack" : "sem porta no rack (o rack tem 6)")}");
            }
            else
            {
                NotebookEstacao nbk = st.GetComponentInChildren<NotebookEstacao>();
                Bridge<NotebookTicketBridge>(nbk.gameObject, tickets, $"Notebook da {label}", nbIds, npc, Extra("NotebookExtra"));
                L.Add($"{st.name}: notebook · {(npc != null ? $"NPC {emp.employeeName}" : "sem NPC (plaquinha: " + label + ")")}");
            }
        }
        foreach (ImpressoraEstacao pr in Object.FindObjectsByType<ImpressoraEstacao>(FindObjectsSortMode.None))
            if (pr.GetComponentInParent<StationEmployee>() == null)
                Bridge<ImpressoraTicketBridge>(pr.gameObject, tickets, pr.transform.parent.name == "Setor_2" ? "Impressora do setor 2" : "Impressora " +
                    (pr.transform.position.x < 25f ? "do lado oeste" : "da área de impressão"), Field<string[]>("PrinterTickets"), null, Extra("PrinterExtra"));
        typeof(TesteIntegradoSetup).GetMethod("ConfigureRestock", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Invoke(null, new object[] { tickets.gameObject, L });
    }

    internal static Transform CriarNpc(Transform station, string modeloPath, Scene scene)
    {
        var go = new GameObject("NPC_Estacao");
        SceneManager.MoveGameObjectToScene(go, scene);
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modeloPath), scene);
        visual.name = "Visual";
        visual.transform.SetParent(go.transform, false);
        Bounds vb = WorldBounds(visual);
        if (vb.size.y > 0.01f) visual.transform.localScale *= 1.65f / vb.size.y;
        vb = WorldBounds(visual);
        visual.transform.position += new Vector3(-vb.center.x, -vb.min.y, -vb.center.z);
        var obs = go.AddComponent<NavMeshObstacle>();
        obs.shape = NavMeshObstacleShape.Capsule; obs.radius = 0.3f; obs.height = 1.65f; obs.center = new Vector3(0f, 0.825f, 0f); obs.carving = true;
        go.transform.SetParent(station, true);

        // de pé ao lado da cadeira, num ponto livre, olhando para a mesa
        Physics.SyncTransforms();
        Bounds db = WorldBounds(station.GetComponentInChildren<WorkSurface>().gameObject);
        Transform cadeira = station.Find("Cadeira");
        Vector3 c = cadeira != null ? cadeira.position : db.center - station.forward;
        Vector3 paraCadeira = Flat(c - db.center).normalized;
        Vector3 lado = Vector3.Cross(Vector3.up, paraCadeira);
        Vector3 melhor = c + lado * 0.75f;
        foreach (float s in new[] { 1f, -1f })
            foreach (float a in new[] { 0.7f, 0.85f, 1.0f })
                foreach (float f in new[] { 0f, 0.3f, 0.6f })
                {
                    Vector3 q = c + lado * s * a + paraCadeira * f; q.y = 0f;
                    bool livre = Physics.OverlapCapsule(q + Vector3.up * 0.35f, q + Vector3.up * 1.5f, 0.3f, ~0, QueryTriggerInteraction.Ignore)
                        .All(col => col.bounds.max.y < 0.05f || col.transform.IsChildOf(go.transform));
                    if (livre && NavMesh.SamplePosition(q, out _, 0.4f, NavMesh.AllAreas)) { melhor = q; goto achou; }
                }
        achou:
        go.transform.position = melhor;
        Vector3 olhar = Flat(db.center - melhor);
        if (olhar.sqrMagnitude > 1e-4f) go.transform.rotation = Quaternion.LookRotation(olhar.normalized);
        return go.transform;
    }

    // ================================================================== mesas e plaquinhas

    // Mesa de cubículo: o modelo desce 12 cm abaixo do piso. Escala vertical do visual em torno do TAMPO (que fica a
    // 0,75 m, onde estão o colisor e os equipamentos), até o pé encostar no piso. Só nas instâncias da cena.
    internal static void MesasNoPiso(Scene scene)
    {
        int n = 0;
        foreach (WorkSurface ws in Object.FindObjectsByType<WorkSurface>(FindObjectsSortMode.None))
        {
            var bc = ws.GetComponent<BoxCollider>();
            if (bc == null) continue;
            float topo = bc.bounds.max.y, piso = bc.bounds.min.y;
            foreach (Transform v in ws.transform.Cast<Transform>())
            {
                var rs = v.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                Bounds b = WorldBounds(v.gameObject);
                if (b.min.y > piso - 0.01f) continue;
                float f = (topo - piso) / (topo - b.min.y);
                Vector3 s = v.localScale; s.y *= f; v.localScale = s;
                Vector3 p = v.position; p.y = topo - (topo - p.y) * f; v.position = p;
                Bounds nb = WorldBounds(v.gameObject);
                n++;
                L.Add($"mesa {PathOf(v)}: pés de y {b.min.y:0.000} → {nb.min.y:0.000} (tampo {topo:0.000}; topo do modelo {b.max.y:0.000} → {nb.max.y:0.000})");
            }
        }
        if (n == 0) L.Add("mesas: nenhuma atravessando o piso");
    }

    // Plaquinha: no tampo da mesa da estação (colisor do WorkSurface), na beira do lado da cadeira, no canto oposto ao
    // equipamento, virada para a cadeira; altura = o tampo (raio de cima).
    internal static void Plaquinhas(Scene scene)
    {
        Physics.SyncTransforms();
        foreach (StationNamePlate placa in Object.FindObjectsByType<StationNamePlate>(FindObjectsSortMode.None))
        {
            StationEmployee st = StationEmployee.Of(placa);
            WorkSurface mesa = st != null ? st.GetComponentInChildren<WorkSurface>() : null;
            var bc = mesa != null ? mesa.GetComponent<BoxCollider>() : null;
            if (bc == null) { L.Add($"plaquinha sem mesa: {PathOf(placa.transform)}"); continue; }
            Transform cadeira = st.transform.Find("Cadeira");
            Transform equip = st.GetComponentInChildren<ComputerCase>() != null ? st.GetComponentInChildren<ComputerCase>().transform
                : st.GetComponentInChildren<NotebookEstacao>().transform;
            Transform mt = mesa.transform;
            Vector3 h = bc.size * 0.5f;
            Vector3 melhor = Vector3.zero; float melhorNota = float.MinValue;
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    Vector3 local = bc.center + new Vector3(sx * (h.x - 0.28f), h.y, sz * (h.z - 0.14f));
                    Vector3 w = mt.TransformPoint(local);
                    float nota = (cadeira != null ? -Flat(w - cadeira.position).magnitude * 2f : 0f) + Flat(w - equip.position).magnitude;
                    // não em cima de outro objeto (teclado, mouse, xícara)
                    bool livre = !Physics.OverlapBox(w + Vector3.up * 0.06f, new Vector3(0.1f, 0.05f, 0.06f), mt.rotation, ~0, QueryTriggerInteraction.Ignore)
                        .Any(c => !c.transform.IsChildOf(mesa.transform) && !c.transform.IsChildOf(placa.transform));
                    if (!livre) nota -= 100f;
                    if (nota > melhorNota) { melhorNota = nota; melhor = w; }
                }
            float topo = bc.bounds.max.y;
            foreach (RaycastHit hit in Physics.RaycastAll(melhor + Vector3.up * 0.5f, Vector3.down, 1f))
                if (hit.collider == bc) topo = hit.point.y;
            Vector3 paraCadeira = cadeira != null ? Flat(cadeira.position - melhor).normalized : -Flat(mt.forward);
            // a placa encara a cadeira, alinhada com a borda da mesa
            Vector3 eixo = Mathf.Abs(Vector3.Dot(paraCadeira, Flat(mt.forward))) > Mathf.Abs(Vector3.Dot(paraCadeira, Flat(mt.right)))
                ? Flat(mt.forward) * Mathf.Sign(Vector3.Dot(paraCadeira, Flat(mt.forward))) : Flat(mt.right) * Mathf.Sign(Vector3.Dot(paraCadeira, Flat(mt.right)));
            placa.transform.SetPositionAndRotation(new Vector3(melhor.x, topo, melhor.z), Quaternion.LookRotation(eixo));
            placa.Refresh();
            foreach (TextMesh t in placa.GetComponentsInChildren<TextMesh>(true)) EditorUtility.SetDirty(t);
        }
        L.Add("plaquinhas: apoiadas no tampo da mesa de cada estação");
    }

    // ================================================================== ronda

    internal static void Ronda(Scene scene)
    {
        Transform ronda = Find(scene, "Ronda_Supervisor");
        Transform player = Find(scene, "Player");
        NavMesh.SamplePosition(player.position, out NavMeshHit inicio, 2f, NavMesh.AllAreas);
        Rect[] areas = { Setor1, Setor2, SalaTI, CorredorCentral, CorredorNorte };
        var pontos = new List<(Vector3 p, int area)>();
        for (int a = 0; a < areas.Length; a++)
            for (float x = areas[a].xMin; x <= areas[a].xMax; x += 1f)
                for (float z = areas[a].yMin; z <= areas[a].yMax; z += 1f)
                {
                    if (!NavMesh.SamplePosition(new Vector3(x, 0f, z), out NavMeshHit h, 0.4f, NavMesh.AllAreas) || h.position.y > 0.3f) continue;
                    if (!NavMesh.FindClosestEdge(h.position, out NavMeshHit borda, NavMesh.AllAreas) || borda.distance < (a == 2 ? 0.5f : 0.7f)) continue;
                    var path = new NavMeshPath();
                    if (!NavMesh.CalculatePath(inicio.position, h.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                    pontos.Add((h.position, a));
                }
        // por área: setor 1 = 5 pontos, setor 2 = 4, TI = 1, corredores = 1 cada (espalhados: o mais longe dos já escolhidos)
        int[] cota = { 5, 4, 1, 1, 1 };
        var escolhidos = new List<Vector3>();
        for (int a = 0; a < areas.Length; a++)
        {
            var daArea = pontos.Where(p => p.area == a).Select(p => p.p).ToList();
            var aqui = new List<Vector3>();
            for (int i = 0; i < cota[a] && daArea.Count > 0; i++)
            {
                Vector3 c = aqui.Count == 0 ? daArea.OrderBy(p => (p - new Vector3(areas[a].center.x, 0, areas[a].center.y)).sqrMagnitude).First()
                    : daArea.OrderByDescending(p => aqui.Concat(escolhidos).Min(e => (p - e).sqrMagnitude)).First();
                aqui.Add(c);
            }
            escolhidos.AddRange(aqui);
        }
        // volta pelo vizinho mais próximo (caminho no NavMesh), começando perto do jogador
        var rota = new List<Vector3>();
        var resto = new List<Vector3>(escolhidos);
        Vector3 atual = inicio.position;
        while (resto.Count > 0)
        {
            Vector3 prox = resto.OrderBy(p => PathLen(atual, p)).First();
            rota.Add(prox); resto.Remove(prox); atual = prox;
        }
        for (int i = ronda.childCount - 1; i >= 0; i--) Object.DestroyImmediate(ronda.GetChild(i).gameObject);
        for (int i = 0; i < rota.Count; i++)
        {
            var p = new GameObject("Ponto_" + (i + 1)).transform;
            p.SetParent(ronda, false);
            p.position = rota[i] + Vector3.up * 0.05f;
        }
        SupervisorPatrol boss = Object.FindAnyObjectByType<SupervisorPatrol>(FindObjectsInactive.Include);
        var so = new SerializedObject(boss);
        so.FindProperty("route").objectReferenceValue = ronda;
        SerializedProperty ip = so.FindProperty("interestPoints");
        var interesse = StationRoots(scene).ToList();
        interesse.AddRange(Object.FindObjectsByType<ImpressoraEstacao>(FindObjectsSortMode.None).Select(i => i.transform));
        WorkSurface manut = Find(scene, "Sala_TI").GetComponentInChildren<WorkSurface>();
        if (manut != null) interesse.Add(manut.transform);
        ip.arraySize = interesse.Count;
        for (int i = 0; i < interesse.Count; i++) ip.GetArrayElementAtIndex(i).objectReferenceValue = interesse[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        if (rota.Count > 0) boss.transform.position = rota[0] + Vector3.up * 0.05f;
        string Area(Vector3 p) => SalaTI.Contains(new Vector2(p.x, p.z)) ? "TI" : Setor2.Contains(new Vector2(p.x, p.z)) ? "S2" : Setor1.Contains(new Vector2(p.x, p.z)) ? "S1" : "corredor";
        L.Add($"ronda: {rota.Count} pontos ({string.Join(" → ", rota.Select(Area))}); {interesse.Count} pontos de interesse");
    }

    internal static float PathLen(Vector3 a, Vector3 b)
    {
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(a, b, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return float.MaxValue;
        float d = 0f;
        for (int i = 1; i < path.corners.Length; i++) d += (path.corners[i] - path.corners[i - 1]).magnitude;
        return d;
    }

    // ================================================================== validação

    private static void Validar(Scene scene)
    {
        var problemas = new List<string>();
        Physics.SyncTransforms();
        Transform player = Find(scene, "Player");
        NavMesh.SamplePosition(player.position, out NavMeshHit inicio, 2f, NavMesh.AllAreas);
        void Alcance(string nome, Vector3 alvo)
        {
            if (!NavMesh.SamplePosition(alvo, out NavMeshHit h, 1.0f, NavMesh.AllAreas)) { problemas.Add($"{nome}: sem NavMesh perto"); return; }
            var path = new NavMeshPath();
            bool ok = NavMesh.CalculatePath(inicio.position, h.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
            if (!ok) problemas.Add(nome + " inalcançável");
        }
        L.Add("VALIDAÇÃO");
        var estacoes = StationRoots(scene);
        foreach (Transform st in estacoes)
        {
            Transform cad = st.Find("Cadeira");
            Bounds mesa = WorldBounds(st.GetComponentInChildren<WorkSurface>().gameObject);
            Alcance(st.name, cad != null ? cad.position + Flat(cad.position - mesa.center).normalized * 0.5f : st.position);
            // estação bloqueando porta/janela
            foreach (Rect r in Acessos)
                if (r.Overlaps(Rect.MinMaxRect(mesa.min.x, mesa.min.z, mesa.max.x, mesa.max.z))) problemas.Add($"{st.name} encosta num acesso (porta/janela)");
            // interpenetração com paredes/móveis de outros (fora da própria estação e das divisórias)
            foreach (Collider c in Physics.OverlapBox(mesa.center + Vector3.up * 0.1f, new Vector3(mesa.extents.x, 0.3f, mesa.extents.z) * 0.95f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c.transform.IsChildOf(st) || c.bounds.max.y < 0.05f || c.name.ToLower().StartsWith("divisa") || c.GetComponentInParent<Rigidbody>() != null) continue;
                problemas.Add($"{st.name} encosta em {PathOf(c.transform)}");
            }
            var emp = st.GetComponent<StationEmployee>();
            string placaTxt = st.GetComponentInChildren<StationNamePlate>()?.GetComponentInChildren<TextMesh>()?.text;
            if (placaTxt != emp.DisplayName) problemas.Add($"{st.name}: plaquinha '{placaTxt}' ≠ '{emp.DisplayName}'");
            TicketDeviceBridge br = st.GetComponentInChildren<TicketDeviceBridge>();
            if (br == null) problemas.Add($"{st.name} sem ponte de chamado");
        }
        foreach (ImpressoraEstacao p in Object.FindObjectsByType<ImpressoraEstacao>(FindObjectsSortMode.None))
        {
            Alcance("impressora " + PathOf(p.transform), p.transform.position + Flat(p.transform.forward) * 0.8f);
            if (p.GetComponent<ImpressoraTicketBridge>() == null) problemas.Add("impressora sem ponte: " + PathOf(p.transform));
        }
        foreach (Transform a in Find(scene, "Sala_TI").Find("Moveis").Cast<Transform>().Where(t => t.name.StartsWith("Armario")))
            Alcance(a.name, a.position + Flat(a.forward) * 0.9f);
        Transform rack = Find(scene, "RackInternet");
        Alcance("rack", rack.position + Flat(rack.forward) * 0.8f);
        // pontos da ronda: todos alcançáveis e fora de móveis
        foreach (Transform p in Find(scene, "Ronda_Supervisor"))
        {
            Alcance("ronda " + p.name, p.position);
            if (Physics.OverlapCapsule(p.position + Vector3.up * 0.4f, p.position + Vector3.up * 1.5f, 0.3f, ~0, QueryTriggerInteraction.Ignore)
                .Any(c => c.bounds.max.y > 0.05f && c.GetComponentInParent<SupervisorPatrol>() == null && c.GetComponentInParent<PlayerMovement>() == null))
                problemas.Add($"ronda {p.name} dentro de algo");
        }
        int npcs = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name == "NPC_Estacao");
        L.Add($"  estações {estacoes.Count}, NPCs de estação {npcs}, impressoras {Object.FindObjectsByType<ImpressoraEstacao>(FindObjectsSortMode.None).Length}");
        var nomes = Object.FindObjectsByType<StationEmployee>(FindObjectsSortMode.None).Where(e => e.Occupied).Select(e => e.employeeName).ToList();
        if (nomes.Count != nomes.Distinct().Count()) problemas.Add("nomes de NPC repetidos");
        L.Add($"VALIDAÇÃO: {problemas.Count} problema(s)");
        foreach (string p in problemas) L.Add("  PROBLEMA: " + p);
    }
}
