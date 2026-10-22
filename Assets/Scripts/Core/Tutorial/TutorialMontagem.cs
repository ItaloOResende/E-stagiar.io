using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Tutorial da cena <c>Tutorial_Montagem</c>: montagem completa de um PC, em ETAPAS com avanço automático. O gabinete é
/// montado na bancada de manutenção e depois levado à estação de trabalho, que começa com a mesa vazia (gabinete e monitor
/// no móvel do estoque, teclados e mouses nas estantes: <c>TutorialMontagemSetup</c>); no primeiro quadro o gabinete é
/// esvaziado e perde os cabos (<see cref="PcTicketBridge.PrepareTutorialStation"/>). Cada etapa mostra uma instrução e um
/// marcador no cenário; quando a ação é reconhecida (pelo estado REAL do jogo: chamado, item na mão, gabinete, cabos, pendrive) passa sozinha para a próxima. O índice só avança, então
/// etapas que "deixam de valer" depois (ex.: gabinete aberto, que é fechado em seguida) não fazem o tutorial voltar.
/// O tutorial não conclui nada por conta própria: as etapas do chamado são as do <see cref="PcTicketBridge"/>.
/// Painel em uGUI no visual do HUD (<see cref="HudEstilo"/>) e botões no estilo da pausa (<see cref="PauseMenu.EstilizarBotaoExterno"/>).
/// O marcador aponta o objetivo atual (some quando o objeto está na mão); nas etapas do tablet, aponta o botão do tablet.
/// TAB (ou o botão "-" do painel) minimiza/restaura o painel. F8 (duas vezes) pula o tutorial; ao fim, "Começar o expediente" ou "Voltar ao menu".
/// Vai no objeto <c>Chamados</c> da cena do tutorial (montado por <c>TutorialMontagemSetup</c>).
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialMontagem : MonoBehaviour
{
    [SerializeField] private TicketManager tickets;
    [SerializeField] private StageController stage;
    [SerializeField] private TMP_FontAsset fonte;
    [Header("Marcadores (onde fica cada coisa)")]
    [Tooltip("Estação de trabalho (PC sobre a mesa).")]
    [SerializeField] private Transform alvoBancada;
    [Tooltip("Mesa (WorkSurface) da estação de trabalho: onde o PC fica montado, com monitor, teclado e mouse.")]
    [SerializeField] private Transform alvoMesa;
    [Tooltip("Bancada de manutenção (WorkSurface) onde ficam as ferramentas e onde o gabinete é aberto e montado.")]
    [SerializeField] private Transform alvoFerramentas;
    [Tooltip("A chave de fenda (pegável) da bancada de manutenção.")]
    [SerializeField] private Transform alvoChave;
    [Tooltip("Teclados e mouses pegáveis do estoque: qualquer um deles serve na estação.")]
    [SerializeField] private Transform[] teclados = new Transform[0];
    [SerializeField] private Transform[] mouses = new Transform[0];
    [SerializeField] private Transform alvoHardware;
    [SerializeField] private Transform alvoCabos;
    [SerializeField] private Transform alvoSuprimentos;

    private sealed class Etapa
    {
        public string Titulo, Texto; public Func<Transform> Alvo; public Func<bool> Feita;
        public bool Tablet;   // o marcador aponta o botão do tablet (na tela), não um objeto do cenário
    }

    private readonly List<Etapa> etapas = new List<Etapa>();
    private int atual;
    private bool concluido, confirmandoPulo;
    private float avisoAte = -1f, confirmarAte = -1f;
    private TicketInstance ticket;
    private PcTicketBridge pc;
    private ComputerCase gabinete;
    private ComputerPower energia;
    private InteractionDetector jogador;

    /// <summary>Etapa atual (0 = primeira); igual a <see cref="TotalEtapas"/> quando todas foram feitas.</summary>
    public int EtapaAtual => atual;
    public int TotalEtapas => etapas.Count;
    public bool Concluido => concluido;
    /// <summary>Título da etapa atual (testes).</summary>
    public string TituloAtual => atual < etapas.Count ? etapas[atual].Titulo : "";
    /// <summary>Objeto que o marcador aponta agora (null = nenhum ou o botão do tablet). Atualizado a cada quadro.</summary>
    public Transform AlvoDoMarcador { get; private set; }
    /// <summary>O marcador aponta o botão do tablet.</summary>
    public bool MarcandoTablet { get; private set; }
    public bool Minimizado => minimizado;

    // ------------------------------------------------------------------ etapas
    private void Awake()
    {
        if (tickets == null) tickets = GetComponent<TicketManager>();
        if (stage == null) stage = GetComponent<StageController>();
        Nova("Inicie o atendimento",
            "Abra o tablet [T] e toque em \"Iniciar atendimento\" no chamado pendente.",
            null, () => ticket != null && ticket.Status != TicketStatus.Pending).Tablet = true;
        Nova("Pegue o gabinete",
            "Vamos precisar de um gabinete para montar o computador. Há um vazio no móvel do estoque.",
            () => Gabinete(), () => SegurandoGabinete() || GabineteEm(alvoFerramentas) || PassouDe("abrir-gabinete"));
        Nova("Leve o gabinete à bancada de manutenção",
            "É lá que ficam as ferramentas. Solte o gabinete sobre o tampo da bancada para começar a montagem.",
            () => SegurandoGabinete() ? alvoFerramentas : Gabinete(), () => GabineteEm(alvoFerramentas) || PassouDe("abrir-gabinete"));
        Nova("Pegue a chave de fenda",
            "Ela está na própria bancada. Você vai usá-la para soltar os parafusos do gabinete.",
            () => alvoChave != null ? alvoChave : alvoFerramentas, () => Segurando(ToolType.Screwdriver) || PassouDe("abrir-gabinete"));
        Nova("Abra o gabinete",
            "Clique com o botão direito no gabinete e vá à vista Traseira. Com a chave, retire os 3 parafusos do lado direito: a tampa abre sozinha.",
            () => Segurando(ToolType.Screwdriver) || alvoChave == null ? Gabinete() : alvoChave,
            () => PassouDe("abrir-gabinete") || (gabinete != null && gabinete.State == CaseState.Open));
        Peca("placa-mae", "Instale a placa-mãe",
            "É a base do computador: as outras peças se ligam nela. Pegue-a no gaveteiro de hardware e clique no encaixe dela, no Interior.");
        Peca("ram", "Instale a memória RAM",
            "Ela guarda os programas em uso. Está na mesma gaveta; encaixe-a no slot de memória da placa-mãe.");
        Peca("cooler", "Instale o cooler",
            "Ele evita que o processador superaqueça. Pegue-o na mesma gaveta e instale-o sobre o processador.");
        Peca("fonte", "Instale a fonte",
            "Ela leva energia a todas as peças. Fica na gaveta de baixo do gaveteiro de hardware.");
        Peca("hd", "Instale o HD",
            "É nele que o sistema operacional vai ficar gravado. Está na mesma gaveta da fonte.");
        Nova("Feche o gabinete",
            "Com a chave de fenda, recoloque os parafusos da tampa. Gabinete aberto não pode ser entregue.",
            () => Segurando(ToolType.Screwdriver) || alvoChave == null ? Gabinete() : alvoChave, () => PassouDe("fechar-gabinete"));
        Nova("Leve o gabinete à estação de trabalho",
            "A montagem interna terminou. Agora o computador vai para a mesa onde vai ser usado.",
            () => SegurandoGabinete() ? alvoMesa : Gabinete(), () => GabineteNaMesa() || PassouDe("posicionar-equipamentos"));
        Nova("Busque os periféricos",
            "Traga o monitor, um teclado e um mouse do estoque e coloque-os na mesa, ao lado do gabinete.",
            ProximoPeriferico, () => PassouDe("posicionar-equipamentos") && AlgumNaMesa(teclados) && AlgumNaMesa(mouses));
        Nova("Ligue a energia",
            "Pegue dois cabos de energia no gaveteiro de cabos: um vai da fonte do PC à tomada, o outro do monitor à tomada ao lado da mesa.",
            () => SeguraCabo() ? Gabinete() : alvoCabos, () => PassouDe("conectar-energia"));
        Nova("Ligue o cabo de vídeo",
            "Pegue um cabo HDMI ou VGA no gaveteiro de cabos e ligue o gabinete ao monitor. Sem ele, a tela fica sem sinal.",
            () => SeguraCabo() ? Gabinete() : alvoCabos, () => PassouDe("conectar-video"));
        Nova("Ligue o cabo de rede",
            "Pegue um cabo de rede no gaveteiro de cabos e ligue a porta de rede do gabinete ao ponto de rede da parede.",
            () => SeguraCabo() ? Gabinete() : alvoCabos, () => PassouDe("conectar-rede"));
        Nova("Encaixe o pendrive",
            "Ele tem o instalador do sistema. Pegue um na bancada de manutenção e encaixe-o numa USB da traseira do gabinete.",
            () => SeguraPendrive() ? Gabinete() : alvoFerramentas, () => (energia != null && energia.PendriveConnected) || PassouDe("instalar-sistema"));
        Nova("Ligue o PC",
            "Aperte o botão Power na vista Frente. A instalação do sistema só avança enquanto você olha para o monitor.",
            () => energia != null && energia.State != PcPowerState.Off ? Monitor() : Gabinete(), () => PassouDe("validar-funcionamento"));
        Nova("Finalize o atendimento",
            "O computador está funcionando. Abra o tablet [T] e toque em \"Finalizar atendimento\".",
            null, () => ticket != null && ticket.Status == TicketStatus.Completed).Tablet = true;
    }

    private Etapa Nova(string titulo, string texto, Func<Transform> alvo, Func<bool> feita)
    {
        var e = new Etapa { Titulo = titulo, Texto = texto, Alvo = alvo, Feita = feita };
        etapas.Add(e); return e;
    }
    // peça: o marcador mostra o gaveteiro até o jogador pegar uma peça; com ela na mão, a bancada
    private void Peca(string id, string titulo, string texto)
        => Nova(titulo, texto, () => SeguraPeca() ? Gabinete() : alvoHardware, () => PassouDe("instalar-" + id));

    // Etapa do chamado já cumprida (o PcTicketBridge marca no TicketInstance).
    private bool PassouDe(string id) => ticket != null && (ticket.Status == TicketStatus.Completed || ticket.HasCompleted(id));

    private bool Segurando(ToolType tipo)
    {
        var item = jogador != null && jogador.Holder != null ? jogador.Holder.Current : null;
        if (item == null) return false;
        var ferramenta = item.GetComponent<ToolItem>();
        return tipo == ToolType.None ? ferramenta == null : ferramenta != null && ferramenta.Type == tipo;
    }
    private bool SeguraCabo()
    {
        var item = jogador != null && jogador.Holder != null ? jogador.Holder.Current : null;
        return item != null && item.GetComponent<CableItem>() != null;
    }
    private bool SegurandoGabinete()
    {
        var item = jogador != null && jogador.Holder != null ? jogador.Holder.Current : null;
        return item != null && gabinete != null && item.transform == gabinete.transform;
    }
    // gabinete solto, apoiado no móvel dado (a mesa da estação ou a bancada de manutenção)
    private bool GabineteEm(Transform movel)
    {
        WorkSurface apoio = gabinete != null && !SegurandoGabinete() ? gabinete.SupportingSurface() : null;
        return apoio != null && movel != null && (apoio.transform == movel || apoio.transform.IsChildOf(movel));
    }
    private bool GabineteNaMesa() => GabineteEm(alvoMesa);
    private bool SeguraPendrive()
    {
        var item = jogador != null && jogador.Holder != null ? jogador.Holder.Current : null;
        return item != null && item.GetComponentInChildren<PendriveItem>() != null;
    }
    // periférico na mão → a mesa; senão, o monitor, um teclado e um mouse que ainda faltam (o mais perto do jogador)
    private Transform ProximoPeriferico()
    {
        var item = jogador != null && jogador.Holder != null ? jogador.Holder.Current : null;
        Transform monitor = energia != null ? energia.Monitor : null;
        if (item != null && (Contem(item.transform, monitor) || NaLista(item.transform, teclados) || NaLista(item.transform, mouses))) return alvoMesa;
        if (monitor != null && !NaMesa(monitor)) return monitor;
        if (!AlgumNaMesa(teclados)) return MaisPerto(teclados);
        if (!AlgumNaMesa(mouses)) return MaisPerto(mouses);
        return null;
    }
    private static bool NaLista(Transform item, Transform[] lista)
    {
        foreach (Transform t in lista) if (Contem(item, t)) return true;
        return false;
    }
    private bool AlgumNaMesa(Transform[] lista)
    {
        if (lista.Length == 0) return true;
        foreach (Transform t in lista) if (t != null && NaMesa(t)) return true;
        return false;
    }
    private Transform MaisPerto(Transform[] lista)
    {
        Vector3 de = jogador != null ? jogador.transform.position : transform.position;
        Transform melhor = null; float d = float.MaxValue;
        foreach (Transform t in lista)
        {
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            float dd = (t.position - de).sqrMagnitude;
            if (dd < d) { d = dd; melhor = t; }
        }
        return melhor;
    }
    private static bool Contem(Transform raiz, Transform t) => t != null && (t == raiz || t.IsChildOf(raiz));
    // item solto apoiado na bancada de montagem (raio para baixo a partir da base dele). Sem referência = não exigido.
    private bool NaMesa(Transform t)
    {
        if (t == null) return true;
        var item = t.GetComponentInParent<PickupInteractable>();
        if (item == null || item.IsHeld || alvoMesa == null) return false;
        Bounds b = item.WorldBounds;
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(b.center.x, b.min.y + 0.05f, b.center.z), Vector3.down, 0.3f, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
        foreach (RaycastHit h in hits)
        {
            if (h.collider.transform.IsChildOf(item.transform)) continue;
            return h.collider.transform == alvoMesa || h.collider.transform.IsChildOf(alvoMesa);
        }
        return false;
    }
    private Transform Gabinete() => gabinete != null ? gabinete.transform : alvoBancada;
    private Transform Monitor() => energia != null && energia.Monitor != null ? energia.Monitor : alvoBancada;
    private bool SeguraPeca()
    {
        var item = jogador != null && jogador.Holder != null ? jogador.Holder.Current : null;
        return item != null && item.GetComponent<ComputerPartItem>() != null;
    }

    // ------------------------------------------------------------------ ciclo
    private void Update()
    {
        if (tickets == null || stage == null) return;
        if (jogador == null) jogador = FindFirstObjectByType<InteractionDetector>();
        if (ticket == null && tickets.Tickets.Count > 0) ticket = tickets.Tickets[0];
        if (pc == null && ticket != null) AcharPc();

        if (!concluido)
        {
            int antes = atual;
            while (atual < etapas.Count && etapas[atual].Feita()) atual++;
            if (atual != antes) avisoAte = Time.unscaledTime + 1.8f;
            if (atual >= etapas.Count || (ticket != null && ticket.Status == TicketStatus.Completed)) Concluir();
        }

        if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame && !concluido)
        {
            if (confirmandoPulo && Time.unscaledTime < confirmarAte) Pular();
            else { confirmandoPulo = true; confirmarAte = Time.unscaledTime + 4f; }
        }
        if (confirmandoPulo && Time.unscaledTime >= confirmarAte) confirmandoPulo = false;
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame && !concluido) AlternarMinimizado();
        Desenhar();
    }

    private void AcharPc()
    {
        // o PC da estação = o que atende o chamado do tutorial; ao achá-lo (primeiro quadro), a mesa fica vazia
        foreach (PcTicketBridge b in FindObjectsByType<PcTicketBridge>())
        {
            if (ticket == null || ticket.Definition == null || !b.AcceptedTicketIds.Contains(ticket.Definition.ticketId)) continue;
            pc = b; gabinete = b.GetComponent<ComputerCase>(); energia = b.GetComponent<ComputerPower>();
            pc.PrepareTutorialStation();
            return;
        }
    }

    private void Concluir()
    {
        if (concluido) return;
        concluido = true; atual = etapas.Count;
        MostrarFim();
    }

    /// <summary>Pular o tutorial: volta ao menu inicial (dali o jogador começa o expediente ou volta ao tutorial).</summary>
    public void Pular() { GameSession.GoToMainMenu(); }
    public void ComecarExpediente() { GameSession.StartWorkday(); }
    public void VoltarAoMenu() { GameSession.GoToMainMenu(); }

    // ------------------------------------------------------------------ interface (uGUI)
    private Canvas canvas;
    // marcador do cenário em vermelho forte: tem que saltar aos olhos em qualquer fundo
    private static readonly Color CorMarcador = new Color(1f, .16f, .16f);
    private RectTransform painel, marcador, barraCheia, fim;
    private TMP_Text rotulo, contagem, titulo, texto, aviso, dica, atalho, textoMarcador, seta;
    private Image fundoPainel; private Outline bordaPainel;
    private TicketTabletPrototype tablet;
    private readonly Vector3[] cantos = new Vector3[4];
    private bool minimizado;
    private TMP_Text textoMinimizar;
    private RectTransform botaoTutorial;   // painel minimizado: só este botão no canto (não cobre o gabinete na tela 2D)
    private readonly List<GameObject> detalhes = new List<GameObject>();   // some com o painel minimizado
    private Sprite arredondado;
    private readonly List<UnityEngine.Object> recursos = new List<UnityEngine.Object>();
    private readonly List<Behaviour> controlesDesligados = new List<Behaviour>();

    private void Montar()
    {
        if (canvas != null) return;
        var go = new GameObject("TutorialMontagem_UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 31000;
        var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = .5f;

        // painel da etapa: canto superior esquerdo (no tutorial não há pontuação nem cronômetro ali)
        painel = Caixa("Painel", go.transform, out fundoPainel, out bordaPainel);
        painel.anchorMin = painel.anchorMax = painel.pivot = new Vector2(0f, 1f);
        painel.anchoredPosition = new Vector2(24f, -24f); painel.sizeDelta = new Vector2(560f, 10f);
        var v = painel.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(24, 24, 18, 20); v.spacing = 8; v.childControlHeight = true; v.childControlWidth = true; v.childForceExpandHeight = false;
        painel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        rotulo = Texto(painel, "TREINAMENTO  •  MONTAGEM DE PC", 17, HudEstilo.Rotulo, FontStyles.Bold);
        contagem = Texto(painel, "", 16, new Color(HudEstilo.Valor.r, HudEstilo.Valor.g, HudEstilo.Valor.b, .7f), FontStyles.Normal);
        // barra de progresso (fundo escuro + preenchimento verde-água)
        var barra = new GameObject("Progresso", typeof(RectTransform), typeof(Image), typeof(LayoutElement)).GetComponent<RectTransform>();
        barra.SetParent(painel, false); barra.GetComponent<LayoutElement>().preferredHeight = 8; barra.GetComponent<Image>().color = new Color(0f, 0f, 0f, .45f);
        barraCheia = new GameObject("Cheio", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        barraCheia.SetParent(barra, false); barraCheia.anchorMin = Vector2.zero; barraCheia.offsetMin = barraCheia.offsetMax = Vector2.zero;
        barraCheia.GetComponent<Image>().color = HudEstilo.Borda;
        // blocos: cabeçalho (acima) | título da etapa | instrução principal | dica complementar | atalho de pular,
        // separados por respiros e linhas finas
        Separador(painel, 10f, 12f);
        titulo = Texto(painel, "", 27, HudEstilo.Valor, FontStyles.Bold);
        int antes = painel.childCount;
        Espaco(painel, 2f);
        texto = Texto(painel, "", 21, HudEstilo.Valor, FontStyles.Normal);
        texto.lineSpacing = 6f;
        aviso = Texto(painel, "", 19, new Color(.55f, 1f, .6f), FontStyles.Bold);
        Separador(painel, 12f, 8f);
        dica = Texto(painel, "", 16, new Color(HudEstilo.Valor.r, HudEstilo.Valor.g, HudEstilo.Valor.b, .75f), FontStyles.Italic);
        Separador(painel, 8f, 6f);
        for (int i = antes; i < painel.childCount; i++) if (painel.GetChild(i) != aviso.transform) detalhes.Add(painel.GetChild(i).gameObject);
        atalho = Texto(painel, "", 15, new Color(HudEstilo.Valor.r, HudEstilo.Valor.g, HudEstilo.Valor.b, .55f), FontStyles.Normal);

        // botão pequeno no canto do painel: minimiza/restaura (fora do layout; o resto do painel não recebe cliques)
        var minimizar = new GameObject("Minimizar", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement)).GetComponent<Image>();
        minimizar.transform.SetParent(painel, false); minimizar.GetComponent<LayoutElement>().ignoreLayout = true;
        minimizar.rectTransform.anchorMin = minimizar.rectTransform.anchorMax = minimizar.rectTransform.pivot = new Vector2(1f, 1f);
        minimizar.rectTransform.anchoredPosition = new Vector2(-12f, -12f); minimizar.rectTransform.sizeDelta = new Vector2(30f, 30f);
        minimizar.sprite = Arredondado(); minimizar.type = Image.Type.Sliced;
        minimizar.color = new Color(HudEstilo.Borda.r, HudEstilo.Borda.g, HudEstilo.Borda.b, .35f);
        var bm = minimizar.GetComponent<Button>(); bm.targetGraphic = minimizar; bm.onClick.AddListener(AlternarMinimizado);
        bm.navigation = new Navigation { mode = Navigation.Mode.None };
        textoMinimizar = Texto(minimizar.transform, "-", 22, new Color(0.62f, 0.70f, 0.78f), FontStyles.Bold);   // cinza (branco sumia no fundo)
        textoMinimizar.alignment = TextAlignmentOptions.Center; Esticar(textoMinimizar.rectTransform);

        // minimizado: o painel some e fica só um botão "TUTORIAL" pequeno no canto; clicar nele (ou TAB) expande de novo
        botaoTutorial = Caixa("BotaoTutorial", go.transform, out Image fundoBotao, out _);
        botaoTutorial.anchorMin = botaoTutorial.anchorMax = botaoTutorial.pivot = new Vector2(0f, 1f);
        botaoTutorial.anchoredPosition = new Vector2(24f, -24f); botaoTutorial.sizeDelta = new Vector2(210f, 54f);
        fundoBotao.raycastTarget = true;
        var bt = botaoTutorial.gameObject.AddComponent<Button>(); bt.targetGraphic = fundoBotao; bt.onClick.AddListener(AlternarMinimizado);
        bt.navigation = new Navigation { mode = Navigation.Mode.None };
        var textoBotao = Texto(botaoTutorial, "TUTORIAL  [TAB]", 20, HudEstilo.Valor, FontStyles.Bold);
        textoBotao.alignment = TextAlignmentOptions.Center; Esticar(textoBotao.rectTransform);
        botaoTutorial.gameObject.SetActive(false);

        tablet = FindFirstObjectByType<TicketTabletPrototype>();

        // marcador no cenário
        marcador = new GameObject("Marcador", typeof(RectTransform)).GetComponent<RectTransform>();
        marcador.SetParent(go.transform, false); marcador.sizeDelta = new Vector2(400f, 86f);
        // nome do alvo (em cima) + seta (embaixo; fora da tela, gira para o lado do alvo)
        textoMarcador = Texto(marcador, "", 28, CorMarcador, FontStyles.Bold);
        textoMarcador.alignment = TextAlignmentOptions.Bottom;
        var r = textoMarcador.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(0f, 44f); r.offsetMax = Vector2.zero;
        textoMarcador.outlineWidth = .3f; textoMarcador.outlineColor = new Color32(5, 10, 18, 255);
        seta = Texto(marcador, "▼", 36, CorMarcador, FontStyles.Bold);
        seta.alignment = TextAlignmentOptions.Center;
        seta.rectTransform.anchorMin = seta.rectTransform.anchorMax = new Vector2(.5f, 0f);
        seta.rectTransform.anchoredPosition = new Vector2(0f, 20f); seta.rectTransform.sizeDelta = new Vector2(42f, 42f);
        seta.outlineWidth = .3f; seta.outlineColor = new Color32(5, 10, 18, 255);
    }

    private void Desenhar()
    {
        Montar();
        bool oculto = PauseMenu.IsPaused || MainMenuScreen.Showing;
        canvas.enabled = !oculto;
        if (oculto) return;
        painel.gameObject.SetActive(!concluido && (!minimizado || confirmandoPulo));
        botaoTutorial.gameObject.SetActive(!concluido && minimizado && !confirmandoPulo);
        if (concluido) { marcador.gameObject.SetActive(false); AlvoDoMarcador = null; MarcandoTablet = false; return; }

        Etapa e = etapas[Mathf.Min(atual, etapas.Count - 1)];
        contagem.text = $"ETAPA {Mathf.Min(atual + 1, etapas.Count)} DE {etapas.Count}";
        barraCheia.anchorMax = new Vector2((float)atual / etapas.Count, 1f);
        titulo.text = e.Titulo;
        texto.text = e.Texto;
        bool flash = Time.unscaledTime < avisoAte;
        aviso.gameObject.SetActive(flash && !minimizado); aviso.text = "ETAPA CONCLUÍDA!";   // sem símbolo: a fonte do jogo não tem o glifo de visto (saía um quadrado)
        bordaPainel.effectColor = flash ? new Color(.55f, 1f, .6f, 1f) : HudEstilo.Borda;
        dica.text = "Dica: consulte o app Peças do computador no tablet [T].";
        atalho.text = confirmandoPulo ? "Aperte F8 de novo para pular o tutorial e voltar ao menu."
                    : minimizado ? "TAB  ·  mostrar as instruções" : "TAB  ·  minimizar      F8  ·  pular o tutorial";
        atalho.color = confirmandoPulo ? new Color(1f, .78f, .4f) : new Color(HudEstilo.Valor.r, HudEstilo.Valor.g, HudEstilo.Valor.b, .55f);

        // marcador: só no 3D (sem tela 2D nem tablet abertos). Etapa do tablet: aponta o botão do tablet na tela. Objeto
        // na mão do jogador não é marcado (o marcador já mostra para onde levá-lo).
        bool telaAberta = (UIScreenManager.Active != null && UIScreenManager.Active.IsOpen) || TicketTabletPrototype.CelularAberto;
        bool mostrar = false;
        if (!telaAberta && e.Tablet)
        {
            RectTransform botao = tablet != null ? tablet.BotaoAtalho : null;
            if (botao != null)
            {
                botao.GetWorldCorners(cantos);   // canvas overlay: mundo = tela
                Vector2 topo = (cantos[1] + cantos[2]) * .5f;
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, topo, null, out Vector2 local);
                marcador.anchoredPosition = local + Vector2.up * 49f;
                textoMarcador.text = "";
                seta.rectTransform.localEulerAngles = Vector3.zero;
                mostrar = true;
            }
        }
        Transform alvo = !e.Tablet && e.Alvo != null ? e.Alvo() : null;
        if (alvo != null)
        {
            var pego = alvo.GetComponentInParent<PickupInteractable>();
            if (pego != null && pego.IsHeld) alvo = null;
        }
        Camera cam = Camera.main;
        if (alvo != null && !telaAberta && cam != null)
        {
            // na tela: em cima do alvo, seta para baixo. Fora dela (ou atrás): preso à borda, seta girada para o lado dele
            var area = (RectTransform)canvas.transform;
            Vector3 p = cam.WorldToScreenPoint(Topo(alvo) + Vector3.up * .06f);   // colado no item
            bool naTela = p.z > 0f && p.x > 0f && p.x < Screen.width && p.y > 0f && p.y < Screen.height;
            textoMarcador.text = NomeDoAlvo(alvo);
            if (naTela)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(area, p, null, out Vector2 local);
                marcador.anchoredPosition = local + Vector2.up * 45f;   // ponta da seta logo acima do ponto
                seta.rectTransform.localEulerAngles = Vector3.zero;
            }
            else
            {
                Vector2 centro = new Vector2(Screen.width, Screen.height) * .5f;
                Vector2 dir = (Vector2)p - centro;
                if (p.z < 0f) dir = -dir;
                if (dir.sqrMagnitude < 1f) dir = Vector2.down;
                Vector2 meia = centro - new Vector2(240f, 130f) * (Screen.height / 1080f);
                float k = Mathf.Min(meia.x / Mathf.Max(Mathf.Abs(dir.x), .001f), meia.y / Mathf.Max(Mathf.Abs(dir.y), .001f));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(area, centro + dir * k, null, out Vector2 local);
                marcador.anchoredPosition = local;
                seta.rectTransform.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f);
            }
            mostrar = true;
        }
        marcador.gameObject.SetActive(mostrar);
        AlvoDoMarcador = mostrar ? alvo : null;
        MarcandoTablet = mostrar && alvo == null;
    }

    public void AlternarMinimizado()
    {
        minimizado = !minimizado;
        foreach (GameObject d in detalhes) if (d != null) d.SetActive(!minimizado);
        if (textoMinimizar != null) textoMinimizar.text = minimizado ? "+" : "-";
    }

    private string NomeDoAlvo(Transform alvo)
        => gabinete != null && alvo == gabinete.transform ? "GABINETE" : energia != null && alvo == energia.Monitor ? "MONITOR"
         : alvo == alvoMesa ? "ESTAÇÃO DE TRABALHO" : NaLista(alvo, teclados) ? "TECLADO" : NaLista(alvo, mouses) ? "MOUSE"
         : alvo == alvoChave ? "CHAVE DE FENDA" : alvo == alvoHardware ? "GAVETEIRO DE HARDWARE" : alvo == alvoCabos ? "GAVETEIRO DE CABOS"
         : alvo == alvoSuprimentos ? "GAVETEIRO DE SUPRIMENTOS" : alvo == alvoFerramentas ? "BANCADA DE MANUTENÇÃO" : "ESTAÇÃO DE TRABALHO";

    private static Vector3 Topo(Transform t)
    {
        Bounds b = default; bool tem = false;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>()) { if (!tem) { b = r.bounds; tem = true; } else b.Encapsulate(r.bounds); }
        return tem ? new Vector3(b.center.x, b.max.y, b.center.z) : t.position;
    }

    // Tela final: "Tutorial concluído" + Começar o expediente / Voltar ao menu (cursor livre, jogador parado).
    private void MostrarFim()
    {
        Montar();
        fim = Caixa("Fim", canvas.transform, out _, out _);
        fim.anchorMin = fim.anchorMax = fim.pivot = new Vector2(.5f, .5f); fim.sizeDelta = new Vector2(760f, 420f);
        var t1 = Texto(fim, "TUTORIAL CONCLUÍDO", 54, Color.white, FontStyles.Bold);
        t1.alignment = TextAlignmentOptions.Center; Posicionar(t1.rectTransform, new Vector2(0f, 130f), new Vector2(700f, 80f));
        t1.enableVertexGradient = true;
        PauseMenu estilo = FindFirstObjectByType<PauseMenu>();
        var t2 = Texto(fim, "Você montou, ligou e entregou um computador completo. Agora é com você no expediente!", 23, HudEstilo.Valor, FontStyles.Normal);
        t2.alignment = TextAlignmentOptions.Center; Posicionar(t2.rectTransform, new Vector2(0f, 40f), new Vector2(660f, 90f));
        Botao(fim, "COMEÇAR O EXPEDIENTE", new Vector2(0f, -70f), estilo, ComecarExpediente);
        Botao(fim, "VOLTAR AO MENU", new Vector2(0f, -160f), estilo, VoltarAoMenu);
        t1.colorGradient = new VertexGradient(new Color(.8f, 1f, .41f), new Color(.8f, 1f, .41f), new Color(.31f, .95f, .31f), new Color(.31f, .95f, .31f));

        // o jogador para e o cursor fica livre para os botões. O tablet fecha ANTES (ao fechar ele devolve os controles
        // que tinha retido) e sai de cena; a trava do cursor do 3D (UIScreenManager) respeita TelaFinalAberta.
        TelaFinalAberta = true;
        var tablet = FindFirstObjectByType<TicketTabletPrototype>();
        if (tablet != null) { tablet.Fechar(); tablet.enabled = false; }
        foreach (Behaviour c in new Behaviour[] { FindFirstObjectByType<PlayerLook>(), FindFirstObjectByType<PlayerMovement>(), FindFirstObjectByType<InteractionDetector>(), tablet })
            if (c != null) { c.enabled = false; if (!controlesDesligados.Contains(c)) controlesDesligados.Add(c); }
        LiberarCursorFinal();
    }

    /// <summary>A tela "Tutorial concluído" está aberta (cursor livre para os botões; nada do 3D o recaptura).</summary>
    public static bool TelaFinalAberta { get; private set; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ZerarTelaFinal() => TelaFinalAberta = false;

    // Na tela final, a cada quadro: os controles do jogador continuam desligados e o cursor, livre e visível.
    private void LateUpdate()
    {
        if (!TelaFinalAberta || fim == null) return;
        foreach (var c in controlesDesligados) if (c != null && c.enabled) c.enabled = false;
        if (Cursor.lockState != CursorLockMode.None || !Cursor.visible) LiberarCursorFinal();
    }
    private static void LiberarCursorFinal()
    {
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true; UICursor.SobreporMao();
    }

    private void Botao(Transform pai, string rotuloBotao, Vector2 pos, PauseMenu estilo, Action acao)
    {
        var img = new GameObject("Botao_" + rotuloBotao, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Image>();
        img.transform.SetParent(pai, false); Posicionar(img.rectTransform, pos, new Vector2(460f, 74f));
        var b = img.GetComponent<Button>(); b.targetGraphic = img;
        b.onClick.AddListener(() => { if (estilo != null) estilo.TocarCliqueBotao(); acao(); });
        if (estilo != null) estilo.EstilizarBotaoExterno(b, rotuloBotao, 26f);
        else { img.color = HudEstilo.Borda; var t = Texto(img.transform, rotuloBotao, 26, Color.white, FontStyles.Bold); t.alignment = TextAlignmentOptions.Center; Esticar(t.rectTransform); }
    }

    private RectTransform Caixa(string nome, Transform pai, out Image img, out Outline borda)
    {
        img = new GameObject(nome, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        img.transform.SetParent(pai, false);
        img.sprite = Arredondado(); img.type = Image.Type.Sliced; img.color = HudEstilo.Fundo; img.raycastTarget = false;
        borda = img.gameObject.AddComponent<Outline>(); borda.effectColor = HudEstilo.Borda; borda.effectDistance = new Vector2(2f, -2f); borda.useGraphicAlpha = false;
        var sombra = img.gameObject.AddComponent<Shadow>(); sombra.effectColor = HudEstilo.Sombra; sombra.effectDistance = new Vector2(4f, -5f);
        return img.rectTransform;
    }

    private TMP_Text Texto(Transform pai, string valor, float tamanho, Color cor, FontStyles estilo)
    {
        var t = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        t.transform.SetParent(pai, false);
        if (fonte != null) t.font = fonte;
        t.text = valor; t.fontSize = tamanho; t.color = cor; t.fontStyle = estilo; t.raycastTarget = false; t.richText = false;
        t.enableWordWrapping = true;
        return t;
    }

    // respiro vertical e linha fina (1 px, verde-água bem suave) entre os blocos do card
    private static void Espaco(Transform pai, float altura)
    {
        var le = new GameObject("Espaco", typeof(RectTransform), typeof(LayoutElement)).GetComponent<LayoutElement>();
        le.transform.SetParent(pai, false); le.minHeight = le.preferredHeight = altura;
    }
    private static void Separador(Transform pai, float antes, float depois)
    {
        Espaco(pai, antes);
        var linha = new GameObject("Separador", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        linha.transform.SetParent(pai, false);
        var img = linha.GetComponent<Image>(); img.color = new Color(HudEstilo.Borda.r, HudEstilo.Borda.g, HudEstilo.Borda.b, .28f); img.raycastTarget = false;
        var le = linha.GetComponent<LayoutElement>(); le.minHeight = le.preferredHeight = 1f;
        Espaco(pai, depois);
    }
    private static void Posicionar(RectTransform r, Vector2 pos, Vector2 tamanho)
    { r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = pos; r.sizeDelta = tamanho; }
    private static void Esticar(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

    private Sprite Arredondado()
    {
        if (arredondado != null) return arredondado;
        const int raio = 14, lado = raio * 2 + 4;
        var tex = new Texture2D(lado, lado, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[lado * lado];
        for (int y = 0; y < lado; y++) for (int x = 0; x < lado; x++)
        {
            float dx = Mathf.Max(raio - (x + .5f), (x + .5f) - (lado - raio)), dy = Mathf.Max(raio - (y + .5f), (y + .5f) - (lado - raio));
            px[y * lado + x] = new Color(1, 1, 1, Mathf.Clamp01(raio + .5f - new Vector2(Mathf.Max(0, dx), Mathf.Max(0, dy)).magnitude));
        }
        tex.SetPixels(px); tex.Apply(false, true);
        arredondado = Sprite.Create(tex, new Rect(0, 0, lado, lado), Vector2.one * .5f, 100f, 0, SpriteMeshType.FullRect, Vector4.one * raio);
        arredondado.hideFlags = HideFlags.HideAndDontSave;
        recursos.Add(tex); recursos.Add(arredondado);
        return arredondado;
    }

    private void OnDestroy()
    {
        TelaFinalAberta = false;
        foreach (var c in controlesDesligados) if (c != null) c.enabled = true;
        foreach (var r in recursos) if (r != null) Destroy(r);
    }
}
