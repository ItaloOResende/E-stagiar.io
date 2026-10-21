#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Monta a cena do tutorial <c>Tutorial_Montagem.unity</c> a partir de uma CÓPIA da Office_Placeholder (a original não é
/// aberta para salvar nem alterada). A cena fica só com a sala de TI, isolada do escritório (o cenário de fora sai e as
/// aberturas das paredes da sala são fechadas), e com o essencial do jogo: jogador, HUD, telas 2D e chamados.
/// O conjunto do fundo da sala (móvel com monitor, teclado e cadeira de testes) vira uma estação de trabalho de verdade
/// (prefab <c>Estacao_Trabalho</c>: mesa com WorkSurface + PC_Escritorio com gabinete, monitor e ponto elétrico), onde o
/// jogador monta o PC do zero pelas mecânicas reais. O chamado do tutorial fica no gabinete da estação (como nas estações
/// do escritório). A mesa começa vazia: gabinete e monitor da estação ficam no móvel do estoque (<c>Bancada_Armarios (1)</c>),
/// no lugar dos gabinetes e do monitor de reserva, que saem desta cena; teclados e mouses (pegáveis) ficam nas estantes
/// do estoque, no lugar da caixa ao lado dos teclados. A cadeira da estação sai (atrapalhava o acesso ao estoque).
/// Idempotente: refaz a cena do zero a cada execução (ajustes manuais na cena do tutorial se perdem; faça-os aqui).
/// Também cria o chamado do tutorial e põe as duas cenas no Build Settings.
/// Menu: Tools &gt; E-stagiar.io &gt; Tutorial &gt; Montar cena Tutorial_Montagem.
/// </summary>
public static class TutorialMontagemSetup
{
    private const string Origem = "Assets/Scenes/Office_Placeholder.unity";
    private const string Destino = "Assets/Scenes/Tutorial_Montagem.unity";
    private const string Chamado = "Assets/TicketsData/Tutorial/Tutorial_Montagem.asset";
    private const string PrefabEstacao = "Assets/Prefabs/Gameplay/Estacao_Trabalho.prefab";

    // Raízes da Office_Placeholder que não fazem parte do tutorial (estações, impressoras, NPCs e rotas).
    private static readonly string[] RaizesRemovidas =
    {
        "Estacoes_Trabalho", "Decoracao_Estacoes", "Area_Impressoras", "Cartaz_IP_Impressora", "Navegacao_NPCs",
        "Ronda_Supervisor", "Ronda_Fora_Da_Demo"
    };
    // Itens da sala que a estação do tutorial substitui: o gabinete e os cabos soltos da bancada de manutenção (a chave
    // de fenda e o alicate ficam) e o conjunto do fundo (móvel, monitor, teclado, cadeira e testador sobre ele).
    private static readonly string[] ItensRemovidos =
    {
        "Gabinete_Bancada", "CaboEnergia_Bancada", "CaboRede_Bancada",
        "Monitor_Reserva_2", "Teclado_Testes", "Cadeira_Bancada", "Testador_CaboRede",
        // reservas do móvel do estoque: o lugar delas é ocupado pelo equipamento da estação (GuardarNoEstoque)
        "Gabinete_Reserva_1", "Gabinete_Reserva_2", "Monitor_Reserva_1"
    };
    // Raízes de cenário do escritório: delas só fica o que toca a sala de TI (paredes, piso, teto, decoração) e as luzes.
    private static readonly string[] RaizesDeCenario =
        { "MAPA", "Escritorio", "Demo_Parede_Corredor", "Mesa_Minigames", "Interactables_Test", "Conduta", "bebedouro", "bebedouro_1" };

