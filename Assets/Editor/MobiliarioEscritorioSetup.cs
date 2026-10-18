using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Etapa 1 do escritório real (cena <c>Office_Placeholder</c>, montada pelo Matheus): estações de trabalho nas
/// divisórias, sala de TI (bancada de manutenção, armário, estante de estoque, gaveteiros) e bancada das impressoras.
///
/// Prefabs criados (só se ainda não existirem, para não sobrescrever ajustes manuais):
///   Props/Bancada_Manutencao, Props/Armario_TI, Props/Estante_Estoque, Props/Bancada_Impressoras
///     = raiz lógica (BoxCollider(s), WorkSurface na bancada de manutenção) + filho Visual (.fbx de
///       Tools/blender/mobiliario_escritorio_build.py) com os materiais do Matheus por slot.
///   Gameplay/Estacao_Notebook = variante do Estacao_Trabalho sem o PC_Escritorio, com o Notebook_Estacao (visual toon).
///
/// Cena: só ACRESCENTA objetos nos grupos <c>Estacoes_Trabalho</c>, <c>Sala_TI</c> e <c>Area_Impressoras</c>
/// (recriados a cada execução). O que o Matheus montou não é apagado nem movido, exceto a altura da
/// Estacao_Direita_Problema, que flutuava (ver relatório). Posições derivadas das divisas e paredes reais da cena.
///
/// Menu: Tools > E-stagiar.io > Escritório: mobiliário (etapa 1). Batch: -executeMethod MobiliarioEscritorioSetup.Run
/// Relatório: Logs/MobiliarioEscritorio.txt (+ mapa de colisão Logs/MobiliarioEscritorio_mapa.png).
/// </summary>
public static class MobiliarioEscritorioSetup
{
    private const string Cena = "Assets/Scenes/Office_Placeholder.unity";
    private const string Models = "Assets/Art/Models/";
    private const string Mats = "Assets/Art/Materials/";
    private const string Props = "Assets/Prefabs/Props/";
    private const string Gameplay = "Assets/Prefabs/Gameplay/";
    private const string Relatorio = "Logs/MobiliarioEscritorio.txt";

    // slots dos .fbx -> materiais do Matheus (mesmos dos gaveteiros e da mesa)
    private static readonly Dictionary<string, string> Slot = new Dictionary<string, string>
    {
        { "Claro", "M_Escritorio_Claro" }, { "Escuro", "M_Escritorio_Escuro" },
        { "Metal", "M_PC_Chapa" }, { "Tapete", "M_Impressora_Escuro" },
    };

    // medidas (m) iguais às do script do Blender; frente em +Z local, origem no chão
    private const float BancadaTopo = 0.90f, BancadaW = 2.0f, BancadaD = 0.8f;
    private static readonly float[] EstanteTopos = { 0.125f, 0.925f, 1.525f, 2.005f };
    private const float ImpTopo = 0.78f, ImpNicho = 0.425f;

    private static readonly List<string> log = new List<string>();

    [MenuItem("Tools/E-stagiar.io/Escritório: mobiliário (etapa 1)")]
    public static void Menu() => Executar();

    public static void Run()
    {
        Executar();
        EditorApplication.Exit(0);
    }

    private static void Executar()
    {
        log.Clear();
        AssetDatabase.Refresh();
        GameObject bancada = PrefabMovel("Bancada_Manutencao", "bancada_manutencao", ColBancada, true);
        GameObject armario = PrefabMovel("Armario_TI", "armario_ti", ColArmario, false);
        GameObject estante = PrefabMovel("Estante_Estoque", "estante_estoque", ColEstante, false);
        GameObject impBancada = PrefabMovel("Bancada_Impressoras", "bancada_impressoras", ColImpressoras, false);

        var scene = EditorSceneManager.OpenScene(Cena);
        GameObject notebookEst = PrefabEstacaoNotebook(scene);

        foreach (string g in new[] { "Estacoes_Trabalho", "Sala_TI", "Area_Impressoras" })
        {
            var old = scene.GetRootGameObjects().FirstOrDefault(r => r.name == g);
            if (old != null) Object.DestroyImmediate(old);
        }
        Physics.SyncTransforms();

        Estacoes(scene, notebookEst);
        SalaTI(scene, bancada, armario, estante);
        AreaImpressoras(scene, impBancada);
        Physics.SyncTransforms();
        Validar(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Relatorio, string.Join("\n", log));
        Debug.Log("[Mobiliário] " + string.Join(" · ", log));
    }

    // ------------------------------------------------------------------ prefabs dos móveis

