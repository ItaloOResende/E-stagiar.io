using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Etapa "estações dos minigames, quebráveis e novos itens" (2026-10-14). Pontual e idempotente: NÃO reconstrói a cena,
/// só mexe no que é desta etapa.
///  1. Telas 2D dos minigames (UI_Screen_Notebook/Impressora/Crimpagem/Rack) viram prefabs em Prefabs/UI, ligadas às
///     instâncias que já estão na cena (mesmo objeto, mesmas referências).
///  2. Estações (Notebook_Estacao, Impressora_Estacao, Caixa_CaboRede, RackInternet) viram prefabs em Prefabs/Gameplay.
///     No prefab, as estações apontam para os PREFABS das telas (cópias novas funcionam sozinhas: o UIScreenManager
///     instancia a tela); na cena continuam apontando para as telas da cena (override), como antes.
///  3. Impressora: modelo oficial do Matheus (impressora.fbx, sem alterar) dentro do Visual, os blocos do Ítalo
///     desativados (não apagados), MeshCollider no modelo e o painel 3D (PainelControle: Item_PainelImpressora +
///     visor ao vivo ImpressoraPainel3D + ImpressoraPainelInteractable, que abre a tela no teclado do IP).
///  4. Itens: Pickup_Alicate (alicate de crimpar, ToolItem CrimpingTool) e Pickup_Ssd com o modelo 3D Item_Ssd.
///     Alicate também na gaveta de ferramentas do Gaveteiro_Hardware e um em cima da caixa de cabo.
///  5. Quebráveis: Gabinete.prefab e o monitor do PC_Escritorio com BreakableEquipment + PartFallDamage e uma camada
///     de rachaduras (Visual_Quebrado, M_Rachadura).
/// Menu: Tools &gt; E-stagiar.io &gt; Estações dos minigames, quebráveis e novos itens
/// </summary>
public static class MinigamesEstacoesSetup
{
    private const string Cena = "Assets/Scenes/Office_Placeholder.unity";
    private const string Gameplay = "Assets/Prefabs/Gameplay/";
    private const string PrefabsUI = "Assets/Prefabs/UI/";
    private const string Componentes = "Assets/Art/Models/componentes.fbx";
    private const string ModeloImpressora = "Assets/Art/Models/impressora.fbx";
    private const string Rachadura = "Assets/Art/Textures/T_Rachadura.png";
    private const string MatRachadura = "Assets/Art/Materials/M_Rachadura.mat";
    /// <summary>Material ORIGINAL do Matheus para a impressora: o "Material.001" do FBX usa a textura officepapers.png, a mesma
    /// do M_Objetos_Parede (dele). O FBX não acha a textura sozinho (ela está em Art/Textures), por isso o remapeamento.</summary>
    private const string MatOriginalImpressora = "Assets/Art/Materials/M_Objetos_Parede.mat";
    private const string MatAlphaClip = "Assets/Art/Materials/PH_M_Item2D_HD.mat";   // só para copiar a configuração (URP/Lit, Alpha Clip)

    /// <summary>Profundidade (m) da impressora (eixo curto do modelo, da frente para trás).</summary>
    private const float ProfundidadeImpressora = 0.30f;
    /// <summary>Inclinação (graus) do painel para cima, na face da frente (para quem está em pé na frente).</summary>
    private const float InclinacaoPainel = 15f;

