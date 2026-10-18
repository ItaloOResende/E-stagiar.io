using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Vistas do notebook (a mesma tela, trocando de vista pelos botões Anterior/Próxima).</summary>
public enum NotebookVista { Baixo, Aberto, Lado, Frente }

/// <summary>
/// Tela 2D do notebook. Monta a própria interface por código (só primitivas de UI: retângulos, textos,
/// botões e um círculo gerado em execução), então não depende de nenhuma arte do projeto — quando houver
/// arte de verdade, basta trocar as formas por sprites aqui.
///
/// Quatro vistas, como o PC:
///  - <b>Baixo</b>: a tampa de baixo com os parafusos (chave de fenda) para abrir o notebook.
///  - <b>Aberto</b>: os encaixes da memória SO-DIMM e do SSD.
///  - <b>Lado</b>: a porta USB para o pendrive.
///  - <b>Frente</b>: a tela do notebook e o botão Power (BIOS, instalação pelo pendrive, sistema).
///
/// O estado NÃO fica aqui: fica no <see cref="NotebookEstacao"/> do objeto do mundo (<c>UIScreen.Source</c>),
/// então fechar e reabrir a tela não perde nada.
/// </summary>
[RequireComponent(typeof(UIScreen))]
public class NotebookTela : MonoBehaviour
{
    private static readonly Color CorPainel = new Color(0.09f, 0.11f, 0.14f);
    private static readonly Color CorChassi = new Color(0.17f, 0.2f, 0.24f);
    private static readonly Color CorChassiClaro = new Color(0.23f, 0.27f, 0.32f);
    private static readonly Color CorBorda = new Color(0.3f, 0.35f, 0.42f);
    private static readonly Color CorVazio = new Color(0.11f, 0.13f, 0.16f);
    private static readonly Color CorMetal = new Color(0.63f, 0.67f, 0.73f);
    private static readonly Color CorPlaca = new Color(0.16f, 0.42f, 0.24f);
    private static readonly Color CorSsd = new Color(0.2f, 0.23f, 0.28f);
    private static readonly Color CorTela = new Color(0.05f, 0.06f, 0.08f);
    private static readonly Color TextoClaro = new Color(0.9f, 0.93f, 0.97f);
    private static readonly Color TextoFraco = new Color(0.6f, 0.66f, 0.74f);
    private static readonly Color Verde = new Color(0.36f, 0.85f, 0.45f);
    private static readonly Color Amarelo = new Color(1f, 0.82f, 0.25f);
    private static readonly Color Vermelho = new Color(0.95f, 0.4f, 0.4f);
    private static readonly Color Azul = new Color(0.42f, 0.7f, 1f);

    // Posições dos parafusos da tampa de baixo (fração da tampa; 0 = centro).
    private static readonly Vector2[] PosicaoParafusos =
    {
        new Vector2(-0.43f, 0.33f), new Vector2(0.43f, 0.33f),
        new Vector2(-0.43f, 0.0f), new Vector2(0.43f, 0.0f),
        new Vector2(-0.43f, -0.33f), new Vector2(0.43f, -0.33f)
    };

    private static readonly Vector2 TamanhoTampa = new Vector2(1150f, 640f);
    private static readonly Vector2 TamanhoFuroRam = new Vector2(430f, 120f);
    private static readonly Vector2 TamanhoFuroSsd = new Vector2(400f, 150f);

    [Header("Mesmo padrão da tela do gabinete (UI_Screen_Gabinete)")]
    [Tooltip("Parafuso do gabinete (Prefabs/UI/ScrewView): botão, furo, brilho e animações de tirar/recolocar/recusar. " +
             "Vazio = parafusos desenhados por código (versão antiga).")]
    [SerializeField] private ScrewView parafusoPrefab;
    [Tooltip("Cursor de chave de fenda (o mesmo da Traseira do gabinete), na tampa de baixo com a chave na mão.")]
    [SerializeField] private Texture2D cursorChave;
    [SerializeField] private Vector2 cursorChaveHotspot;
    [Tooltip("Altura do cursor em relação ao parafuso na tela (igual ao gabinete).")]
    [SerializeField] private float chaveParafusoProporcao = 4f;
    [Tooltip("Telas do monitor do PC reaproveitadas: BIOS sem armazenamento, sem sistema, instalando (por etapa) e sistema. " +
             "Vazias = texto (versão antiga).")]
    [SerializeField] private Sprite telaSemArmazenamento;
    [SerializeField] private Sprite telaSemSistema;
    [SerializeField] private Sprite[] telaInstalando = new Sprite[0];
    [SerializeField] private Sprite telaSistema;

    private bool construido;
    private bool vistaInicial;      // já escolheu a vista da primeira abertura? (depois a vista é lembrada)
    private UIScreen tela;

    private NotebookVista vista = NotebookVista.Baixo;
    private RectTransform[] vistas;
    private Text msgTxt;

    // Vista "Baixo".
    private Image[] parafusoImg;
    private RectTransform[] parafusoFenda;
    private float[] parafusoFlash;
    private ScrewView[] parafusoViews;      // com o prefab do gabinete
    private bool[] parafusoEstado;          // último estado desenhado (para animar só quando muda)
    private bool cursorDeChave;

    // Vista "Aberto".
    private Image ramMiolo, ramPeca, ssdMiolo, ssdPeca;

    // Vista "Lado".
    private Image usbMiolo, pendriveImg;

    // Vista "Frente".
    private Image telaFundo, telaPower, barraFill;
    private Text telaTxt, etapaTxt;

    // Modo 3D: o MODELO real do notebook (filho Visual_Toon do Notebook_Estacao) num ângulo fixo por vista, como os
    // renders do gabinete; os pontos clicáveis ficam em cima das peças do modelo. Sem o modelo, vale o desenho antigo.
    private RectTransform areaVistas;
    private NotebookModeloEstado estado3D;   // a cópia do teclado (frente): o LED pisca nela quando o Power não liga
    private GameObject origem3D;
    private RectTransform[] overlays;
    private RectTransform hotPower, hotUsb, hotRam, hotSsd;
    private Image barra3D;
    private Text etapa3D;
    private bool modo3D;

    private void Awake() => Construir();

    private void OnEnable()
    {
        if (tela == null) tela = GetComponent<UIScreen>();
        // A vista é LEMBRADA entre as aberturas (a tela é o mesmo objeto: só desliga e liga de novo). Saiu na
        // tela do notebook (Frente)? Ao voltar, volta nela. Na primeira vez começa na parte útil: se a tampa
        // já está aberta, vai direto ao interior; senão, à tampa de baixo.
        if (!vistaInicial)
        {
            NotebookEstacao est = Estacao();
            vista = est != null && est.Sessao.Estado == NotebookEstado.Aberto ? NotebookVista.Aberto : NotebookVista.Baixo;
            vistaInicial = true;
        }
        // A vista lembrada pode ser a de dentro (Aberto) e a tampa ter sido presa de novo: cai para a tampa de baixo.
        if (!PodeVer(vista)) vista = NotebookVista.Baixo;
        PrepararModelo3D(Estacao());
        parafusoEstado = null;   // ao abrir, desenha o estado atual sem animar
        cursorDeChave = false;
        Mensagem("", TextoFraco);
        Atualizar();
    }

