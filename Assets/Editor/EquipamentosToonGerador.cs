using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gera os modelos cartunizados do TESTADOR DE CABOS e do NOTEBOOK no padrão de arte do Matheus:
///  - shader do projeto: o <c>Toon/Toon</c> (Unity Toon Shader) com contorno, o mesmo dos personagens e do supervisor.
///    Cada material novo é uma CÓPIA de <c>M_Character_Tex1_1</c> (contorno, degraus de sombra), sem textura e com a
///    cor da peça; o material do Matheus não é alterado;
///  - malhas de caixas ARREDONDADAS com normais suaves (o contorno do Toon Shader sai inteiro, sem rachar nas quinas).
/// Organização igual à dos assets existentes:
///  malhas em <c>Art/Meshes</c> (Objeto_Parte.asset, como Gaveteiro_*), materiais em <c>Art/Materials/&lt;Objeto&gt;</c>
///  (M_Objeto_Parte), prefabs em <c>Prefabs/Gameplay</c> (testador, usável) e <c>Prefabs/Props</c> (visual do notebook).
/// Rodar de novo atualiza malhas, materiais e prefabs nos MESMOS arquivos (as referências das cenas continuam valendo).
/// Cada peça é um objeto separado com nome claro, para o Matheus poder trocar por um .fbx depois.
/// </summary>
public static class EquipamentosToonGerador
{
    private const string BaseToonMaterial = "Assets/Art/Materials/M_Character_Tex1_1.mat";
    private const string Meshes = "Assets/Art/Meshes";
    private const string TesterMaterials = "Assets/Art/Materials/Testador";
    private const string NotebookMaterials = "Assets/Art/Materials/Notebook";
    public const string TesterPrefabPath = "Assets/Prefabs/Gameplay/Testador_CaboRede.prefab";
    public const string NotebookPrefabPath = "Assets/Prefabs/Props/Notebook_Toon.prefab";
    public const string TonerPrefabPath = "Assets/Prefabs/Props/Toner_Toon.prefab";
    public const string ResmaPrefabPath = "Assets/Prefabs/Props/Resma_Toon.prefab";

    [MenuItem("Tools/E-stagiar.io/Gerar modelos toon (testador e notebook)")]
    public static void GerarTudo()
    {
        GerarTestador(AssetDatabase.LoadAssetAtPath<UIScreen>("Assets/Prefabs/UI/UI_Screen_TestadorCabo.prefab"));
        GerarNotebook();
        GerarToner();
        GerarResma();
        AplicarModeloNoPickup("Assets/Prefabs/Gameplay/Pickup_Toner.prefab", TonerPrefabPath);
        AplicarModeloNoPickup("Assets/Prefabs/Gameplay/Pickup_ResmaPapel.prefab", ResmaPrefabPath);
        AssetDatabase.SaveAssets();
        Debug.Log("EquipamentosToonGerador: testador, notebook, toner e resma gerados.");
    }

    // ================================================================== toner e resma (impressora)

    /// <summary>
    /// Cartucho de toner (0,24 × 0,07 × 0,07 m, as medidas do Pickup_Toner; base em y = 0, comprimento em X): corpo
    /// escuro, alça em cima, cilindro (tambor) verde embaixo na frente, trava laranja e etiqueta na lateral.
    /// </summary>
    public static GameObject GerarToner()
    {
        const string pasta = "Assets/Art/Materials/Toner";
        Material corpo = Toon(pasta, "M_Toner_Corpo", new Color(0.16f, 0.17f, 0.2f), 1.6f);
        Material escuro = Toon(pasta, "M_Toner_Detalhe", new Color(0.07f, 0.075f, 0.09f), 0f);
        Material tambor = Toon(pasta, "M_Toner_Tambor", new Color(0.2f, 0.6f, 0.38f), 0f);
        Material trava = Toon(pasta, "M_Toner_Trava", new Color(1f, 0.55f, 0.15f), 0f);
        Material etiqueta = Toon(pasta, "M_Toner_Etiqueta", new Color(0.93f, 0.93f, 0.9f), 0f);
        var root = new GameObject("Toner_Toon");
        Transform r = root.transform;
        Peca(r, "Corpo", RoundedBox("Toner_Corpo", new Vector3(0.236f, 0.056f, 0.066f), 0.012f), corpo, new Vector3(0f, 0.032f, 0f));
        Peca(r, "Base", RoundedBox("Toner_Base", new Vector3(0.226f, 0.012f, 0.05f), 0.004f), escuro, new Vector3(0f, 0.006f, -0.006f));
        // alça: barra com dois apoios, em cima
        Peca(r, "Alca", RoundedBox("Toner_Alca", new Vector3(0.11f, 0.008f, 0.018f), 0.004f), escuro, new Vector3(0f, 0.071f, 0f));
        foreach (float x in new[] { -0.05f, 0.05f })
            Peca(r, "ApoioAlca", RoundedBox("Toner_ApoioAlca", new Vector3(0.012f, 0.012f, 0.018f), 0.003f), escuro, new Vector3(x, 0.063f, 0f));
        // tambor (cilindro deitado em X) aparecendo na frente, embaixo
        Transform t = Disco(r, "Tambor", new Vector3(0.016f, 0.2f, 0.016f), tambor, new Vector3(0f, 0.012f, 0.03f));
        t.localRotation = Quaternion.Euler(0f, 0f, 90f);
        // trava laranja numa ponta e etiqueta branca na lateral (lado +Z)
        Peca(r, "Trava", RoundedBox("Toner_Trava", new Vector3(0.012f, 0.03f, 0.04f), 0.004f), trava, new Vector3(0.121f, 0.04f, 0f));
        Peca(r, "Etiqueta", RoundedBox("Toner_Etiqueta", new Vector3(0.09f, 0.03f, 0.002f), 0.0009f), etiqueta, new Vector3(-0.04f, 0.036f, 0.0345f));
        for (int i = 0; i < 3; i++)
            Peca(r, "Linha" + i, RoundedBox("Toner_LinhaEtiqueta", new Vector3(0.06f - i * 0.015f, 0.003f, 0.002f), 0.0009f), escuro,
                 new Vector3(-0.05f + i * 0.0075f, 0.044f - i * 0.007f, 0.037f));
        return Salvar(root, TonerPrefabPath);
    }