    [MenuItem("Tools/E-stagiar.io/Tutorial/Montar cena Tutorial_Montagem")]
    public static void Montar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[Tutorial] Saia do Play antes."); return; }
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var log = new StringBuilder();
        CriarChamado();

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Destino) != null) AssetDatabase.DeleteAsset(Destino);
        if (!AssetDatabase.CopyAsset(Origem, Destino)) { Debug.LogError("[Tutorial] Não consegui copiar " + Origem); return; }
        Scene cena = EditorSceneManager.OpenScene(Destino, OpenSceneMode.Single);
        // referência válida depois da cópia da cena (a instância criada antes dela não serve para a cena salva)
        TicketDefinition chamado = AssetDatabase.LoadAssetAtPath<TicketDefinition>(Chamado);

        // 0) caixa da sala de TI (pelos móveis dela), medida antes de qualquer remoção
        Bounds sala = Limites(Achar(cena, "Sala_TI").transform);
        sala.Encapsulate(Limites(Achar(cena, "Sala_TI_Complementos").transform));
        log.AppendLine($"sala de TI: {sala.min:F2} .. {sala.max:F2}");

        // 1) estação de montagem REAL no lugar do conjunto do fundo (o Bancada_Armarios mais ao fundo da sala)
        PcTicketBridge molde = Raizes(cena).SelectMany(r => r.GetComponentsInChildren<PcTicketBridge>(true)).FirstOrDefault();
        GameObject movel = Raizes(cena).SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .Where(t => t.name == "Bancada_Armarios").Select(t => t.gameObject).OrderByDescending(g => g.transform.position.z).FirstOrDefault();
        if (molde == null || movel == null) { Debug.LogError("[Tutorial] PC de estação ou Bancada_Armarios não encontrados."); return; }
        Bounds lugar = Limites(movel.transform);
        var estacao = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabEstacao), cena);
        estacao.name = "Estacao_Montagem_Tutorial";
        estacao.transform.SetParent(Achar(cena, "Sala_TI").transform, true);
        PosicionarEstacao(estacao.transform, lugar, sala, log);
        // o PC da estação = a instância de PC_Escritorio (raiz com gabinete, monitor e tomada). A ponte do chamado vai no
        // GABINETE (o objeto com ComputerCase/ComputerPower): na raiz do PC o RequireComponent criava um segundo
        // ComputerCase, que o tutorial observava enquanto o jogador abria o gabinete de verdade.
        ComputerPower energia = estacao.GetComponentInChildren<ComputerPower>(true);
        GameObject pc = estacao.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject)
            .FirstOrDefault(g => g.name.StartsWith("PC_Escritorio")) ?? energia.transform.parent.gameObject;
        GameObject gabinete = energia.gameObject;
        var ponte = gabinete.GetComponent<PcTicketBridge>() ?? gabinete.AddComponent<PcTicketBridge>();
        EditorUtility.CopySerialized(molde, ponte);
        var so = new SerializedObject(ponte);
        so.FindProperty("deviceLabel").stringValue = "PC da estação de montagem";
        var ids = so.FindProperty("acceptedTicketIds"); ids.ClearArray(); ids.InsertArrayElementAtIndex(0); ids.GetArrayElementAtIndex(0).stringValue = "tutorial-montagem";
        so.FindProperty("prepareProblemOnClaim").boolValue = true;
        so.FindProperty("mesaDaEstacao").objectReferenceValue = estacao.GetComponentInChildren<WorkSurface>(true);
        so.ApplyModifiedPropertiesWithoutUndo();
        log.AppendLine($"estação em {estacao.transform.position} rotY {estacao.transform.eulerAngles.y:0}; PC '{pc.name}'; " +
                       $"WorkSurface={estacao.GetComponentInChildren<WorkSurface>(true) != null}; tomadas={estacao.GetComponentsInChildren<WallConnectionPoint>(true).Length}");

        // 2) remove o que não faz parte do tutorial
        foreach (GameObject r in Raizes(cena))
        {
            bool npc = r.GetComponentInChildren<NpcCaminhada>(true) != null || r.GetComponentInChildren<NpcSentado>(true) != null
                       || r.GetComponentInChildren<SupervisorPatrol>(true) != null || r.name.StartsWith("npc") || r.name.StartsWith("NPC_");
            if (npc || RaizesRemovidas.Contains(r.name)) { log.AppendLine("removido: " + r.name); Object.DestroyImmediate(r); }
        }
        Desempacotar(movel); Object.DestroyImmediate(movel); log.AppendLine("removido da sala: Bancada_Armarios (fundo)");
        GuardarNoEstoque(cena, pc.transform, gabinete.transform, log);
        PrepararEstantes(cena, pc.transform, log, out List<Transform> teclados, out List<Transform> mouses);
        Transform cadeira = estacao.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Cadeira");
        if (cadeira != null) { Desempacotar(cadeira.gameObject); Object.DestroyImmediate(cadeira.gameObject); log.AppendLine("removido da sala: Cadeira da estação"); }
        foreach (string nome in ItensRemovidos)
        {
            GameObject item = Achar(cena, nome);
            if (item != null) { Desempacotar(item); log.AppendLine("removido da sala: " + nome); Object.DestroyImmediate(item); }
        }
        foreach (AssemblyPickupPoint p in Raizes(cena).SelectMany(r => r.GetComponentsInChildren<AssemblyPickupPoint>(true)).ToArray())
        { Desempacotar(p.gameObject, true); Object.DestroyImmediate(p); }

        // 3) sala isolada: sai o cenário do resto do escritório e as aberturas das paredes da sala são fechadas
        IsolarSala(cena, sala, log);
        FecharAberturas(sala, log);

        // 4) chamados: fase 0 (tutorial), começa sozinha, sem menu e sem relatório de expediente
        StageController stage = Componente<StageController>(cena);
        TicketManager tickets = Componente<TicketManager>(cena);
        Definir(stage, "stageNumber", p => p.intValue = 0);
        Definir(stage, "startAutomatically", p => p.boolValue = true);
        Definir(stage, "showStartMenu", p => p.boolValue = false);
        Definir(tickets, "tutorialTicket", p => p.objectReferenceValue = chamado);
        Definir(tickets, "requirePlayerToFinish", p => p.boolValue = true);
        StageReportScreen relatorio = Componente<StageReportScreen>(cena);
        if (relatorio != null) Definir(relatorio, "showOnStageEnd", p => p.boolValue = false);
        var guia = stage.GetComponent<TutorialMontagem>() ?? stage.gameObject.AddComponent<TutorialMontagem>();
        var tablet = Componente<TicketTabletPrototype>(cena);
        Definir(guia, "tickets", p => p.objectReferenceValue = tickets);
        Definir(guia, "stage", p => p.objectReferenceValue = stage);
        if (tablet != null) Definir(guia, "fonte", p => p.objectReferenceValue = new SerializedObject(tablet).FindProperty("fonte").objectReferenceValue);
        Definir(guia, "alvoBancada", p => p.objectReferenceValue = pc.transform);
        Definir(guia, "alvoMesa", p => p.objectReferenceValue = Transf(estacao.GetComponentInChildren<WorkSurface>(true)?.gameObject));
        Definir(guia, "alvoChave", p => p.objectReferenceValue = Transf(Achar(cena, "ChaveFenda_Bancada")));
        Definir(guia, "teclados", p => Lista(p, teclados));
        Definir(guia, "mouses", p => Lista(p, mouses));
        Definir(guia, "alvoFerramentas", p => p.objectReferenceValue = Transf(Achar(cena, "Bancada_Manutencao")));
        Definir(guia, "alvoHardware", p => p.objectReferenceValue = Transf(Achar(cena, "Gaveteiro3_Hardware")));
        Definir(guia, "alvoCabos", p => p.objectReferenceValue = Transf(Achar(cena, "Gaveteiro3_Cabos")));
        Definir(guia, "alvoSuprimentos", p => p.objectReferenceValue = Transf(Achar(cena, "Gaveteiro3_Suprimentos")));
        log.AppendLine("chamado do tutorial: " + (chamado != null ? AssetDatabase.GetAssetPath(chamado) : "NULO"));

        // 5) jogador dentro da sala, a 1 m da frente da mesa da estação, olhando para ela
        GameObject jogador = Achar(cena, "Player");
        if (jogador != null)
        {
            Bounds mesa = Limites(estacao.GetComponentInChildren<WorkSurface>(true).transform);
            Vector3 frente = -estacao.transform.forward; frente = Mathf.Abs(frente.x) > Mathf.Abs(frente.z) ? new Vector3(Mathf.Sign(frente.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(frente.z));
            Vector3 paraSala = sala.center - mesa.center; if (Vector3.Dot(paraSala, frente) < 0f) frente = -frente;
            Vector3 borda = mesa.center + Vector3.Scale(mesa.extents, frente);
            Vector3 pos = new Vector3(borda.x, jogador.transform.position.y, borda.z) + frente * 1.9f;
            jogador.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-frente));
            log.AppendLine("jogador em " + pos);
        }

        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        ConfigurarBuild();
        AssetDatabase.SaveAssets();
        File.WriteAllText("Temp/tutorial_setup.txt", log.ToString());
        Debug.Log("[Tutorial] Cena montada: " + Destino + "\n" + log);
    }

    private static void CriarChamado()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Chamado));
        var t = AssetDatabase.LoadAssetAtPath<TicketDefinition>(Chamado);
        if (t == null) { t = ScriptableObject.CreateInstance<TicketDefinition>(); AssetDatabase.CreateAsset(t, Chamado); }
        t.ticketId = "tutorial-montagem";
        t.title = "Monte um PC completo";
        t.description = "A estação de montagem precisa de um computador funcionando: monte o PC, ligue os cabos e instale o sistema.";
        t.requester = "Supervisor da TI";
        t.department = "Sala de TI";
        t.minimumStage = 0;
        t.tutorialOnly = true;
        t.slaSecondsOverride = 3600f;
        t.steps = new List<TicketStepDefinition>
        {
            Passo("abrir-gabinete", "Abrir o gabinete"),
            Passo("instalar-placa-mae", "Instalar a placa-mãe"),
            Passo("instalar-ram", "Instalar a memória RAM"),
            Passo("instalar-cooler", "Instalar o cooler"),
            Passo("instalar-fonte", "Instalar a fonte"),
            Passo("instalar-hd", "Instalar o HD"),
            Passo("fechar-gabinete", "Fechar o gabinete"),
            Passo("posicionar-equipamentos", "Levar o gabinete e o monitor até a estação de trabalho"),
            Passo("conectar-energia", "Ligar a energia do PC e do monitor"),
            Passo("conectar-video", "Ligar o cabo de vídeo"),
            Passo("conectar-rede", "Ligar o cabo de rede"),
            Passo("instalar-sistema", "Instalar o sistema operacional"),
            Passo("validar-funcionamento", "Confirmar que o PC chega ao sistema"),
        };
        EditorUtility.SetDirty(t);
        AssetDatabase.SaveAssets();
    }
    private static TicketStepDefinition Passo(string id, string texto) => new TicketStepDefinition { id = id, description = texto, weight = 1 };

    // Office_Placeholder e Tutorial_Montagem no Build (a principal primeiro: é a que abre o jogo).
    public static void ConfigurarBuild()
    {
        var cenas = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(Origem, true), new EditorBuildSettingsScene(Destino, true) };
        foreach (var c in EditorBuildSettings.scenes)
            if (c.path != Origem && c.path != Destino && File.Exists(c.path) && c.path != "Assets/Scenes/SampleScene.unity") cenas.Add(c);
        EditorBuildSettings.scenes = cenas.ToArray();
    }

    // Estação no lugar do móvel antigo: encostada na mesma parede, com a cadeira (frente da mesa) virada para o meio da sala.
    private static void PosicionarEstacao(Transform estacao, Bounds lugar, Bounds sala, StringBuilder log)
    {
        estacao.rotation = Quaternion.identity;
        Transform mesa = estacao.GetComponentInChildren<WorkSurface>(true).transform;
        Transform cadeira = estacao.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.Contains("Cadeira")) ?? mesa;
        Vector3 frenteLocal = Centro(cadeira) - Centro(mesa); frenteLocal.y = 0f;
        Vector3 paraSala = sala.center - lugar.center; paraSala.y = 0f;
        // a frente aponta para longe da parede mais próxima do móvel (eixo dominante, normalizado pelo tamanho da sala)
        Vector3 frenteDesejada = Mathf.Abs(paraSala.z) / sala.extents.z >= Mathf.Abs(paraSala.x) / sala.extents.x
            ? new Vector3(0f, 0f, Mathf.Sign(paraSala.z)) : new Vector3(Mathf.Sign(paraSala.x), 0f, 0f);
        float giro = Vector3.SignedAngle(frenteLocal.sqrMagnitude > 0.001f ? frenteLocal : Vector3.back, frenteDesejada, Vector3.up);
        giro = Mathf.Round(giro / 90f) * 90f;   // encostada na parede: giro em múltiplos de 90
        estacao.rotation = Quaternion.Euler(0f, giro, 0f);
        // encosta o fundo da mesa onde estava o fundo do móvel antigo e centra no comprimento dele
        Bounds m = Limites(mesa);
        Vector3 desvio = Vector3.zero;
        if (frenteDesejada.z != 0f)
        {
            desvio.x = lugar.center.x - m.center.x;
            desvio.z = frenteDesejada.z < 0f ? lugar.max.z - m.max.z : lugar.min.z - m.min.z;
        }
        else
        {
            desvio.z = lugar.center.z - m.center.z;
            desvio.x = frenteDesejada.x < 0f ? lugar.max.x - m.max.x : lugar.min.x - m.min.x;
        }
        desvio.y = lugar.min.y - Limites(mesa).min.y;
        estacao.position += desvio;
        log.AppendLine($"móvel antigo {lugar.min:F2}..{lugar.max:F2}; mesa nova {Limites(mesa).min:F2}..{Limites(mesa).max:F2}; giro {giro:0}; frente {frenteDesejada}");
    }

    // Mesa vazia: gabinete e monitor da estação vão para o móvel do estoque, no lugar das reservas (medidas aqui antes de
    // saírem da cena); continuam filhos do PC_Escritorio. O monitor fica de frente para a sala, apoiado no tampo.
    private static void GuardarNoEstoque(Scene cena, Transform pc, Transform gabinete, StringBuilder log)
    {
        GameObject gab1 = Achar(cena, "Gabinete_Reserva_1"), mon = Achar(cena, "Monitor_Reserva_1"), movel = Achar(cena, "Bancada_Armarios (1)");
        Transform monitor = pc.Find("Monitor");
        if (gab1 == null || mon == null || movel == null || monitor == null)
        { Debug.LogError("[Tutorial] Reservas do estoque ou monitor da estação não encontrados; a mesa não foi esvaziada."); return; }
        PegadaDoMonitor(monitor, log);
        Bounds g1 = Limites(gab1.transform), m = Limites(mon.transform);
        float tampo = Limites(movel.transform).max.y;
        Colocar(gabinete, gab1.transform.rotation, g1.center, tampo);
        Colocar(monitor, Quaternion.Euler(0f, 180f, 0f) * monitor.rotation, m.center, tampo);
        foreach (Transform t in new[] { gabinete, monitor })
            log.AppendLine($"no estoque: {t.name} {Limites(t).min:F2}..{Limites(t).max:F2}");
    }

    // O monitor era segurado pela rotação de repouso (a pose na mão gira junto com o giro em que ele estava apoiado):
    // guardado no estoque, virado para a sala, ia para a mão de costas. Passa a usar a empunhadura (pose relativa aos
    // eixos do próprio monitor, igual em qualquer giro), com a mesma pose que ele tinha ao ser pego na mesa da estação
    // olhando para ela. Só neste monitor (override da instância nesta cena).
    private static void PegadaDoMonitor(Transform monitor, StringBuilder log)
    {
        var pega = monitor.GetComponent<PickupInteractable>();
        if (pega == null) return;
        var so = new SerializedObject(pega);
        if (so.FindProperty("useGripPoint").boolValue) return;
        Vector3 euler = so.FindProperty("holdEulerOffset").vector3Value;
        Quaternion naMao = Quaternion.Euler(euler) * monitor.rotation;   // jogador de frente para a mesa (olhando +Z)
        so.FindProperty("useGripPoint").boolValue = true;
        so.FindProperty("gripLocalPoint").vector3Value = monitor.InverseTransformPoint(Limites(monitor).center);
        so.FindProperty("holdEulerOffset").vector3Value = naMao.eulerAngles;
        so.ApplyModifiedPropertiesWithoutUndo();
        log.AppendLine($"monitor: empunhadura {naMao.eulerAngles:F0} (antes {euler:F0} sobre o giro de repouso)");
    }

    // Estantes do estoque: os teclados ficam pegáveis (mesmos componentes do teclado da estação; o de cima da pilha com
    // física, para cair na prateleira quando o de baixo sai); a caixa ao lado deles sai e no lugar dela ficam dois mouses
    // (cópias do mouse da estação). O teclado e o mouse da própria estação saem da cena.
    private static void PrepararEstantes(Scene cena, Transform pc, StringBuilder log, out List<Transform> teclados, out List<Transform> mouses)
    {
        teclados = new List<Transform>(); mouses = new List<Transform>();
        Transform modeloTeclado = pc.Find("Teclado"), modeloMouse = pc.Find("Mouse");
        if (modeloTeclado == null || modeloMouse == null) { Debug.LogError("[Tutorial] Teclado/mouse da estação não encontrados."); return; }
        var todos = Raizes(cena).SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        var estoque = todos.Where(t => t.name.StartsWith("Teclado_Estoque")).ToList();
        var caixas = todos.Where(t => t.name.StartsWith("Caixa_Estoque")).ToList();
        Physics.SyncTransforms();
        var empilhados = new HashSet<Transform>();
        foreach (Transform teclado in estoque)
        {
            foreach (Component c in modeloTeclado.GetComponents<Component>())
            {
                if (c is Transform || c is Collider || c is Renderer || c is MeshFilter || teclado.GetComponent(c.GetType()) != null) continue;
                UnityEditorInternal.ComponentUtility.CopyComponent(c);
                UnityEditorInternal.ComponentUtility.PasteComponentAsNew(teclado.gameObject);
            }
            Bounds b = Limites(teclado);
            bool empilhado = estoque.Any(o => o != teclado && Mathf.Abs(Limites(o).max.y - b.min.y) < 0.02f && Limites(o).Intersects(new Bounds(b.center - Vector3.up * 0.03f, b.size)));
            if (empilhado)
            {
                empilhados.Add(teclado);
                Definir(teclado.GetComponent<PickupInteractable>(), "startsFrozen", p => p.boolValue = false);
                var corpo = teclado.GetComponent<Rigidbody>(); if (corpo != null) corpo.isKinematic = false;
            }
            teclados.Add(teclado);
            log.AppendLine($"teclado pegável: {teclado.name} {(empilhado ? "(em cima de outro, com física)" : "")}");
        }
        // a caixa da mesma prateleira, ao lado dos teclados de cada estante (pelo teclado de baixo da pilha)
        foreach (Transform teclado in estoque)
        {
            if (empilhados.Contains(teclado)) continue;
            Bounds b = Limites(teclado);
            Transform caixa = caixas.Where(c => c != null && Mathf.Abs(Limites(c).min.y - b.min.y) < 0.05f && Vector3.Distance(Limites(c).center, b.center) < 1.2f)
                                    .OrderBy(c => Vector3.Distance(Limites(c).center, b.center)).FirstOrDefault();
            if (caixa == null) continue;
            Bounds cb = Limites(caixa);
            Vector3 ao = cb.center - b.center; ao.y = 0f; ao = Mathf.Abs(ao.x) > Mathf.Abs(ao.z) ? new Vector3(Mathf.Sign(ao.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(ao.z));
            float prateleira = cb.min.y;
            if (Physics.Raycast(new Vector3(cb.center.x, cb.max.y, cb.center.z), Vector3.down, out RaycastHit h, 1f, ~0, QueryTriggerInteraction.Ignore)) prateleira = h.point.y;
            caixas.Remove(caixa);
            Desempacotar(caixa.gameObject); log.AppendLine("removido da estante: " + caixa.name); Object.DestroyImmediate(caixa.gameObject);
            foreach (float lado in new[] { -0.18f, 0.18f })
            {
                var copia = Object.Instantiate(modeloMouse.gameObject, teclado.parent);
                copia.name = "Mouse_Estoque_" + (mouses.Count + 1);
                Colocar(copia.transform, teclado.rotation, cb.center + ao * lado, prateleira + 0.002f);
                mouses.Add(copia.transform);
                log.AppendLine($"mouse na estante: {copia.name} {Limites(copia.transform).min:F2}..{Limites(copia.transform).max:F2}");
            }
        }
        foreach (Transform t in new[] { modeloTeclado, modeloMouse }) { Desempacotar(t.gameObject); Object.DestroyImmediate(t.gameObject); }
    }
    private static void Lista(SerializedProperty p, List<Transform> itens)
    {
        p.ClearArray();
        for (int i = 0; i < itens.Count; i++) { p.InsertArrayElementAtIndex(i); p.GetArrayElementAtIndex(i).objectReferenceValue = itens[i]; }
    }

    // gira e apoia o item: centro (x, z) do volume no ponto dado e a base na altura dada
    private static void Colocar(Transform item, Quaternion rotacao, Vector3 centro, float baseY)
    {
        item.rotation = rotacao;
        Bounds b = Limites(item);
        item.position += new Vector3(centro.x - b.center.x, baseY - b.min.y, centro.z - b.center.z);
    }

    // Tira do cenário do escritório tudo o que não toca a sala de TI (folga de 0,5 m); luzes ficam.
    private static void IsolarSala(Scene cena, Bounds sala, StringBuilder log)
    {
        Bounds caixa = sala; caixa.Expand(new Vector3(1f, 40f, 1f));
        int removidos = 0;
        foreach (GameObject raiz in cena.GetRootGameObjects().Where(r => RaizesDeCenario.Contains(r.name)).ToArray())
        {
            bool algumaPerto = raiz.GetComponentsInChildren<Renderer>(true).Any(r => caixa.Intersects(r.bounds))
                            || raiz.GetComponentsInChildren<Collider>(true).Any(c => caixa.Intersects(c.bounds));
            if (!algumaPerto && raiz.GetComponentInChildren<Light>(true) == null) { Object.DestroyImmediate(raiz); removidos++; continue; }
            // de baixo para cima: objeto com visual/colisor longe da sala, sem luz e sem nada perto embaixo dele, sai
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true).Reverse().ToArray())
            {
                if (t == null || t == raiz.transform) continue;
                if (t.GetComponentInChildren<Light>(true) != null) continue;
                var rs = t.GetComponentsInChildren<Renderer>(true); var cs = t.GetComponentsInChildren<Collider>(true);
                if (rs.Length == 0 && cs.Length == 0) continue;
                if (rs.Any(r => caixa.Intersects(r.bounds)) || cs.Any(c => caixa.Intersects(c.bounds))) continue;
                Desempacotar(t.gameObject);
                if (t != null) { Object.DestroyImmediate(t.gameObject); removidos++; }
            }
        }
        log.AppendLine("cenário fora da sala removido: " + removidos + " objetos");
    }

    // Procura, ao longo das quatro paredes da sala, trechos sem parede (raios de dentro para fora em duas alturas) e os
    // fecha com uma parede do mesmo material da parede vizinha. Sem abertura, não cria nada.
    private static void FecharAberturas(Bounds sala, StringBuilder log)
    {
        Physics.SyncTransforms();
        var pai = new GameObject("Paredes_Fechamento_Tutorial");
        int criadas = 0;
        var lados = new[] { (Vector3.back, Vector3.right), (Vector3.forward, Vector3.right), (Vector3.left, Vector3.forward), (Vector3.right, Vector3.forward) };
        foreach (var (normal, ao) in lados)
        {
            float ini = Vector3.Dot(sala.min, ao), fim = Vector3.Dot(sala.max, ao);
            Vector3 borda = sala.center + Vector3.Scale(sala.extents, normal);   // linha dos móveis encostados nessa parede
            Material material = null; float paredeEm = float.NaN, inicioBuraco = float.NaN;
            var trechos = new List<(float, float)>();
            for (float u = ini + 0.1f; u <= fim - 0.1f + 1e-3f; u += 0.1f)
            {
                Vector3 p = borda - normal * 0.6f; p += ao * (u - Vector3.Dot(p, ao));
                bool parede = false;
                foreach (float y in new[] { 1.1f, 2.0f })
                {
                    // só conta parede (altura cheia): um móvel na frente de uma abertura não a esconde
                    foreach (RaycastHit h in Physics.RaycastAll(new Vector3(p.x, y, p.z), normal, 1.6f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (h.collider.bounds.size.y < 3f) continue;
                        parede = true;
                        var mr = h.collider.GetComponent<MeshRenderer>();
                        if (mr != null) { material = mr.sharedMaterial; paredeEm = Vector3.Dot(h.point, normal); }
                    }
                }
                if (!parede && float.IsNaN(inicioBuraco)) inicioBuraco = u;
                if (parede && !float.IsNaN(inicioBuraco)) { trechos.Add((inicioBuraco, u)); inicioBuraco = float.NaN; }
            }
            if (!float.IsNaN(inicioBuraco)) trechos.Add((inicioBuraco, fim));
            foreach (var (a, b) in trechos.Where(t => t.Item2 - t.Item1 >= 0.3f))
            {
                var parede = GameObject.CreatePrimitive(PrimitiveType.Cube);
                parede.name = "Parede_Fechamento";
                parede.transform.SetParent(pai.transform, false);
                float linha = float.IsNaN(paredeEm) ? Vector3.Dot(borda, normal) + 0.45f : paredeEm + 0.12f;
                Vector3 c = ao * ((a + b) * .5f) + normal * linha; c.y = 2.1f;
                parede.transform.position = c;
                Vector3 escala = ao * (b - a + 0.2f) + normal * 0.24f;
                parede.transform.localScale = new Vector3(Mathf.Abs(escala.x), 4.2f, Mathf.Abs(escala.z));
                if (material != null) parede.GetComponent<MeshRenderer>().sharedMaterial = material;
                GameObjectUtility.SetStaticEditorFlags(parede, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
                criadas++;
                log.AppendLine($"abertura fechada: lado {normal} de {a:0.00} a {b:0.00} (parede em {linha:0.00})");
            }
        }
        if (criadas == 0) { Object.DestroyImmediate(pai); log.AppendLine("nenhuma abertura encontrada nas paredes da sala"); }
    }

    private static Transform Transf(GameObject g) => g != null ? g.transform : null;
    private static Vector3 Centro(Transform t) => Limites(t).center;
    private static Bounds Limites(Transform t)
    {
        Bounds b = new Bounds(t.position, Vector3.zero); bool tem = false;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
        { if (!tem) { b = r.bounds; tem = true; } else b.Encapsulate(r.bounds); }
        return b;
    }
    private static IEnumerable<GameObject> Raizes(Scene cena) => cena.GetRootGameObjects();
    private static GameObject Achar(Scene cena, string nome)
    {
        foreach (GameObject r in cena.GetRootGameObjects())
            foreach (Transform t in r.GetComponentsInChildren<Transform>(true))
                if (t.name == nome) return t.gameObject;
        return null;
    }
    private static T Componente<T>(Scene cena) where T : Component
        => cena.GetRootGameObjects().Select(r => r.GetComponentInChildren<T>(true)).FirstOrDefault(c => c != null);
    private static void Definir(Object alvo, string campo, System.Action<SerializedProperty> valor)
    {
        if (alvo == null) return;
        var so = new SerializedObject(alvo); var p = so.FindProperty(campo);
        if (p == null) { Debug.LogWarning($"[Tutorial] {alvo.GetType().Name}.{campo} não existe."); return; }
        valor(p); so.ApplyModifiedPropertiesWithoutUndo();
    }
    // Para destruir um objeto/componente que está dentro de um prefab instanciado na cena, ele precisa ser desempacotado.
    // inteiro = true: desempacota também quando o próprio objeto é a raiz da instância (para remover um componente dele).
    private static void Desempacotar(GameObject go, bool inteiro = false)
    {
        for (int i = 0; i < 8 && go != null && PrefabUtility.IsPartOfPrefabInstance(go); i++)
        {
            GameObject externo = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            if (externo == go && !inteiro) return;
            PrefabUtility.UnpackPrefabInstance(externo, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        }
    }
}
#endif