    private void Update()
    {
        if (!construido || gameObject.activeInHierarchy == false) return;

        // As setas do teclado fazem o MESMO que os botões ◄ ► (que continuam funcionando).
        Keyboard teclado = Keyboard.current;
        if (teclado != null)
        {
            if (teclado.leftArrowKey.wasPressedThisFrame) TrocarVista(-1);
            else if (teclado.rightArrowKey.wasPressedThisFrame) TrocarVista(1);
        }

        bool precisa = false;
        for (int i = 0; i < parafusoFlash.Length; i++)
        {
            if (parafusoFlash[i] > 0f)
            {
                parafusoFlash[i] = Mathf.Max(0f, parafusoFlash[i] - Time.deltaTime);
                precisa = true;
            }
        }
        if (vista == NotebookVista.Frente || vista == NotebookVista.Aberto || precisa) Atualizar();
        AtualizarCursor();
    }

    /// <summary>
    /// Igual à Traseira do gabinete: na tampa de baixo, com a chave de fenda na mão, o cursor vira a chave (do tamanho
    /// proporcional aos parafusos); fora disso, o cursor padrão das telas (mãozinha).
    /// </summary>
    private void AtualizarCursor()
    {
        bool chave = vista == NotebookVista.Baixo && cursorChave != null && parafusoViews != null &&
                     FerramentaNaMao() == ToolType.Screwdriver;
        if (chave == cursorDeChave) return;
        cursorDeChave = chave;
        if (chave)
        {
            ScrewView v = parafusoViews.Length > 0 ? parafusoViews[0] : null;
            UICursor.Set(cursorChave, cursorChaveHotspot, ScrewView.CursorHeight(v, chaveParafusoProporcao));
        }
        else if (tela != null && tela.Owner != null) tela.Owner.ApplyDefaultCursor();
        else UICursor.Reset();
    }

    private NotebookEstacao Estacao()
    {
        if (tela == null) tela = GetComponent<UIScreen>();
        return tela != null && tela.Source != null ? tela.Source.GetComponent<NotebookEstacao>() : null;
    }

    private ItemHolder Mao()
    {
        if (tela == null) tela = GetComponent<UIScreen>();
        return tela != null && tela.Interactor != null ? tela.Interactor.Holder : null;
    }

    // ================================================================== construção

    private void Construir()
    {
        if (construido) return;
        construido = true;

        RedeUi.Esticar((RectTransform)transform);
        RectTransform painel = RedeUi.Cheio(transform, "Painel", CorPainel);
        RedeUi.BotaoFechar(painel, Fechar);

        RectTransform area = RedeUi.Cheio(painel, "Vistas", Color.clear);
        areaVistas = area;
        vistas = new RectTransform[4];
        for (int i = 0; i < 4; i++) vistas[i] = RedeUi.Cheio(area, "Vista" + (NotebookVista)i, Color.clear);

        ConstruirBaixo(vistas[(int)NotebookVista.Baixo]);
        ConstruirAberto(vistas[(int)NotebookVista.Aberto]);
        ConstruirLado(vistas[(int)NotebookVista.Lado]);
        ConstruirFrente(vistas[(int)NotebookVista.Frente]);

        // Setas IGUAIS às do canvas do PC (UI_Screen_Gabinete): os cantos de baixo, sem título de vista e sem
        // dica escrita — a própria tela mostra o que fazer. A "►" fica à ESQUERDA do celular (RedeUi.Setas).
        RedeUi.Setas(painel, () => TrocarVista(-1), () => TrocarVista(1));

        msgTxt = RedeUi.Texto(painel, "Mensagem", "", 26, TextAnchor.MiddleCenter, TextoFraco);
        RedeUi.Ancorar((RectTransform)msgTxt.transform, new Vector2(0.5f, 0.5f), new Vector2(1300f, 40f), new Vector2(0.5f, 0.5f));
        ((RectTransform)msgTxt.transform).anchoredPosition = new Vector2(0f, -498f);
        msgTxt.fontStyle = FontStyle.Bold;
    }

    private void ConstruirBaixo(RectTransform vista)
    {
        RectTransform tampa = RedeUi.Imagem(vista, "Tampa", CorChassi);
        RedeUi.Ancorar(tampa, new Vector2(0.5f, 0.5f), TamanhoTampa, new Vector2(0.5f, 0.5f));
        tampa.anchoredPosition = new Vector2(0f, 20f);

        RectTransform dentro = RedeUi.Imagem(tampa, "Dentro", CorChassiClaro);
        RedeUi.Esticar(dentro, 10f);

        parafusoImg = new Image[PosicaoParafusos.Length];
        parafusoFenda = new RectTransform[PosicaoParafusos.Length];
        parafusoFlash = new float[PosicaoParafusos.Length];

        if (parafusoPrefab != null)
        {
            // Os mesmos parafusos do gabinete, posicionados por âncora (o ScrewView volta à âncora ao animar).
            parafusoViews = new ScrewView[PosicaoParafusos.Length];
            Vector2 tam = TamanhoTampa - new Vector2(20f, 20f);   // "Dentro" tem 10 px de borda
            for (int i = 0; i < PosicaoParafusos.Length; i++)
            {
                Vector2 ancora = new Vector2(0.5f + PosicaoParafusos[i].x * TamanhoTampa.x / tam.x,
                                             0.5f + PosicaoParafusos[i].y * TamanhoTampa.y / tam.y);
                // Cabeça Phillips desenhada POR BAIXO do ScrewView, como o parafuso que está no render da traseira do
                // gabinete: o furo do ScrewView (30 px) a cobre quando o parafuso sai.
                Image aro = MiniUi.ImagemCircular(dentro, "Cabeca" + i, new Color(0.38f, 0.41f, 0.46f), 28f);
                RectTransform art = (RectTransform)aro.transform;
                art.anchorMin = art.anchorMax = ancora;
                art.anchoredPosition = Vector2.zero;
                aro.raycastTarget = false;
                Image cabeca = MiniUi.ImagemCircular(art, "Metal", CorMetal, 23f);
                cabeca.raycastTarget = false;
                for (int k = 0; k < 2; k++)
                {
                    RectTransform fenda = RedeUi.Imagem(cabeca.transform, "Fenda" + k, new Color(0.22f, 0.24f, 0.28f));
                    RedeUi.Ancorar(fenda, new Vector2(0.5f, 0.5f), new Vector2(14f, 3.5f), new Vector2(0.5f, 0.5f));
                    fenda.localRotation = Quaternion.Euler(0f, 0f, 20f + k * 90f);
                    fenda.GetComponent<Image>().raycastTarget = false;
                }

                ScrewView v = Instantiate(parafusoPrefab, dentro, false);
                v.name = "Parafuso" + i;
                var rt = (RectTransform)v.transform;
                rt.anchorMin = rt.anchorMax = ancora;
                rt.anchoredPosition = Vector2.zero;
                v.Init(i, ClicarParafuso);
                parafusoViews[i] = v;
            }
            return;
        }

        for (int i = 0; i < PosicaoParafusos.Length; i++)
        {
            int indice = i;
            Image parafuso = MiniUi.BotaoCircular(dentro, "Parafuso" + i, CorMetal, 82f, () => ClicarParafuso(indice));
            RectTransform rt = (RectTransform)parafuso.transform;
            rt.anchoredPosition = new Vector2(PosicaoParafusos[i].x * TamanhoTampa.x, PosicaoParafusos[i].y * TamanhoTampa.y);

            // Círculo amarelo IGUAL ao do parafuso do PC (ScrewView.prefab): quem pinta é o próprio botão —
            // o anel é o alvo da transição de cor (normal transparente, com o mouse em cima amarelo).
            Image anel = MiniUi.ImagemCircular(rt, "Anel", Color.white, 82f);
            Button botaoParafuso = parafuso.GetComponent<Button>();
            botaoParafuso.targetGraphic = anel;
            ColorBlock cores = botaoParafuso.colors;
            cores.normalColor = new Color(1f, 1f, 1f, 0f);
            cores.highlightedColor = new Color(1f, 0.88f, 0.2f, 0.95f);
            cores.pressedColor = Color.white;
            cores.selectedColor = new Color(1f, 1f, 1f, 0f);
            cores.disabledColor = new Color(1f, 1f, 1f, 0f);
            cores.fadeDuration = 0.08f;
            botaoParafuso.colors = cores;
            anel.canvasRenderer.SetColor(cores.normalColor);   // já nasce invisível (sem piscar branco)

            // Fenda do parafuso (só decorativa, não clicável).
            RectTransform fenda = RedeUi.Imagem(rt, "Fenda", new Color(0.16f, 0.18f, 0.21f));
            RedeUi.Ancorar(fenda, new Vector2(0.5f, 0.5f), new Vector2(52f, 9f), new Vector2(0.5f, 0.5f));
            fenda.localRotation = Quaternion.Euler(0f, 0f, 32f);

            parafusoImg[i] = parafuso;
            parafusoFenda[i] = fenda;
        }
    }

