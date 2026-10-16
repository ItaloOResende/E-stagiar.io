using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Monta a cena <c>Fase_01.unity</c>: a MESMA aparência da <c>Office_Placeholder</c> (a sala de arte do Matheus) com
/// todos os sistemas da <c>Escritorio_Teste</c> funcionando.
/// Menu: Tools &gt; E-stagiar.io &gt; Fase 01 ...
///
/// Como funciona:
///  - copia a <c>Office_Placeholder</c> (arte) para <c>Fase_01</c>;
///  - abre a <c>Escritorio_Teste</c> em aditivo e traz de lá TODAS as raízes funcionais (estações, minigames,
///    gaveteiros, pegáveis, <c>Chamados</c>, <c>Sons_Interacao</c>, <c>EventSystem</c>, luz, volume, NavMesh, NPC e a
///    ronda). Como o grupo inteiro vem junto, as referências internas continuam válidas;
///  - da sala de teste só ficam de fora o piso/teto/paredes e os enfeites (<c>Porta</c>, <c>Janela</c>, <c>Vaso</c>);
///    o grupo funcional é então deslocado para dentro da sala de arte. O deslocamento é o MESMO que a arte já usa: o
///    <c>Player</c> da <c>Office_Placeholder</c> já está na sala nova, então uso a diferença entre os dois jogadores.
///    Um piso-colidor invisível (do tamanho da área de jogo, não do mundo) garante que ninguém caia;
///  - três objetos trocam de lugar de propósito, para a arte virar funcional:
///      * a <c>impressora</c> da arte dá lugar à <c>Impressora_Estacao</c> (mesma posição/rotação);
///      * a <c>caixa2</c> da arte é a caixa do cabo de rede: dá lugar à <c>Caixa_CaboRede</c> (mesma posição), e o
///        modelo da caixa passa a ser o próprio <c>caixa2.fbx</c> (ver <see cref="TrocarModeloCaixa"/>);
///      * o <c>Notebook_Estacao</c> vai para cima da <c>mesa_2</c>;
///  - o NavMesh é reassado SÓ no piso térreo (volume com teto em y=3, para o NavMesh não subir para o andar de cima
///    nem para o telhado) e os pontos da ronda/NPC são reencaixados no NavMesh novo;
///  - o <c>NPC_Supervisor</c> vem DESLIGADO da cena de teste: é ligado (senão o chefe fica parado) e as referências
///    dele (rota, jogador, modelo, pontos de interesse) são religadas se a mudança de cena deixou alguma vazia.
///
/// IMPORTANTE: não mexer em referências de cena por SerializedObject. Objetos que vieram como PrefabInstance
/// (impressora, notebook, caixa do cabo, NPC, telas 2D) carregam marcas internas do prefab
/// (<c>m_CorrespondingSourceObject</c> / <c>m_PrefabInstance</c>); anulá-las "desmonta" o prefab e o objeto volta
/// para a posição padrão do prefab (foi o que jogou a impressora para a origem e matou o botão de fechar das telas).
/// Ao mover os roots com <c>MoveGameObjectToScene</c> a Unity já mantém as referências entre os objetos que vieram
/// juntos; as que apontavam para o que ficou na sala de teste se anulam sozinhas quando a cena é fechada.
///
/// Não altera a <c>Office_Placeholder</c> nem a <c>Escritorio_Teste</c> (a cena de teste é fechada sem salvar).
/// </summary>
public static class Fase01Setup
{
    private const string CenaArte = "Assets/Scenes/Office_Placeholder.unity";
    private const string CenaFuncional = "Assets/Scenes/Escritorio_Teste.unity";
    public const string CenaDestino = "Assets/Scenes/Fase_01.unity";
    private const string NavAsset = "Assets/Scenes/Fase_01_NavMesh.asset";

    private const string PrefabCaixa = "Assets/Prefabs/Gameplay/Caixa_CaboRede.prefab";
    private const string ModeloCaixa = "Assets/Art/Models/caixa2.fbx";

    /// <summary>Dentro do <c>Environment_Placeholder</c> isto é só a sala de teste — fica para trás.</summary>
    private static readonly string[] SalaDeTeste =
    {
        "Floor", "Ceiling", "Wall_N", "Wall_S", "Wall_E", "Wall_W", "Porta", "Janela", "Vaso",
    };