    /// <summary>
    /// Resma de papel A4 (0,21 × 0,05 × 0,297 m, as medidas do Pickup_ResmaPapel; base em y = 0): pilha branca com a
    /// embalagem azul em volta do meio e etiqueta em cima (as pontas mostram as folhas).
    /// </summary>
    public static GameObject GerarResma()
    {
        const string pasta = "Assets/Art/Materials/Resma";
        Material papel = Toon(pasta, "M_Resma_Papel", new Color(0.96f, 0.96f, 0.93f), 1.2f);
        Material embalagem = Toon(pasta, "M_Resma_Embalagem", new Color(0.24f, 0.46f, 0.8f), 1.2f);
        Material etiqueta = Toon(pasta, "M_Resma_Etiqueta", new Color(0.97f, 0.97f, 0.95f), 0f);
        Material folhas = Toon(pasta, "M_Resma_Folhas", new Color(0.78f, 0.79f, 0.77f), 0f);
        var root = new GameObject("Resma_Toon");
        Transform r = root.transform;
        Peca(r, "Pilha", RoundedBox("Resma_Pilha", new Vector3(0.208f, 0.048f, 0.295f), 0.003f), papel, new Vector3(0f, 0.024f, 0f));
        Peca(r, "Embalagem", RoundedBox("Resma_Embalagem", new Vector3(0.212f, 0.05f, 0.2f), 0.004f), embalagem, new Vector3(0f, 0.025f, 0f));
        Peca(r, "Etiqueta", RoundedBox("Resma_Etiqueta", new Vector3(0.12f, 0.002f, 0.08f), 0.0009f), etiqueta, new Vector3(0f, 0.0505f, 0f));
        Peca(r, "Faixa", RoundedBox("Resma_Faixa", new Vector3(0.12f, 0.002f, 0.012f), 0.0009f), embalagem, new Vector3(0f, 0.0515f, 0.026f));
        foreach (float z in new[] { -0.1475f, 0.1475f })   // as folhas nas pontas (riscos finos na face)
            for (int i = 0; i < 4; i++)
                Peca(r, "Folhas", RoundedBox("Resma_LinhaFolhas", new Vector3(0.2f, 0.0012f, 0.002f), 0.0005f), folhas,
                     new Vector3(0f, 0.01f + i * 0.0095f, z * 1.0f + Mathf.Sign(z) * 0.0008f));
        return Salvar(root, ResmaPrefabPath);
    }

    private static GameObject Salvar(GameObject root, string path)
    {
        EnsureFolder("Assets/Prefabs", "Props");
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return saved;
    }

    /// <summary>
    /// Põe o modelo toon no pickup (filho "Modelo" da raiz) e desliga só o desenho do cubo antigo do "Visual" (o objeto
    /// fica: o dano por queda e as rachaduras continuam iguais). Pode rodar de novo.
    /// </summary>
    private static void AplicarModeloNoPickup(string pickupPath, string modeloPath)
    {
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(modeloPath);
        if (modelo == null || AssetDatabase.LoadAssetAtPath<GameObject>(pickupPath) == null) return;
        GameObject root = PrefabUtility.LoadPrefabContents(pickupPath);
        Transform velho = root.transform.Find("Modelo");
        if (velho != null) Object.DestroyImmediate(velho.gameObject);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(modelo, root.transform);
        inst.name = "Modelo";
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        Transform visual = root.transform.Find("Visual");
        if (visual != null && visual.TryGetComponent(out MeshRenderer mr)) mr.enabled = false;
        PrefabUtility.SaveAsPrefabAsset(root, pickupPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // ================================================================== testador

    /// <summary>Cria/atualiza o prefab do testador (lógica + modelo). <paramref name="screen"/> = tela 2D.</summary>
    public static GameObject GerarTestador(UIScreen screen)
    {
        Material corpo = Toon(TesterMaterials, "M_Testador_Corpo", new Color(0.98f, 0.74f, 0.16f), 2.2f);
        Material borracha = Toon(TesterMaterials, "M_Testador_Borracha", new Color(0.18f, 0.2f, 0.24f), 2.2f);
        Material painel = Toon(TesterMaterials, "M_Testador_Painel", new Color(0.1f, 0.13f, 0.12f), 1.2f);
        Material led = Toon(TesterMaterials, "M_Testador_Led", new Color(0.16f, 0.2f, 0.17f), 0.6f);
        Material porta = Toon(TesterMaterials, "M_Testador_Porta", new Color(0.06f, 0.07f, 0.08f), 0.6f);
        Material contato = Toon(TesterMaterials, "M_Testador_Contato", new Color(0.9f, 0.74f, 0.3f), 0f);

        // prefab existente: edita o MESMO asset (as instâncias nas cenas acompanham); senão cria
        bool novo = AssetDatabase.LoadAssetAtPath<GameObject>(TesterPrefabPath) == null;
        GameObject root = novo ? new GameObject("Testador_CaboRede") : PrefabUtility.LoadPrefabContents(TesterPrefabPath);
        Transform velho = root.transform.Find("Visual");
        if (velho != null) Object.DestroyImmediate(velho.gameObject);

        var visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);
        var principal = new Renderer[8];
        var remota = new Renderer[8];
        Unidade(visual, "Principal", new Vector3(-0.045f, 0f, 0f), new Vector3(0.068f, 0.032f, 0.118f),
                corpo, borracha, painel, led, porta, contato, principal);
        Unidade(visual, "Remota", new Vector3(0.05f, -0.004f, 0.014f), new Vector3(0.052f, 0.026f, 0.088f),
                corpo, borracha, painel, led, porta, contato, remota);

        BoxCollider box = Comp<BoxCollider>(root);
        box.center = new Vector3(0.0025f, 0f, 0f);
        box.size = new Vector3(0.18f, 0.04f, 0.125f);
        Rigidbody rb = Comp<Rigidbody>(root);
        rb.mass = 0.3f;
        PickupInteractable pickup = Comp<PickupInteractable>(root);
        pickup.interactionMessage = "Pegar o testador de cabo";
        var soPick = new SerializedObject(pickup);
        SerializedProperty frozen = soPick.FindProperty("startsFrozen");
        if (frozen != null) frozen.boolValue = true;
        soPick.ApplyModifiedPropertiesWithoutUndo();
        TestadorCaboInteractable use = Comp<TestadorCaboInteractable>(root);
        use.interactionMessage = "Testar cabo de rede";
        if (screen != null) use.screen = screen;
        TestadorCaboLeds leds = Comp<TestadorCaboLeds>(root);
        var soLeds = new SerializedObject(leds);
        SetArray(soLeds.FindProperty("principal"), principal);
        SetArray(soLeds.FindProperty("remota"), remota);
        soLeds.ApplyModifiedPropertiesWithoutUndo();

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, TesterPrefabPath);
        if (novo) Object.DestroyImmediate(root); else PrefabUtility.UnloadPrefabContents(root);
        return saved;
    }