    private void ConstruirAberto(RectTransform vista)
    {
        RectTransform chassi = RedeUi.Imagem(vista, "Chassi", CorChassi);
        RedeUi.Ancorar(chassi, new Vector2(0.5f, 0.5f), new Vector2(1250f, 660f), new Vector2(0.5f, 0.5f));
        chassi.anchoredPosition = new Vector2(0f, 20f);

        RectTransform placa = RedeUi.Imagem(chassi, "Placa", new Color(0.12f, 0.26f, 0.18f));
        RedeUi.Ancorar(placa, new Vector2(0.5f, 0.5f), new Vector2(1130f, 560f), new Vector2(0.5f, 0.5f));

        // ---- encaixe da memória SO-DIMM (em cima, deitada)
        RectTransform furoRam = RedeUi.Imagem(placa, "FuroRam", CorBorda);
        RedeUi.Ancorar(furoRam, new Vector2(0.5f, 0.5f), TamanhoFuroRam, new Vector2(0.5f, 0.5f));
        furoRam.anchoredPosition = new Vector2(-60f, 150f);
        ramMiolo = RedeUi.Imagem(furoRam, "Miolo", CorVazio).GetComponent<Image>();
        RedeUi.Esticar((RectTransform)ramMiolo.transform, 6f);
        ramPeca = RedeUi.Imagem(furoRam, "Peca", CorPlaca).GetComponent<Image>();
        RedeUi.Esticar((RectTransform)ramPeca.transform, 6f);
        Rotulo(placa, "RAM (SO-DIMM)", new Vector2(-60f, 232f));
        TornarClicavel(furoRam, () => ClicarEncaixe(NotebookParte.Ram));

        // ---- encaixe do SSD (embaixo)
        RectTransform furoSsd = RedeUi.Imagem(placa, "FuroSsd", CorBorda);
        RedeUi.Ancorar(furoSsd, new Vector2(0.5f, 0.5f), TamanhoFuroSsd, new Vector2(0.5f, 0.5f));
        furoSsd.anchoredPosition = new Vector2(60f, -150f);
        ssdMiolo = RedeUi.Imagem(furoSsd, "Miolo", CorVazio).GetComponent<Image>();
        RedeUi.Esticar((RectTransform)ssdMiolo.transform, 6f);
        ssdPeca = RedeUi.Imagem(furoSsd, "Peca", CorSsd).GetComponent<Image>();
        RedeUi.Esticar((RectTransform)ssdPeca.transform, 6f);
        Rotulo(placa, "SSD (M.2)", new Vector2(60f, -240f));
        TornarClicavel(furoSsd, () => ClicarEncaixe(NotebookParte.Ssd));
    }

    private void ConstruirLado(RectTransform vista)
    {
        RectTransform corpo = RedeUi.Imagem(vista, "Corpo", CorChassi);
        RedeUi.Ancorar(corpo, new Vector2(0.5f, 0.5f), new Vector2(1150f, 470f), new Vector2(0.5f, 0.5f));
        corpo.anchoredPosition = new Vector2(0f, 10f);

        RectTransform dentro = RedeUi.Imagem(corpo, "Dentro", CorChassiClaro);
        RedeUi.Esticar(dentro, 10f);

        Rotulo(dentro, "PERFIL DIREITO DO NOTEBOOK", new Vector2(0f, 160f));

        RectTransform furoUsb = RedeUi.Imagem(dentro, "Usb", CorBorda);
        RedeUi.Ancorar(furoUsb, new Vector2(0.5f, 0.5f), new Vector2(190f, 90f), new Vector2(0.5f, 0.5f));
        furoUsb.anchoredPosition = new Vector2(-350f, 0f);
        usbMiolo = RedeUi.Imagem(furoUsb, "Miolo", CorVazio).GetComponent<Image>();
        RedeUi.Esticar((RectTransform)usbMiolo.transform, 7f);
        Rotulo(dentro, "USB", new Vector2(-350f, -80f));
        TornarClicavel(furoUsb, ClicarUsb);

        // O pendrive "entrando" pela esquerda da porta.
        pendriveImg = RedeUi.Imagem(dentro, "Pendrive", new Color(0.25f, 0.3f, 0.36f)).GetComponent<Image>();
        RedeUi.Ancorar((RectTransform)pendriveImg.transform, new Vector2(0.5f, 0.5f), new Vector2(210f, 74f), new Vector2(0.5f, 0.5f));
        ((RectTransform)pendriveImg.transform).anchoredPosition = new Vector2(-560f, 0f);

        RectTransform led = RedeUi.Imagem((RectTransform)pendriveImg.transform, "Led", Verde);
        RedeUi.Ancorar(led, new Vector2(1f, 0.5f), new Vector2(14f, 14f), new Vector2(0.5f, 0.5f));
        led.anchoredPosition = new Vector2(10f, 0f);
    }