    /// <summary>Só existe na cena de teste e não deve vir.</summary>
    private static readonly string[] Ignorar = { "boss" };

    /// <summary>Vêm da cena de teste, mas ficam ancorados na arte (não andam com o grupo).</summary>
    private static readonly string[] AncoradosNaArte = { "Impressora_Estacao", "Caixa_CaboRede", "Notebook_Estacao" };

    /// <summary>Casca da arte (filhos de <c>Escritorio</c>) que sai para dar lugar ao funcional.</summary>
    private static readonly string[] Casca = { "impressora", "caixa2", "Estacao_1 (1)", "Estacao_1 (2)" };

    private const string EscritorioArte = "Escritorio";
    private const string AncorImpressora = "impressora";
    private const string AncorCaixa = "caixa2";
    private const string AncorMesaNotebook = "mesa_2";
    private static readonly Vector3 DeslocamentoPadrao = new Vector3(16.5f, 0f, 0f);

    private const string ChaoNome = "Chao_Fase01";
    private const float ChaoEspessura = 0.3f;   // espessura do piso-colidor invisível
    private const float ChaoTopo = 0.15f;       // altura do topo do piso invisível
    private const float ChaoMaxLado = 40f;      // trava de segurança: o piso nunca fica maior que isto
    private const float VolumeAltura = 3f;      // nada acima disto entra no NavMesh (andar de cima/telhado)
    private const float VolumeEspessura = 4f;
    private const float MinPisoGrande = 20f;    // piso/teto maior que isto (x ou z) e perto do chão fica fora do NavMesh

