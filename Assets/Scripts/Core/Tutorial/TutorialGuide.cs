using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Tutorial nao intrusivo, orientado pelas etapas REAIS do TicketManager/PcTicketBridge.
/// Nao conclui etapas por conta propria: somente observa os resultados dos minigames.
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialGuide : MonoBehaviour
{
    private TicketManager tickets;
    private StageController stage;
    private TicketTabletPrototype tablet;
    private TicketInstance ticket;
    private PcTicketBridge pc;
    private StationEmployee employee;
    private bool visitedEmployee;
    private int hintLevel;
    private string currentObjective = "";
    private bool confirmSkip;
    private GUIStyle panelStyle, whiteStyle, titleStyle, buttonStyle, markerStyle;
    private Texture2D panelTexture;
    private static readonly Dictionary<string, string[]> Hints = new Dictionary<string, string[]>
    {
        {"posicionar-equipamentos", new[]{"O computador novo ainda precisa ser levado ate a mesa. Procure a area de suprimentos da TI.", "Pegue o gabinete e o monitor no ponto de retirada e leve cada um ate a mesa do solicitante.", "Use a interacao de pegar/soltar do jogo. Apoie ambos na bancada da mesma estacao que abriu o chamado."}},
        {"abrir-gabinete", new[]{"Antes de instalar pecas, e preciso acessar o interior do gabinete.", "Observe o gabinete e procure os parafusos e a interacao de abertura.", "Use a chave de fenda nos parafusos exigidos e entao abra a tampa na tela de manutencao."}},
        {"instalar-placa-mae", new[]{"A placa-mae e a base na qual outros componentes se conectam.", "Procure a placa-mae nas gavetas de componentes e leve-a para o gabinete aberto.", "No minigame de manutencao, selecione ou posicione a placa-mae no encaixe indicado."}},
        {"instalar-fonte", new[]{"Sem fonte o computador nao recebe energia.", "Pegue uma fonte no gaveteiro de hardware e examine o gabinete aberto.", "Use a interacao de instalar a fonte no lugar reservado para ela."}},
        {"instalar-cooler", new[]{"O processador precisa de refrigeracao.", "Procure o cooler do processador nas pecas de reposicao.", "Posicione-o no lugar indicado pelo minigame de montagem."}},
        {"instalar-ram", new[]{"A memoria RAM e essencial para iniciar o computador.", "Encontre a RAM na gaveta de componentes; confira a aba Computador no tablet.", "Com o gabinete aberto, encaixe a RAM no slot exibido na interface de manutencao."}},
        {"instalar-hd", new[]{"O sistema operacional precisa de armazenamento.", "Procure a unidade de armazenamento na gaveta de hardware.", "Pegue a unidade e use o encaixe de armazenamento do minigame do gabinete."}},
        {"fechar-gabinete", new[]{"Hora de proteger o que voce montou.", "Feche a tampa do gabinete antes de entregar a maquina.", "Recoloque todos os parafusos obrigatorios: um gabinete incompleto nao passa na entrega."}},
        {"conectar-energia", new[]{"O gabinete e o monitor precisam de alimentacao.", "Consulte o app Cabos e confira as duas pontas dos cabos de energia.", "Conecte energia do PC e do monitor aos conectores correspondentes e a tomada."}},
        {"conectar-video", new[]{"O monitor precisa receber a imagem do PC.", "O jogo permite conexao de video por VGA ou HDMI.", "Pegue o cabo de video e ligue uma ponta ao computador e outra ao monitor."}},
        {"conectar-rede", new[]{"A rede permite a comunicacao da estacao com o escritorio.", "Procure o cabo de rede com conectores RJ45.", "Ligue o cabo de rede na porta do computador e no ponto de rede da estacao."}},
        {"instalar-sistema", new[]{"Um computador montado ainda precisa de um sistema operacional.", "Procure a opcao de instalacao do sistema no equipamento; pode haver uma ferramenta ou pendrive para isso.", "Use a interacao de instalacao do sistema do proprio jogo e confirme a conclusao antes de testar."}},
        {"validar-funcionamento", new[]{"Montar nao basta: teste a estacao.", "Tente ligar o PC e observe o resultado no monitor.", "Confirme que o equipamento chegou a tela de sistema operacional, e nao apenas a BIOS."}}
    };

    private void Awake()
    {
        tickets = GetComponent<TicketManager>();
        stage = GetComponent<StageController>();
        tablet = GetComponent<TicketTabletPrototype>();
    }

    private void Start()
    {
        if (stage != null) stage.StageEnded += OnStageEnded;
    }

    private void OnDestroy()
    {
        if (stage != null) stage.StageEnded -= OnStageEnded;
        if (panelTexture != null) Destroy(panelTexture);
    }
    private void OnStageEnded() { currentObjective = ""; }

    private PlayerMovement movement;

    private void Update()
    {
        if (stage == null || tickets == null) return;
        // O relatorio pode solicitar "Ir ao menu" e recarregar a cena tutorial.
        // Nesse caso redirecionamos ao menu do expediente normal, nao a um segundo tutorial.
        if (MainMenuScreen.Showing && SceneManager.GetActiveScene().name == "Tutorial_Estagiar")
        { LoadNormalScene(); return; }
        if (!stage.IsRunning || tickets.StageNumber != 0) return;
        if (ticket == null && tickets.Tickets.Count > 0) ticket = tickets.Tickets[0];
        if (ticket == null) return;
        if (ticket.Status == TicketStatus.InProgress && pc == null) FindOwner();
        if (employee != null && !visitedEmployee)
        {
            if (movement == null) movement = FindAnyObjectByType<PlayerMovement>();   // busca só até achar (antes: todo quadro)
            if (movement != null && Vector3.Distance(movement.transform.position, employee.transform.position) < 4.5f)
                visitedEmployee = true;
        }
        string next = Objective();
        if (next != currentObjective) { currentObjective = next; hintLevel = 0; }
        if (Keyboard.current != null)
        {
            if (Keyboard.current.hKey.wasPressedThisFrame) hintLevel = Math.Min(3, hintLevel + 1);
            if (Keyboard.current.f8Key.wasPressedThisFrame)
            {
                if (confirmSkip) { Skip(); return; }
                confirmSkip = true;
            }
        }
    }

    private void FindOwner()
    {
        foreach (PcTicketBridge b in FindObjectsByType<PcTicketBridge>(FindObjectsSortMode.None))
        {
            if (b.Current != ticket) continue;
            pc = b;
            employee = StationEmployee.Of(b);
            break;
        }
        if (employee == null && ticket != null)
            foreach (StationEmployee e in FindObjectsByType<StationEmployee>(FindObjectsSortMode.None))
                if (e.Occupied && string.Equals(e.employeeName, ticket.Requester, StringComparison.OrdinalIgnoreCase))
                { employee = e; break; }
    }

    private string StepId()
    {
        if (ticket == null || ticket.Definition == null || ticket.Definition.steps == null) return "";
        foreach (TicketStepDefinition s in ticket.Definition.steps)
            if (s != null && !ticket.HasCompleted(s.id)) return s.id;
        return "finalizar";
    }

    private string Objective()
    {
        if (ticket == null || ticket.Status == TicketStatus.Pending) return "Abra o tablet [T] e inicie o chamado de montagem";
        if (!visitedEmployee && employee != null) return "Encontre " + employee.employeeName + " na estacao indicada";
        string id = StepId();
        if (id == "finalizar") return "Abra o tablet [T] e FINALIZE o chamado";
        if (ticket.Definition != null)
            foreach (TicketStepDefinition s in ticket.Definition.steps)
                if (s != null && s.id == id) return s.description;
        return "Continue a montagem do computador";
    }

    private string Hint()
    {
        if (ticket == null || ticket.Status == TicketStatus.Pending)
            return hintLevel == 1 ? "Use T para abrir o tablet." : hintLevel == 2 ? "Na aba Pendentes, encontre o chamado #1." : "Clique em Iniciar atendimento; a mesa sera reservada.";
        if (!visitedEmployee && employee != null)
            return hintLevel == 1 ? "O solicitante esta na propria estacao de trabalho." : hintLevel == 2 ? "Siga a seta com o nome do funcionario." : "Aproxime-se da estacao de " + employee.employeeName + ".";
        if (StepId() == "finalizar") return "Na aba Em Atendimento, pressione Finalizar somente depois de completar as etapas.";
        string[] arr;
        if (Hints.TryGetValue(StepId(), out arr)) return arr[Math.Max(0, Math.Min(2, hintLevel - 1))];
        return "Abra o tablet e consulte a biblioteca de ferramentas e componentes.";
    }

    private void EnsureStyles()
    {
        if (panelStyle != null) return;
        panelTexture = new Texture2D(1, 1);
        panelTexture.SetPixel(0, 0, new Color(.05f, .10f, .17f, .94f)); panelTexture.Apply();
        panelStyle = new GUIStyle(GUI.skin.box) { normal = { background = panelTexture }, padding = new RectOffset(18, 18, 13, 12) };
        whiteStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, normal = { textColor = Color.white } };
        titleStyle = new GUIStyle(whiteStyle) { fontStyle = FontStyle.Bold, fontSize = 19 };
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };
        markerStyle = new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 21 };
    }
    private void OnGUI()
    {
        if (tickets == null || stage == null || !stage.IsRunning || tickets.StageNumber != 0 || MainMenuScreen.Showing || StageReportScreen.Showing) return;
        EnsureStyles();
        float scale = Mathf.Clamp(Screen.width / 1600f, .75f, 1.25f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        float w = Screen.width / scale, h = Screen.height / scale;
        float width = Mathf.Min(500, w - 30);
        GUILayout.BeginArea(new Rect(15, 15, width, hintLevel > 0 ? 320 : 205), panelStyle);
        GUILayout.Label("PRIMEIRO DIA  •  TREINAMENTO", titleStyle);
        GUILayout.Label("Sobreviva ao escritorio. E tente nao irritar o chefe.", whiteStyle);
        GUILayout.Space(6);
        GUILayout.Label("OBJETIVO: " + (string.IsNullOrEmpty(currentObjective) ? "Aguardando chamado..." : currentObjective), whiteStyle);
        GUILayout.Space(7);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("DICA  [H]", buttonStyle, GUILayout.Height(30))) hintLevel = Math.Min(3, hintLevel + 1);
        if (GUILayout.Button(confirmSkip ? "CONFIRMAR SAIDA" : "PULAR  [F8]", buttonStyle, GUILayout.Height(30)))
        {
            if (confirmSkip) { Skip(); GUIUtility.ExitGUI(); }
            else confirmSkip = true;
        }
        if (confirmSkip && GUILayout.Button("CANCELAR", buttonStyle, GUILayout.Height(30))) confirmSkip = false;
        GUILayout.EndHorizontal();
        if (hintLevel > 0) { GUILayout.Space(8); GUILayout.Label("DICA " + hintLevel + ": " + Hint(), whiteStyle); }
        GUILayout.EndArea();
        if (ticket != null && ticket.Status == TicketStatus.InProgress && !visitedEmployee && employee != null)
            Marker(employee.transform.position + Vector3.up * 2.5f, "▼ " + employee.employeeName + "  (SOLICITANTE)", w, h, scale);
        else if (ticket != null && ticket.Status == TicketStatus.InProgress && pc != null && StepId() != "finalizar")
        {
            if (StepId() == "posicionar-equipamentos")
            {
                AssemblyPickupPoint pt = AssemblyPickupPoint.Nearest(pc.transform.position);
                if (pt != null) Marker(pt.transform.position + Vector3.up * 1.8f, "▼ RETIRADA DE EQUIPAMENTOS", w, h, scale);
            }
            else Marker(pc.transform.position + Vector3.up * 1.7f, "▼ COMPUTADOR DO CHAMADO", w, h, scale);
        }
        GUI.matrix = Matrix4x4.identity;
    }
    private void Marker(Vector3 pos, string label, float w, float h, float scale)
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 p = cam.WorldToScreenPoint(pos);
        if (p.z <= 0f) return;
        float x = p.x / scale - 155f, y = (Screen.height - p.y) / scale - 30f;
        if (x < 0 || x > w - 310 || y < 160 || y > h - 35) return;
        GUI.Label(new Rect(x, y, 310, 38), label, markerStyle);
    }
    private void Skip()
    {
        Time.timeScale = 1f;
        LoadNormalScene();
    }
    private void LoadNormalScene()
    {
#if UNITY_EDITOR
        // Sem precisar editar EditorBuildSettings.asset da equipe.
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            "Assets/Scenes/Fase_01.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene("Fase_01");
#endif
    }
}