    private void ConstruirFrente(RectTransform vista)
    {
        // Tela do notebook (aberto, de frente).
        RectTransform tampa = RedeUi.Imagem(vista, "Tampa", CorChassi);
        RedeUi.Ancorar(tampa, new Vector2(0.5f, 0.5f), new Vector2(1040f, 540f), new Vector2(0.5f, 0.5f));
        tampa.anchoredPosition = new Vector2(0f, 150f);

        telaFundo = RedeUi.Imagem(tampa, "Display", CorTela).GetComponent<Image>();
        RedeUi.Esticar((RectTransform)telaFundo.transform, 16f);

        telaTxt = RedeUi.Texto(telaFundo.transform, "Conteudo", "", 30, TextAnchor.MiddleCenter, TextoClaro);
        RedeUi.Esticar((RectTransform)telaTxt.transform, 40f);
        telaTxt.fontStyle = FontStyle.Bold;

        // Base com o teclado e o botão Power.
        RectTransform baseNote = RedeUi.Imagem(vista, "Base", CorChassi);
        RedeUi.Ancorar(baseNote, new Vector2(0.5f, 0.5f), new Vector2(1080f, 200f), new Vector2(0.5f, 0.5f));
        baseNote.anchoredPosition = new Vector2(0f, -230f);

        RectTransform teclado = RedeUi.Imagem(baseNote, "Teclado", new Color(0.28f, 0.32f, 0.37f));
        RedeUi.Ancorar(teclado, new Vector2(0.5f, 0.5f), new Vector2(760f, 140f), new Vector2(0.5f, 0.5f));
        teclado.anchoredPosition = new Vector2(-60f, 0f);

        telaPower = MiniUi.BotaoCircular(baseNote, "Power", CorMetal, 84f, ClicarPower);
        ((RectTransform)telaPower.transform).anchoredPosition = new Vector2(420f, 0f);

        RectTransform simbolo = RedeUi.Imagem(telaPower.transform, "Simbolo", new Color(0.14f, 0.16f, 0.19f));
        RedeUi.Ancorar((RectTransform)simbolo.transform, new Vector2(0.5f, 0.5f), new Vector2(10f, 32f), new Vector2(0.5f, 0.5f));
        simbolo.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);

        Rotulo(baseNote, "POWER", new Vector2(420f, -78f));

        // Barra de progresso da instalação (fica sobre o rodapé da tela).
        barraFill = MiniUi.BarraProgresso(vista, "Barra", new Color(0.16f, 0.19f, 0.23f), Verde,
            new Vector2(0f, -64f), new Vector2(700f, 26f), out RectTransform barraRaiz);
        barraRaiz.gameObject.SetActive(false);