    // --------------------------------------------------------------------- menu
    [MenuItem("Tools/E-stagiar.io/Fase 01: aplicar sistemas do escritório de teste")]
    public static void Aplicar()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CenaDestino) == null)
        {
            Montar();
            return;
        }

        Scene existente = EditorSceneManager.OpenScene(CenaDestino, OpenSceneMode.Single);
        bool temSistemas = existente.GetRootGameObjects().Any(g => g.name == "Chamados");
        if (!temSistemas)
        {
            Montar();
            return;
        }

        if (!FaseAntiga(existente))
        {
            // A cena já está montada: só garante o cronômetro da fase (idempotente) e salva.
            if (GarantirChamados(existente))
            {
                EditorSceneManager.MarkSceneDirty(existente);
                EditorSceneManager.SaveScene(existente);
                Debug.Log("Fase01Setup: " + CenaDestino + " já estava aplicada; liguei o cronômetro da fase.");
                return;
            }
            Debug.LogWarning("Fase01Setup: " + CenaDestino + " já tem os sistemas aplicados. Para refazer do zero use " +
                             "\"Tools > E-stagiar.io > Fase 01: recriar do zero (apaga a atual)\".");
            return;
        }

        Debug.Log("Fase01Setup: a Fase_01 era de uma versão anterior (piso/NavMesh antigos). Refazendo do zero.");
        Recriar();
    }

    /// <summary>
    /// Menu direto para a cena que já existe: abre a <c>Fase_01</c> e liga só o cronômetro da fase
    /// (<see cref="StageTimerHUD"/>), sem remontar nada. Útil quando a cena já foi montada por uma versão
    /// deste script que não criava o HUD.
    /// </summary>
    [MenuItem("Tools/E-stagiar.io/Fase 01: ligar o cronômetro da fase (StageTimerHUD)")]
    public static void LigarCronometro()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CenaDestino) == null)
        {
            Debug.LogWarning("Fase01Setup: " + CenaDestino + " não existe. Use \"recriar do zero\".");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;   // não perde trabalho aberto

        Scene cena = EditorSceneManager.OpenScene(CenaDestino, OpenSceneMode.Single);
        bool mexeu = GarantirChamados(cena);
        if (mexeu)
        {
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            AssetDatabase.SaveAssets();
            Debug.Log("Fase01Setup: cronômetro da fase ligado na " + CenaDestino + ".");
        }
        else
        {
            Debug.Log("Fase01Setup: a " + CenaDestino + " já tinha o cronômetro da fase.");
        }
    }

    /// <summary>
    /// Detecta uma Fase_01 feita por uma versão antiga deste script: o piso-colidor antigo tinha 80x80 (o NavMesh
    /// ficava do tamanho do mundo) e não existia em versões anteriores a ele.
    /// </summary>
    private static bool FaseAntiga(Scene cena)
    {
        GameObject chao = Achar(cena, ChaoNome);
        if (chao == null) return true;
        BoxCollider col = chao.GetComponent<BoxCollider>();
        return col != null && (col.size.x > ChaoMaxLado || col.size.z > ChaoMaxLado);
    }

    [MenuItem("Tools/E-stagiar.io/Fase 01: recriar do zero (apaga a atual)")]
    public static void Recriar()
    {
        Scene aberta = SceneManager.GetSceneByPath(CenaDestino);
        if (aberta.IsValid() && aberta.isLoaded)
        {
            if (SceneManager.sceneCount <= 1) EditorSceneManager.OpenScene(CenaArte, OpenSceneMode.Single);
            else EditorSceneManager.CloseScene(aberta, true);
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CenaDestino) != null) AssetDatabase.DeleteAsset(CenaDestino);
        Montar();
    }

    // --------------------------------------------------------------------- montagem
    private static void Montar()
    {
        // o modelo da caixa do cabo de rede é o caixa2.fbx (idempotente)
        TrocarModeloCaixa();

        bool copiou = AssetDatabase.LoadAssetAtPath<SceneAsset>(CenaDestino) != null
                      || AssetDatabase.CopyAsset(CenaArte, CenaDestino);
        if (!copiou) throw new System.Exception("Fase01Setup: não consegui copiar " + CenaArte);

        Scene destino = EditorSceneManager.OpenScene(CenaDestino, OpenSceneMode.Single);
        Scene origem = EditorSceneManager.OpenScene(CenaFuncional, OpenSceneMode.Additive);

        // deslocamento pretendido pela própria arte: a Player da Office_Placeholder já está na sala nova
        Vector3 posPlayerArte = Posicao(destino, "Player");
        Vector3 posPlayerTeste = Posicao(origem, "Player");
        Vector3 deslocamento = posPlayerArte != Vector3.zero ? posPlayerArte - posPlayerTeste : DeslocamentoPadrao;
        Debug.Log("Fase01Setup: deslocamento do grupo funcional = " + deslocamento);

        // âncoras da arte (lidas antes de mexer em qualquer coisa)
        bool temImpressora = Pegar(destino, AncorImpressora, out Vector3 posImpressora, out Quaternion rotImpressora);
        bool temCaixa = Pegar(destino, AncorCaixa, out Vector3 posCaixa, out Quaternion rotCaixa);
        bool temMesa = Pegar(destino, AncorMesaNotebook, out Vector3 posMesa, out Quaternion rotMesa);
        float topoMesa = AlturaTopo(destino, AncorMesaNotebook, posMesa);

        // casca da arte que dá lugar ao funcional
        foreach (string nome in Casca) DestruirCasca(destino, nome);

        // da sala de teste: piso, teto, paredes e enfeites não vêm
        GameObject ambiente = Raizes(origem).FirstOrDefault(g => g.name == "Environment_Placeholder");
        if (ambiente != null)
            foreach (Transform filho in ambiente.transform.Cast<Transform>().ToList())
                if (SalaDeTeste.Contains(filho.name))
                {
                    Debug.Log("Fase01Setup: fora da sala de teste: " + filho.name);
                    Object.DestroyImmediate(filho.gameObject);
                }

        // traz as raízes funcionais da cena de teste (o funcional vence a casca de mesmo nome)
        var trazidos = new List<string>();
        foreach (GameObject raiz in Raizes(origem))
        {
            if (Ignorar.Contains(raiz.name)) continue;

            foreach (GameObject antigo in Raizes(destino).Where(g => g.name == raiz.name).ToList())
            {
                Debug.Log("Fase01Setup: substituído (só casca): " + antigo.name);
                Object.DestroyImmediate(antigo);
            }

            string nome = raiz.name;
            SceneManager.MoveGameObjectToScene(raiz, destino);
            if (!AncoradosNaArte.Contains(nome)) raiz.transform.position += deslocamento;
            trazidos.Add(nome);
        }

        // âncoras: a arte vira funcional
        AplicarAncora(destino, "Impressora_Estacao", temImpressora, posImpressora, rotImpressora);
        AplicarAncora(destino, "Caixa_CaboRede", temCaixa, posCaixa, rotCaixa);
        if (temMesa) AplicarAncora(destino, "Notebook_Estacao", true,
                                   new Vector3(posMesa.x, topoMesa + 0.02f, posMesa.z), rotMesa);

        // área funcional = tudo que veio da cena de teste + o jogador + as âncoras da arte. O piso e o NavMesh saem
        // daqui, então eles cobrem exatamente onde o chefe anda (e não o mundo inteiro).
        Bounds area = new Bounds(posPlayerArte, Vector3.one);
        foreach (string nome in trazidos)
        {
            GameObject g = Achar(destino, nome);
            if (g == null) continue;
            foreach (Collider c in g.GetComponentsInChildren<Collider>(true)) area.Encapsulate(c.bounds);
        }
        if (temImpressora) area.Encapsulate(posImpressora);
        if (temCaixa) area.Encapsulate(posCaixa);
        if (temMesa) area.Encapsulate(posMesa);

        // os pontos da ronda não têm collider: entram na conta para o caminho do chefe caber no piso/NavMesh
        GameObject rotaArea = Achar(destino, "Ronda_Supervisor");
        if (rotaArea != null)
            foreach (Transform ponto in rotaArea.transform) area.Encapsulate(ponto.position);

        area.Expand(2f);

        float largura = Mathf.Min(area.size.x, ChaoMaxLado);
        float fundura = Mathf.Min(area.size.z, ChaoMaxLado);

        // piso-colidor invisível, do tamanho da ÁREA FUNCIONAL (não do mundo): é ele que o NavMesh usa como chão
        var chao = new GameObject(ChaoNome);
        SceneManager.MoveGameObjectToScene(chao, destino);
        chao.transform.position = new Vector3(area.center.x, ChaoTopo - ChaoEspessura * 0.5f, area.center.z);
        chao.AddComponent<BoxCollider>().size = new Vector3(largura, ChaoEspessura, fundura);
        Debug.Log("Fase01Setup: piso/NavMesh centrado em " + area.center + ", " + largura.ToString("0.0") +
                  " x " + fundura.ToString("0.0"));

        // NavMesh novo (só o térreo) + chefe ligado + rota reencaixada
        AssarNavMesh(destino, area.center, largura, fundura);
        PrepararNPC(destino);
        ReencaixarRonda(destino);

        // chamados da Fernanda: o grupo vem da cena de teste sem o HUD do cronômetro da fase
        GarantirChamados(destino);

        EditorSceneManager.MarkSceneDirty(destino);
        EditorSceneManager.SaveScene(destino);
        EditorSceneManager.CloseScene(origem, true);
        AssetDatabase.SaveAssets();

        Debug.Log("Fase01Setup: " + CenaDestino + " pronto. De " + CenaFuncional + " vieram " + trazidos.Count +
                  " raízes: " + string.Join(", ", trazidos));
    }

    // --------------------------------------------------------------------- chamados / cronômetro da fase
    /// <summary>
    /// O grupo funcional traz o objeto <c>Chamados</c> completo (TicketManager + StageController + celular [C]), mas o
    /// <c>StageTimerHUD</c> — o relógio da fase no canto superior direito da tela — nunca foi colocado em cena nenhuma. Sem ele o
    /// cronômetro de 15 minutos roda invisível. Aqui o HUD é criado uma vez no próprio <c>Chamados</c> e ligado ao
    /// StageController e ao TicketManager; as referências que já estiverem preenchidas não são tocadas.
    /// Devolve <c>true</c> quando mexeu em alguma coisa.
    /// </summary>
    private static bool GarantirChamados(Scene destino)
    {
        GameObject chamados = Achar(destino, "Chamados");
        if (chamados == null)
        {
            Debug.LogWarning("Fase01Setup: não veio o objeto \"Chamados\" (chamados/cronômetro da fase ficam sem HUD)");
            return false;
        }

        var stage = chamados.GetComponent<StageController>();
        var manager = chamados.GetComponent<TicketManager>();
        if (stage == null || manager == null)
        {
            Debug.LogWarning("Fase01Setup: o \"" + chamados.name + "\" está sem StageController/TicketManager; " +
                             "o cronômetro da fase não tem como funcionar");
            return false;
        }

        bool mexeu = false;

        // O cronômetro só aparece se o TicketManager estiver numa fase de verdade (0 = tutorial, sem relógio).
        var soStage = new SerializedObject(stage);
        mexeu |= Preencher(soStage, "ticketManager", manager);
        SerializedProperty fase = soStage.FindProperty("stageNumber");
        if (fase != null && fase.intValue == 0)
        {
            fase.intValue = 1;
            mexeu = true;
            Debug.Log("Fase01Setup: StageController estava no tutorial (fase 0); a Fase_01 é a fase 1");
        }
        if (mexeu) soStage.ApplyModifiedPropertiesWithoutUndo();

        var hud = chamados.GetComponent<StageTimerHUD>();
        if (hud == null)
        {
            hud = chamados.AddComponent<StageTimerHUD>();
            mexeu = true;
            Debug.Log("Fase01Setup: criei o cronômetro da fase (StageTimerHUD) no " + chamados.name);
        }

        var soHud = new SerializedObject(hud);
        bool ligou = Preencher(soHud, "stageController", stage);
        ligou |= Preencher(soHud, "ticketManager", manager);
        if (ligou) soHud.ApplyModifiedPropertiesWithoutUndo();
        return mexeu || ligou;
    }

    private static void AplicarAncora(Scene destino, string nome, bool temAncora, Vector3 pos, Quaternion rot)
    {
        if (!temAncora) return;
        GameObject g = Achar(destino, nome);
        if (g == null)
        {
            Debug.LogWarning("Fase01Setup: não veio o objeto \"" + nome + "\" da cena de teste");
            return;
        }
        g.transform.SetParent(null, true);
        g.transform.SetPositionAndRotation(pos, rot);
    }

    // --------------------------------------------------------------------- chefe (NPC)
    /// <summary>
    /// O <c>NPC_Supervisor</c> vem da cena de teste DESLIGADO (é o que deixa o chefe parado na Fase_01) e com as
    /// referências dele (rota, jogador, modelo, pontos de interesse) guardadas como override do prefab. Aqui ele é
    /// ligado e, se alguma dessas referências ficou vazia ao mudar de cena, ela é religada pelo nome.
    /// </summary>
    private static void PrepararNPC(Scene destino)
    {
        GameObject npc = Achar(destino, "NPC_Supervisor");
        if (npc == null)
        {
            Debug.LogWarning("Fase01Setup: não veio o NPC_Supervisor da cena de teste");
            return;
        }

        if (!npc.activeSelf)
        {
            npc.SetActive(true);
            Debug.Log("Fase01Setup: NPC_Supervisor ligado (vinha desligado na cena de teste)");
        }

        var patrulha = npc.GetComponent<SupervisorPatrol>();
        if (patrulha == null)
        {
            Debug.LogWarning("Fase01Setup: NPC_Supervisor sem SupervisorPatrol");
            return;
        }

        Transform rota = Transforme(destino, "Ronda_Supervisor");
        Transform player = Transforme(destino, "Player");

        var so = new SerializedObject(patrulha);
        bool mexeu = Preencher(so, "route", rota);
        mexeu |= Preencher(so, "player", player);
        if (npc.transform.childCount == 1) mexeu |= Preencher(so, "visual", npc.transform.GetChild(0));
        mexeu |= PreencherLista(so, "interestPoints", destino,
                                new[] { "Mesa_Minigames", "Bancada", "RackInternet", "Gaveteiro_Hardware" });
        if (mexeu) so.ApplyModifiedPropertiesWithoutUndo();

        var percepcao = npc.GetComponent<SupervisorPerception>();
        if (percepcao != null)
        {
            var soPercepcao = new SerializedObject(percepcao);
            if (Preencher(soPercepcao, "player", player)) soPercepcao.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>Religa uma referência SÓ se ela estiver vazia: não sobrescreve o que o prefab já trouxe certo.</summary>
    private static bool Preencher(SerializedObject so, string caminho, Object valor)
    {
        if (valor == null) return false;
        SerializedProperty p = so.FindProperty(caminho);
        if (p == null || p.propertyType != SerializedPropertyType.ObjectReference || p.objectReferenceValue != null) return false;
        p.objectReferenceValue = valor;
        Debug.Log("Fase01Setup: religuei " + so.targetObject.GetType().Name + "." + caminho + " -> " + valor.name);
        return true;
    }

    private static bool PreencherLista(SerializedObject so, string caminho, Scene cena, string[] nomes)
    {
        SerializedProperty p = so.FindProperty(caminho);
        if (p == null || !p.isArray || p.arraySize > 0) return false;

        var alvos = new List<Object>();
        foreach (string n in nomes)
        {
            GameObject g = Achar(cena, n);
            if (g != null) alvos.Add(g.transform);
        }
        if (alvos.Count == 0) return false;

        p.arraySize = alvos.Count;
        for (int i = 0; i < alvos.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = alvos[i];
        Debug.Log("Fase01Setup: religuei " + caminho + " (" + alvos.Count + " pontos de interesse)");
        return true;
    }

    private static Transform Transforme(Scene cena, string nome)
    {
        GameObject g = Achar(cena, nome);
        return g != null ? g.transform : null;
    }

    // --------------------------------------------------------------------- modelo da caixa
    /// <summary>
    /// Troca o visual da <c>Caixa_CaboRede</c> (que era um cubo escalado) pelo modelo <c>caixa2.fbx</c>,
    /// copiando malha, materiais e a orientação/escala do nó do modelo. O BoxCollider do visual acompanha
    /// os limites da malha, para continuar batendo com o que se vê.
    /// </summary>
    private static void TrocarModeloCaixa()
    {
        GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(ModeloCaixa);
        if (modelo == null)
        {
            Debug.LogWarning("Fase01Setup: não achei o modelo " + ModeloCaixa);
            return;
        }

        MeshFilter mfOrig = modelo.GetComponentInChildren<MeshFilter>();
        MeshRenderer mrOrig = mfOrig != null ? mfOrig.GetComponent<MeshRenderer>() : null;
        if (mfOrig == null || mrOrig == null || mfOrig.sharedMesh == null)
        {
            Debug.LogWarning("Fase01Setup: " + ModeloCaixa + " não tem malha/renderizador utilizável");
            return;
        }

        Mesh malha = mfOrig.sharedMesh;
        Material[] materiais = mrOrig.sharedMaterials;
        Quaternion rot = Quaternion.Inverse(modelo.transform.rotation) * mfOrig.transform.rotation;
        Vector3 esc = mfOrig.transform.lossyScale;

        GameObject raiz = PrefabUtility.LoadPrefabContents(PrefabCaixa);
        try
        {
            Transform visual = raiz.transform.Find("Visual");
            if (visual == null)
            {
                Debug.LogWarning("Fase01Setup: " + PrefabCaixa + " não tem o filho \"Visual\"");
                return;
            }

            MeshFilter mf = visual.GetComponent<MeshFilter>();
            MeshRenderer mr = visual.GetComponent<MeshRenderer>();
            if (mf == null || mr == null)
            {
                Debug.LogWarning("Fase01Setup: o Visual da caixa não tem MeshFilter/MeshRenderer");
                return;
            }

            mf.sharedMesh = malha;
            mr.sharedMaterials = materiais;
            visual.localRotation = rot;
            visual.localScale = esc;
            visual.localPosition = Vector3.zero;

            BoxCollider col = visual.GetComponent<BoxCollider>();
            if (col != null)
            {
                col.center = malha.bounds.center;
                col.size = malha.bounds.size;
            }

            PrefabUtility.SaveAsPrefabAsset(raiz, PrefabCaixa);
            Debug.Log("Fase01Setup: visual da Caixa_CaboRede = " + ModeloCaixa + " (escala " + esc + ")");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }

    // --------------------------------------------------------------------- NavMesh
    private static void AssarNavMesh(Scene destino, Vector3 centro, float largura, float fundura)
    {
        GameObject navGo = Achar(destino, "NavMesh_Escritorio");
        if (navGo == null)
        {
            navGo = new GameObject("NavMesh_Escritorio");
            SceneManager.MoveGameObjectToScene(navGo, destino);
        }
        var surface = navGo.GetComponent<NavMeshSurface>();
        if (surface == null) surface = navGo.AddComponent<NavMeshSurface>();

        // piso/teto/telhado gigantes (ex.: o chão da arte, de 35 m, ou o do andar de cima) ficam fora do NavMesh
        IgnorarPisosGrandes(destino);

        surface.collectObjects = CollectObjects.Volume;
        // ATENÇÃO: o "center" do NavMeshSurface é LOCAL ao transform dele (a Unity aplica TRS(position, rotation)).
        // Como este objeto veio deslocado da cena de teste, o centro do mundo precisa virar local.
        surface.center = navGo.transform.InverseTransformPoint(
            new Vector3(centro.x, VolumeAltura - VolumeEspessura * 0.5f, centro.z));
        surface.size = new Vector3(largura + 6f, VolumeEspessura, fundura + 6f);   // teto em y≈3: andar de cima/telhado fora
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        int mascara = ~((1 << 5) | (1 << 8));
        int preview = LayerMask.NameToLayer("ItemPreview");
        if (preview >= 0) mascara &= ~(1 << preview);
        surface.layerMask = mascara;
        surface.BuildNavMesh();

        if (surface.navMeshData == null)
        {
            Debug.LogWarning("Fase01Setup: o NavMesh saiu vazio (nenhum collider no volume)");
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<NavMeshData>(NavAsset) != null) AssetDatabase.DeleteAsset(NavAsset);
        AssetDatabase.CreateAsset(surface.navMeshData, NavAsset);
    }

    /// <summary>
    /// Marca como "não assar" os colliders que são piso/teto: muito largos (mais de <see cref="MinPisoGrande"/> em X
    /// ou Z) e que chegam perto do chão. Sem isto o NavMesh se espalha por todo o chão da arte (dezenas de metros).
    /// </summary>
    private static void IgnorarPisosGrandes(Scene destino)
    {
        foreach (Collider c in Raizes(destino).SelectMany(g => g.GetComponentsInChildren<Collider>(true)))
        {
            if (c == null || c.gameObject.name == ChaoNome) continue;
            Bounds b = c.bounds;
            if ((b.size.x > MinPisoGrande || b.size.z > MinPisoGrande) && b.min.y < 1f)
            {
                var mod = c.GetComponent<NavMeshModifier>();
                if (mod == null) mod = c.gameObject.AddComponent<NavMeshModifier>();
                mod.ignoreFromBuild = true;
                Debug.Log("Fase01Setup: fora do NavMesh (piso grande): " + Caminho(c.transform) +
                          " (" + b.size.x.ToString("0") + " x " + b.size.z.ToString("0") + ")");
            }
        }
    }

    private static void ReencaixarRonda(Scene destino)
    {
        GameObject rota = Achar(destino, "Ronda_Supervisor");
        if (rota != null)
            foreach (Transform ponto in rota.transform)
            {
                if (!NavMesh.SamplePosition(ponto.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                {
                    Debug.LogWarning("Fase01Setup: ponto fora do NavMesh: " + ponto.name + " " + ponto.position);
                    continue;
                }
                if (Mathf.Abs(hit.position.y - ponto.position.y) > 1.5f)
                {
                    Debug.LogWarning("Fase01Setup: ponto caiu em outro piso (" + ponto.name + "): " +
                                     ponto.position + " -> " + hit.position);
                    continue;                                             // não deixa o ponto subir para o andar de cima
                }
                // só corrige X/Z: a altura do ponto é a altura do NPC (o NavMeshAgent tem baseOffset)
                ponto.position = new Vector3(hit.position.x, ponto.position.y, hit.position.z);
            }

        GameObject npc = Achar(destino, "NPC_Supervisor");
        if (npc != null && rota != null && rota.transform.childCount > 0)
        {
            Vector3 alvo = rota.transform.GetChild(0).position;
            npc.transform.position = alvo;
            var agente = npc.GetComponent<NavMeshAgent>();
            if (agente != null && !agente.Warp(alvo))
                Debug.LogWarning("Fase01Setup: não consegui colocar o NPC no NavMesh em " + alvo);
        }

        // o jogador NÃO é movido: a arte já o coloca de pé no piso (mexer no Y subia ele para o andar de cima)
        GameObject player = Achar(destino, "Player");
        if (player != null && !NavMesh.SamplePosition(player.transform.position, out _, 2f, NavMesh.AllAreas))
            Debug.LogWarning("Fase01Setup: jogador longe do NavMesh: " + player.transform.position);
    }

    // --------------------------------------------------------------------- utilidades
    private static List<GameObject> Raizes(Scene cena) => cena.GetRootGameObjects().ToList();

    /// <summary>Primeiro objeto com esse nome, descendo pela hierarquia (a arte tem coisas dentro de "Escritorio").</summary>
    private static GameObject Achar(Scene cena, string nome)
    {
        foreach (GameObject raiz in cena.GetRootGameObjects())
        {
            if (raiz.name == nome) return raiz;
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name == nome) return t.gameObject;
        }
        return null;
    }

    private static Vector3 Posicao(Scene cena, string nome)
    {
        GameObject g = Achar(cena, nome);
        return g != null ? g.transform.position : Vector3.zero;
    }

    /// <summary>Altura do tampo: um raio para baixo a partir do móvel; se falhar, o topo dos colliders dele.</summary>
    private static float AlturaTopo(Scene cena, string nome, Vector3 pos)
    {
        GameObject g = Achar(cena, nome);
        if (g == null) return pos.y;

        if (Physics.Raycast(pos + Vector3.up * 2.5f, Vector3.down, out RaycastHit hit, 6f))
        {
            Debug.Log("Fase01Setup: topo de \"" + nome + "\" por raio = " + hit.point.y +
                      " (bateu em " + Caminho(hit.transform) + ")");
            return hit.point.y;
        }

        Collider[] cols = g.GetComponentsInChildren<Collider>(true);
        if (cols.Length > 0)
        {
            float topo = cols.Max(c => c.bounds.max.y);
            Debug.Log("Fase01Setup: topo de \"" + nome + "\" pelos colliders = " + topo);
            return topo;
        }

        Debug.LogWarning("Fase01Setup: não consegui medir o topo de \"" + nome + "\"; usando " + pos.y);
        return pos.y;
    }

    private static bool Pegar(Scene cena, string nome, out Vector3 pos, out Quaternion rot)
    {
        GameObject g = Achar(cena, nome);
        if (g == null)
        {
            Debug.LogWarning("Fase01Setup: não achei a âncora de arte \"" + nome + "\"");
            pos = Vector3.zero;
            rot = Quaternion.identity;
            return false;
        }
        pos = g.transform.position;
        rot = g.transform.rotation;
        return true;
    }

    /// <summary>Remove a casca da arte: só filhos de <c>Escritorio</c>, para não apagar nada de dentro de um prefab.</summary>
    private static void DestruirCasca(Scene cena, string nome)
    {
        var alvos = new List<GameObject>();
        GameObject pai = Achar(cena, EscritorioArte);
        if (pai != null)
            foreach (Transform t in pai.GetComponentsInChildren<Transform>(true))
                if (t.gameObject != pai && t.name == nome) alvos.Add(t.gameObject);
        if (alvos.Count == 0)
        {
            GameObject solto = Achar(cena, nome);
            if (solto != null) alvos.Add(solto);
        }
        foreach (GameObject g in alvos)
        {
            Debug.Log("Fase01Setup: removido " + Caminho(g.transform) + " (dá lugar ao funcional)");
            Object.DestroyImmediate(g);
        }
    }

    private static string Caminho(Transform t)
    {
        var partes = new List<string>();
        for (Transform p = t; p != null; p = p.parent) partes.Insert(0, p.name);
        return string.Join("/", partes);
    }
}