    // Carcaça amarela com cantoneiras de borracha, visor escuro com 8 LEDs, entrada RJ45 com contatos dourados.
    private static void Unidade(Transform parent, string name, Vector3 pos, Vector3 size, Material corpo, Material borracha,
                                Material painel, Material led, Material porta, Material contato, Renderer[] leds)
    {
        var u = new GameObject(name).transform;
        u.SetParent(parent, false);
        u.localPosition = pos;
        string m = "Testador_" + name;
        float r = Mathf.Min(size.x, size.y, size.z) * 0.32f;
        Peca(u, "Carcaca", RoundedBox(m + "_Carcaca", size, r), corpo, Vector3.zero);
        // cantoneiras: faixa de borracha na base e na tampa
        Peca(u, "Borracha_Base", RoundedBox(m + "_Borracha", new Vector3(size.x + 0.004f, size.y * 0.3f, size.z + 0.004f), size.y * 0.14f),
             borracha, new Vector3(0f, -size.y * 0.36f, 0f));
        Vector3 vis = new Vector3(size.x * 0.78f, 0.004f, size.z * 0.42f);
        Peca(u, "Visor", RoundedBox(m + "_Visor", vis, 0.0018f), painel, new Vector3(0f, size.y * 0.5f, -size.z * 0.12f));
        for (int i = 0; i < 8; i++)
        {
            float x = (i - 3.5f) * vis.x / 8.6f;
            leds[i] = Peca(u, "Led" + (i + 1), RoundedBox("Testador_Led", new Vector3(0.0055f, 0.004f, 0.0055f), 0.0018f), led,
                           new Vector3(x, size.y * 0.5f + 0.003f, -size.z * 0.12f));
        }
        Vector3 rj = new Vector3(0.017f, 0.014f, 0.006f);
        Peca(u, "EntradaRJ45", RoundedBox("Testador_RJ45", rj, 0.0015f), porta, new Vector3(0f, 0f, size.z * 0.5f + 0.001f));
        Peca(u, "Contatos", RoundedBox("Testador_Contatos", new Vector3(0.011f, 0.0015f, 0.002f), 0.0005f), contato,
             new Vector3(0f, 0.004f, size.z * 0.5f + 0.0035f));
        if (name == "Principal")
        {
            // botão TEST (azul, com aro escuro) no tampo, entre o visor e a entrada: a tela 2D clica nele
            Material azul = Toon(TesterMaterials, "M_Testador_Botao", new Color(0.2f, 0.5f, 0.92f), 1f);
            Disco(u, "BotaoTeste_Aro", new Vector3(0.024f, 0.003f, 0.024f), painel, new Vector3(0f, size.y * 0.5f + 0.0005f, size.z * 0.3f));
            Disco(u, "BotaoTeste", new Vector3(0.019f, 0.005f, 0.019f), azul, new Vector3(0f, size.y * 0.5f + 0.0015f, size.z * 0.3f));
        }
    }

    // ================================================================== notebook

    // Medidas do notebook (m): um 15,6" real, retangular. Frente = +Z, dobradiça em -Z, lado das portas = +X.
    public const float NbLargura = 0.36f, NbProfundidade = 0.245f, NbAltura = 0.02f;
    private const float NbFundo = 0.009f;   // altura da parte oca de baixo (pés + tampa de baixo + cavidade do interior)