        etapaTxt = RedeUi.Texto(vista, "Etapa", "", 24, TextAnchor.MiddleCenter, TextoClaro);
        RedeUi.Ancorar((RectTransform)etapaTxt.transform, new Vector2(0.5f, 0.5f), new Vector2(900f, 34f), new Vector2(0.5f, 0.5f));
        ((RectTransform)etapaTxt.transform).anchoredPosition = new Vector2(0f, -104f);
    }

    private void Rotulo(Transform pai, string texto, Vector2 pos)
    {
        Text t = RedeUi.Texto(pai, "Rotulo_" + texto, texto, 22, TextAnchor.MiddleCenter, TextoClaro);
        RedeUi.Ancorar((RectTransform)t.transform, new Vector2(0.5f, 0.5f), new Vector2(520f, 30f), new Vector2(0.5f, 0.5f));
        ((RectTransform)t.transform).anchoredPosition = pos;
    }

    /// <summary>Põe um quadrado transparente clicável por cima de um retângulo.</summary>
    private void TornarClicavel(RectTransform alvo, UnityAction acao)
    {
        var go = new GameObject("Clique", typeof(RectTransform));
        go.transform.SetParent(alvo, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;
        var b = go.AddComponent<Button>();
        b.targetGraphic = img;
        b.onClick.AddListener(acao);
        RedeUi.Esticar((RectTransform)go.transform);
    }

    // ================================================================== ações

    // Tampa solta com sucesso: depois de um instante a tela mostra o interior sozinha.
    private System.Collections.IEnumerator AbrirInteriorDepois()
    {
        yield return new WaitForSecondsRealtime(0.6f);
        if (vista == NotebookVista.Baixo && PodeVer(NotebookVista.Aberto))
        {
            vista = NotebookVista.Aberto;
            Atualizar();
            Mensagem("Tampa aberta: interior acessível.", Verde);
        }
    }

    private void TrocarVista(int passo)
    {
        if (passo == 0) return;
        int direcao = passo > 0 ? 1 : -1;
        int atual = (int)vista;
        // Anda de vista em vista pulando as bloqueadas: sem a tampa solta (nenhum parafuso preso), a vista
        // de dentro (Aberto, onde vão a RAM e o SSD) sai do giro das setas.
        for (int i = 0; i < 4; i++)
        {
            atual = ((atual + direcao) % 4 + 4) % 4;
            if (PodeVer((NotebookVista)atual)) break;
        }
        vista = (NotebookVista)atual;
        Mensagem("", TextoFraco);
        Atualizar();
    }

    /// <summary>
    /// A vista <see cref="NotebookVista.Aberto"/> (parte de dentro, onde vão a memória e o SSD) só pode ser
    /// vista com a tampa solta — quer dizer, com NENHUM dos 6 parafusos na traseira. Com qualquer parafuso
    /// preso, ela fica bloqueada (as setas a pulam).
    /// </summary>
    private bool PodeVer(NotebookVista v)
    {
        if (v != NotebookVista.Aberto) return true;
        NotebookEstacao est = Estacao();
        return est != null && est.Sessao.Estado == NotebookEstado.Aberto;
    }

    private void ClicarParafuso(int indice)
    {
        NotebookEstacao est = Estacao();
        if (est == null) return;

        ToolType ferramenta = FerramentaNaMao();

        // Furo vazio (parafuso já removido) = recolocar; parafuso no lugar = soltar (estado real de cada um).
        if (est.Sessao.ParafusoRemovido(indice))
        {
            switch (est.InstalarParafuso(indice, ferramenta))
            {
                case NotebookParafusoResultado.Instalado:
                    InteractionSounds.Raise(InteractionSound.ScrewInstalled);
                    // Todos de volta = tampa presa de novo (notebook montado para entregar).
                    if (est.Sessao.TodosParafusosInstalados)
                    {
                        InteractionSounds.Raise(InteractionSound.CaseClosed);
                        Mensagem("Tampa de baixo presa de novo — o notebook está montado para entregar.", Verde);
                    }
                    else Mensagem("Parafuso " + (indice + 1) + " de volta no lugar.", TextoFraco);
                    break;
                case NotebookParafusoResultado.FerramentaErrada:
                    InteractionSounds.Raise(InteractionSound.Error);   // ferramenta errada: som de erro
                    Mensagem("Precisa de uma chave de fenda na mão.", Vermelho);
                    Recusar(indice);
                    break;
                default:
                    Mensagem("Não dá para recolocar esse parafuso agora.", Amarelo);
                    break;
            }
            Atualizar();
            return;
        }

        NotebookParafusoResultado r = est.RemoverParafuso(indice, ferramenta);
        switch (r)
        {
            case NotebookParafusoResultado.Removido:
                InteractionSounds.Raise(InteractionSound.ScrewRemoved);
                if (est.Sessao.TodosParafusosRemovidos)
                {
                    InteractionSounds.Raise(InteractionSound.CaseOpened);   // a tampa abriu (como o gabinete do PC)
                    Mensagem("Tampa aberta: interior acessível.", Verde);
                    StartCoroutine(AbrirInteriorDepois());   // vai sozinho para a vista de dentro
                }
                else Mensagem("Parafuso " + (indice + 1) + " removido.", TextoFraco);
                break;
            case NotebookParafusoResultado.FerramentaErrada:
                InteractionSounds.Raise(InteractionSound.Error);   // ferramenta errada: som de erro
                Mensagem("Precisa de uma chave de fenda na mão.", Vermelho);
                Recusar(indice);
                break;
            case NotebookParafusoResultado.JaRemovido:
                Mensagem("Esse parafuso já saiu.", Amarelo);
                break;
            default:
                Mensagem("Não dá para soltar esse parafuso agora.", Amarelo);
                break;
        }
        Atualizar();
    }

    private void ClicarEncaixe(NotebookParte encaixe)
    {
        NotebookEstacao est = Estacao();
        if (est == null) return;

        ItemHolder mao = Mao();
        PickupInteractable naMao = mao != null ? mao.Current : null;

        NotebookEncaixeResultado r = est.UsarEncaixe(encaixe, naMao, out GameObject devolver);
        switch (r)
        {
            case NotebookEncaixeResultado.Instalado:
                if (mao != null) mao.Stow(est.transform.position);
                InteractionSounds.Raise(InteractionSound.PartInstalled);
                Mensagem(encaixe == NotebookParte.Ram ? "Memória SO-DIMM instalada." : "SSD instalado.", Verde);
                break;
            case NotebookEncaixeResultado.Removido:
                if (mao != null && devolver != null) mao.Retrieve(devolver);
                InteractionSounds.Raise(InteractionSound.PartRemoved);
                Mensagem("Peça retirada.", TextoFraco);
                break;
            case NotebookEncaixeResultado.PrecisaPeca:
                Mensagem("Encaixe vazio.", Amarelo);
                break;
            case NotebookEncaixeResultado.PecaErrada:
                InteractionSounds.Raise(InteractionSound.Error);   // peça no encaixe errado
                Mensagem("Essa peça não encaixa aqui.", Vermelho);
                break;
            case NotebookEncaixeResultado.MaoCheia:
                Mensagem("Tire a peça da mão para retirar esta.", Amarelo);
                break;
            case NotebookEncaixeResultado.Fechado:
                Mensagem("A tampa de baixo ainda está fechada.", Amarelo);
                break;
            case NotebookEncaixeResultado.PecaQuebrada:
                Mensagem("Essa peça quebrou na queda: pegue outra.", Vermelho);
                break;
        }
        Atualizar();
    }

    private void ClicarUsb()
    {
        NotebookEstacao est = Estacao();
        if (est == null) return;

        ItemHolder mao = Mao();
        PickupInteractable naMao = mao != null ? mao.Current : null;

        NotebookUsbResultado r = est.UsarUsb(naMao, out GameObject devolver);
        switch (r)
        {
            case NotebookUsbResultado.Conectado:
                if (mao != null) mao.Stow(est.transform.position);
                InteractionSounds.Raise(InteractionSound.PendrivePlugged);
                Mensagem("Pendrive conectado.", Verde);
                break;
            case NotebookUsbResultado.Devolvido:
                if (mao != null && devolver != null) mao.Retrieve(devolver);
                InteractionSounds.Raise(InteractionSound.PendriveUnplugged);
                Mensagem("Pendrive retirado.", TextoFraco);
                break;
            case NotebookUsbResultado.PrecisaPendrive:
                Mensagem("Ponha um pendrive na mão e clique na porta USB.", Amarelo);
                break;
            case NotebookUsbResultado.ItemErrado:
                InteractionSounds.Raise(InteractionSound.Error);   // item errado na porta USB
                Mensagem("Só um pendrive entra nessa porta.", Vermelho);
                break;
            case NotebookUsbResultado.MaoCheia:
                Mensagem("Tire o que está na mão para retirar o pendrive.", Amarelo);
                break;
        }
        Atualizar();
    }

    private void ClicarPower()
    {
        NotebookEstacao est = Estacao();
        if (est == null) return;

        NotebookBotaoResultado r = est.PressPower();
        InteractionSounds.Raise(InteractionSound.PowerButton);
        if (r == NotebookBotaoResultado.FaltamRequisitos)
        {
            // sintoma, não diagnóstico: o LED pisca e nada acontece
            Mensagem("O notebook não deu sinal de vida.", Amarelo);
            if (estado3D != null) estado3D.PiscarFalha();
            NotebookModeloEstado mundo = est.GetComponentInChildren<NotebookModeloEstado>();
            if (mundo != null) mundo.PiscarFalha();
        }
        else if (r == NotebookBotaoResultado.Desligou)
            Mensagem("Notebook desligado.", TextoFraco);
        else if (est.Sessao.Instalando)
            Mensagem("Iniciando pelo pendrive...", Amarelo);
        else if (est.Sessao.TemSistema)
            Mensagem("Notebook ligado.", Verde);
        else
            Mensagem("O notebook ligou, mas não iniciou o sistema.", Amarelo);
        Atualizar();
    }

    /// <summary>Recusa (ferramenta errada): o parafuso do gabinete treme; na versão antiga, pisca vermelho.</summary>
    private void Recusar(int indice)
    {
        if (parafusoViews != null) parafusoViews[indice].PlayRejected();
        else parafusoFlash[indice] = 0.45f;
    }

    private ToolType FerramentaNaMao()
    {
        ItemHolder mao = Mao();
        if (mao == null || mao.Current == null) return ToolType.None;
        ToolItem t = mao.Current.GetComponentInParent<ToolItem>();
        return t != null ? t.Type : ToolType.None;
    }

    // ================================================================== desenho

    private void Atualizar()
    {
        if (!construido) return;
        // Proteção: a vista de dentro (Aberto) não pode aparecer com a tampa presa — cai para a tampa de baixo.
        if (!PodeVer(vista)) vista = NotebookVista.Baixo;
        NotebookEstacao est = Estacao();

        for (int i = 0; i < 4; i++) vistas[i].gameObject.SetActive(!modo3D && i == (int)vista);
        if (modo3D)
        {
            for (int i = 0; i < 4; i++) overlays[i].gameObject.SetActive(i == (int)vista);
        }

        if (est == null) return;

        if (vista == NotebookVista.Baixo && parafusoViews != null)
        {
            bool primeira = parafusoEstado == null;
            if (primeira) parafusoEstado = new bool[parafusoViews.Length];
            for (int i = 0; i < parafusoViews.Length; i++)
            {
                bool removido = est.Sessao.ParafusoRemovido(i);
                if (!primeira && removido == parafusoEstado[i]) continue;
                parafusoViews[i].SetRemoved(removido, animate: !primeira);   // anima só o que acabou de mudar
                parafusoEstado[i] = removido;
            }
        }
        else if (vista == NotebookVista.Baixo)
        {
            for (int i = 0; i < parafusoImg.Length; i++)
            {
                bool removido = est.Sessao.ParafusoRemovido(i);
                Color cor = removido ? CorVazio : (parafusoFlash[i] > 0f ? Vermelho : CorMetal);
                parafusoImg[i].color = cor;
                // O furo vazio continua clicável: clicar nele recoloca o parafuso (com a chave de fenda na mão).
                parafusoImg[i].raycastTarget = true;
                parafusoFenda[i].gameObject.SetActive(!removido);
            }
        }
        else if (vista == NotebookVista.Aberto)
        {
            bool ram = est.Sessao.TemRam;
            bool ssd = est.Sessao.TemSsd;
            ramPeca.gameObject.SetActive(ram);
            ramMiolo.gameObject.SetActive(!ram);
            ssdPeca.gameObject.SetActive(ssd);
            ssdMiolo.gameObject.SetActive(!ssd);
        }
        else if (vista == NotebookVista.Lado)
        {
            bool usb = est.Sessao.PendriveConectado;
            pendriveImg.gameObject.SetActive(usb);
            usbMiolo.color = usb ? new Color(0.2f, 0.35f, 0.25f) : CorVazio;
        }
        else if (vista == NotebookVista.Frente) AtualizarFrente(est);
    }

    // ================================================================== modo 3D

    // Uma vista 3D: a cópia do modelo com câmera fixa (ModeloNaTela) e o estado dela. Os pontos clicáveis são filhos da
    // própria imagem e ficam ancorados (fração 0..1) em cima das peças, como os parafusos do gabinete.
    private sealed class Vista3D
    {
        public ModeloNaTela modelo;
        public NotebookModeloEstado estado;
        public Vector3 olhar, cima;
        public string[] enquadrar;   // peças que a câmera enquadra (null = o notebook todo)
        public float borda;
        public bool parafusos = true;
        public bool manterTampa;     // tampa de baixo sempre à vista (vista de baixo)
        public string[] apenas;      // só estas partes (e filhas) aparecem (null = todas)
        public string[] ocultar;     // partes (e filhas) que não aparecem
    }

    private Vista3D v3Baixo, v3Aberto, v3Lado, v3Tela, v3Teclado;

    /// <summary>Copia o modelo do notebook que abriu a tela (uma vez por notebook) em cada vista 3D.</summary>
    private void PrepararModelo3D(NotebookEstacao est)
    {
        Transform visual = est != null ? est.transform.Find("Visual_Toon") : null;
        if (visual == null || visual.GetComponent<NotebookModeloEstado>() == null)
        {
            modo3D = false;
            if (overlays != null) foreach (RectTransform o in overlays) o.gameObject.SetActive(false);
            return;
        }
        if (overlays == null) ConstruirOverlays();
        if (origem3D != visual.gameObject)
        {
            foreach (Vista3D v in new[] { v3Baixo, v3Aberto, v3Lado, v3Tela, v3Teclado })
            {
                Transform copia = v.modelo.Copiar(visual.gameObject, typeof(NotebookModeloEstado));
                v.estado = copia != null ? copia.GetComponent<NotebookModeloEstado>() : null;
                if (v.estado == null) continue;
                v.estado.MostrarParafusos = v.parafusos;
                v.estado.ManterTampa = v.manterTampa;
                Esconder(copia, v);
                Transform[] alvos = v.enquadrar == null ? new Transform[0] : System.Array.ConvertAll(v.enquadrar, n => v.estado.Parte(n));
                v.modelo.Enquadrar(v.olhar, v.cima, v.modelo.BoundsLocal(alvos), v.borda);
            }
            origem3D = visual.gameObject;
        }
        foreach (Vista3D v in new[] { v3Baixo, v3Aberto, v3Lado, v3Tela, v3Teclado }) if (v.estado != null) v.estado.Vincular(est);
        estado3D = v3Teclado.estado;
        modo3D = v3Baixo.estado != null;
    }

    // Tira da imagem as partes que não são desta vista (só o desenho: as peças continuam lá para o estado e os alvos).
    private static void Esconder(Transform copia, Vista3D v)
    {
        if (v.apenas == null && v.ocultar == null) return;
        var visiveis = new System.Collections.Generic.List<Transform>();
        if (v.apenas != null) foreach (string n in v.apenas) { Transform p = v.estado.Parte(n); if (p != null) visiveis.Add(p); }
        foreach (Renderer r in copia.GetComponentsInChildren<Renderer>(true))
        {
            bool mostra = v.apenas == null || visiveis.Exists(p => r.transform.IsChildOf(p));
            if (mostra && v.ocultar != null)
                foreach (string n in v.ocultar) { Transform p = v.estado.Parte(n); if (p != null && r.transform.IsChildOf(p)) mostra = false; }
            r.forceRenderingOff = !mostra;
        }
    }

    private Vista3D NovaVista(RectTransform pai, string nome, Rect ancoras, Vector3 olhar, Vector3 cima, string[] enquadrar, float borda)
    {
        var v = new Vista3D { olhar = olhar, cima = cima, enquadrar = enquadrar, borda = borda };
        v.modelo = ModeloNaTela.Criar(pai, nome, new Vector2Int(1600, 1000));
        var rt = (RectTransform)v.modelo.transform;
        rt.anchorMin = ancoras.min;
        rt.anchorMax = ancoras.max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return v;
    }

    private void ConstruirOverlays()
    {
        var raiz = RedeUi.Cheio(areaVistas, "Vistas3D", Color.clear);
        raiz.GetComponent<Image>().raycastTarget = false;
        overlays = new RectTransform[4];
        for (int i = 0; i < 4; i++)
        {
            overlays[i] = RedeUi.Cheio(raiz, "Vista3D_" + (NotebookVista)i, Color.clear);
            overlays[i].GetComponent<Image>().raycastTarget = false;
        }
        // No espaço do modelo: frente = +Z, portas = +X, dobradiça em -Z; tela aberta ~110°.
        Rect centro = Rect.MinMaxRect(0.17f, 0.12f, 0.83f, 0.92f);   // menor que a tela inteira: sobra espaço em volta
        v3Baixo = NovaVista(overlays[(int)NotebookVista.Baixo], "Baixo", centro, Vector3.up, Vector3.back, new[] { "Base" }, 0.12f);
        v3Baixo.parafusos = false;   // as cabeças e os furos são da tela (ScrewView), como no gabinete
        v3Baixo.manterTampa = true;  // o interior aparece só na vista "Aberto": aqui é sempre a tampa com os parafusos
        v3Aberto = NovaVista(overlays[(int)NotebookVista.Aberto], "Aberto", centro,
                             new Vector3(0f, 1f, -0.42f), Vector3.back, new[] { "Base" }, 0.08f);   // de baixo, pouco inclinado: quase a proporção da vista "Baixo" (a 45° o notebook saía achatado), ainda com relevo
        v3Aberto.parafusos = false;  // parafusos só na vista de baixo (recolocados com a tampa aberta, ficariam soltos aqui)
        v3Lado = NovaVista(overlays[(int)NotebookVista.Lado], "Lado", centro, Vector3.left, Vector3.up, null, 0.1f);
        // Frente: como o monitor e o gabinete na manutenção do PC, a tela de um lado e o teclado "em pé" do outro
        Quaternion tampa = Quaternion.Euler(-110f, 0f, 0f);
        v3Tela = NovaVista(overlays[(int)NotebookVista.Frente], "Tela", Rect.MinMaxRect(0.03f, 0.08f, 0.47f, 0.95f),
                           tampa * Vector3.up, tampa * Vector3.forward, new[] { "Tampa" }, 0.06f);
        v3Tela.apenas = new[] { "Dobradica" };   // só a tampa com a tela
        v3Teclado = NovaVista(overlays[(int)NotebookVista.Frente], "Teclado", Rect.MinMaxRect(0.49f, 0.08f, 0.86f, 0.95f)   /* à direita fica a miniatura da mão */,
                              Vector3.down, Vector3.back, new[] { "Base" }, 0.06f);
        v3Teclado.ocultar = new[] { "Dobradica" };   // só a base com o teclado (a tela aberta ficaria na frente)

        // Baixo: cabeça Phillips desenhada + o MESMO ScrewView do gabinete (furo, anel, animações, cursor da chave)
        if (parafusoPrefab != null)
        {
            parafusoViews = new ScrewView[PosicaoParafusos.Length];
            cabecas3D = new RectTransform[PosicaoParafusos.Length];
            for (int i = 0; i < parafusoViews.Length; i++)
            {
                cabecas3D[i] = CabecaPhillips(v3Baixo.modelo.transform, "Cabeca" + i);
                ScrewView v = Instantiate(parafusoPrefab, v3Baixo.modelo.transform, false);
                v.name = "Parafuso" + i;
                v.Init(i, ClicarParafuso);
                parafusoViews[i] = v;
            }
        }

        hotRam = Alvo(v3Aberto.modelo.transform, "Encaixe_RAM", () => ClicarEncaixe(NotebookParte.Ram));
        hotSsd = Alvo(v3Aberto.modelo.transform, "Encaixe_SSD", () => ClicarEncaixe(NotebookParte.Ssd));
        hotUsb = Alvo(v3Lado.modelo.transform, "Porta_USB", ClicarUsb);
        hotPower = Alvo(v3Teclado.modelo.transform, "Power", ClicarPower);

        barra3D = MiniUi.BarraProgresso(v3Tela.modelo.transform, "Barra", new Color(0.16f, 0.19f, 0.23f), Verde,
            Vector2.zero, new Vector2(520f, 20f), out RectTransform barraRaiz);
        RedeUi.Ancorar(barraRaiz, new Vector2(0.5f, 0f), new Vector2(520f, 20f), new Vector2(0.5f, 1f));
        barraRaiz.anchoredPosition = new Vector2(0f, -10f);
        barraRaiz.gameObject.SetActive(false);
        etapa3D = RedeUi.Texto(barraRaiz, "Etapa", "", 22, TextAnchor.MiddleCenter, TextoClaro);
        RedeUi.Ancorar((RectTransform)etapa3D.transform, new Vector2(0.5f, 0f), new Vector2(600f, 30f), new Vector2(0.5f, 1f));
        ((RectTransform)etapa3D.transform).anchoredPosition = new Vector2(0f, -4f);
    }

    private RectTransform[] cabecas3D;
    // Cabeça de parafuso Phillips (aro escuro, metal e a cruz), como a dos renders do gabinete: o furo do ScrewView a cobre
    // quando o parafuso sai.
    private RectTransform CabecaPhillips(Transform pai, string nome)
    {
        Image aro = MiniUi.ImagemCircular(pai, nome, new Color(0.3f, 0.32f, 0.37f), 30f);
        aro.raycastTarget = false;
        Image metal = MiniUi.ImagemCircular(aro.transform, "Metal", CorMetal, 24f);
        metal.raycastTarget = false;
        for (int k = 0; k < 2; k++)
        {
            RectTransform fenda = RedeUi.Imagem(metal.transform, "Fenda" + k, new Color(0.22f, 0.24f, 0.28f));
            RedeUi.Ancorar(fenda, new Vector2(0.5f, 0.5f), new Vector2(15f, 3.5f), new Vector2(0.5f, 0.5f));
            fenda.localRotation = Quaternion.Euler(0f, 0f, 20f + k * 90f);
            fenda.GetComponent<Image>().raycastTarget = false;
        }
        return (RectTransform)aro.transform;
    }

    /// <summary>
    /// Área clicável sobre uma peça do modelo: INVISÍVEL (a peça encaixando continua à vista), com uma moldura amarela
    /// fina só enquanto o mouse está em cima — o mesmo retorno dos encaixes do gabinete.
    /// </summary>
    private RectTransform Alvo(Transform pai, string nome, UnityAction acao)
    {
        var go = new GameObject("Alvo_" + nome, typeof(RectTransform));
        go.transform.SetParent(pai, false);
        var rt = (RectTransform)go.transform;
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);   // só recebe o clique
        var b = go.AddComponent<Button>();
        b.targetGraphic = img;
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(acao);
        var moldura = new GameObject("Moldura", typeof(RectTransform)).transform;
        moldura.SetParent(rt, false);
        RedeUi.Esticar((RectTransform)moldura);
        Color amarelo = new Color(1f, 0.86f, 0.2f, 0.95f);
        foreach ((Vector2 min, Vector2 max) in new[] { (new Vector2(0f, 1f), new Vector2(1f, 1f)), (new Vector2(0f, 0f), new Vector2(1f, 0f)),
                                                       (new Vector2(0f, 0f), new Vector2(0f, 1f)), (new Vector2(1f, 0f), new Vector2(1f, 1f)) })
        {
            RectTransform linha = RedeUi.Imagem(moldura, "Linha", amarelo);
            linha.anchorMin = min; linha.anchorMax = max;
            bool horizontal = min.y == max.y;
            linha.sizeDelta = horizontal ? new Vector2(0f, 4f) : new Vector2(4f, 0f);
            linha.anchoredPosition = Vector2.zero;
            linha.GetComponent<Image>().raycastTarget = false;
        }
        moldura.gameObject.SetActive(false);
        var hover = go.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        Gatilho(hover, UnityEngine.EventSystems.EventTriggerType.PointerEnter, () => moldura.gameObject.SetActive(true));
        Gatilho(hover, UnityEngine.EventSystems.EventTriggerType.PointerExit, () => moldura.gameObject.SetActive(false));
        return rt;
    }

    private static void Gatilho(UnityEngine.EventSystems.EventTrigger t, UnityEngine.EventSystems.EventTriggerType tipo, UnityAction acao)
    {
        var e = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = tipo };
        e.callback.AddListener(_ => acao());
        t.triggers.Add(e);
    }

    // Os pontos clicáveis acompanham as peças do modelo (ancorados: o ScrewView volta à âncora quando anima).
    private void LateUpdate()
    {
        if (!modo3D) return;
        switch (vista)
        {
            case NotebookVista.Baixo:
                if (parafusoViews == null) break;
                for (int i = 0; i < parafusoViews.Length; i++)
                {
                    Transform t = v3Baixo.estado.Parte("Parafuso_" + i);
                    if (t == null) continue;
                    Vector2 a = v3Baixo.modelo.Ancora(t.position);
                    Ancorar(parafusoViews[i].transform, a);
                    Ancorar(cabecas3D[i], a);
                }
                break;
            case NotebookVista.Aberto:
                Cobrir(hotRam, v3Aberto, "SlotRAM", "RAM_Instalada", 12f);
                Cobrir(hotSsd, v3Aberto, "SlotSSD", "SSD_Instalado", 12f);
                break;
            case NotebookVista.Lado:
                Cobrir(hotUsb, v3Lado, "Porta_USB", "Pendrive", 18f);
                break;
            default:
                Cobrir(hotPower, v3Teclado, "BotaoPower", "BotaoPower", 16f);
                break;
        }
    }

    private static void Ancorar(Transform t, Vector2 a)
    {
        var rt = (RectTransform)t;
        rt.anchorMin = rt.anchorMax = a;
        rt.anchoredPosition = Vector2.zero;
    }

    // Área clicável cobrindo o encaixe e a peça encaixada (mesmo com o encaixe vazio a área é a mesma).
    private static void Cobrir(RectTransform alvo, Vista3D v, string encaixe, string peca, float folga)
    {
        if (alvo == null || v.estado == null) return;
        Rect a = v.modelo.Retangulo(v.estado.Parte(encaixe)), b = v.modelo.Retangulo(v.estado.Parte(peca));
        Rect r = Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin) - folga, Mathf.Min(a.yMin, b.yMin) - folga,
                                 Mathf.Max(a.xMax, b.xMax) + folga, Mathf.Max(a.yMax, b.yMax) + folga);
        alvo.anchorMin = alvo.anchorMax = new Vector2(0.5f, 0.5f);
        alvo.anchoredPosition = r.center;
        alvo.sizeDelta = new Vector2(Mathf.Max(44f, r.width), Mathf.Max(44f, r.height));
    }

    private void AtualizarFrente(NotebookEstacao est)
    {
        NotebookSession s = est.Sessao;
        if (modo3D)
        {
            // sem barra nem "Etapa N de 5": a própria tela de instalação (imagens por etapa) mostra o andamento
            ((RectTransform)barra3D.transform.parent).gameObject.SetActive(false);
            etapa3D.text = "";
            return;
        }
        bool ligado = s.Ligado;

        telaFundo.color = ligado ? new Color(0.08f, 0.1f, 0.14f) : CorTela;
        telaPower.color = ligado ? Verde : CorMetal;

        // Mesmas telas do monitor do PC (BIOS, sem sistema, instalando por etapa, sistema), quando configuradas.
        // Desligado = tela preta, como o monitor (screenOffColor do gabinete).
        Sprite imagem = ImagemDaTela(s);
        if (telaSistema != null)
        {
            telaFundo.sprite = imagem;
            telaFundo.color = imagem != null ? Color.white : Color.black;
            telaTxt.text = "";
            ((RectTransform)barraFill.transform.parent).gameObject.SetActive(false);
            etapaTxt.text = "";
            return;
        }

        string texto;
        switch (s.Tela)
        {
            case NotebookEstadoTela.SemArmazenamento:
                texto = "BIOS\n\nFalha ao iniciar o sistema.";
                break;
            case NotebookEstadoTela.SemSistema:
                texto = "BIOS\n\nFalha ao iniciar o sistema.";
                break;
            case NotebookEstadoTela.Instalando:
                texto = "INSTALANDO O SISTEMA...\n\nNão retire o pendrive nem o SSD.";
                break;
            case NotebookEstadoTela.Sistema:
                texto = "SISTEMA INSTALADO\n\nO notebook está pronto para usar.";
                break;
            default:
                texto = "Notebook desligado.\n\nClique no botão Power.";
                break;
        }
        telaTxt.text = texto;

        // sem barra nem "Etapa N de 5" (redundantes com a tela "INSTALANDO O SISTEMA...")
        ((RectTransform)barraFill.transform.parent).gameObject.SetActive(false);
        etapaTxt.text = "";
    }

    private Sprite ImagemDaTela(NotebookSession s)
    {
        switch (s.Tela)
        {
            case NotebookEstadoTela.SemArmazenamento: return telaSemArmazenamento;
            case NotebookEstadoTela.SemSistema: return telaSemSistema != null ? telaSemSistema : telaSemArmazenamento;
            case NotebookEstadoTela.Instalando:
                return telaInstalando.Length > 0 ? telaInstalando[Mathf.Clamp(s.EtapaInstalacao, 0, telaInstalando.Length - 1)] : null;
            case NotebookEstadoTela.Sistema: return telaSistema;
            default: return null;
        }
    }

    // Recados relevantes (verde = deu certo, amarelo/vermelho = algo impediu) vão para o cartão da manutenção
    // (FeedbackManutencao, o mesmo do PC); os neutros (parafuso N removido...) ficam na linha discreta de baixo.
    private void Mensagem(string texto, Color cor)
    {
        if (msgTxt == null) return;
        bool cartao = !string.IsNullOrEmpty(texto) && (cor == Verde || cor == Amarelo || cor == Vermelho);
        msgTxt.text = cartao ? "" : texto;
        msgTxt.color = cor;
        if (cartao)
            FeedbackManutencao.Mostrar(this, texto, cor == Verde ? TipoFeedback.Sucesso : TipoFeedback.Alerta);
    }

    private void Fechar()
    {
        if (tela != null) tela.RequestClose();
    }
}