    [MenuItem("Tools/E-stagiar.io/Estações dos minigames, quebráveis e novos itens")]
    public static void Run()
    {
        AssetDatabase.Refresh();
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Material rachadura = GarantirMaterialRachadura();
        GameObject alicate = GarantirAlicate();
        ConfigurarSsd();
        AlicateNaGaveta(alicate);
        ConfigurarGabinete(rachadura);
        ConfigurarMonitor(rachadura);

        var cena = EditorSceneManager.OpenScene(Cena, OpenSceneMode.Single);

        // 1) telas -> prefabs (antes das estações, que apontam para elas)
        var telas = new Dictionary<string, (UIScreen cena, UIScreen prefab)>();
        foreach (string nome in new[] { "UI_Screen_Notebook", "UI_Screen_Impressora", "UI_Screen_Crimpagem", "UI_Screen_Rack" })
            telas[nome] = TelaComoPrefab(nome);

        // 3) impressora (na cena, antes de virar prefab)
        GameObject imp = Achar("Impressora_Estacao");
        if (imp != null) ConfigurarImpressora(imp, telas["UI_Screen_Impressora"].cena);
        else Debug.LogWarning("MinigamesEstacoesSetup: 'Impressora_Estacao' não está na cena.");

        // 2) estações -> prefabs
        EstacaoComoPrefab("Notebook_Estacao", telas["UI_Screen_Notebook"]);
        EstacaoComoPrefab("Impressora_Estacao", telas["UI_Screen_Impressora"]);
        EstacaoComoPrefab("Caixa_CaboRede", telas["UI_Screen_Crimpagem"]);
        EstacaoComoPrefab("RackInternet", telas["UI_Screen_Rack"]);

        // 4) um alicate em cima da caixa de cabo (posição inicial da cena: não conta como "largado")
        AlicateNaCaixa(alicate);

        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        AssetDatabase.SaveAssets();
        Validar();
        Debug.Log("MinigamesEstacoesSetup: concluído.");
    }

    // ================================================================== materiais e malhas