    /// <summary>
    /// Cria/atualiza o modelo do notebook (só visual: a lógica fica no Notebook_Estacao). Partes com nomes fixos que o
    /// <see cref="NotebookModeloEstado"/> e a tela 2D usam: Tela, LedEnergia, BotaoPower, Teclado, Touchpad,
    /// TampaInferior, Parafuso_0..5, SlotRAM, SlotSSD, RAM_Instalada, SSD_Instalado, Porta_USB, Pendrive.
    /// </summary>
    public static GameObject GerarNotebook()
    {
        Material carcaca = Toon(NotebookMaterials, "M_Notebook_Carcaca", new Color(0.62f, 0.68f, 0.8f), 0f);
        Material teclas = Toon(NotebookMaterials, "M_Notebook_Teclas", new Color(0.2f, 0.22f, 0.28f), 0f);
        Material deck = Toon(NotebookMaterials, "M_Notebook_Deck", new Color(0.45f, 0.5f, 0.62f), 0f);
        Material tela = Toon(NotebookMaterials, "M_Notebook_Tela", new Color(0.03f, 0.035f, 0.05f), 0f);
        Material moldura = Toon(NotebookMaterials, "M_Notebook_Moldura", new Color(0.14f, 0.15f, 0.19f), 0f);
        Material fundo = Toon(NotebookMaterials, "M_Notebook_Fundo", new Color(0.3f, 0.33f, 0.4f), 0f);
        Material borracha = Toon(NotebookMaterials, "M_Notebook_Borracha", new Color(0.1f, 0.1f, 0.12f), 0f);
        Material metal = Toon(NotebookMaterials, "M_Notebook_Metal", new Color(0.72f, 0.75f, 0.8f), 0f);
        Material porta = Toon(NotebookMaterials, "M_Notebook_Porta", new Color(0.05f, 0.055f, 0.07f), 0f);
        Material pcb = Toon(NotebookMaterials, "M_Notebook_PCB", new Color(0.18f, 0.55f, 0.32f), 0f);   // RAM e placa Wi-Fi
        // placa-mãe azul escura (não se confunde com o verde da RAM) e SSD grafite com etiqueta (como o item Pickup_Ssd)
        Material placaMat = Toon(NotebookMaterials, "M_Notebook_PlacaMae", new Color(0.15f, 0.25f, 0.5f), 0f);
        Material ssdMat = Toon(NotebookMaterials, "M_Notebook_SSD", new Color(0.2f, 0.21f, 0.25f), 0f);
        Material ssdFaixa = Toon(NotebookMaterials, "M_Notebook_SSD_Faixa", new Color(0.55f, 0.35f, 0.85f), 0f);
        Material chip = Toon(NotebookMaterials, "M_Notebook_Chip", new Color(0.1f, 0.1f, 0.12f), 0f);
        Material dourado = Toon(NotebookMaterials, "M_Notebook_Contato", new Color(0.9f, 0.74f, 0.3f), 0f);
        Material etiqueta = Toon(NotebookMaterials, "M_Notebook_Etiqueta", new Color(0.92f, 0.92f, 0.88f), 0f);
        Material led = Toon(NotebookMaterials, "M_Notebook_Led", new Color(0.12f, 0.14f, 0.13f), 0f);
        // detalhes finos (fendas, emendas, símbolos): SEM contorno — o contorno do toon em peças tão finas vira riscos
        // pretos que saem do modelo
        Material detalhe = Toon(NotebookMaterials, "M_Notebook_Detalhe", new Color(0.05f, 0.055f, 0.07f), 0f);
        Material metalLiso = Toon(NotebookMaterials, "M_Notebook_MetalLiso", new Color(0.72f, 0.75f, 0.8f), 0f);
        Material pendriveMat = Toon(NotebookMaterials, "M_Notebook_Pendrive", new Color(0.22f, 0.45f, 0.85f), 0f);

        const float w = NbLargura, d = NbProfundidade, h = NbAltura, f = NbFundo;
        var root = new GameObject("Notebook_Toon");
        Transform r = root.transform;

        // ---- carcaça de baixo, deck e teclado
        float hc = h - f;   // altura da carcaça (o fundo fica embaixo dela)
        Peca(r, "Base", RoundedBox("Notebook_Base", new Vector3(w, hc, d), 0.01f), carcaca, new Vector3(0f, f + hc * 0.5f, 0f));
        Peca(r, "Deck", RoundedBox("Notebook_Deck", new Vector3(w * 0.93f, 0.0016f, d * 0.88f), 0.006f), deck, new Vector3(0f, h + 0.0002f, 0.004f));
        Peca(r, "Teclado", Teclado(), teclas, new Vector3(0f, h + 0.0022f, -0.018f));
        Peca(r, "Touchpad", RoundedBox("Notebook_Touchpad", new Vector3(0.11f, 0.0014f, 0.068f), 0.006f), moldura, new Vector3(0f, h + 0.0012f, 0.075f));
        // botão de energia: tecla redonda com o símbolo universal (aro aberto em cima + haste) gravado nela
        Transform power = Disco(r, "BotaoPower", new Vector3(0.019f, 0.0035f, 0.019f), metal, new Vector3(w * 0.41f, h + 0.002f, -d * 0.36f));
        Disco(power, "SimboloAro", new Vector3(0.0115f, 0.0004f, 0.0115f), detalhe, new Vector3(0f, 0.0028f, 0.0005f));
        Disco(power, "SimboloMiolo", new Vector3(0.0078f, 0.0004f, 0.0078f), metal, new Vector3(0f, 0.0038f, 0.0005f));
        Peca(power, "SimboloAbertura", RoundedBox("Notebook_SimboloAbertura", new Vector3(0.0048f, 0.0004f, 0.0045f), 0.0001f), metal, new Vector3(0f, 0.0048f, -0.0042f));
        Peca(power, "SimboloHaste", RoundedBox("Notebook_SimboloHaste", new Vector3(0.0016f, 0.0004f, 0.0068f), 0.0001f), detalhe, new Vector3(0f, 0.0058f, -0.0028f));
        Disco(r, "LedEnergia", new Vector3(0.0065f, 0.0018f, 0.0065f), led, new Vector3(w * 0.41f - 0.02f, h + 0.0012f, -d * 0.36f));

        // ---- lateral direita (+X): portas (a USB é a do pendrive; as outras são decorativas) e ventilação
        var portas = new GameObject("Portas").transform;
        portas.SetParent(r, false);
        float xLado = w * 0.5f + 0.0004f, yPorta = f + hc * 0.5f;
        Porta(portas, "Porta_USB", new Vector3(xLado, yPorta, -0.02f), new Vector3(0.0035f, 0.0065f, 0.014f), porta, dourado);
        Porta(portas, "Porta_USB2", new Vector3(xLado, yPorta, 0.0f), new Vector3(0.0035f, 0.0065f, 0.014f), porta, dourado);
        Porta(portas, "Porta_HDMI", new Vector3(xLado, yPorta, 0.025f), new Vector3(0.0035f, 0.0062f, 0.016f), porta, dourado);
        Peca(portas, "Porta_Energia", RoundedBox("Notebook_PortaEnergia", new Vector3(0.0035f, 0.0075f, 0.0075f), 0.0034f), porta,
             new Vector3(xLado, yPorta, 0.06f));
        for (int i = 0; i < 6; i++)
            Peca(portas, "Ventilacao_Lateral_" + i, RoundedBox("Notebook_FendaLateral", new Vector3(0.003f, 0.0016f, 0.03f), 0.0007f), porta,
                 new Vector3(xLado, f + hc * 0.28f + i * 0.0019f, -0.075f));
        Transform pen = new GameObject("Pendrive").transform;
        pen.SetParent(r, false);
        pen.localPosition = new Vector3(xLado + 0.022f, yPorta, -0.02f);
        Peca(pen, "Corpo", RoundedBox("Notebook_PendriveCorpo", new Vector3(0.036f, 0.0085f, 0.017f), 0.0035f), pendriveMat, Vector3.zero);
        Peca(pen, "Conector", RoundedBox("Notebook_PendriveConector", new Vector3(0.012f, 0.0048f, 0.0125f), 0.0006f), metal,
             new Vector3(-0.022f, 0f, 0f));
        pen.gameObject.SetActive(false);   // aparece só com o pendrive conectado (NotebookModeloEstado)

        // ---- fundo: a carcaça de baixo é OCA (paredes + cavidade aberta embaixo), fechada pela tampa de baixo encaixada
        //      entre as paredes; pés de borracha nas paredes; 6 parafusos Phillips na tampa
        Material cobre = Toon(NotebookMaterials, "M_Notebook_Cobre", new Color(0.86f, 0.52f, 0.28f), 0f);
        Material flat = Toon(NotebookMaterials, "M_Notebook_CaboFlat", new Color(0.95f, 0.6f, 0.18f), 0f);
        Material bateriaMat = Toon(NotebookMaterials, "M_Notebook_Bateria", new Color(0.24f, 0.26f, 0.3f), 0f);
        const float parede = 0.006f, pe = 0.0015f, esp = 0.002f;
        float cw = w - 2f * parede, cd = d - 2f * parede, hp = f - pe;   // abertura da cavidade e altura das paredes
        Peca(r, "Parede_Frente", RoundedBox("Notebook_ParedeX", new Vector3(w, hp, parede), 0.002f), carcaca, new Vector3(0f, pe + hp * 0.5f, d * 0.5f - parede * 0.5f));
        Peca(r, "Parede_Tras", RoundedBox("Notebook_ParedeX", new Vector3(w, hp, parede), 0.002f), carcaca, new Vector3(0f, pe + hp * 0.5f, -d * 0.5f + parede * 0.5f));
        Peca(r, "Parede_Esquerda", RoundedBox("Notebook_ParedeZ", new Vector3(parede, hp, d), 0.002f), carcaca, new Vector3(-w * 0.5f + parede * 0.5f, pe + hp * 0.5f, 0f));
        Peca(r, "Parede_Direita", RoundedBox("Notebook_ParedeZ", new Vector3(parede, hp, d), 0.002f), carcaca, new Vector3(w * 0.5f - parede * 0.5f, pe + hp * 0.5f, 0f));
        foreach (Vector2 p in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
            Peca(r, "Pe", RoundedBox("Notebook_Pe", new Vector3(0.06f, pe, 0.0055f), 0.0012f), borracha,
                 new Vector3(p.x * w * 0.3f, pe * 0.5f, p.y * (d * 0.5f - parede * 0.5f)));

        Transform tampa = Peca(r, "TampaInferior", RoundedBox("Notebook_TampaInferior", new Vector3(cw - 0.001f, esp, cd - 0.001f), 0.003f), fundo,
                               new Vector3(0f, pe + esp * 0.5f, 0f)).transform;
        float baixo = -esp * 0.5f - 0.0005f;   // face de baixo da tampa (no espaço dela)
        for (int fila = 0; fila < 2; fila++)
            for (int i = 0; i < 13; i++)
                Peca(tampa, "Ventilacao_" + fila + "_" + i, RoundedBox("Notebook_FendaFundo", new Vector3(0.0045f, 0.0004f, 0.03f), 0.0012f), detalhe,
                     new Vector3(-0.15f + i * 0.0085f, baixo, -0.075f + fila * 0.036f));
        foreach (float lado in new[] { -1f, 1f })   // grades dos alto-falantes, nos cantos da frente
            for (int a = 0; a < 6; a++)
                for (int b = 0; b < 3; b++)
                    Peca(tampa, "Grade", RoundedBox("Notebook_FuroGrade", new Vector3(0.0022f, 0.0004f, 0.0022f), 0.001f), detalhe,
                         new Vector3(lado * (cw * 0.36f) + (a - 2.5f) * 0.0045f, baixo, cd * 0.38f + (b - 1f) * 0.0045f));
        Transform etiq = Peca(tampa, "Etiqueta", RoundedBox("Notebook_Etiqueta", new Vector3(0.064f, 0.0004f, 0.036f), 0.002f), etiqueta,
                              new Vector3(0.085f, baixo, 0.035f)).transform;
        for (int i = 0; i < 14; i++)   // código de barras
            Peca(etiq, "Barra" + i, RoundedBox(i % 3 == 0 ? "Notebook_BarraLarga" : "Notebook_BarraFina",
                 new Vector3(i % 3 == 0 ? 0.0016f : 0.0008f, 0.0002f, 0.012f), 0.0002f), detalhe,
                 new Vector3(-0.024f + i * 0.0026f, -0.0003f, 0.007f));
        // saída de ar na parede de trás, pés longos e marcas de certificação ao lado da etiqueta (as peças do notebook
        // ficam SEM contorno: o contorno do toon em peças finas vira espetos pretos para fora do modelo)
        for (int i = 0; i < 16; i++)
            Peca(r, "SaidaAr" + i, RoundedBox("Notebook_SaidaAr", new Vector3(0.0018f, 0.0004f, 0.0042f), 0.0006f), detalhe,
                 new Vector3(-0.07f + i * 0.0045f, pe - 0.0005f, -d * 0.5f + parede * 0.5f));   // entre os pés de trás
        foreach (float z in new[] { -1f, 1f })
            Peca(r, "PeLongo", RoundedBox("Notebook_PeLongo", new Vector3(w * 0.34f, pe * 0.8f, 0.004f), 0.0012f), borracha,
                 new Vector3(0f, pe * 0.4f, z * (cd * 0.5f - 0.012f)));
        for (int i = 0; i < 3; i++)   // linhas de texto (certificações) e o selo
            Peca(tampa, "Marcas" + i, RoundedBox("Notebook_Marcas", new Vector3(0.05f - i * 0.012f, 0.0003f, 0.0018f), 0.0003f), detalhe,
                 new Vector3(0.085f - i * 0.006f, baixo, 0.062f + i * 0.0045f));
        Disco(tampa, "Selo", new Vector3(0.008f, 0.0003f, 0.008f), detalhe, new Vector3(0.128f, baixo, 0.066f));

        Vector2[] pos = { new Vector2(-0.44f, 0.36f), new Vector2(0.44f, 0.36f), new Vector2(-0.44f, 0f),
                          new Vector2(0.44f, 0f), new Vector2(-0.44f, -0.36f), new Vector2(0.44f, -0.36f) };
        for (int i = 0; i < 6; i++)
        {
            Vector3 p = new Vector3(pos[i].x * cw, 0f, -pos[i].y * cd);
            // parafuso Phillips: cabeça redonda com a fenda em cruz (some quando sai: NotebookModeloEstado)
            Transform par = Disco(r, "Parafuso_" + i, new Vector3(0.0078f, 0.0009f, 0.0078f), metal, new Vector3(p.x, pe - 0.0004f, p.z));
            Peca(par, "Fenda1", RoundedBox("Notebook_FendaParafuso", new Vector3(0.0052f, 0.0003f, 0.0009f), 0.0002f), detalhe, new Vector3(0f, -0.0013f, 0f));
            Peca(par, "Fenda2", RoundedBox("Notebook_FendaParafuso", new Vector3(0.0052f, 0.0003f, 0.0009f), 0.0002f), detalhe, new Vector3(0f, -0.0013f, 0f))
                .transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        }

        // ---- interior (na cavidade, pendurado no "teto" y = f): placa, processador com dissipador e heatpipes, ventoinha
        //      com pás, bateria com células, alto-falantes, cabo flat, placa Wi-Fi, slots de RAM/SSD e as peças instaladas
        var interior = new GameObject("Interior").transform;
        interior.SetParent(r, false);
        Peca(interior, "Forro", RoundedBox("Notebook_Forro", new Vector3(cw, 0.0005f, cd), 0.002f), moldura, new Vector3(0f, f - 0.00025f, 0f));
        // placa-mãe com volume: 1,6 mm (cresce para o forro; a face com as peças fica no mesmo plano) e proporção mais
        // próxima de quadrada (mais alta e um pouco mais estreita, sem invadir a bateria nem a parede de trás)
        Transform placa = Peca(interior, "PlacaMae", RoundedBox("Notebook_PlacaMae", new Vector3(w * 0.486f, 0.0016f, d * 0.563f), 0.002f), placaMat,
                               new Vector3(w * 0.117f, f - 0.0005f, -d * 0.172f)).transform;
        for (int i = 0; i < 6; i++)   // chips e componentes pequenos da placa
            Peca(interior, "ChipPlaca" + i, RoundedBox("Notebook_ChipPlaca", new Vector3(0.008f + (i % 3) * 0.003f, 0.0008f, 0.008f), 0.0006f), chip,
                 new Vector3(w * 0.26f + (i % 3) * 0.014f, f - 0.0016f, -d * 0.02f - (i / 3) * 0.016f));
        Peca(interior, "Processador", RoundedBox("Notebook_Processador", new Vector3(0.028f, 0.001f, 0.028f), 0.001f), chip,
             new Vector3(-w * 0.02f, f - 0.0017f, -d * 0.2f));
        Peca(interior, "Dissipador", RoundedBox("Notebook_Dissipador", new Vector3(0.036f, 0.0008f, 0.036f), 0.002f), cobre,
             new Vector3(-w * 0.02f, f - 0.0026f, -d * 0.2f));
        for (int k = 0; k < 2; k++)
            Peca(interior, "Heatpipe" + k, RoundedBox("Notebook_Heatpipe", new Vector3(0.105f, 0.0016f, 0.006f), 0.0008f), cobre,
                 new Vector3(-w * 0.17f, f - 0.0028f, -d * 0.2f - 0.006f + k * 0.012f));
        Transform fan = Disco(interior, "Ventoinha", new Vector3(0.066f, 0.0035f, 0.066f), borracha, new Vector3(-w * 0.33f, f - 0.00195f, -d * 0.2f));
        Disco(fan, "Miolo", new Vector3(0.05f, 0.0006f, 0.05f), porta, new Vector3(0f, -0.0017f, 0f));
        for (int k = 0; k < 9; k++)
        {
            float ang = k * 40f;
            Vector3 radial = Quaternion.Euler(0f, ang, 0f) * new Vector3(0.0135f, 0f, 0f);
            Peca(fan, "Pa" + k, RoundedBox("Notebook_Pa", new Vector3(0.019f, 0.0005f, 0.0055f), 0.0012f), metal,
                 new Vector3(radial.x, -0.0021f, radial.z)).transform.localRotation = Quaternion.Euler(0f, ang + 18f, 0f);
        }
        Disco(fan, "Cubo", new Vector3(0.012f, 0.0007f, 0.012f), metal, new Vector3(0f, -0.0023f, 0f));
        for (int k = 0; k < 9; k++)   // aletas de cobre na saída de ar (atrás, junto à parede)
            Peca(interior, "Aleta" + k, RoundedBox("Notebook_Aleta", new Vector3(0.0008f, 0.0032f, 0.018f), 0.0003f), cobre,
                 new Vector3(-w * 0.33f - 0.028f + k * 0.007f, f - 0.0018f, -cd * 0.5f + 0.011f));
        Transform bat = Peca(interior, "Bateria", RoundedBox("Notebook_Bateria", new Vector3(w * 0.66f, 0.0042f, d * 0.27f), 0.003f), bateriaMat,
                             new Vector3(-w * 0.03f, f - 0.0021f, d * 0.27f)).transform;
        for (int k = 0; k < 3; k++)   // divisões das células
            Peca(bat, "Celula" + k, RoundedBox("Notebook_DivisaoCelula", new Vector3(0.0018f, 0.0003f, d * 0.25f), 0.0003f), chip,
                 new Vector3(-w * 0.16f + k * w * 0.16f, -0.0022f, 0f));
        Peca(bat, "Etiqueta", RoundedBox("Notebook_EtiquetaBateria", new Vector3(0.05f, 0.0003f, 0.025f), 0.0015f), etiqueta, new Vector3(w * 0.2f, -0.0022f, 0f));
        foreach (float lado in new[] { -1f, 1f })
            Peca(interior, "AltoFalante", RoundedBox("Notebook_AltoFalante", new Vector3(0.034f, 0.0032f, 0.015f), 0.003f), chip,
                 new Vector3(lado * cw * 0.36f, f - 0.0016f, cd * 0.38f));
        Peca(interior, "CaboFlat", RoundedBox("Notebook_CaboFlat", new Vector3(0.018f, 0.0003f, 0.075f), 0.0004f), flat,
             new Vector3(w * 0.361f, f - 0.0042f, d * 0.265f));   // ao lado da bateria, depois da ponta do SSD (não o cobre)
        Transform wifi = Peca(interior, "PlacaWifi", RoundedBox("Notebook_Wifi", new Vector3(0.022f, 0.0008f, 0.03f), 0.001f), pcb,
                              new Vector3(-w * 0.13f, f - 0.0016f, -d * 0.02f)).transform;
        for (int k = 0; k < 2; k++)
            Disco(wifi, "Antena" + k, new Vector3(0.0035f, 0.0012f, 0.0035f), dourado, new Vector3(-0.005f + k * 0.01f, -0.0009f, -0.01f));

        // slot da RAM (conector com as duas travas de metal) e a memória deitada, encaixada nele
        Transform slotRam = Peca(interior, "SlotRAM", RoundedBox("Notebook_SlotRAM", new Vector3(0.072f, 0.0026f, 0.008f), 0.001f), chip,
                                 new Vector3(w * 0.16f, f - 0.0021f, -d * 0.08f)).transform;
        foreach (float lado in new[] { -1f, 1f })
            Peca(interior, "TravaRAM", RoundedBox("Notebook_TravaRAM", new Vector3(0.0035f, 0.0024f, 0.03f), 0.0006f), metal,
                 new Vector3(slotRam.localPosition.x + lado * 0.0365f, f - 0.0022f, slotRam.localPosition.z + 0.017f));
        Transform ramI = Peca(interior, "RAM_Instalada", RoundedBox("Notebook_RAM", new Vector3(0.068f, 0.0008f, 0.03f), 0.001f), pcb,
                              new Vector3(slotRam.localPosition.x, f - 0.0033f, slotRam.localPosition.z + 0.019f)).transform;
        for (int k = 0; k < 4; k++)
            Peca(ramI, "Chip" + k, RoundedBox("Notebook_ChipRAM", new Vector3(0.011f, 0.0007f, 0.009f), 0.0004f), chip,
                 new Vector3(-0.024f + k * 0.016f, -0.0007f, 0.003f));
        Peca(ramI, "Contatos", RoundedBox("Notebook_ContatosRAM", new Vector3(0.06f, 0.0004f, 0.003f), 0.0002f), dourado,
             new Vector3(0f, -0.0005f, -0.0135f));

        // slot M.2 do SSD (conector + parafuso de fixação na outra ponta) e o SSD encaixado
        Transform slotSsd = Peca(interior, "SlotSSD", RoundedBox("Notebook_SlotSSD", new Vector3(0.024f, 0.0024f, 0.006f), 0.001f), chip,
                                 new Vector3(w * 0.33f, f - 0.002f, -d * 0.25f)).transform;
        Disco(interior, "FixacaoSSD", new Vector3(0.006f, 0.0026f, 0.006f), metal, new Vector3(slotSsd.localPosition.x, f - 0.0021f, slotSsd.localPosition.z + 0.085f));
        Transform ssdI = Peca(interior, "SSD_Instalado", RoundedBox("Notebook_SSD", new Vector3(0.022f, 0.0014f, 0.08f), 0.001f), ssdMat,
                              new Vector3(slotSsd.localPosition.x, f - 0.00295f, slotSsd.localPosition.z + 0.043f)).transform;
        Peca(ssdI, "Chip", RoundedBox("Notebook_ChipSSD", new Vector3(0.016f, 0.001f, 0.024f), 0.0005f), chip, new Vector3(0f, -0.0009f, 0.012f));
        Peca(ssdI, "Etiqueta", RoundedBox("Notebook_EtiquetaSSD", new Vector3(0.017f, 0.0004f, 0.026f), 0.0005f), etiqueta, new Vector3(0f, -0.00085f, -0.016f));
        Peca(ssdI, "Faixa", RoundedBox("Notebook_FaixaSSD", new Vector3(0.017f, 0.0005f, 0.005f), 0.0003f), ssdFaixa, new Vector3(0f, -0.00095f, -0.024f));
        for (int k = 0; k < 6; k++)   // contatos dourados da chave M, na ponta encaixada no slot
            Peca(ssdI, "Contato" + k, RoundedBox("Notebook_ContatoSSD", new Vector3(0.0018f, 0.0003f, 0.003f), 0.0001f), dourado,
                 new Vector3(-0.0075f + k * 0.003f, -0.0008f, -0.0385f));

        // ---- tampa da tela: gira em X na dobradiça (aberta ~110°)
        var hinge = new GameObject("Dobradica").transform;
        hinge.SetParent(r, false);
        hinge.localPosition = new Vector3(0f, h, -d * 0.5f + 0.007f);
        hinge.localRotation = Quaternion.Euler(-110f, 0f, 0f);
        Peca(hinge, "Eixo", RoundedBox("Notebook_Eixo", new Vector3(w * 0.72f, 0.011f, 0.011f), 0.005f), moldura, Vector3.zero);
        float dl = d - 0.008f;
        Transform lid = Peca(hinge, "Tampa", RoundedBox("Notebook_Tampa", new Vector3(w, 0.008f, dl), 0.008f), carcaca,
                             new Vector3(0f, 0.004f, dl * 0.5f)).transform;
        Peca(lid, "Moldura", RoundedBox("Notebook_Moldura", new Vector3(w * 0.97f, 0.0014f, dl * 0.95f), 0.007f), moldura,
             new Vector3(0f, -0.0046f, 0f));
        var telaGo = new GameObject("Tela");
        telaGo.transform.SetParent(lid, false);
        telaGo.transform.localPosition = new Vector3(0f, -0.0055f, 0.004f);
        telaGo.AddComponent<MeshFilter>().sharedMesh = QuadTela(w * 0.9f, dl * 0.82f);
        telaGo.AddComponent<MeshRenderer>().sharedMaterial = tela;
        Peca(lid, "Webcam", RoundedBox("Notebook_Webcam", new Vector3(0.004f, 0.0008f, 0.004f), 0.0015f), porta,
             new Vector3(0f, -0.0055f, dl * 0.455f));

        // ---- estado ao vivo (tela, LED, tampa, parafusos, peças, pendrive)
        var estado = root.AddComponent<NotebookModeloEstado>();
        estado.Configurar(Tex("PH_ui_tela_notebook_sistema"), Tex("PH_ui_tela_notebook_bios"), Tex("PH_ui_tela_notebook_bios_sem_sistema"),
                          new[] { Tex("PH_ui_tela_notebook_instalando_0"), Tex("PH_ui_tela_notebook_instalando_1"),
                                  Tex("PH_ui_tela_notebook_instalando_2"), Tex("PH_ui_tela_notebook_instalando_3"),
                                  Tex("PH_ui_tela_notebook_instalando_4") });

        EnsureFolder("Assets/Prefabs", "Props");
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, NotebookPrefabPath);
        Object.DestroyImmediate(root);
        return saved;
    }

