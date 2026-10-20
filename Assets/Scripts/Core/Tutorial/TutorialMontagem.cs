using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Tutorial da cena <c>Tutorial_Montagem</c>: montagem completa de um PC na estação da sala de TI, em ETAPAS com avanço
/// automático. Cada etapa mostra uma instrução e um marcador no cenário; quando a ação é reconhecida (pelo estado REAL do
/// jogo: chamado, item na mão, gabinete, cabos, pendrive) passa sozinha para a próxima. O índice só avança, então
/// etapas que "deixam de valer" depois (ex.: gabinete aberto, que é fechado em seguida) não fazem o tutorial voltar.
/// O tutorial não conclui nada por conta própria: as etapas do chamado são as do <see cref="PcTicketBridge"/>.
/// Painel em uGUI no visual do HUD (<see cref="HudEstilo"/>) e botões no estilo da pausa (<see cref="PauseMenu.EstilizarBotaoExterno"/>).
/// F8 (duas vezes) pula o tutorial; ao fim, "Começar o expediente" ou "Voltar ao menu".
/// Vai no objeto <c>Chamados</c> da cena do tutorial (montado por <c>TutorialMontagemSetup</c>).
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialMontagem : MonoBehaviour
{
    [SerializeField] private TicketManager tickets;
    [SerializeField] private StageController stage;
    [SerializeField] private TMP_FontAsset fonte;
    [Header("Marcadores (onde fica cada coisa)")]
    [Tooltip("Estação de montagem (PC sobre a mesa).")]
    [SerializeField] private Transform alvoBancada;
    [Tooltip("Bancada de manutenção onde ficam as ferramentas (chave de fenda).")]
    [SerializeField] private Transform alvoFerramentas;
    [SerializeField] private Transform alvoHardware;
    [SerializeField] private Transform alvoCabos;
    [SerializeField] private Transform alvoSuprimentos;

    private sealed class Etapa
    {
        public string Titulo, Texto; public Func<Transform> Alvo; public Func<bool> Feita;
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

    // ------------------------------------------------------------------ etapas
    private void Awake()
    {
        if (tickets == null) tickets = GetComponent<TicketManager>();
        if (stage == null) stage = GetComponent<StageController>();
        Nova("Inicie o chamado de treinamento",
            "Aperte T para abrir o tablet. Na aba PENDENTES, toque em \"Iniciar atendimento\" no chamado de treinamento.",
            null, () => ticket != null && ticket.Status != TicketStatus.Pending);
        Nova("Pegue a chave de fenda",
            "A chave de fenda está na bancada de manutenção da sala de TI. Mire nela e use o clique esquerdo para pegá-la.",
            () => alvoFerramentas != null ? alvoFerramentas : alvoBancada, () => Segurando(ToolType.Screwdriver) || PassouDe("abrir-gabinete"));
        Nova("Abra o gabinete",
            "Com a chave na mão, clique com o botão direito no gabinete. Na vista Traseira, clique nos parafusos da tampa para retirá-los.",
            () => alvoBancada, () => PassouDe("abrir-gabinete"));
        Peca("placa-mae", "Instale a placa-mãe",
            "No gaveteiro de HARDWARE, abra a gaveta da placa-mãe, da RAM e do cooler e pegue a placa-mãe. Leve-a ao gabinete e, no Interior, clique no encaixe da placa-mãe. Ela é a base: a RAM e o cooler vão sobre ela.");
        Peca("ram", "Instale a memória RAM",
            "Na mesma gaveta, pegue a memória RAM (o pente de PC de mesa) e clique no slot de memória da placa-mãe, no Interior do gabinete.");
        Peca("cooler", "Instale o cooler",
            "Pegue o cooler na mesma gaveta e instale-o sobre o processador da placa-mãe. Ele evita que o processador superaqueça.");
        Peca("fonte", "Instale a fonte",
            "Na gaveta de baixo do gaveteiro de HARDWARE estão a fonte e o HD. Pegue a fonte e instale-a no encaixe da fonte, no Interior do gabinete.");
        Peca("hd", "Instale o HD",
            "Pegue o HD na mesma gaveta e instale-o no encaixe de armazenamento. É nele que o sistema operacional será gravado.");
        Nova("Feche o gabinete",
            "Com a chave de fenda na mão, recoloque os parafusos da tampa (clique nos encaixes vazios). Um gabinete aberto não pode ser entregue.",
            () => alvoBancada, () => PassouDe("fechar-gabinete"));
        Nova("Ligue os cabos de energia",
            "No gaveteiro de CABOS, abra a gaveta de energia e pegue um cabo. Com ele na mão, clique direito no gabinete e, na Traseira, ligue a entrada da fonte e uma tomada. Faça o mesmo com um segundo cabo para o monitor (entrada de energia do monitor e a outra tomada).",
            () => Segurando(ToolType.None) && SeguraCabo() ? alvoBancada : alvoCabos, () => PassouDe("conectar-energia"));
        Nova("Ligue o cabo de vídeo",
            "No gaveteiro de CABOS, na gaveta de vídeo, pegue o cabo HDMI (ou o VGA) e ligue a saída de vídeo do gabinete à entrada do mesmo tipo no monitor, na vista Traseira. Sem ele, o monitor mostra \"Sem sinal\".",
            () => Segurando(ToolType.None) && SeguraCabo() ? alvoBancada : alvoCabos, () => PassouDe("conectar-video"));
        Nova("Encaixe o pendrive bootável",
            "No gaveteiro de SUPRIMENTOS, pegue o pendrive bootável (ele tem o instalador do sistema) e encaixe-o numa USB da Traseira do gabinete.",
            () => alvoSuprimentos, () => (energia != null && energia.PendriveConnected) || PassouDe("instalar-sistema"));
        Nova("Ligue o PC e instale o sistema",
            "Na vista Frente, aperte o botão Power. O PC inicia pelo pendrive e instala o sistema no HD; a instalação só avança enquanto você olha para o monitor.",
            () => alvoBancada, () => PassouDe("validar-funcionamento"));
        Nova("Finalize o chamado",
            "O PC chegou ao sistema operacional. Abra o tablet [T], vá à aba EM ATENDIMENTO e toque em \"Finalizar atendimento\".",
            null, () => ticket != null && ticket.Status == TicketStatus.Completed);
    }

    private void Nova(string titulo, string texto, Func<Transform> alvo, Func<bool> feita)
        => etapas.Add(new Etapa { Titulo = titulo, Texto = texto, Alvo = alvo, Feita = feita });
    // peça: o marcador mostra o gaveteiro até o jogador pegar uma peça; com ela na mão, a bancada
    private void Peca(string id, string titulo, string texto)
        => Nova(titulo, texto, () => SeguraPeca() ? alvoBancada : alvoHardware, () => PassouDe("instalar-" + id));

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
        if (ticket != null && pc == null && ticket.Status != TicketStatus.Pending) AcharPc();

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
        Desenhar();
    }

    private void AcharPc()
    {
        foreach (PcTicketBridge b in FindObjectsByType<PcTicketBridge>())
        {
            if (b.Current != ticket) continue;
            pc = b; gabinete = b.GetComponentInChildren<ComputerCase>(true); energia = b.GetComponentInChildren<ComputerPower>(true);
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
    private RectTransform painel, marcador, barraCheia, fim;
    private TMP_Text rotulo, contagem, titulo, texto, aviso, dica, atalho, textoMarcador;
    private Image fundoPainel; private Outline bordaPainel;
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
        Espaco(painel, 2f);
        texto = Texto(painel, "", 21, HudEstilo.Valor, FontStyles.Normal);
        texto.lineSpacing = 6f;
        aviso = Texto(painel, "", 19, new Color(.55f, 1f, .6f), FontStyles.Bold);
        Separador(painel, 12f, 8f);
        dica = Texto(painel, "", 16, new Color(HudEstilo.Valor.r, HudEstilo.Valor.g, HudEstilo.Valor.b, .75f), FontStyles.Italic);
        Separador(painel, 8f, 6f);
        atalho = Texto(painel, "", 15, new Color(HudEstilo.Valor.r, HudEstilo.Valor.g, HudEstilo.Valor.b, .55f), FontStyles.Normal);

        // marcador no cenário
        marcador = new GameObject("Marcador", typeof(RectTransform)).GetComponent<RectTransform>();
        marcador.SetParent(go.transform, false); marcador.sizeDelta = new Vector2(300f, 70f);
        textoMarcador = Texto(marcador, "▼", 34, new Color(.55f, 1f, .6f), FontStyles.Bold);
        textoMarcador.alignment = TextAlignmentOptions.Bottom;
        var r = textoMarcador.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        textoMarcador.outlineWidth = .25f; textoMarcador.outlineColor = new Color32(5, 10, 18, 255);
    }

    private void Desenhar()
    {
        Montar();
        bool oculto = PauseMenu.IsPaused || MainMenuScreen.Showing;
        canvas.enabled = !oculto;
        if (oculto) return;
        painel.gameObject.SetActive(!concluido);
        if (concluido) { marcador.gameObject.SetActive(false); return; }

        Etapa e = etapas[Mathf.Min(atual, etapas.Count - 1)];
        contagem.text = $"ETAPA {Mathf.Min(atual + 1, etapas.Count)} DE {etapas.Count}";
        barraCheia.anchorMax = new Vector2((float)atual / etapas.Count, 1f);
        titulo.text = e.Titulo;
        texto.text = e.Texto;
        bool flash = Time.unscaledTime < avisoAte;
        aviso.gameObject.SetActive(flash); aviso.text = "ETAPA CONCLUÍDA!";   // sem símbolo: a fonte do jogo não tem o glifo de visto (saía um quadrado)
        bordaPainel.effectColor = flash ? new Color(.55f, 1f, .6f, 1f) : HudEstilo.Borda;
        dica.text = "Dica: consulte o app Peças do computador no tablet [T].";
        atalho.text = confirmandoPulo ? "Aperte F8 de novo para pular o tutorial e voltar ao menu." : "F8  ·  pular o tutorial";
        atalho.color = confirmandoPulo ? new Color(1f, .78f, .4f) : new Color(HudEstilo.Valor.r, HudEstilo.Valor.g, HudEstilo.Valor.b, .55f);

        // marcador: só no 3D (sem tela 2D nem tablet abertos) e com o alvo na frente da câmera
        Transform alvo = e.Alvo != null ? e.Alvo() : null;
        bool telaAberta = (UIScreenManager.Active != null && UIScreenManager.Active.IsOpen) || TicketTabletPrototype.CelularAberto;
        Camera cam = Camera.main;
        bool mostrar = alvo != null && !telaAberta && cam != null;
        if (mostrar)
        {
            Vector3 p = cam.WorldToScreenPoint(Topo(alvo) + Vector3.up * .35f);
            mostrar = p.z > 0f;
            if (mostrar)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, p, null, out Vector2 local);
                marcador.anchoredPosition = local + Vector2.up * 30f;
                textoMarcador.text = NomeDoAlvo(alvo) + "\n▼";
            }
        }
        marcador.gameObject.SetActive(mostrar);
    }

    private string NomeDoAlvo(Transform alvo)
        => alvo == alvoHardware ? "GAVETEIRO DE HARDWARE" : alvo == alvoCabos ? "GAVETEIRO DE CABOS"
         : alvo == alvoSuprimentos ? "GAVETEIRO DE SUPRIMENTOS" : alvo == alvoFerramentas ? "BANCADA DE FERRAMENTAS" : "ESTAÇÃO DE MONTAGEM";

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

        // o jogador para e o cursor fica livre para os botões
        foreach (Behaviour c in new Behaviour[] { FindFirstObjectByType<PlayerLook>(), FindFirstObjectByType<PlayerMovement>(), FindFirstObjectByType<InteractionDetector>() })
            if (c != null && c.enabled) { c.enabled = false; controlesDesligados.Add(c); }
        var tablet = FindFirstObjectByType<TicketTabletPrototype>(); if (tablet != null) tablet.Fechar();
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
        foreach (var c in controlesDesligados) if (c != null) c.enabled = true;
        foreach (var r in recursos) if (r != null) Destroy(r);
    }
}