    private static Material GarantirMaterialRachadura()
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(Rachadura);
        if (imp != null && (!imp.alphaIsTransparency || imp.wrapMode != TextureWrapMode.Clamp))
        {
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Rachadura);
        var m = AssetDatabase.LoadAssetAtPath<Material>(MatRachadura);
        if (m == null)
        {
            m = new Material(AssetDatabase.LoadAssetAtPath<Material>(MatAlphaClip)) { name = "M_Rachadura" };
            AssetDatabase.CreateAsset(m, MatRachadura);
        }
        m.SetTexture("_BaseMap", tex);
        m.SetTexture("_MainTex", tex);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Cutoff", 0.35f);
        m.SetFloat("_Cull", 0f);                 // as duas faces
        m.doubleSidedGI = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>Malha e materiais de um item do componentes.fbx (ex.: "Item_Ssd").</summary>
    internal static (Mesh mesh, Material[] mats) ItemFbx(string nome)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Componentes);
        MeshFilter f = model.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(x => x.sharedMesh != null && x.sharedMesh.name == nome);
        if (f == null) throw new System.Exception("MinigamesEstacoesSetup: '" + nome + "' não está no componentes.fbx (reimporte o FBX).");
        return (f.sharedMesh, f.GetComponent<MeshRenderer>().sharedMaterials);
    }

    /// <summary>Visual = a malha do item (como a chave de fenda) e BoxCollider da raiz com altura mínima para a mira.</summary>
    internal static void VisualDoItem(GameObject root, string item, float alturaMinima)
    {
        var (mesh, mats) = ItemFbx(item);
        Transform visual = root.transform.Find("Visual");
        if (visual == null) { visual = new GameObject("Visual").transform; visual.SetParent(root.transform, false); }
        for (int i = visual.childCount - 1; i >= 0; i--) Object.DestroyImmediate(visual.GetChild(i).gameObject);
        visual.localPosition = Vector3.zero; visual.localRotation = Quaternion.identity; visual.localScale = Vector3.one;
        foreach (Collider c in visual.GetComponents<Collider>()) Object.DestroyImmediate(c);
        MeshFilter mf = Garantir<MeshFilter>(visual);
        MeshRenderer mr = Garantir<MeshRenderer>(visual);
        mf.sharedMesh = mesh;
        mr.sharedMaterials = mats;

        Bounds b = mesh.bounds;
        var box = Garantir<BoxCollider>(root);
        float h = Mathf.Max(b.size.y, alturaMinima);
        box.size = new Vector3(b.size.x + 0.01f, h, b.size.z + 0.01f);
        box.center = new Vector3(b.center.x, b.min.y + h / 2f, b.center.z);
    }

    // ================================================================== itens

    private static GameObject GarantirAlicate()
    {
        string path = Gameplay + "Pickup_Alicate.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            AssetDatabase.CopyAsset(Gameplay + "Pickup_Ferramenta.prefab", path);   // mesma pose de mão, física e prompt

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        root.name = "Pickup_Alicate";
        VisualDoItem(root, "Item_AlicateCrimpar", 0.04f);
        var so = new SerializedObject(root.GetComponent<ToolItem>());
        so.FindProperty("toolType").intValue = (int)ToolType.CrimpingTool;
        so.FindProperty("displayName").stringValue = "alicate de crimpar";
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static void ConfigurarSsd()
    {
        string path = Gameplay + "Pickup_Ssd.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        VisualDoItem(root, "Item_Ssd", 0.02f);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    /// <summary>Uma unidade do alicate na gaveta de ferramentas (a mesma da chave de fenda): pegar e guardar de volta.</summary>
    private static void AlicateNaGaveta(GameObject alicate)
    {
        string path = Gameplay + "Gaveteiro_Hardware.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        var pick = alicate.GetComponent<PickupInteractable>();
        bool mudou = false;
        foreach (DrawerContents dc in root.GetComponentsInChildren<DrawerContents>(true))
        {
            var so = new SerializedObject(dc);
            SerializedProperty entries = so.FindProperty("entries");
            bool temChave = false, temAlicate = false;
            for (int i = 0; i < entries.arraySize; i++)
            {
                var p = entries.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue as PickupInteractable;
                if (p == null) continue;
                if (p.name == "Pickup_Ferramenta") temChave = true;
                if (p == pick) temAlicate = true;
            }
            if (!temChave || temAlicate) continue;
            entries.arraySize++;
            SerializedProperty e = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            e.FindPropertyRelative("prefab").objectReferenceValue = pick;
            e.FindPropertyRelative("quantity").intValue = 1;
            e.FindPropertyRelative("delivery").enumValueIndex = (int)DrawerContents.Delivery.PhysicalInDrawer;
            e.FindPropertyRelative("localPosition").vector3Value = new Vector3(0f, GaveteirosSetup.Floor + 0.002f, -0.07f);
            e.FindPropertyRelative("localEuler").vector3Value = Vector3.zero;
            e.FindPropertyRelative("step").vector3Value = Vector3.zero;
            so.ApplyModifiedPropertiesWithoutUndo();
            mudou = true;
        }
        if (mudou) PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void AlicateNaCaixa(GameObject alicate)
    {
        GameObject caixa = Achar("Caixa_CaboRede");
        if (caixa == null || Achar("Alicate_Pickup") != null) return;
        Bounds b = Limites(caixa);
        var item = (GameObject)PrefabUtility.InstantiatePrefab(alicate);
        item.name = "Alicate_Pickup";
        item.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, caixa.transform.eulerAngles.y, 0f));
        Bounds ib = Limites(item);
        Vector3 alvo = new Vector3(b.center.x, b.max.y + 0.002f, b.center.z) - caixa.transform.right * (b.extents.x * 0.35f);
        item.transform.position += alvo - new Vector3(ib.center.x, ib.min.y, ib.center.z);
    }

    // ================================================================== quebráveis

    internal static void Quebravel(GameObject root, string nome, int quedas, GameObject camada)
    {
        var eq = Garantir<BreakableEquipment>(root);
        var so = new SerializedObject(eq);
        so.FindProperty("displayName").stringValue = nome;
        so.ApplyModifiedPropertiesWithoutUndo();

        var dano = Garantir<PartFallDamage>(root);
        so = new SerializedObject(dano);
        so.FindProperty("dropsToBreak").intValue = quedas;
        so.FindProperty("intactVisual").objectReferenceValue = null;   // camada por cima: o modelo oficial fica
        so.FindProperty("brokenVisual").objectReferenceValue = camada;
        so.ApplyModifiedPropertiesWithoutUndo();
        camada.SetActive(false);
    }

    internal static GameObject NovaCamada(Transform pai)
    {
        Transform velho = pai.Find("Visual_Quebrado");
        if (velho != null) Object.DestroyImmediate(velho.gameObject);
        var go = new GameObject("Visual_Quebrado");
        go.transform.SetParent(pai, false);
        return go;
    }

    internal static void Placa(Transform pai, Mesh mesh, Material mat, Vector3 posMundo, Quaternion rotMundo, Vector3 escalaMundo)
    {
        var go = new GameObject("Rachadura");
        go.transform.SetParent(pai, false);
        go.transform.SetPositionAndRotation(posMundo, rotMundo);
        Vector3 ls = pai.lossyScale;
        go.transform.localScale = new Vector3(escalaMundo.x / ls.x, escalaMundo.y / ls.y, escalaMundo.z / ls.z);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>Gabinete: quebra na 2ª queda; rachaduras nas duas laterais (as faces maiores).</summary>
    private static void ConfigurarGabinete(Material mat)
    {
        string path = Gameplay + "Gabinete.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        Transform visual = root.transform.Find("Visual");
        Bounds b = LimitesLocais(root.transform, visual != null ? visual.gameObject : root);
        GameObject camada = NovaCamada(root.transform);
        Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        bool xEstreito = b.size.x < b.size.z;
        Vector3 eixo = xEstreito ? Vector3.right : Vector3.forward;
        float meia = (xEstreito ? b.extents.x : b.extents.z) + 0.003f;
        float largura = (xEstreito ? b.size.z : b.size.x) * 0.85f;
        foreach (float s in new[] { 1f, -1f })
        {
            Vector3 n = root.transform.TransformDirection(eixo * s);
            Vector3 c = root.transform.TransformPoint(b.center + eixo * s * meia);
            Placa(camada.transform, quad, mat, c, Quaternion.LookRotation(-n, root.transform.up), new Vector3(largura, b.size.y * 0.85f, 1f));
        }
        Quebravel(root, "gabinete", 2, camada);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    /// <summary>Monitor do PC_Escritorio: quebra na 1ª queda; rachaduras por cima da tela (MonitorDisplay).</summary>
    private static void ConfigurarMonitor(Material mat)
    {
        string path = Gameplay + "PC_Escritorio.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        MonitorDisplay tela = root.GetComponentInChildren<MonitorDisplay>(true);
        PickupInteractable monitor = tela != null ? tela.GetComponentInParent<PickupInteractable>(true) : null;
        if (monitor == null)
        {
            Debug.LogWarning("MinigamesEstacoesSetup: monitor pegável não encontrado no PC_Escritorio.");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }
        Renderer r = tela.GetComponent<Renderer>();
        MeshFilter mf = tela.GetComponent<MeshFilter>();
        GameObject camada = NovaCamada(monitor.transform);
        if (r != null && mf != null && mf.sharedMesh != null)
        {
            Vector3 n = tela.transform.TransformDirection(mf.sharedMesh.normals.Length > 0 ? mf.sharedMesh.normals[0] : Vector3.back).normalized;
            Placa(camada.transform, mf.sharedMesh, mat, tela.transform.position + n * 0.0015f, tela.transform.rotation, tela.transform.lossyScale);
        }
        Quebravel(monitor.gameObject, "monitor", 1, camada);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // ================================================================== telas e estações

    private static (UIScreen cena, UIScreen prefab) TelaComoPrefab(string nome)
    {
        GameObject go = Achar(nome);
        if (go == null) throw new System.Exception("MinigamesEstacoesSetup: tela '" + nome + "' não está na cena.");
        GameObject asset = Conectar(go, PrefabsUI + nome + ".prefab");
        return (go.GetComponent<UIScreen>(), asset.GetComponent<UIScreen>());
    }

    /// <summary>Transforma o objeto da cena em instância de um prefab novo (ou reaproveita, se já for).</summary>
    private static GameObject Conectar(GameObject go, string path)
    {
        if (PrefabUtility.IsAnyPrefabInstanceRoot(go) && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go) == path)
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return PrefabUtility.SaveAsPrefabAssetAndConnect(go, path, InteractionMode.AutomatedAction);
    }

    private static void EstacaoComoPrefab(string nome, (UIScreen cena, UIScreen prefab) tela)
    {
        GameObject go = Achar(nome);
        if (go == null) { Debug.LogWarning("MinigamesEstacoesSetup: '" + nome + "' não está na cena."); return; }
        string path = Gameplay + nome + ".prefab";
        Conectar(go, path);

        // No prefab: as telas são os PREFABS (uma cópia nova da estação funciona sozinha).
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        ApontarTelas(root, tela.prefab);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);

        // Na cena: continuam as telas da cena (como antes; override da instância).
        ApontarTelas(go, tela.cena);
        foreach (Component c in go.GetComponentsInChildren<Component>(true))
            if (c is RedeEstacaoInteractable || c is UIScreenInteractable || c is ImpressoraPainelInteractable)
                PrefabUtility.RecordPrefabInstancePropertyModifications(c);
    }

    private static void ApontarTelas(GameObject root, UIScreen tela)
    {
        foreach (var c in root.GetComponentsInChildren<RedeEstacaoInteractable>(true)) c.screen = tela;
        foreach (var c in root.GetComponentsInChildren<UIScreenInteractable>(true)) c.screen = tela;
        foreach (var c in root.GetComponentsInChildren<ImpressoraPainelInteractable>(true)) c.screen = tela;
    }

    // ================================================================== impressora do Matheus + painel

    private static void ConfigurarImpressora(GameObject imp, UIScreen telaCena)
    {
        Transform visual = imp.transform.Find("Visual");
        if (visual == null) { visual = new GameObject("Visual").transform; visual.SetParent(imp.transform, false); }

        // blocos do Ítalo: desativados (não apagados; o setup dele continua achando-os pelo nome)
        foreach (string bloco in new[] { "Corpo", "SaidaPapel", "Bandeja", "Painel" })
        {
            Transform t = visual.Find(bloco);
            if (t != null) t.gameObject.SetActive(false);
        }

        Transform modelo = visual.Find("Modelo");
        if (modelo == null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModeloImpressora));
            go.name = "Modelo";
            go.transform.SetParent(visual, false);
            modelo = go.transform;
        }
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModeloImpressora);
        Quaternion r0 = asset.transform.localRotation;
        Vector3 s0 = asset.transform.localScale;
        // materiais: os ORIGINAIS do modelo (remapeados no importador); nenhuma troca na instância
        GarantirMaterialOriginalImpressora();
        foreach (Renderer r in modelo.GetComponentsInChildren<Renderer>(true)) RevertMateriais(r);

        // orientação: suporte de papel (parte alta) para trás (-Z); escala pela profundidade
        modelo.localPosition = Vector3.zero;
        modelo.localRotation = r0;
        modelo.localScale = s0;
        List<Vector3> v = Vertices(imp.transform, modelo);
        Bounds b = Caixa(v);
        bool longoEmX = b.size.x >= b.size.z;
        float alto = b.min.y + b.size.y * 0.75f;
        var altos = v.Where(p => p.y > alto).ToList();
        float lado = altos.Count > 0 ? (longoEmX ? altos.Average(p => p.z) - b.center.z : altos.Average(p => p.x) - b.center.x) : 0f;
        float yaw = longoEmX ? (lado > 0f ? 180f : 0f) : (lado > 0f ? 90f : -90f);
        modelo.localRotation = Quaternion.Euler(0f, yaw, 0f) * r0;
        b = Caixa(Vertices(imp.transform, modelo));
        float k = ProfundidadeImpressora / Mathf.Max(0.0001f, b.size.z);
        modelo.localScale = s0 * k;
        b = Caixa(Vertices(imp.transform, modelo));
        modelo.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
        b = Caixa(Vertices(imp.transform, modelo));

        // colliders: o do modelo (mira e apoio); a raiz não tem Rigidbody (a impressora não é pegável)
        MeshFilter mfModelo = modelo.GetComponentInChildren<MeshFilter>(true);
        var mc = Garantir<MeshCollider>(mfModelo);
        mc.sharedMesh = mfModelo.sharedMesh;
        Physics.SyncTransforms();

        float ladoPainel = LadoDoPainel(imp.transform, modelo, out b);

        // painel
        Transform painel = imp.transform.Find("PainelControle");
        if (painel == null) { painel = new GameObject("PainelControle").transform; painel.SetParent(imp.transform, false); }
        var (meshPainel, matsPainel) = ItemFbx("Item_PainelImpressora");
        Bounds pb = meshPainel.bounds;

        PosicionarPainelNaFrente(imp.transform, mc, painel, pb, ladoPainel, b);

        Transform pv = painel.Find("Visual");
        if (pv == null) { pv = new GameObject("Visual").transform; pv.SetParent(painel, false); }
        pv.localPosition = Vector3.zero; pv.localRotation = Quaternion.identity; pv.localScale = Vector3.one;
        (Garantir<MeshFilter>(pv)).sharedMesh = meshPainel;
        (Garantir<MeshRenderer>(pv)).sharedMaterials = matsPainel;

        var box = Garantir<BoxCollider>(painel);
        box.center = pb.center + Vector3.up * 0.004f;
        box.size = pb.size + new Vector3(0.004f, 0.008f, 0.004f);

        var inter = Garantir<ImpressoraPainelInteractable>(painel);
        inter.interactionMessage = "Configurar o IP";
        inter.screen = telaCena;

        // visor ao vivo sobre o LCD (submalha do material do visor, M_PC_Porta)
        Bounds lcd = pb;
        for (int i = 0; i < matsPainel.Length && i < meshPainel.subMeshCount; i++)
            if (matsPainel[i] != null && matsPainel[i].name == "M_PC_Porta") lcd = meshPainel.GetSubMesh(i).bounds;
        var visor3d = Garantir<ImpressoraPainel3D>(painel);
        Transform visor = painel.Find("Visor");
        if (visor != null) Object.DestroyImmediate(visor.gameObject);
        var canvasGo = new GameObject("Visor", typeof(RectTransform), typeof(Canvas));
        canvasGo.transform.SetParent(painel, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var crt = (RectTransform)canvasGo.transform;
        const float px = 600f;
        crt.sizeDelta = new Vector2(px, px * lcd.size.z / Mathf.Max(0.001f, lcd.size.x));
        float esc = lcd.size.x * 0.92f / px;
        crt.localScale = new Vector3(esc, esc, esc);
        crt.localPosition = new Vector3(lcd.center.x, lcd.max.y + 0.0006f, lcd.center.z);
        crt.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.back);   // lido de quem está na frente (+Z)
        Text status = TextoVisor(crt, "Status", 64, new Vector2(0f, 0.5f), new Vector2(1f, 1f));
        Text ip = TextoVisor(crt, "Ip", 54, new Vector2(0f, 0f), new Vector2(1f, 0.5f));
        var so = new SerializedObject(visor3d);
        so.FindProperty("statusText").objectReferenceValue = status;
        so.FindProperty("ipText").objectReferenceValue = ip;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Lado (em X, -1 ou +1) do painel: o oposto à saída de papel (a parte fina e baixa numa das pontas).
    /// <paramref name="b"/> = limites do modelo no espaço da impressora.
    /// </summary>
    internal static float LadoDoPainel(Transform imp, Transform modelo, out Bounds b)
    {
        List<Vector3> vv = Vertices(imp, modelo);
        b = Caixa(vv);
        float xMin = b.min.x, xMax = b.max.x, faixa = b.size.x * 0.1f;
        float altoEsq = vv.Where(p => p.x < xMin + faixa).Select(p => p.y).DefaultIfEmpty(0f).Max();
        float altoDir = vv.Where(p => p.x > xMax - faixa).Select(p => p.y).DefaultIfEmpty(0f).Max();
        return altoDir < altoEsq ? -1f : 1f;
    }

    /// <summary>
    /// Remapeia o material do impressora.fbx ("Material.001") para o material original do Matheus (M_Objetos_Parede,
    /// textura officepapers.png). Não cria material novo.
    /// </summary>
    internal static void GarantirMaterialOriginalImpressora()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModeloImpressora);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatOriginalImpressora);
        if (importer == null || mat == null) return;
        var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), "Material.001");
        if (importer.GetExternalObjectMap().TryGetValue(id, out Object atual) && atual == mat) return;
        importer.AddRemap(id, mat);
        importer.SaveAndReimport();
    }

    /// <summary>Desfaz uma troca de materiais feita na instância (volta aos do modelo/importador).</summary>
    internal static void RevertMateriais(Renderer r)
    {
        if (!PrefabUtility.IsPartOfPrefabInstance(r)) return;
        SerializedProperty prop = new SerializedObject(r).FindProperty("m_Materials");
        if (prop.prefabOverride) PrefabUtility.RevertPropertyOverride(prop, InteractionMode.AutomatedAction);
    }

    /// <summary>
    /// Painel na FACE DA FRENTE do corpo da impressora (logo abaixo da borda do topo, onde fica o painel de uma impressora
    /// comum), do lado oposto à saída de papel, inclinado <see cref="InclinacaoPainel"/>° para cima. Fora do topo (papel,
    /// tampa) e da bandeja. Posição achada por raios no MeshCollider do modelo (geometria real).
    /// </summary>
    internal static void PosicionarPainelNaFrente(Transform imp, MeshCollider mc, Transform painel, Bounds pb, float ladoPainel, Bounds b)
    {
        Physics.SyncTransforms();
        float x = ladoPainel * b.extents.x * 0.45f;
        float yTopo = b.min.y + b.size.y * 0.45f;
        Vector3 de = imp.TransformPoint(new Vector3(x, b.max.y + 0.3f, b.max.z - 0.008f));
        if (mc.Raycast(new Ray(de, -imp.up), out RaycastHit topo, b.size.y + 1f)) yTopo = imp.InverseTransformPoint(topo.point).y;

        float yc = yTopo - pb.size.z * 0.5f - 0.012f;           // em pé, o painel ocupa a "profundidade" dele
        Vector3 ponto = new Vector3(x, yc, b.max.z), normal = Vector3.forward;
        Vector3 deFrente = imp.TransformPoint(new Vector3(x, yc, b.max.z + 0.5f));
        if (mc.Raycast(new Ray(deFrente, -imp.forward), out RaycastHit frente, 1f))
        {
            ponto = imp.InverseTransformPoint(frente.point);
            normal = imp.InverseTransformDirection(frente.normal).normalized;
        }
        Vector3 face = (normal + Vector3.up * Mathf.Tan(InclinacaoPainel * Mathf.Deg2Rad)).normalized;
        Vector3 subida = Vector3.ProjectOnPlane(Vector3.up, face).normalized;
        painel.localRotation = Quaternion.LookRotation(-subida, face);     // +Y (face do painel) para fora; topo do visor para cima
        float afasta = Mathf.Sin(InclinacaoPainel * Mathf.Deg2Rad) * pb.extents.z + 0.002f;
        painel.localPosition = ponto + normal * afasta - painel.localRotation * new Vector3(0f, pb.min.y, 0f);
        painel.localScale = Vector3.one;
    }

    private static Text TextoVisor(RectTransform pai, string nome, int tamanho, Vector2 min, Vector2 max)
    {
        var go = new GameObject(nome, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(pai, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = new Vector2(16f, 0f); rt.offsetMax = new Vector2(-16f, 0f);
        Text t = go.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = tamanho;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleLeft;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.color = new Color(0.55f, 0.95f, 0.6f);
        t.raycastTarget = false;
        t.text = nome == "Ip" ? "IP ---.---.---.---" : "SEM TONER";
        return t;
    }

    // ================================================================== validação

    private static void Validar()
    {
        var problemas = new List<string>();
        foreach (string nome in new[] { "Notebook_Estacao", "Impressora_Estacao", "Caixa_CaboRede", "RackInternet" })
        {
            GameObject go = Achar(nome);
            if (go == null) { problemas.Add(nome + " sumiu da cena"); continue; }
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(go)) problemas.Add(nome + " não é instância de prefab");
            foreach (var c in go.GetComponentsInChildren<RedeEstacaoInteractable>(true)) if (c.screen == null) problemas.Add(nome + ": tela vazia");
            foreach (var c in go.GetComponentsInChildren<UIScreenInteractable>(true)) if (c.screen == null) problemas.Add(nome + ": tela (dir.) vazia");
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + nome + ".prefab");
            if (asset == null) problemas.Add(nome + ".prefab não existe");
            else foreach (var c in asset.GetComponentsInChildren<RedeEstacaoInteractable>(true))
                if (c.screen == null || c.screen.gameObject.scene.IsValid()) problemas.Add(nome + ".prefab: tela não é prefab");
        }
        GameObject imp = Achar("Impressora_Estacao");
        if (imp != null)
        {
            var painel = imp.GetComponentInChildren<ImpressoraPainel3D>(true);
            if (painel == null) problemas.Add("impressora sem painel");
            Bounds b = Limites(imp);
            Debug.Log($"MinigamesEstacoesSetup: impressora {b.size:F3} m; painel em {(painel != null ? painel.transform.localPosition : Vector3.zero):F3}.");
        }
        foreach (string nome in new[] { "Ssd_Pickup", "Alicate_Pickup" })
            if (Achar(nome) == null) problemas.Add(nome + " não está na cena");
        if (problemas.Count > 0) Debug.LogError("MinigamesEstacoesSetup: " + string.Join("; ", problemas));
        else Debug.Log("MinigamesEstacoesSetup: validação OK (estações, telas, painel, itens).");
    }

    // ================================================================== utilidades

    internal static List<Vector3> Vertices(Transform espaco, Transform modelo)
    {
        var lista = new List<Vector3>();
        foreach (MeshFilter f in modelo.GetComponentsInChildren<MeshFilter>(true))
        {
            if (f.sharedMesh == null) continue;
            foreach (Vector3 p in f.sharedMesh.vertices) lista.Add(espaco.InverseTransformPoint(f.transform.TransformPoint(p)));
        }
        return lista;
    }

    internal static Bounds Caixa(List<Vector3> v)
    {
        var b = new Bounds(v[0], Vector3.zero);
        foreach (Vector3 p in v) b.Encapsulate(p);
        return b;
    }

    internal static Bounds Limites(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    private static Bounds LimitesLocais(Transform espaco, GameObject go)
    {
        var v = new List<Vector3>();
        foreach (MeshFilter f in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (f.sharedMesh == null || f.GetComponent<Renderer>() == null || !f.GetComponent<Renderer>().enabled) continue;
            Bounds mb = f.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                v.Add(espaco.InverseTransformPoint(f.transform.TransformPoint(c)));
            }
        }
        return v.Count > 0 ? Caixa(v) : new Bounds(Vector3.zero, Vector3.one * 0.1f);
    }

    /// <summary>Componente do objeto, criando se faltar (sem "??": no Editor o GetComponent vazio não é null de verdade).</summary>
    internal static T Garantir<T>(Component c) where T : Component => Garantir<T>(c.gameObject);

    internal static T Garantir<T>(GameObject g) where T : Component
    {
        T c = g.GetComponent<T>();
        return c != null ? c : g.AddComponent<T>();
    }

    internal static GameObject Achar(string nome)
    {
        foreach (GameObject raiz in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            Transform t = AcharRec(raiz.transform, nome);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    private static Transform AcharRec(Transform t, string nome)
    {
        if (t.name == nome) return t;
        foreach (Transform filho in t)
        {
            Transform r = AcharRec(filho, nome);
            if (r != null) return r;
        }
        return null;
    }
}