    private static Texture Tex(string nome) => AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/UI/" + nome + ".png");

    // Porta (rebaixo escuro) com uma lingueta de contato dentro.
    private static void Porta(Transform parent, string name, Vector3 pos, Vector3 size, Material porta, Material contato)
    {
        Transform p = Peca(parent, name, RoundedBox("Notebook_" + name.Replace("Porta_", "Porta").Replace("2", ""), size, 0.0008f), porta, pos).transform;
        Peca(p, "Lingueta", RoundedBox("Notebook_Lingueta", new Vector3(size.x * 0.6f, size.y * 0.3f, size.z * 0.7f), 0.0003f), contato,
             new Vector3(0.0004f, -size.y * 0.12f, 0f));
    }

    // Quadro da tela: a imagem aparece em pé e sem espelhar quando a tampa está aberta (u = X, v = longe da dobradiça).
    private static Mesh QuadTela(float largura, float altura)
    {
        var m = new Mesh { name = "Notebook_TelaQuad" };
        float x = largura * 0.5f, z = altura * 0.5f;
        m.vertices = new[] { new Vector3(-x, 0f, -z), new Vector3(x, 0f, -z), new Vector3(-x, 0f, z), new Vector3(x, 0f, z) };
        // quem olha a tela de frente vê o +X do modelo à ESQUERDA: u cresce para -X (sem espelhar)
        m.uv = new[] { new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        m.normals = new[] { Vector3.down, Vector3.down, Vector3.down, Vector3.down };
        m.triangles = new[] { 0, 1, 2, 1, 3, 2 };   // face para -Y (a face interna da tampa)
        m.RecalculateBounds();
        return SaveMesh(m);
    }

    // 6 fileiras de teclas arredondadas numa malha só (Notebook_Teclado.asset).
    private static Mesh Teclado()
    {
        Mesh tecla = RoundedBox("Notebook_Tecla", new Vector3(0.0165f, 0.004f, 0.0155f), 0.0035f);
        var parts = new List<CombineInstance>();
        int[] porLinha = { 15, 14, 14, 13, 12, 9 };
        const float passo = 0.0195f;
        for (int l = 0; l < porLinha.Length; l++)
        {
            int n = porLinha[l];
            for (int k = 0; k < n; k++)
            {
                if (l == porLinha.Length - 1 && k == n / 2) continue;   // espaço da barra
                float x = (k - (n - 1) * 0.5f) * passo;
                float escalaZ = l == 0 ? 0.6f : 1f;                     // fileira das teclas de função, mais baixa
                parts.Add(new CombineInstance { mesh = tecla, transform = Matrix4x4.TRS(new Vector3(x, 0f, (l - 2.5f) * 0.0185f), Quaternion.identity, new Vector3(1f, 1f, escalaZ)) });
            }
        }
        Mesh barra = RoundedBox("Notebook_Barra", new Vector3(0.105f, 0.004f, 0.0155f), 0.0035f);
        parts.Add(new CombineInstance { mesh = barra, transform = Matrix4x4.Translate(new Vector3(0f, 0f, 2.5f * 0.0185f)) });
        var m = new Mesh { name = "Notebook_Teclado" };
        m.CombineMeshes(parts.ToArray(), true, true);
        m.RecalculateBounds();
        return SaveMesh(m);
    }

    // ================================================================== peças, malhas e materiais

    private static MeshRenderer Peca(Transform parent, string name, Mesh mesh, Material mat, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        return r;
    }

    /// <summary>
    /// Caixa arredondada com normais suaves: cada vértice de uma grade nas 6 faces é empurrado para a "casca" de raio
    /// <paramref name="r"/> em volta da caixa interna. Salva/atualiza <c>Art/Meshes/&lt;name&gt;.asset</c>.
    /// </summary>
    public static Mesh RoundedBox(string name, Vector3 size, float r, int seg = 4)
    {
        Vector3 h = size * 0.5f;
        r = Mathf.Clamp(r, 0.0001f, Mathf.Min(h.x, h.y, h.z) * 0.999f);
        float[] Amostras(float he)
        {
            var a = new List<float>();
            for (int k = 0; k <= seg; k++) a.Add(-he + r * k / seg);
            for (int k = 0; k <= seg; k++) a.Add(he - r + r * k / seg);
            return a.ToArray();
        }
        float[] ax = Amostras(h.x), ay = Amostras(h.y), az = Amostras(h.z);
        Vector3 inner = h - Vector3.one * r;
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        void Face(Vector3 n, Vector3 u, Vector3 v, float[] su, float[] sv, float hn, Vector3 hu, Vector3 hv)
        {
            int start = verts.Count;
            for (int j = 0; j < sv.Length; j++)
                for (int i = 0; i < su.Length; i++)
                {
                    Vector3 p = n * hn + u * su[i] + v * sv[j];
                    Vector3 c = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                    Vector3 nn = (p - c).sqrMagnitude > 1e-12f ? (p - c).normalized : n;
                    verts.Add(c + nn * r);
                    norms.Add(nn);
                    uvs.Add(new Vector2(Vector3.Dot(p, u) / (2f * Vector3.Dot(hu, u)) + 0.5f, Vector3.Dot(p, v) / (2f * Vector3.Dot(hv, v)) + 0.5f));
                }
            int w = su.Length;
            for (int j = 0; j < sv.Length - 1; j++)
                for (int i = 0; i < w - 1; i++)
                {
                    int a = start + j * w + i, b = a + 1, c2 = a + w, d2 = c2 + 1;
                    // Unity: (b - a) x (c - a) aponta para fora na face da frente
                    tris.Add(a); tris.Add(b); tris.Add(c2);
                    tris.Add(b); tris.Add(d2); tris.Add(c2);
                }
        }
        // (u × v = n para o enrolamento ficar para fora)
        Face(Vector3.up, Vector3.right, Vector3.back, ax, az, h.y, h, h);
        Face(Vector3.down, Vector3.right, Vector3.forward, ax, az, h.y, h, h);
        Face(Vector3.forward, Vector3.right, Vector3.up, ax, ay, h.z, h, h);
        Face(Vector3.back, Vector3.left, Vector3.up, ax, ay, h.z, h, h);
        Face(Vector3.right, Vector3.back, Vector3.up, az, ay, h.x, h, h);
        Face(Vector3.left, Vector3.forward, Vector3.up, az, ay, h.x, h, h);

        var m = new Mesh { name = name };
        m.SetVertices(verts);
        m.SetNormals(norms);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        m.RecalculateTangents();
        return SaveMesh(m);
    }

    /// <summary>
    /// Cilindro em pé (eixo Y) com elipse <paramref name="size"/>.x × .z e altura .y: peças redondas (parafusos, botões,
    /// LEDs). O <see cref="RoundedBox"/> usa um raio só, limitado pela espessura, e peças finas saíam quadradas.
    /// <paramref name="r"/> e <paramref name="seg"/> ficam pela compatibilidade das chamadas (a borda é reta).
    /// </summary>
    /// <summary>
    /// Peça redonda (parafuso, botão, LED): contêiner SEM escala em <paramref name="pos"/> com o filho "Forma" = o
    /// cilindro nativo do Unity escalado para <paramref name="size"/> (diâmetro x/z, altura y). Os filhos da peça
    /// ficam no contêiner (não herdam a escala). O RoundedBox usa um raio só, limitado pela espessura: saía quadrado.
    /// </summary>
    private static Transform Disco(Transform parent, string name, Vector3 size, Material mat, Vector3 pos)
    {
        var go = new GameObject(name).transform;
        go.SetParent(parent, false);
        go.localPosition = pos;
        var forma = new GameObject("Forma").transform;
        forma.SetParent(go, false);
        forma.localScale = size * 0.5f;   // o cilindro nativo tem diâmetro 2 e altura 2
        forma.gameObject.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        forma.gameObject.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    public static Mesh Cilindro(string name, Vector3 size, float r, int seg)
    {
        // a malha de cilindro nativa do Unity (diâmetro 1, altura 2), redimensionada para a peça
        var src = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        var m = Object.Instantiate(src);
        m.name = name;
        var escala = new Vector3(size.x, size.y * 0.5f, size.z);
        Vector3[] v = m.vertices, n = m.normals;
        for (int i = 0; i < v.Length; i++)
        {
            v[i] = Vector3.Scale(v[i], escala);
            n[i] = new Vector3(n[i].x / escala.x, n[i].y / escala.y, n[i].z / escala.z).normalized;
        }
        m.vertices = v;
        m.normals = n;
        m.RecalculateBounds();
        m.RecalculateTangents();
        return SaveMesh(m);
    }

    // Grava a malha em Art/Meshes; se já existe, atualiza o MESMO asset (as referências continuam valendo).
    private static Mesh SaveMesh(Mesh m)
    {
        string path = $"{Meshes}/{m.name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(m, path); return m; }
        EditorUtility.CopySerialized(m, existing);
        Object.DestroyImmediate(m);
        return existing;
    }

    /// <summary>
    /// Material Toon na pasta do objeto: cópia do material dos personagens do Matheus (Toon/Toon, contorno preto), sem
    /// textura, na cor da peça. <paramref name="outline"/> = largura do contorno (0 = sem contorno).
    /// </summary>
    private static Material Toon(string folder, string name, Color color, float outline)
    {
        var src = AssetDatabase.LoadAssetAtPath<Material>(BaseToonMaterial);
        EnsureFolder("Assets/Art/Materials", folder.Substring(folder.LastIndexOf('/') + 1));
        string path = $"{folder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool create = m == null;
        if (create) m = src != null ? new Material(src) : new Material(Shader.Find("Toon/Toon"));
        else if (src != null) { m.shader = src.shader; m.CopyPropertiesFromMaterial(src); }
        foreach (string tex in new[] { "_MainTex", "_BaseMap", "_1st_ShadeMap", "_2nd_ShadeMap", "_BaseColorMap" })
            if (m.HasProperty(tex)) m.SetTexture(tex, null);
        Set(m, "_BaseColor", color);
        Set(m, "_Color", color);
        Set(m, "_1st_ShadeColor", Escurecer(color, 0.78f));
        Set(m, "_2nd_ShadeColor", Escurecer(color, 0.6f));
        if (m.HasProperty("_Outline_Width")) m.SetFloat("_Outline_Width", outline);
        // o passe "SRPDefaultUnlit" fica SEMPRE ligado: desligá-lo apaga a cor do material (sai preto); sem contorno = largura 0
        m.SetShaderPassEnabled("SRPDefaultUnlit", true);
        m.name = name;
        if (create) AssetDatabase.CreateAsset(m, path);
        else EditorUtility.SetDirty(m);
        return m;
    }

    private static Color Escurecer(Color c, float k) => new Color(c.r * k, c.g * k, Mathf.Min(1f, c.b * k * 1.08f), 1f);

    private static void Set(Material m, string prop, Color c)
    {
        if (m.HasProperty(prop)) m.SetColor(prop, c);
    }

    // GetComponent no Editor devolve um "falso null" (o operador ?? não o reconhece): TryGetComponent é seguro.
    private static T Comp<T>(GameObject go) where T : Component => go.TryGetComponent(out T c) ? c : go.AddComponent<T>();

    private static void SetArray(SerializedProperty p, Renderer[] values)
    {
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
    }
}