    private static GameObject PrefabMovel(string nome, string fbx, System.Action<GameObject> colisores, bool workSurface)
    {
        string path = Props + nome + ".prefab";
        var existente = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existente != null) { log.Add($"{nome}: prefab já existia (mantido)"); return existente; }

        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(Models + fbx + ".fbx");
        if (modelo == null) { log.Add($"FALTA {Models}{fbx}.fbx (rodar o script do Blender)"); return null; }
        var root = new GameObject(nome);
        var vis = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
        vis.name = "Visual";
        vis.transform.SetParent(root.transform, false);
        foreach (var r in vis.GetComponentsInChildren<MeshRenderer>())
        {
            var ms = r.sharedMaterials;
            for (int i = 0; i < ms.Length; i++)
            {
                string slot = ms[i] != null ? ms[i].name : "";
                if (Slot.TryGetValue(slot, out string mat))
                    ms[i] = AssetDatabase.LoadAssetAtPath<Material>(Mats + mat + ".mat");
                else log.Add($"{nome}: slot '{slot}' sem material");
            }
            r.sharedMaterials = ms;
        }
        colisores(root);
        if (workSurface) root.AddComponent<WorkSurface>();
        var pref = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        log.Add($"{nome}: prefab criado");
        return pref;
    }

    private static BoxCollider Box(GameObject go, Vector3 c, Vector3 s)
    {
        var b = go.AddComponent<BoxCollider>();
        b.center = c; b.size = s;
        return b;
    }

    private static GameObject Filho(GameObject root, string nome)
    {
        var t = root.transform.Find(nome);
        if (t != null) return t.gameObject;
        var go = new GameObject(nome);
        go.transform.SetParent(root.transform, false);
        return go;
    }

    // bancada: volume até o tampo (superfície de trabalho) + painel e prateleira alta nos fundos (-Z)
    private static void ColBancada(GameObject r)
    {
        Box(r, new Vector3(0, BancadaTopo / 2, 0), new Vector3(BancadaW, BancadaTopo, BancadaD));
        var c = Filho(r, "Colisores");
        Box(c, new Vector3(0, 1.31f, -0.385f), new Vector3(BancadaW, 0.82f, 0.03f));
        Box(c, new Vector3(0, 1.6325f, -0.26f), new Vector3(BancadaW, 0.025f, 0.28f));
    }

    private static void ColArmario(GameObject r) => Box(r, new Vector3(0, 0.975f, 0), new Vector3(1.0f, 1.95f, 0.5f));

    // estante: um colisor por tampo (itens apoiam em cada nível) + colunas
    private static void ColEstante(GameObject r)
    {
        Box(r, new Vector3(0, EstanteTopos[0] - 0.0125f, 0), new Vector3(1.8f, 0.025f, 0.5f));
        var c = Filho(r, "Colisores");
        for (int i = 1; i < EstanteTopos.Length; i++)
            Box(c, new Vector3(0, EstanteTopos[i] - 0.0125f, 0), new Vector3(1.8f, 0.025f, 0.5f));
        foreach (float x in new[] { -0.88f, 0.88f })
            foreach (float z in new[] { -0.23f, 0.23f })
                Box(c, new Vector3(x, 1.015f, z), new Vector3(0.04f, 2.03f, 0.04f));
    }

    // bancada das impressoras: módulos laterais cheios, nicho central aberto (resmas) com prateleira
    private static void ColImpressoras(GameObject r)
    {
        Box(r, new Vector3(-0.535f, ImpTopo / 2, 0), new Vector3(0.53f, ImpTopo, 0.6f));
        var c = Filho(r, "Colisores");
        Box(c, new Vector3(0.535f, ImpTopo / 2, 0), new Vector3(0.53f, ImpTopo, 0.6f));
        Box(c, new Vector3(0, ImpTopo - 0.015f, 0), new Vector3(0.54f, 0.03f, 0.6f));
        Box(c, new Vector3(0, 0.0475f, 0), new Vector3(0.54f, 0.095f, 0.6f));
        Box(c, new Vector3(0, ImpNicho - 0.0125f, 0.0125f), new Vector3(0.54f, 0.025f, 0.575f));
        Box(c, new Vector3(0, ImpTopo / 2, -0.2875f), new Vector3(0.54f, ImpTopo, 0.025f));
    }

    // ------------------------------------------------------------------ estação com notebook

    private static GameObject PrefabEstacaoNotebook(UnityEngine.SceneManagement.Scene scene)
    {
        string path = Gameplay + "Estacao_Notebook.prefab";
        var existente = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existente != null) { log.Add("Estacao_Notebook: prefab já existia (mantido)"); return existente; }

        var baseP = AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + "Estacao_Trabalho.prefab");
        var nbP = AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + "Notebook_Estacao.prefab");
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(baseP, scene);
        inst.transform.SetPositionAndRotation(new Vector3(0, -60f, 0), Quaternion.identity);   // longe de tudo (raycasts)
        inst.name = "Estacao_Notebook";
        Transform pc = inst.transform.Find("PC_Escritorio");
        if (pc != null)
        {
            try { Object.DestroyImmediate(pc.gameObject); log.Add("Estacao_Notebook: PC_Escritorio removido da variante"); }
            catch (System.Exception e) { pc.gameObject.SetActive(false); log.Add("Estacao_Notebook: PC desativado (" + e.Message + ")"); }
        }
        var nb = (GameObject)PrefabUtility.InstantiatePrefab(nbP, scene);
        nb.transform.SetParent(inst.transform, false);
        nb.name = "Notebook_Estacao";
        // mesma pose das estações de notebook da Teste_Integrado (sobre o tampo, um pouco virado para quem senta)
        nb.transform.localPosition = new Vector3(0.19f, 1.003f, -0.139f);
        nb.transform.localRotation = Quaternion.Euler(0, 342f, 0);
        Physics.SyncTransforms();
        typeof(TesteIntegradoSetup).GetMethod("ToonNotebookVisual", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Invoke(null, new object[] { nb, inst.transform.Find("Cadeira") });
        var pref = PrefabUtility.SaveAsPrefabAsset(inst, path);
        Object.DestroyImmediate(inst);
        log.Add("Estacao_Notebook: variante do Estacao_Trabalho criada");
        return pref;
    }

    // ------------------------------------------------------------------ estações nas divisórias

    private static void Estacoes(UnityEngine.SceneManagement.Scene scene, GameObject notebookEst)
    {
        Transform dA = Achar("Escritorio/divisa"), dB = Achar("Escritorio/divisa (1)");
        GameObject sBgo = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Estacao_Direita_Problema");
        if (dA == null || dB == null || sBgo == null) { log.Add("FALTA divisa, divisa (1) ou Estacao_Direita_Problema na cena"); return; }
        Transform sB = sBgo.transform;

        // altura: a estação do Matheus estava 12 cm acima do piso (cadeira flutuando)
        float pisoB = Piso(sB.position, sB);
        if (Mathf.Abs(sB.position.y - pisoB) > 0.01f)
        {
            log.Add($"Estacao_Direita_Problema: y {sB.position.y:0.000} -> {pisoB:0.000} (cadeira flutuava; mesa no piso como nas outras cenas)");
            Undo.RecordObject(sB, "piso");
            sB.position = new Vector3(sB.position.x, pisoB, sB.position.z);
        }

        Vector3 cA = Centro(dA), cB = Centro(dB);
        var grupo = new GameObject("Estacoes_Trabalho");
        var divisas = new GameObject("Divisas_Novas");
        divisas.transform.SetParent(grupo.transform, false);

        var esq = AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + "Estacao_Esquerda_Funcional.prefab");
        var dir = AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + "Estacao_Direita_Funcional.prefab");

        // A: cubículo vazio da "divisa" (L espelhado do da "divisa (1)") -> desktop com gabinete do lado da parede
        Vector3 offB = sB.position - cB;
        Estacao(grupo, esq, "Estacao_A_Desktop", new Vector3(cA.x - offB.x, 0, cA.z + offB.z), sB.rotation);

        // E: novo cubículo igual ao da "divisa (1)", 7,4 m a leste (mesmo corredor de 3,4 m entre cubículos)
        Vector3 dx = new Vector3(7.4f, 0, 0);
        Divisa(divisas, dB, "Divisa_E", dB.position + dx, dB.rotation);
        Estacao(grupo, dir, "Estacao_E_Desktop", sB.position + dx, sB.rotation);

        // fileira de baixo (contra a parede sul, z ~ -7,7): cubículos girados 180°, abertos para o corredor central
        const float zFileira2 = -5.5f;
        Quaternion meiaVolta = Quaternion.Euler(0, 180, 0);
        Vector3 cC = new Vector3(cA.x, 0, zFileira2), cD = new Vector3(cB.x + 5.18f, 0, zFileira2);
        Vector3 sA = new Vector3(cA.x - offB.x, sB.position.y, cA.z + offB.z);
        Divisa(divisas, dA, "Divisa_C", Girar(dA.position, cA, cC), meiaVolta * dA.rotation);
        Estacao(grupo, notebookEst, "Estacao_C_Notebook", Girar(sA, cA, cC), meiaVolta * sB.rotation);
        Divisa(divisas, dB, "Divisa_D", Girar(dB.position, cB, cD), meiaVolta * dB.rotation);
        Estacao(grupo, notebookEst, "Estacao_D_Notebook", Girar(sB.position, cB, cD), meiaVolta * sB.rotation);
    }

    private static Vector3 Girar(Vector3 p, Vector3 de, Vector3 para)
    {
        Vector3 d = p - new Vector3(de.x, p.y, de.z);
        return new Vector3(para.x - d.x, p.y, para.z - d.z);
    }

    private static void Divisa(GameObject pai, Transform modelo, string nome, Vector3 pos, Quaternion rot)
    {
        var src = PrefabUtility.GetCorrespondingObjectFromSource(modelo.gameObject);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(src, pai.scene);
        go.name = nome;
        go.transform.SetParent(pai.transform, false);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = modelo.lossyScale;        // mesma escala final das divisas do Matheus
        // mesmos componentes da divisa original (MeshCollider) e mesmos materiais
        var mcSrc = modelo.GetComponent<MeshCollider>();
        if (mcSrc != null && go.GetComponent<MeshCollider>() == null)
            go.AddComponent<MeshCollider>().sharedMesh = mcSrc.sharedMesh;
        var rSrc = modelo.GetComponentInChildren<Renderer>(); var r = go.GetComponentInChildren<Renderer>();
        if (rSrc != null && r != null) r.sharedMaterials = rSrc.sharedMaterials;
        log.Add($"{nome}: divisa em ({pos.x:0.00}; {pos.z:0.00})");
    }

    private static void Estacao(GameObject grupo, GameObject prefab, string nome, Vector3 pos, Quaternion rot)
    {
        if (prefab == null) { log.Add("FALTA prefab para " + nome); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, grupo.scene);
        go.name = nome;
        go.transform.SetParent(grupo.transform, false);
        go.transform.SetPositionAndRotation(pos, rot);
        Physics.SyncTransforms();
        go.transform.position = new Vector3(pos.x, Piso(pos, go.transform), pos.z);
        EncostarSemInvadir(go.transform);
        LigarTelasDaCena(go);
        log.Add($"{nome}: {prefab.name} em ({go.transform.position.x:0.00}; {go.transform.position.y:0.00}; {go.transform.position.z:0.00}) rot {go.transform.eulerAngles.y:0}");
    }

    // Se a mesa invade divisa/parede, recua pelo vetor de penetração (+1 cm).
    private static void EncostarSemInvadir(Transform est)
    {
        var mesa = est.Find("Mesa")?.GetComponent<BoxCollider>();
        if (mesa == null) return;
        for (int it = 0; it < 4; it++)
        {
            Physics.SyncTransforms();
            Vector3 total = Vector3.zero;
            foreach (var c in Physics.OverlapBox(mesa.bounds.center, mesa.bounds.extents, est.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c.transform.IsChildOf(est) || c.attachedRigidbody != null) continue;
                if (Physics.ComputePenetration(mesa, mesa.transform.position, mesa.transform.rotation,
                        c, c.transform.position, c.transform.rotation, out Vector3 d, out float dist) && dist > 0.001f)
                {
                    d.y = 0; total += d.normalized * (dist + 0.01f);
                    log.Add($"{est.name}: mesa encostava em {Caminho(c.transform)} ({dist * 100:0.0} cm), recuada");
                }
            }
            if (total == Vector3.zero) return;
            est.position += total;
        }
    }

    // ------------------------------------------------------------------ sala de TI

    private static void SalaTI(UnityEngine.SceneManagement.Scene scene, GameObject bancada, GameObject armario, GameObject estante)
    {
        // a sala com o monitor no chão (DECORAÇÃO/decoração paredes/Visual (1)): limites pelas paredes reais
        Vector3 centro = new Vector3(35.3f, 1.0f, 14.6f);
        float xW = Parede(centro, Vector3.left), xE = Parede(centro, Vector3.right);
        float zN = Parede(centro, Vector3.forward);
        // parede sul: o raio do centro sai pela porta; vale a face mais ao norte entre amostras ao longo da sala
        float zS = float.NegativeInfinity;
        for (float x = xW + 0.5f; x < xE - 0.4f; x += 0.5f)
            if (Physics.Raycast(new Vector3(x, 1.0f, centro.z), Vector3.back, out RaycastHit hs, 4f, ~0, QueryTriggerInteraction.Ignore))
                zS = Mathf.Max(zS, hs.point.z);
        if (float.IsNegativeInfinity(zS)) zS = centro.z - 2.3f;
        log.Add($"Sala de TI: interior x {xW:0.00}..{xE:0.00}, z {zS:0.00}..{zN:0.00} ({xE - xW:0.0} x {zN - zS:0.0} m)");
        // porta: vão da parede sul
        float porta0 = float.NaN, porta1 = float.NaN;
        for (float x = xW + 0.2f; x < xE - 0.2f; x += 0.05f)
        {
            bool livre = !Physics.Raycast(new Vector3(x, 1.0f, zS + 0.3f), Vector3.back, 0.8f, ~0, QueryTriggerInteraction.Ignore);
            if (livre && float.IsNaN(porta0)) porta0 = x;
            if (livre) porta1 = x;
        }
        log.Add($"Sala de TI: porta na parede sul, x {porta0:0.00}..{porta1:0.00}");
        float y0 = Piso(centro, null);

        var grupo = new GameObject("Sala_TI");
        var moveis = Filho(grupo, "Moveis"); var estoque = Filho(grupo, "Estoque"); var bancadaItens = Filho(grupo, "Bancada_Itens");
        Quaternion paraSul = Quaternion.Euler(0, 180, 0), paraOeste = Quaternion.Euler(0, -90, 0), paraLeste = Quaternion.Euler(0, 90, 0);

        // parede norte: bancada de manutenção no eixo da porta, gaveteiros dos dois lados
        float xBanc = float.IsNaN(porta0) ? 35.9f : (porta0 + porta1) / 2f;
        var banc = Instanciar(bancada, moveis, "Bancada_Manutencao", new Vector3(xBanc, y0, zN - BancadaD / 2 - 0.01f), paraSul);
        float zGav = zN - 0.28f - 0.01f;
        Instanciar(Gp("Gaveteiro_Hardware"), moveis, "Gaveteiro_Hardware", new Vector3(xBanc - BancadaW / 2 - 0.40f, y0, zGav), paraSul);
        Instanciar(Gp("Gaveteiro_Cabos"), moveis, "Gaveteiro_Cabos", new Vector3(xBanc + BancadaW / 2 + 0.40f, y0, zGav), paraSul);
        Instanciar(Gp("Gaveteiro_Suprimentos"), moveis, "Gaveteiro_Suprimentos", new Vector3(xBanc + BancadaW / 2 + 1.15f, y0, zGav), paraSul);

        // parede leste: estante de estoque; parede oeste: armário fechado
        var est = Instanciar(estante, moveis, "Estante_Estoque", new Vector3(xE - 0.275f, y0, (zS + zN) / 2f + 0.1f), paraOeste);
        Instanciar(armario, moveis, "Armario_TI", new Vector3(xW + 0.27f, y0, zN - 0.95f), paraLeste);

        // estoque na estante (frente da estante = corredor): monitores embaixo, teclados e caixas em cima
        if (est != null)
        {
            Transform e = est.transform;
            var mon = AssetDatabase.LoadAssetAtPath<GameObject>(Props + "OF_Monitor.prefab");
            var tec = AssetDatabase.LoadAssetAtPath<GameObject>(Props + "OF_Teclado.prefab");
            var caixa = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "caixa.fbx");
            Instanciar(mon, estoque, "Monitor_Estoque_1", e.TransformPoint(-0.42f, EstanteTopos[0], 0.02f), e.rotation);
            Instanciar(mon, estoque, "Monitor_Estoque_2", e.TransformPoint(0.42f, EstanteTopos[0], 0.02f), e.rotation);
            Instanciar(tec, estoque, "Teclado_Estoque_1", e.TransformPoint(-0.45f, EstanteTopos[1], 0), e.rotation);
            Instanciar(tec, estoque, "Teclado_Estoque_2", e.TransformPoint(-0.45f, EstanteTopos[1] + 0.055f, 0), e.rotation * Quaternion.Euler(0, 4, 0));
            Caixa(caixa, estoque, "Caixa_Estoque_1", e, new Vector3(0.45f, EstanteTopos[1], 0), new Vector3(0.50f, 0.36f, 0.40f));
            Caixa(caixa, estoque, "Caixa_Estoque_2", e, new Vector3(-0.45f, EstanteTopos[2], 0), new Vector3(0.46f, 0.32f, 0.40f));
            Caixa(caixa, estoque, "Caixa_Estoque_3", e, new Vector3(0.42f, EstanteTopos[2], 0), new Vector3(0.40f, 0.40f, 0.40f));
            Caixa(caixa, estoque, "Caixa_Estoque_4", e, new Vector3(0f, EstanteTopos[3], 0), new Vector3(0.55f, 0.30f, 0.42f));
        }

        // gabinetes de reserva no chão (canto sudoeste, perto do monitor que o Matheus deixou no chão)
        var gab = Gp("Gabinete");
        Instanciar(gab, estoque, "Gabinete_Reserva_1", new Vector3(xW + 0.40f, y0 + 0.262f, zS + 0.35f), paraLeste);
        Instanciar(gab, estoque, "Gabinete_Reserva_2", new Vector3(xW + 0.40f, y0 + 0.262f, zS + 0.72f), paraLeste);

        // bancada: um gabinete pronto para manutenção, ferramentas e cabos
        if (banc != null)
        {
            Transform b = banc.transform; float topo = BancadaTopo + 0.005f;
            Instanciar(gab, bancadaItens, "Gabinete_Bancada", b.TransformPoint(-0.35f, topo + 0.262f, 0.05f), b.rotation * Quaternion.Euler(0, 90, 0));
            Instanciar(Gp("Pickup_Ferramenta"), bancadaItens, "ChaveFenda_Bancada", b.TransformPoint(0.15f, topo, 0.22f), b.rotation * Quaternion.Euler(0, 15, 0));
            Instanciar(Gp("Pickup_Alicate"), bancadaItens, "Alicate_Bancada", b.TransformPoint(0.35f, topo, 0.20f), b.rotation * Quaternion.Euler(0, -10, 0));
            Instanciar(Gp("Pickup_CaboRede"), bancadaItens, "CaboRede_Bancada", b.TransformPoint(0.30f, topo, -0.05f), b.rotation);
            Instanciar(Gp("Pickup_CaboEnergia"), bancadaItens, "CaboEnergia_Bancada", b.TransformPoint(0.55f, topo, -0.05f), b.rotation);
        }
    }

    // ------------------------------------------------------------------ impressoras

    private static void AreaImpressoras(UnityEngine.SceneManagement.Scene scene, GameObject impBancada)
    {
        // fora da sala de TI, encostada na face sul da parede dela (lado do salão), a oeste do pôster e da porta
        float zParede = Parede(new Vector3(31.8f, 1.0f, 10.0f), Vector3.forward);
        float y0 = Piso(new Vector3(31.8f, 0.5f, 11.4f), null);
        var grupo = new GameObject("Area_Impressoras");
        var b = Instanciar(impBancada, grupo, "Bancada_Impressoras", new Vector3(31.8f, y0, zParede - 0.30f - 0.01f), Quaternion.Euler(0, 180, 0));
        if (b == null) return;
        Transform t = b.transform;
        Instanciar(Gp("Impressora_Estacao"), grupo, "Impressora_Estacao", t.TransformPoint(-0.40f, ImpTopo + 0.002f, 0.05f), t.rotation);
        Instanciar(Gp("Pickup_Toner"), grupo, "Toner_Reserva", t.TransformPoint(0.45f, ImpTopo + 0.002f, 0.08f), t.rotation);
        Instanciar(Gp("Pickup_ResmaPapel"), grupo, "Resma_Nicho_1", t.TransformPoint(0f, ImpNicho + 0.002f, 0.02f), t.rotation * Quaternion.Euler(0, 90, 0));
        Instanciar(Gp("Pickup_ResmaPapel"), grupo, "Resma_Nicho_2", t.TransformPoint(0f, 0.095f + 0.002f, 0.02f), t.rotation * Quaternion.Euler(0, 90, 0));
        log.Add($"Bancada_Impressoras em ({b.transform.position.x:0.00}; {b.transform.position.z:0.00}), parede da sala de TI em z {zParede:0.00}");
    }

    // ------------------------------------------------------------------ utilitários

    private static GameObject Gp(string nome) => AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + nome + ".prefab");

    private static GameObject Instanciar(GameObject prefab, GameObject pai, string nome, Vector3 pos, Quaternion rot)
    {
        if (prefab == null) { log.Add("FALTA prefab para " + nome); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pai.scene);
        go.name = nome;
        go.transform.SetParent(pai.transform, false);
        go.transform.SetPositionAndRotation(pos, rot);
        LigarTelasDaCena(go);
        return go;
    }

    // Caixa de papelão do Matheus (caixa.fbx) redimensionada para caber no nível da estante, apoiada no tampo.
    private static void Caixa(GameObject caixa, GameObject pai, string nome, Transform estante, Vector3 local, Vector3 tamanho)
    {
        if (caixa == null) { log.Add("FALTA caixa.fbx"); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(caixa, pai.scene);
        go.name = nome;
        go.transform.SetParent(pai.transform, false);
        go.transform.rotation = estante.rotation;
        go.transform.localScale = Vector3.one;
        go.transform.position = Vector3.zero;
        Bounds b = LocalBounds(go.transform);            // tamanho sem escala, nos eixos da estante
        go.transform.localScale = new Vector3(tamanho.x / b.size.x, tamanho.y / b.size.y, tamanho.z / b.size.z);
        // a estante só gira em múltiplos de 90°: o AABB do mundo serve para apoiar a base no tampo
        var rs = go.GetComponentsInChildren<Renderer>();
        Bounds w = rs[0].bounds; foreach (var r in rs) w.Encapsulate(r.bounds);
        Vector3 alvo = estante.TransformPoint(local);
        go.transform.position += alvo - new Vector3(w.center.x, w.min.y, w.center.z) + Vector3.up * 0.002f;
    }

    // bounds das malhas no espaço local do objeto (sem a escala da raiz)
    private static Bounds LocalBounds(Transform t)
    {
        var b = new Bounds(); bool any = false;
        foreach (var mf in t.GetComponentsInChildren<MeshFilter>())
        {
            var m = mf.sharedMesh; if (m == null) continue;
            var mb = m.bounds;
            for (int i = 0; i < 8; i++)
            {
                var p = new Vector3((i & 1) == 0 ? mb.min.x : mb.max.x, (i & 2) == 0 ? mb.min.y : mb.max.y, (i & 4) == 0 ? mb.min.z : mb.max.z);
                Vector3 w = mf.transform.TransformPoint(p);
                Vector3 l = Quaternion.Inverse(t.rotation) * (w - t.position);
                if (!any) { b = new Bounds(l, Vector3.zero); any = true; } else b.Encapsulate(l);
            }
        }
        return b;
    }

    // Telas 2D: nos prefabs os interativos apontam para o prefab da tela; na cena, para a tela da própria cena.
    private static void LigarTelasDaCena(GameObject go)
    {
        var telas = Object.FindObjectsByType<UIScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(s => s.gameObject.scene == go.scene).ToList();
        foreach (var comp in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (comp == null) continue;
            var f = comp.GetType().GetField("screen");
            if (f == null || f.FieldType != typeof(UIScreen)) continue;
            var atual = (UIScreen)f.GetValue(comp);
            if (atual == null || !EditorUtility.IsPersistent(atual)) continue;
            var daCena = telas.FirstOrDefault(s => PrefabUtility.GetCorrespondingObjectFromSource(s) == atual);
            if (daCena == null) daCena = telas.FirstOrDefault(s => s.name == atual.name);
            if (daCena == null) { log.Add($"{go.name}: tela {atual.name} não existe na cena (fica a do prefab)"); continue; }
            Undo.RecordObject(comp, "tela");
            f.SetValue(comp, daCena);
            PrefabUtility.RecordPrefabInstancePropertyModifications(comp);
        }
    }

    private static Transform Achar(string caminho)
    {
        string[] partes = caminho.Split('/');
        var root = EditorSceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == partes[0]);
        if (root == null) return null;
        Transform t = root.transform;
        for (int i = 1; i < partes.Length && t != null; i++) t = t.Find(partes[i]);
        return t;
    }

    private static Vector3 Centro(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>();
        Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        return new Vector3(b.center.x, 0, b.center.z);
    }

    // Piso visível embaixo do ponto (primeiro colisor abaixo de 0,3 m que não é do próprio objeto).
    private static float Piso(Vector3 p, Transform self)
    {
        Physics.SyncTransforms();
        var hits = Physics.RaycastAll(new Vector3(p.x, 0.6f, p.z), Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        foreach (var h in hits)
            if ((self == null || !h.collider.transform.IsChildOf(self)) && h.point.y < 0.3f && h.point.y > best) best = h.point.y;
        return float.IsNegativeInfinity(best) ? 0f : best;
    }

    // Coordenada da face da parede na direção dada (eixo x ou z), a partir de um ponto interno.
    private static float Parede(Vector3 de, Vector3 dir)
    {
        foreach (float y in new[] { 1.0f, 1.6f, 0.5f })
        {
            var o = new Vector3(de.x, y, de.z);
            if (Physics.Raycast(o, dir, out RaycastHit h, 15f, ~0, QueryTriggerInteraction.Ignore))
                return dir.x != 0 ? h.point.x : h.point.z;
        }
        log.Add($"ATENÇÃO: sem parede na direção {dir} a partir de {de}");
        return dir.x != 0 ? de.x + dir.x * 4 : de.z + dir.z * 4;
    }

    private static string Caminho(Transform t) => t.parent ? Caminho(t.parent) + "/" + t.name : t.name;

    // ------------------------------------------------------------------ validação

    private static void Validar(UnityEngine.SceneManagement.Scene scene)
    {
        var novos = scene.GetRootGameObjects().Where(g => g.name == "Estacoes_Trabalho" || g.name == "Sala_TI" || g.name == "Area_Impressoras").ToList();
        var sB = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Estacao_Direita_Problema");
        if (sB != null) novos.Add(sB);
        int problemas = 0;

        // 1) objetos apoiados: base de cada peça (raiz com renderers) no piso ou numa superfície embaixo
        foreach (var g in novos)
            foreach (Transform t in g.transform)
            {
                if (t.name == "Divisas_Novas") { foreach (Transform u in t) log.Add($"apoio: {u.name} na mesma altura das divisas do Matheus (y {u.position.y:0.00}; a malha desce 25 cm abaixo do piso, como nas originais)"); continue; }
                if (t.name == "Moveis" || t.name == "Estoque" || t.name == "Bancada_Itens") { foreach (Transform u in t) problemas += Apoio(u); continue; }
                problemas += Apoio(t);
            }

        // 2) interpenetração entre colisores sólidos (sem os itens com Rigidbody, que a física acomoda)
        var sol = novos.SelectMany(g => g.GetComponentsInChildren<Collider>()).Where(c => c.enabled && !c.isTrigger && c.attachedRigidbody == null).ToList();
        foreach (var c in sol)
        {
            foreach (var o in Physics.OverlapBox(c.bounds.center, c.bounds.extents * 0.98f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            {
                if (o == c || o.attachedRigidbody != null || Raiz(o.transform) == Raiz(c.transform)) continue;
                if (o.transform.root.name == "Player") continue;
                if (Physics.ComputePenetration(c, c.transform.position, c.transform.rotation, o, o.transform.position, o.transform.rotation, out _, out float d) && d > 0.015f)
                {
                    log.Add($"INVADE: {Caminho(c.transform)} x {Caminho(o.transform)} ({d * 100:0.0} cm)");
                    problemas++;
                }
            }
        }

        // 3) circulação: a cápsula do jogador (r 0,30 m, 1,7 m) chega da posição inicial à frente de cada móvel
        var player = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Player");
        Vector3 ini = player != null ? player.transform.position : new Vector3(16.5f, 0, -0.5f);
        var alvos = new List<(string, Vector3)>();
        foreach (var g in novos)
            foreach (var est in g.GetComponentsInChildren<Transform>().Where(x => x.name.StartsWith("Estacao_") && x.parent == g.transform || x == g.transform && x.name.StartsWith("Estacao_")))
                alvos.Add((est.name + " (cadeira)", est.TransformPoint(0, 0, 1.75f)));
        foreach (var n in new[] { "Bancada_Manutencao", "Gaveteiro_Hardware", "Gaveteiro_Cabos", "Gaveteiro_Suprimentos", "Estante_Estoque", "Armario_TI", "Bancada_Impressoras" })
        {
            var t = novos.SelectMany(g => g.GetComponentsInChildren<Transform>()).FirstOrDefault(x => x.name == n);
            if (t != null) alvos.Add((n + " (frente)", t.TransformPoint(0, 0, n.StartsWith("Bancada_M") ? 1.0f : 0.95f)));
        }
        var mapa = Grade(ini, out float x0, out float z0, out int W, out int H);
        foreach (var (nome, p) in alvos)
        {
            int i = Mathf.FloorToInt((p.x - x0) / Passo), j = Mathf.FloorToInt((p.z - z0) / Passo);
            bool ok = false;
            for (int di = -3; di <= 3 && !ok; di++) for (int dj = -3; dj <= 3 && !ok; dj++)
            {
                int a = i + di, b = j + dj;
                if (a >= 0 && b >= 0 && a < W && b < H && mapa[a, b] == 2) ok = true;
            }
            log.Add($"circulação: {nome} {(ok ? "alcançável" : "SEM ACESSO")}");
            if (!ok) problemas++;
        }
        log.Add($"VALIDAÇÃO: {problemas} problema(s)");
    }

    private static Transform Raiz(Transform t)
    {
        while (t.parent != null && t.parent.name != "Estacoes_Trabalho" && t.parent.name != "Moveis" && t.parent.name != "Estoque"
               && t.parent.name != "Bancada_Itens" && t.parent.name != "Divisas_Novas" && t.parent.name != "Area_Impressoras"
               && t.parent.name != "Escritorio" && t.parent.name != "ESCRITORIO" && t.parent.name != "DECORAÇÃO") t = t.parent;
        return t;
    }

    private static int Apoio(Transform t)
    {
        // base pelos colisores sólidos (a malha da mesa desce 12 cm abaixo do tampo/colisor, como em todas as estações);
        // sem colisor: pelos renderers
        var cs = t.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy).ToArray();
        var rs = t.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
        if (rs.Length == 0 && cs.Length == 0) return 0;
        Bounds b = cs.Length > 0 ? cs[0].bounds : rs[0].bounds;
        if (cs.Length > 0) foreach (var c in cs) b.Encapsulate(c.bounds); else foreach (var r in rs) b.Encapsulate(r.bounds);
        float suporte = float.NegativeInfinity; string nome = "-";
        foreach (var off in new[] { Vector3.zero, new Vector3(0.05f, 0, 0.05f), new Vector3(-0.05f, 0, -0.05f) })
            foreach (var h in Physics.RaycastAll(new Vector3(b.center.x, b.min.y + 0.05f, b.center.z) + off, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore))
                if (!h.collider.transform.IsChildOf(t) && h.point.y > suporte) { suporte = h.point.y; nome = Caminho(h.collider.transform); }
        float gap = b.min.y - suporte;
        bool ok = !float.IsNegativeInfinity(suporte) && gap > -0.13f && gap < 0.02f;
        log.Add($"apoio: {t.name} base y {b.min.y:0.000} sobre {nome} ({(float.IsNegativeInfinity(suporte) ? "nada" : (gap * 100).ToString("0.0") + " cm")}){(ok ? "" : "  <-- VERIFICAR")}");
        return ok ? 0 : 1;
    }

    // Grade de 10 cm: 1 = livre para a cápsula, 2 = alcançável a partir do início (inundação). Também grava o PNG.
    private const float Passo = 0.1f;
    private static int[,] Grade(Vector3 ini, out float x0, out float z0, out int W, out int H)
    {
        x0 = 12f; z0 = -9f; W = (int)((60f - x0) / Passo); H = (int)((34f - z0) / Passo);
        var m = new int[W, H];
        var player = GameObject.Find("Player");
        for (int i = 0; i < W; i++)
            for (int j = 0; j < H; j++)
            {
                Vector3 c = new Vector3(x0 + (i + 0.5f) * Passo, 0, z0 + (j + 0.5f) * Passo);
                bool bloq = false;
                foreach (var col in Physics.OverlapCapsule(c + Vector3.up * 0.35f, c + Vector3.up * 1.4f, 0.30f, ~0, QueryTriggerInteraction.Ignore))
                    if (player == null || !col.transform.IsChildOf(player.transform)) { bloq = true; break; }
                m[i, j] = bloq ? 0 : 1;
            }
        int si = Mathf.Clamp(Mathf.FloorToInt((ini.x - x0) / Passo), 0, W - 1), sj = Mathf.Clamp(Mathf.FloorToInt((ini.z - z0) / Passo), 0, H - 1);
        var q = new Queue<Vector2Int>();
        if (m[si, sj] == 1) { m[si, sj] = 2; q.Enqueue(new Vector2Int(si, sj)); }
        else log.Add("ATENÇÃO: posição inicial do jogador bloqueada na grade");
        while (q.Count > 0)
        {
            var p = q.Dequeue();
            foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                var n = p + d;
                if (n.x < 0 || n.y < 0 || n.x >= W || n.y >= H || m[n.x, n.y] != 1) continue;
                m[n.x, n.y] = 2; q.Enqueue(n);
            }
        }
        var tex = new Texture2D(W, H);
        for (int i = 0; i < W; i++) for (int j = 0; j < H; j++)
            tex.SetPixel(i, j, m[i, j] == 0 ? new Color(0.2f, 0.2f, 0.25f) : m[i, j] == 2 ? new Color(0.75f, 0.92f, 0.75f) : new Color(1f, 0.85f, 0.6f));
        tex.Apply();
        File.WriteAllBytes("Logs/MobiliarioEscritorio_mapa.png", tex.EncodeToPNG());
        return m;
    }
}
