using System.Collections.Generic;
using EstagiarIo.Rede;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Tela 2D (cheia) da crimpagem do cabo de rede: cortar (caixa deitada, cabo saindo DE LADO) -> tirar a capa
/// (um clique só) -> ordenar os 8 fios (T568B) -> clicar no RJ45, que já encaixa e crimpa. O cabo é desenhado
/// na VERTICAL, de baixo para cima, saindo da tela por baixo.
/// <para>
/// Depois de descascado, os 8 fios ficam SOLTOS saindo da capa azul e a fileira de posições (1..8) começa VAZIA:
/// o jogador clica num fio (ele é "pegado" e a ponta segue o mouse, como o cabo de energia) e clica na posição
/// onde ele quer encaixar. É o jogo que percebe se a ordem final está no padrão T568B (ao crimpar).
/// </para>
/// Monta a própria interface por código; por isso não depende de um prefab cheio de elementos amarrados à mão.
/// Fica num objeto de cena sob o ScreenCanvas e começa desativada (o UIScreenManager liga ao abrir).
/// </summary>
[RequireComponent(typeof(UIScreen))]
public class RedeCrimpScreen : MonoBehaviour
{
    private static readonly Color CorPainel = new Color(0.09f, 0.11f, 0.14f, 1f);
    private static readonly Color AzulCapa = new Color(0.16f, 0.42f, 0.86f, 1f);
    private static readonly Color TextoClaro = new Color(0.92f, 0.94f, 0.97f);
    private static readonly Color Transparente = new Color(1f, 1f, 1f, 0f);

    // Geometria do cabo (pixels da referência 1920x1080; coordenadas LOCAIS do container do cabo, centro = 0).
    private const float EspacamentoFio = 46f;     // distância entre os fios (bem juntos, um do lado do outro)
    private const float LarguraFio = 22f;
    private const float LarguraCapa = 360f;       // o cabo azul é largo: TODOS os 8 fios saem dele
    private const float TopoCapaInteira = 10f;    // y do topo da capa quando o cabo ainda está inteiro
    private const float FundoCapa = -620f;        // y do fundo da capa (o cabo sai da tela por baixo)
    private const float BaseFios = -80f;          // y de onde os fios saem (topo da capa descascada)
    private const float RailY = 130f;             // y do trilho onde os fios encaixam
    private const float LarguraTrilho = 8f * EspacamentoFio;
    private const float CentroColuna = 60f;       // centro da área clicável da coluna
    private const float AlturaColuna = 360f;
    // posições (y) DENTRO da coluna
    private const float RecuoNaCapa = 24f;                                           // quanto a base dos fios entra na capa
    private const float BaseDosFios = BaseFios - RecuoNaCapa;                        // base dos fios (dentro da capa azul)
    private const float AlturaFioSolto = 64f;                                        // fio ainda solto
    private const float FioSoltoY = BaseDosFios - CentroColuna + AlturaFioSolto * 0.5f;
    private const float PontaSoltoY = BaseDosFios - CentroColuna + AlturaFioSolto + 8f;
    private const float TopoFioColocado = RailY - 15f;                               // onde o fio encaixado encosta no trilho
    private const float AlturaFioColocado = TopoFioColocado - BaseDosFios;
    private const float FioColocadoY = (TopoFioColocado + BaseDosFios) * 0.5f - CentroColuna;
    private const float PontaColocadoY = TopoFioColocado - CentroColuna + 8f;
    private const float SoqueteY = RailY - CentroColuna;

    // O "macho" (RJ45) da crimpagem é do tamanho do CABO AZUL GRANDE: a boca do conector cobre os 8 fios
    // (a largura do cabo azul), e não a grossura de um cabinho (um fio solto ou o cabo que sai da caixa).
    // Na largura ele sai assim mesmo (no MinigameTest o retângulo do RJ45 tem 240 de largura ao lado de um
    // cabo de 290); na altura ele é esticado para cima com a proporção do próprio desenho: o PÉ fica logo
    // acima dos fios e o resto sobe e sai da tela (não precisa caber).
    private const float LarguraMacho = LarguraCapa;   // 360 = a largura do cabo azul
    private const float PeDoMacho = 157f;             // y (local) do PÉ do conector, logo acima dos fios
    private const float AlturaAreaMacho = 207f;       // altura do brilho/área clicável (no pé do conector)

    // Fase "Cortar": uma caixa GRANDE DEITADA no canto esquerdo (pode até sair da tela). O cabo sai DE LADO,
    // na horizontal, e é bem mais fino que o cabo em pé. O jogador tem de clicar no ponto certo para cortar
    // (sem botão). Depois do corte a caixa não volta mais.
    private const float CaixaCortarLargura = 1300f;
    private const float CaixaCortarAltura = 700f;
    private const float CaixaCortarDireita = -220f;   // borda direita da caixa (não cobre o cabo em pé)
    private const float CaixaCortarX = CaixaCortarDireita - CaixaCortarLargura * 0.5f;
    private const float CaixaCortarY = 40f;
    private const float SaidaCaixaY = -40f;           // altura em que o cabo sai da caixa (na horizontal)
    private const float SaidaCaboComprimento = 300f;  // quanto o cabo sai para fora da caixa
    private const float SaidaCaboEspessura = 90f;
    private const float SaidaCaboX = CaixaCortarDireita + SaidaCaboComprimento * 0.5f;
    private const float PontoCorteX = -140f;          // x do ponto certo do corte (faixa amarela sobre o cabo)
    private const float CaboCentroY = 30f;            // centro (y) do container do cabo em pé

    private readonly RedeCrimpSession sessao = new RedeCrimpSession();
    private readonly List<Vector2> pontosCurva = new List<Vector2>();

    private bool construido;
    private int origemColuna = -1;

    [Tooltip("Prefab do cabo que aparece na mão do jogador quando a crimpagem dá certo (ex.: Pickup_CaboRede).")]
    [SerializeField] private GameObject caboPrefab;

    [Tooltip("Cortar, descascar e crimpar pedem o alicate de crimpar na mão (item com ToolItem do tipo Crimping Tool), " +
             "como os parafusos pedem a chave de fenda. Desligado = mãos livres bastam.")]
    [SerializeField] private bool exigirAlicate = true;

    [Header("Artes do protótipo (cena MinigameTest) — vazio = desenho simples de reserva")]
    [Tooltip("Fundo da tela: Assets/Art/Textures/Sprites/bg1.png")]
    [SerializeField] private Sprite spriteFundo;
    [Tooltip("Caixa de cabo deitada (etapa de cortar): Assets/Art/Textures/Sprites/caixa.png")]
    [SerializeField] private Sprite spriteCaixa;
    [Tooltip("Capa azul do cabo: Assets/Art/Textures/Sprites/capa.png")]
    [SerializeField] private Sprite spriteCapa;
    [Tooltip("Conector RJ45 (etapa de ordenar): Assets/Art/Textures/Sprites/conector.png")]
    [SerializeField] private Sprite spriteRj45;
    [Tooltip("O quanto o fundo (bg1) é escurecido para os textos claros ficarem legíveis. 0 = arte pura, 1 = quase preto.")]
    [Range(0f, 1f)] [SerializeField] private float veuDoFundo = 0.62f;

    [Header("Efeitos do protótipo")]
    [Tooltip("Alicate que segue o mouse na etapa de cortar: Assets/Art/Textures/Sprites/alicate1.png")]
    [SerializeField] private Sprite spriteAlicateAberto;
    [Tooltip("Alicate no momento do clique: Assets/Art/Textures/Sprites/alicate2.png")]
    [SerializeField] private Sprite spriteAlicateFechado;
    [Tooltip("Tamanho do alicate desenhado (pixels da tela), como no MinigameTest.")]
    [SerializeField] private Vector2 tamanhoAlicate = new Vector2(150f, 180f);
    [Tooltip("Deslocamento do alicate em relação ao mouse (a garra fica onde o jogador está apontando).")]
    [SerializeField] private Vector2 deslocamentoAlicate = new Vector2(25f, -45f);
    [Tooltip("Dureza da mola que puxa o alicate para o mouse (MinigameTest = 180).")]
    [SerializeField] private float molaAlicate = 180f;
    [Tooltip("Amortecimento da mola (MinigameTest = 0.65).")]
    [SerializeField] private float amortecimentoAlicate = 0.65f;
    [Tooltip("Quanto o alicate gira ao ser arrastado (MinigameTest = 1). 0 = sempre reto.")]
    [SerializeField] private float giroAlicate = 1f;
    [Tooltip("Fios 'branco-X' desenhados com listras brancas (como no cabo de verdade). Desligado = cor cheia lisa.")]
    [SerializeField] private bool listrarFios = true;

    [Header("Sons do protótipo — vazio = som genérico do jogo")]
    [Tooltip("Som do corte da caixa: Assets/Art/Sounds/corte.mp3")]
    [SerializeField] private AudioClip somCorte;
    [Tooltip("Som de tirar a capa: Assets/Art/Sounds/bip.mp3")]
    [SerializeField] private AudioClip somDescascar;
    [Tooltip("Som de erro (ordem errada / clique que não vale): Assets/Art/Sounds/erro.mp3")]
    [SerializeField] private AudioClip somErro;

    private Text msgTxt;
    private RectTransform faseCortar, faseDescascar, faseFileira;
    private RectTransform caboTela, capaCabo, faixaCabo, marcaCorte, fiosCabo, pontaMao;
    private RectTransform alvoCorte, alvoCapa;
    private RectTransform caixaCortar, rjVazio;
    // Centro (y) do desenho do RJ45: o PÉ fica em PeDoMacho e o resto sobe. Calculado em ConstruirRjVazio.
    private float centroMacho = PeDoMacho + LarguraCapa * 1.3f * 0.5f;
    private Image faixaAlvoImg, sinalCapaImg, sinalRjVazioImg;
    private readonly Image[] fiosImg = new Image[8];
    private readonly Image[] fiosPonta = new Image[8];
    private readonly RectTransform[] fiosSoquete = new RectTransform[8];
    private readonly RectTransform[] fiosListras = new RectTransform[8];
    private UICableCurve curvaMao;
    private Image pontaMaoImg;
    private Canvas canvasTela;
    private AudioSource somUi;
    private Image alicateImg;
    private AvisoRodape aviso;

    // Efeitos do protótipo: alicate seguindo o mouse (etapa de cortar) e a "batida" de entrada de cada etapa.
    private float alicateFechando;
    private bool cursorSobControle;
    private bool cursorEraVisivel;
    private Vector2 velocidadeAlicate;
    private float anguloAlicate, velocidadeAngular;
    private RectTransform animando;
    private Vector2 animandoFinal;
    private Coroutine animacao;
    private RedeCrimpEtapa etapaAnterior = (RedeCrimpEtapa)(-1);

    /// <summary>Quantas listras brancas o fio listrado recebe (as mesmas do protótipo).</summary>
    private const int ListrasNoFio = 5;

    /// <summary>Branco das listras dos fios "branco-X".</summary>
    private static readonly Color BrancoListra = new Color(0.97f, 0.97f, 0.95f);

    private void Awake() => Construir();

    /// <summary>
    /// Uma tela do minigame de rede está aberta agora? É o que o celular do jogador (<see cref="TicketTabletPrototype"/>)
    /// lê para mostrar (só aqui) o app com o padrão de cores T568B.
    /// </summary>
    public static bool Aberta { get; private set; }

    private void OnEnable()
    {
        Aberta = true;
        etapaAnterior = (RedeCrimpEtapa)(-1);   // cada abertura entra com a "batida" da primeira etapa
        cursorSobControle = false;
        if (alicateImg != null) alicateImg.gameObject.SetActive(false);
        NovaPeca();
    }

    /// <summary>
    /// Ao sair da tela, o cursor do sistema volta ao normal (o alicate era só um desenho por cima do mouse).
    /// O <see cref="UIScreenManager"/> também dá o reset ao fechar; isto cobre fechar por outro caminho.
    /// </summary>
    private void OnDisable()
    {
        Aberta = false;
        PararAnimacao(true);
        RestaurarCursor();
    }

    private void NovaPeca()
    {
        sessao.Reset();
        EmbaralharFios();
        Mensagem("", TextoClaro);
        Atualizar();
    }

    /// <summary>Deixa os 8 fios SOLTOS saindo do cabo (posições vazias) e limpa o fio da mão.</summary>
    private void EmbaralharFios()
    {
        sessao.Embaralhar(new System.Random(Random.Range(1, int.MaxValue)));
        origemColuna = -1;
    }

    // ================================================================== construção

    private void Construir()
    {
        if (construido) return;
        construido = true;

        RedeUi.Esticar((RectTransform)transform);

        // UMA região só, de ponta a ponta do canvas (nada de bordas/faixas escuras). Com a arte do protótipo
        // (bg1) ela vira o fundo da tela e leva por cima um véu escuro, para os textos claros continuarem legíveis.
        RectTransform painel = RedeUi.Cheio(transform, "Painel", CorPainel, spriteFundo);
        if (spriteFundo != null)
            RedeUi.Cheio(painel, "Veu", new Color(0.035f, 0.05f, 0.075f, Mathf.Clamp01(veuDoFundo)));

        // Os sons do protótipo (corte, capa, erro) tocam por aqui, em 2D (sem posição no mundo).
        somUi = GetComponent<AudioSource>();
        if (somUi == null) somUi = gameObject.AddComponent<AudioSource>();
        somUi.playOnAwake = false;
        somUi.spatialBlend = 0f;

        // Mesmo botão de fechar da tela do PC (quadrado escuro com "X").
        RedeUi.BotaoFechar(painel, Fechar);

        // A "bancada" agora é o canvas INTEIRO: por isso o cabo pode sair da tela por baixo.
        RectTransform bancada = RedeUi.Cheio(painel, "Bancada", Color.clear);

        // O alicate que segue o mouse (etapa de cortar): uma imagem por cima de tudo, com contorno, como no
        // MinigameTest. O cursor do sistema é escondido enquanto ele está na tela.
        ConstruirAlicate();

        msgTxt = RedeUi.Texto(painel, "Mensagem", "", 24, TextAnchor.MiddleCenter, TextoClaro);
        RedeUi.Ancorar((RectTransform)msgTxt.transform, new Vector2(0.5f, 0f), new Vector2(1500f, 44f), new Vector2(0.5f, 0f));
        ((RectTransform)msgTxt.transform).anchoredPosition = new Vector2(0f, 40f);

        // O cabo é criado ANTES das fases (que são transparentes) para ficar atrás dos textos/botões.
        ConstruirCabo(bancada);

        faseCortar = RedeUi.Cheio(bancada, "FaseCortar", Color.clear);
        faseDescascar = RedeUi.Cheio(bancada, "FaseDescascar", Color.clear);
        faseFileira = RedeUi.Cheio(bancada, "FaseFileira", Color.clear);

        ConstruirCortar();
        ConstruirDescascar();
        ConstruirFileira();
    }

    private static RectTransform Caixa(Transform pai, string nome, Color cor, Vector2 pos, Vector2 tamanho, Sprite sprite = null)
    {
        RectTransform rt = RedeUi.Imagem(pai, nome, cor, sprite);
        RedeUi.Ancorar(rt, new Vector2(0.5f, 0.5f), tamanho, new Vector2(0.5f, 0.5f));
        rt.anchoredPosition = pos;
        return rt;
    }

    /// <summary>Adiciona as listras brancas (ancoradas em frações do retângulo, então acompanham o tamanho dele).</summary>
    private static void Listrar(RectTransform em, int bandas, Color cor)
    {
        if (em == null || bandas < 1) return;
        for (int b = 0; b < bandas; b++)
        {
            float a = b / (float)bandas;
            RectTransform banda = RedeUi.Imagem(em, "Listra" + b, cor);
            banda.anchorMin = new Vector2(0f, a);
            banda.anchorMax = new Vector2(1f, a + 0.5f / bandas);
            banda.offsetMin = Vector2.zero;
            banda.offsetMax = Vector2.zero;
        }
    }

    private static RectTransform Botao(RectTransform pai, string nome, string rotulo, Color cor, UnityEngine.Events.UnityAction acao, int tam, Vector2 ancora, Vector2 pos, Vector2 size,
        Sprite sprite = null, Sprite spriteApertado = null)
    {
        RectTransform rt = RedeUi.Botao(pai, nome, rotulo, cor, acao, tam, sprite, spriteApertado);
        RedeUi.Ancorar(rt, ancora, size, ancora);
        rt.anchoredPosition = pos;
        return rt;
    }

    /// <summary>Botão pequeno no rodapé da bancada (ancorado no canto).</summary>
    private static RectTransform BotaoCanto(RectTransform pai, string nome, string rotulo, Color cor, UnityEngine.Events.UnityAction acao, Vector2 ancora, Vector2 pos, Vector2 size)
    {
        return Botao(pai, nome, rotulo, cor, acao, 22, ancora, pos, size);
    }

    // ------------------------------------------------------------------ o cabo na vertical

    /// <summary>
    /// Cria o alicate que segue o mouse (a arte do MinigameTest): uma imagem com contorno, do tamanho
    /// <see cref="tamanhoAlicate"/>, na frente de todo o resto. Fica escondida até a etapa de cortar.
    /// </summary>
    private void ConstruirAlicate()
    {
        if (spriteAlicateAberto == null) return;

        // Esticado no tamanho do retângulo (sem preservar proporção), exatamente como o MinigameTest fazia com
        // esta arte: assim o alicate sai no mesmo tamanho e no mesmo lugar em relação ao mouse.
        alicateImg = RedeUi.Imagem(transform, "AlicateCursor", Color.white, spriteAlicateAberto).GetComponent<Image>();
        alicateImg.raycastTarget = false;
        RectTransform rt = alicateImg.rectTransform;
        RedeUi.Ancorar(rt, new Vector2(0.5f, 0.5f), tamanhoAlicate, new Vector2(0.5f, 0.5f));
        rt.anchoredPosition = Vector2.zero;

        // Contorno escuro, para o alicate se destacar do fundo (o mesmo papel do Contorno do protótipo).
        Outline contorno = alicateImg.gameObject.AddComponent<Outline>();
        contorno.effectColor = new Color(0.03f, 0.04f, 0.06f, 0.9f);
        contorno.effectDistance = new Vector2(4f, -4f);
        contorno.useGraphicAlpha = true;

        rt.SetAsLastSibling();
        alicateImg.gameObject.SetActive(false);
    }

    private void ConstruirCabo(RectTransform bancada)
    {
        caboTela = Caixa(bancada, "CaboRede", Color.clear, new Vector2(0f, CaboCentroY), new Vector2(900f, 760f));

        // O cabo azul: LARGO e comprido, para parecer que TODOS os fios saem dele. O tamanho muda entre
        // inteiro e descascado em Atualizar(). Com a arte do protótipo (capa.png) ele ganha o desenho do cabo.
        capaCabo = Caixa(caboTela, "CapaAzul", spriteCapa != null ? Color.white : AzulCapa,
            new Vector2(0f, (TopoCapaInteira + FundoCapa) * 0.5f), new Vector2(LarguraCapa, TopoCapaInteira - FundoCapa), spriteCapa);
        RectTransform brilho = RedeUi.Imagem(capaCabo, "Brilho", new Color(1f, 1f, 1f, 0.10f));
        RedeUi.Ancorar(brilho, new Vector2(0f, 1f), new Vector2(22f, 160f), new Vector2(0f, 1f));
        brilho.anchoredPosition = new Vector2(20f, -18f);

        // Borda clara = o lugar onde a capa foi cortada (só aparece descascado).
        faixaCabo = Caixa(caboTela, "BordaCapa", new Color(0.72f, 0.78f, 0.86f), new Vector2(0f, BaseFios - 6f), new Vector2(LarguraCapa, 12f));

        // Marca de onde o alicate vai cortar a capa (etapa "Descascar").
        marcaCorte = Caixa(caboTela, "MarcaCorte", new Color(0.95f, 0.82f, 0.28f), new Vector2(0f, BaseFios + 10f), new Vector2(LarguraCapa + 30f, 8f));

        fiosCabo = RedeUi.Imagem(caboTela, "Fios", Color.clear);
        RedeUi.Esticar(fiosCabo);
        // Os fios são desenhados ATRÁS do cabo azul: assim a "boca" da capa tampa a base deles e eles
        // parecem nascer de dentro do cabo (a ponta continua livre).
        fiosCabo.SetAsFirstSibling();

        // Trilho onde os fios se encaixam (a "fileira" das posições 1..8). Fica atrás das colunas.
        Caixa(fiosCabo, "Trilho", new Color(0.20f, 0.23f, 0.29f), new Vector2(0f, RailY), new Vector2(LarguraTrilho, 30f));
        Caixa(fiosCabo, "TrilhoLuz", new Color(0.32f, 0.36f, 0.44f), new Vector2(0f, RailY + 17f), new Vector2(LarguraTrilho, 4f));

        for (int i = 0; i < 8; i++)
        {
            int indice = i;
            float x = (i - 3.5f) * EspacamentoFio;

            // Área clicável da posição (é a "tomada" onde o fio encaixa).
            RectTransform coluna = RedeUi.Botao(fiosCabo, "Posicao" + (i + 1), "", Transparente, () => ClicarColuna(indice), 16);
            RedeUi.Ancorar(coluna, new Vector2(0.5f, 0.5f), new Vector2(EspacamentoFio, AlturaColuna), new Vector2(0.5f, 0.5f));
            coluna.anchoredPosition = new Vector2(x, CentroColuna);

            // Soquete vazio: mostra o lugar onde o fio ainda NÃO foi encaixado.
            fiosSoquete[i] = Caixa(coluna, "Soquete", new Color(0.12f, 0.14f, 0.18f), new Vector2(0f, SoqueteY), new Vector2(LarguraFio + 14f, 28f));

            // O fio em si (a cor muda conforme o fio que ocupa a posição).
            Image fio = RedeUi.Imagem(coluna, "Fio", Color.white).GetComponent<Image>();
            RedeUi.Ancorar((RectTransform)fio.transform, new Vector2(0.5f, 0.5f), new Vector2(LarguraFio, AlturaFioSolto), new Vector2(0.5f, 0.5f));
            ((RectTransform)fio.transform).anchoredPosition = new Vector2(0f, FioSoltoY);
            fiosImg[i] = fio;

            // Listras brancas do fio "branco-X" (do protótipo): ficam ancoradas em frações do fio, então
            // acompanham sozinhas quando o fio cresce ao ser encaixado no trilho.
            RectTransform listras = RedeUi.Imagem(fio.transform, "Listras", Color.clear);
            RedeUi.Esticar(listras);
            Listrar(listras, ListrasNoFio, BrancoListra);
            fiosListras[i] = listras;
            listras.gameObject.SetActive(false);

            // Ponta de cobre exposta, na extremidade de cima.
            Image ponta = RedeUi.Imagem(coluna, "Ponta", new Color(0.86f, 0.78f, 0.60f)).GetComponent<Image>();
            RedeUi.Ancorar((RectTransform)ponta.transform, new Vector2(0.5f, 0.5f), new Vector2(LarguraFio, 16f), new Vector2(0.5f, 0.5f));
            ((RectTransform)ponta.transform).anchoredPosition = new Vector2(0f, PontaSoltoY);
            fiosPonta[i] = ponta;
        }

        // O RJ45 VAZIO logo acima dos fios: criado DEPOIS das colunas para o clique dele ficar na frente.
        ConstruirRjVazio(fiosCabo);

        // O fio "na mão": a curva que sai do cabo e segue o mouse.
        var go = new GameObject("FioNaMao", typeof(RectTransform));
        go.transform.SetParent(caboTela, false);
        RedeUi.Esticar((RectTransform)go.transform);
        curvaMao = go.AddComponent<UICableCurve>();
        curvaMao.Thickness = LarguraFio;
        curvaMao.raycastTarget = false;
        go.SetActive(false);

        pontaMao = Caixa(caboTela, "PontaNaMao", Color.white, Vector2.zero, new Vector2(30f, 24f));
        pontaMaoImg = pontaMao.GetComponent<Image>();
        pontaMao.gameObject.SetActive(false);

        // O fio desenhado na mão fica na frente dos outros.
        curvaMao.transform.SetAsLastSibling();
        pontaMao.SetAsLastSibling();
    }

    /// <summary>
    /// Desenha um conector RJ45 VAZIO logo acima dos fios: o corpo claro com os 8 encaixes escuros.
    /// Quando a fileira completa, ele acende (moldura amarela pulsando) — e clicar nele encaixa e crimpa
    /// de uma vez: se a ordem estiver certa o cabo já sai pronto na mão do jogador.
    /// </summary>
    private void ConstruirRjVazio(RectTransform pai)
    {
        // O "macho" (RJ45) é do tamanho do CABO AZUL GRANDE (ver <see cref="LarguraMacho"/>): a boca do
        // conector cobre os 8 fios. Na LARGURA ele fica largo assim mesmo (no MinigameTest o retângulo do
        // RJ45 é quase da largura do cabo); na ALTURA ele é esticado para cima com a proporção do próprio
        // desenho (conector.png é alto): o PÉ fica logo acima dos fios e o resto sobe e sai da tela.
        // Com a arte, o desenho é esticado nesse retângulo (a proporção do retângulo já é a do desenho);
        // sem a arte, o corpo claro é desenhado com os 8 encaixes escuros (todos vazios).
        float proporcao = spriteRj45 != null && spriteRj45.rect.width > 0f
            ? spriteRj45.rect.height / spriteRj45.rect.width : 1.3f;
        Vector2 tamanhoMacho = new Vector2(LarguraMacho, LarguraMacho * proporcao);
        centroMacho = PeDoMacho + tamanhoMacho.y * 0.5f;   // o PÉ do conector fica em <see cref="PeDoMacho"/>

        // Brilho e área clicável: no PÉ do conector (a parte visível dele), sem descer até o trilho dos fios.
        Vector2 areaMacho = new Vector2(tamanhoMacho.x + 80f, AlturaAreaMacho);
        float centroArea = PeDoMacho + AlturaAreaMacho * 0.5f;

        // Brilho em volta: só aparece quando os 8 fios já estão na fileira.
        sinalRjVazioImg = RedeUi.Imagem(pai, "SinalRj", new Color(0.97f, 0.84f, 0.30f, 0.5f)).GetComponent<Image>();
        RedeUi.Ancorar((RectTransform)sinalRjVazioImg.transform, new Vector2(0.5f, 0.5f), areaMacho, new Vector2(0.5f, 0.5f));
        ((RectTransform)sinalRjVazioImg.transform).anchoredPosition = new Vector2(0f, centroArea);

        rjVazio = RedeUi.Imagem(pai, "Rj45Vazio", Color.clear);
        RedeUi.Ancorar(rjVazio, new Vector2(0.5f, 0.5f), tamanhoMacho, new Vector2(0.5f, 0.5f));
        rjVazio.anchoredPosition = new Vector2(0f, centroMacho);

        if (spriteRj45 != null)
        {
            // Esticado no retângulo (sem preservar a proporção), como o RJ45 largo do MinigameTest.
            Image arte = RedeUi.Imagem(rjVazio, "Arte", Color.white, spriteRj45).GetComponent<Image>();
            RedeUi.Esticar(arte.rectTransform);
        }
        else
        {
            // Desenho de reserva: um conector do tamanho da ÁREA VISÍVEL (o pé do conector), com tudo em
            // frações desse retângulo, para o desenho inteiro caber na tela mesmo sem a arte.
            Vector2 t = new Vector2(tamanhoMacho.x, AlturaAreaMacho);
            float noPe = centroArea - centroMacho;   // quanto o pé fica abaixo do centro do desenho
            Caixa(rjVazio, "Trava", new Color(0.72f, 0.78f, 0.86f),
                new Vector2(0f, noPe + t.y * 0.35f), new Vector2(t.x * 0.26f, t.y * 0.14f));
            Caixa(rjVazio, "Corpo", new Color(0.84f, 0.89f, 0.95f),
                new Vector2(0f, noPe + t.y * 0.06f), new Vector2(t.x * 0.92f, t.y * 0.55f));

            // 8 encaixes VAZIOS espalhados pela largura do cabo (todos na mesma cor: nada conectado).
            float passo = t.x * 0.90f / 8f;
            for (int i = 0; i < 8; i++)
                Caixa(rjVazio, "Vazio" + i, new Color(0.17f, 0.19f, 0.24f),
                    new Vector2((i - 3.5f) * passo, noPe - t.y * 0.22f),
                    new Vector2(t.x * 0.05f, t.y * 0.24f));
        }

        // Ponto certo para fechar o cabo (sem botão): clicar no RJ45 encaixa e já crimpa.
        // A área clicável fica no PÉ do conector (sobre a parte visível), sem descer até o trilho dos fios.
        Botao(pai, "PontoRj", "", Transparente, ClicarRj, 16, new Vector2(0.5f, 0.5f),
            new Vector2(0f, centroArea), areaMacho).SetAsLastSibling();
    }

    private void ConstruirCortar()
    {
        // A caixa é GRANDE, DEITADA e fica no canto esquerdo (pode até sair da tela). O cabo sai DE LADO,
        // na horizontal, bem mais fino que o cabo em pé. Com a arte do protótipo (caixa.png) ela é a própria
        // caixa, SEM tingir a arte (o branco deixa o desenho sair como no MinigameTest).
        caixaCortar = Caixa(faseCortar, "CaixaCabo", spriteCaixa != null ? Color.white : new Color(0.60f, 0.45f, 0.29f),
            new Vector2(CaixaCortarX, CaixaCortarY), new Vector2(CaixaCortarLargura, CaixaCortarAltura), spriteCaixa);

        if (spriteCaixa == null)
        {
            Caixa(caixaCortar, "Tampa", new Color(0.52f, 0.39f, 0.24f), new Vector2(0f, CaixaCortarAltura * 0.5f - 22f), new Vector2(CaixaCortarLargura, 44f));
            Caixa(caixaCortar, "Base", new Color(0.46f, 0.34f, 0.20f), new Vector2(0f, -CaixaCortarAltura * 0.5f + 24f), new Vector2(CaixaCortarLargura, 48f));
            Caixa(caixaCortar, "Etiqueta", new Color(0.68f, 0.53f, 0.34f), new Vector2(290f, 40f), new Vector2(560f, 330f));
            Caixa(caixaCortar, "Faixa", new Color(0.30f, 0.22f, 0.14f), new Vector2(290f, 40f), new Vector2(460f, 60f));
        }

        // "Boca" escura NA LATERAL direita, por onde o cabo sai (fica sempre: é ela que explica o cabo saindo).
        Caixa(caixaCortar, "Boca", new Color(0.15f, 0.11f, 0.07f, spriteCaixa != null ? 0.85f : 1f),
            new Vector2(CaixaCortarLargura * 0.5f, SaidaCaixaY - CaixaCortarY), new Vector2(96f, SaidaCaboEspessura + 96f));

        // O cabo que sai de lado e vai para a direita.
        Caixa(faseCortar, "CaboDaLateral", spriteCapa != null ? Color.white : AzulCapa,
            new Vector2(SaidaCaboX, SaidaCaixaY), new Vector2(SaidaCaboComprimento, SaidaCaboEspessura), spriteCapa);

        // CORTAR não tem botão: o ponto certo é a faixa amarela que pulsa sobre o cabo.
        alvoCorte = Botao(faseCortar, "PontoCorte", "", Transparente, () =>
        {
            if (!ConferirAlicate()) return;
            FecharAlicate();
            if (sessao.Cortar()) { Tocar(somCorte, InteractionSound.CableUnplugged); Mensagem("", TextoClaro); Atualizar(); }
        }, 16, new Vector2(0.5f, 0.5f), new Vector2(PontoCorteX, SaidaCaixaY), new Vector2(210f, 260f));

        faixaAlvoImg = RedeUi.Imagem(alvoCorte, "FaixaCorte", new Color(0.97f, 0.84f, 0.30f)).GetComponent<Image>();
        RedeUi.Ancorar((RectTransform)faixaAlvoImg.transform, new Vector2(0.5f, 0.5f),
            new Vector2(18f, SaidaCaboEspessura + 60f), new Vector2(0.5f, 0.5f));
        ((RectTransform)faixaAlvoImg.transform).anchoredPosition = Vector2.zero;
    }

    private void ConstruirDescascar()
    {
        float centroSali = CaboCentroY + (BaseFios + TopoCapaInteira) * 0.5f;   // centro da "saliência" azul

        // DESCASCAR também não tem botão: clica-se na "saliência" azul (acima da marca) e é UM puxão só.
        alvoCapa = Botao(faseDescascar, "PontoCapa", "", Transparente, () =>
        {
            if (!ConferirAlicate()) return;
            FecharAlicate();
            if (sessao.Descascar()) { Tocar(somDescascar, InteractionSound.CableUnplugged); Mensagem("", TextoClaro); Atualizar(); }
        }, 16, new Vector2(0.5f, 0.5f), new Vector2(0f, centroSali), new Vector2(LarguraCapa + 40f, TopoCapaInteira - BaseFios + 40f));

        // Brilho suave sobre a saliência, só para o jogador achar onde clicar (nenhum texto).
        sinalCapaImg = RedeUi.Imagem(caboTela, "SinalCapa", new Color(1f, 1f, 1f, 0.12f)).GetComponent<Image>();
        RedeUi.Ancorar((RectTransform)sinalCapaImg.transform, new Vector2(0.5f, 0.5f),
            new Vector2(LarguraCapa, TopoCapaInteira - BaseFios), new Vector2(0.5f, 0.5f));
        ((RectTransform)sinalCapaImg.transform).anchoredPosition = new Vector2(0f, (BaseFios + TopoCapaInteira) * 0.5f);
    }

    /// <summary>
    /// A ordem dos fios é SORTEADA a cada abertura da tela — não existe botão de "soltar tudo de novo".
    /// A ajuda do padrão de cores não é mais um botão aqui dentro: virou o app CORES do celular do jogador
    /// (canto inferior direito da tela, tecla C), que só aparece enquanto esta tela está aberta.
    /// Veja <see cref="TicketTabletPrototype"/> e <see cref="Aberta"/>.
    /// </summary>
    private void ConstruirFileira() { }

    // ================================================================== ordenar: pegar e encaixar o fio

    /// <summary>
    /// Clique numa posição da fileira: se a mão está vazia, pega o fio que está ali (encaixado ou solto);
    /// se a mão está cheia, encaixa o fio na posição (trocando com o que já estava nela).
    /// </summary>
    private void ClicarColuna(int i)
    {
        if (sessao.Etapa != RedeCrimpEtapa.Ordenar) return;

        if (!sessao.NaMao.HasValue)
        {
            bool pegou = sessao.Fila[i] != null
                ? sessao.PegarDaPosicao(i)
                : sessao.PegarSolto(IndiceSoltoNaColuna(i));

            if (pegou)
            {
                origemColuna = i;
                InteractionSounds.Raise(InteractionSound.ItemPickedUp);
            }
        }
        else
        {
            sessao.ColocarNaPosicao(i);
            InteractionSounds.Raise(InteractionSound.ItemPlaced);
            Mensagem("", TextoClaro);
        }

        Atualizar();
    }

    /// <summary>Qual fio solto (índice em <c>Soltos</c>) está sendo mostrado nesta coluna vazia (-1 = nenhum).</summary>
    private int IndiceSoltoNaColuna(int coluna)
    {
        if (sessao.Fila[coluna] != null) return -1;

        int k = 0;
        for (int i = 0; i < sessao.NumFios; i++)
        {
            if (sessao.Fila[i] != null) continue;
            if (i == coluna) return k;
            k++;
        }
        return -1;
    }

    /// <summary>Ponto (local do cabo) de onde os fios saem, para a curva do fio na mão.</summary>
    private Vector2 OrigemFio(int i) => new Vector2((i - 3.5f) * EspacamentoFio, BaseFios);

    private void LateUpdate()
    {
        AnimarAlicate();
        PulsarPontosCertos();

        if (curvaMao == null) return;

        bool naMao = sessao.Etapa == RedeCrimpEtapa.Ordenar && sessao.NaMao.HasValue;
        if (curvaMao.gameObject.activeSelf != naMao) curvaMao.gameObject.SetActive(naMao);
        if (pontaMao.gameObject.activeSelf != naMao) pontaMao.gameObject.SetActive(naMao);
        if (!naMao) return;

        // O fio na mão mantém a MESMA identidade do fio na bancada: o "branco-laranja" continua branco-laranja
        // (cor forte com as listras brancas), e não vira todo laranja.
        Color cor = CorDoFioNaTela(sessao.NaMao.Value);
        curvaMao.color = cor;
        curvaMao.Listrado = listrarFios && RedeCores.EhListrado(sessao.NaMao.Value);
        curvaMao.CorDaListra = BrancoListra;
        if (pontaMaoImg != null) pontaMaoImg.color = cor;

        int col = origemColuna < 0 || origemColuna >= 8 ? 0 : origemColuna;
        Vector2 origem = OrigemFio(col);
        Vector2 alvo = origem + new Vector2(0f, 160f);
        if (TentarPonteiro(out Vector2 mouse)) alvo = mouse;

        pontosCurva.Clear();
        float sag = 50f + Vector2.Distance(origem, alvo) * 0.12f;
        Vector2 c1 = origem + Vector2.down * sag;
        Vector2 c2 = alvo + Vector2.down * sag;
        const int n = 22;
        for (int k = 0; k <= n; k++)
        {
            float t = k / (float)n, u = 1f - t;
            pontosCurva.Add(u * u * u * origem + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * alvo);
        }
        curvaMao.SetPoints(pontosCurva);
        pontaMao.anchoredPosition = alvo;
    }

    /// <summary>
    /// Faz a faixa do corte e a "saliência" da capa pulsarem de leve. Sem nenhum texto, é isso que mostra
    /// ao jogador o PONTO CERTO de clicar em cada etapa.
    /// </summary>
    private void PulsarPontosCertos()
    {
        float onda = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);

        if (faixaAlvoImg != null && alvoCorte != null && alvoCorte.gameObject.activeSelf)
            faixaAlvoImg.color = new Color(0.97f, 0.84f, 0.30f, Mathf.Lerp(0.35f, 1f, onda));

        if (sinalCapaImg != null && sinalCapaImg.gameObject.activeSelf)
            sinalCapaImg.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.05f, 0.18f, onda));

        if (sinalRjVazioImg != null && sinalRjVazioImg.gameObject.activeSelf)
            sinalRjVazioImg.color = new Color(0.97f, 0.84f, 0.30f, Mathf.Lerp(0.15f, 0.55f, onda));
    }

    private bool TentarPonteiro(out Vector2 local)
    {
        local = Vector2.zero;
        if (Mouse.current == null) return false;
        if (canvasTela == null) canvasTela = GetComponentInParent<Canvas>();
        Camera cam = canvasTela != null && canvasTela.renderMode != RenderMode.ScreenSpaceOverlay ? canvasTela.worldCamera : null;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)curvaMao.transform, Mouse.current.position.ReadValue(), cam, out local);
    }

    // ================================================================== outras ações

    /// <summary>
    /// Clique no RJ45 vazio: encaixa o conector e JÁ CRIMPA. Se a ordem estiver certa o cabo sai pronto na
    /// mão do jogador; se estiver errada, o cabo é descartado e volta para a etapa de descascar.
    /// </summary>
    private void ClicarRj()
    {
        if (sessao.Etapa != RedeCrimpEtapa.Ordenar) return;

        if (!sessao.TodasPreenchidas)
        {
            TocarSe(somErro);   // sem texto: o som de erro já diz que o clique não valeu
            return;
        }

        if (!ConferirAlicate()) return;
        if (!sessao.EncaixarRj45()) return;
        Crimpar();
    }

    private void Crimpar()
    {
        if (sessao.Etapa != RedeCrimpEtapa.EncaixarRj45) return;

        RedeCrimpResultado resultado = sessao.Crimpar();

        if (resultado == RedeCrimpResultado.Ok)
        {
            // Deu certo: NÃO aparece tela de "deu certo" — o cabo vai para a mão e a tela fecha na hora.
            InteractionSounds.Raise(InteractionSound.CablePlugged);
            RedeGame.Garantir().RegistrarCabo();
            EntregarCaboNaMao();
            Fechar();
            return;
        }

        if (resultado == RedeCrimpResultado.OrdemErrada)
        {
            // Errou a ordem: o cabo é descartado e volta a descascar (o estado já voltou para Descascar). O aviso é
            // curto e no rodapé: quantos fios estavam fora não interessa ao jogador.
            TocarSe(somErro);
            EmbaralharFios();
            Aviso("Ordem errada");
        }

        Atualizar();
    }

    /// <summary>
    /// Cria o cabo de rede crimpado e o põe na mão do jogador. Se as mãos estiverem ocupadas, solta o cabo
    /// no chão à frente dele (não some).
    /// </summary>
    private void EntregarCaboNaMao()
    {
        if (caboPrefab == null)
        {
            Debug.LogWarning("RedeCrimpScreen: 'caboPrefab' não está configurado — o cabo não foi entregue. " +
                             "Rode Tools > E-stagiar.io > Configurar minigame de rede.");
            return;
        }

        UIScreen tela = GetComponent<UIScreen>();
        ItemHolder mao = tela != null && tela.Interactor != null ? tela.Interactor.Holder : null;
        if (mao == null)
        {
            Debug.LogWarning("RedeCrimpScreen: não achei a mão do jogador para entregar o cabo.");
            return;
        }

        GameObject novo = Instantiate(caboPrefab);
        novo.name = "Cabo_Rede_Crimpado";
        PickupInteractable item = novo.GetComponentInChildren<PickupInteractable>(true);
        if (item == null) { Destroy(novo); return; }

        if (mao.Pick(item)) return;

        // Mão ocupada (em geral pelo próprio alicate): o cabo pronto fica EM CIMA da caixa de cabo. Foi o jogo que o
        // pôs ali (não o jogador), então continua "no lugar inicial" e não conta como item largado.
        if (tela.Source != null && ColocarSobre(tela.Source, item)) return;

        Transform olhos = mao.HoldPoint != null && mao.HoldPoint.parent != null ? mao.HoldPoint.parent : null;
        if (olhos != null) item.OnReleased(olhos.position);
    }

    /// <summary>Apoia o item no topo do objeto da estação (sem mudar o <see cref="PickupInteractable.Placement"/>).</summary>
    private static bool ColocarSobre(GameObject estacao, PickupInteractable item)
    {
        Renderer[] rs = estacao.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return false;
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        item.OnDropped(new Vector3(b.center.x, b.max.y, b.center.z), estacao.transform.eulerAngles.y);
        return true;
    }

    /// <summary>O jogador está com o alicate de crimpar na mão?</summary>
    private bool TemAlicate()
    {
        UIScreen tela = GetComponent<UIScreen>();
        return RedeEstacaoInteractable.TemAlicate(tela != null ? tela.Interactor : null);
    }

    /// <summary>Cortar, descascar e crimpar só com o alicate na mão (se <see cref="exigirAlicate"/>); senão avisa.</summary>
    private bool ConferirAlicate()
    {
        if (!exigirAlicate || TemAlicate()) return true;
        TocarSe(somErro);
        Aviso("Falta ferramenta", spriteAlicateAberto);   // o desenho do alicate que ele precisa pegar
        return false;
    }

    /// <summary>
    /// Aviso curto no rodapé (<see cref="AvisoRodape"/>): o próprio jogo diz o que impediu a ação, sem caixa de
    /// diálogo. <paramref name="icone"/> é o desenho da ferramenta que falta, quando é o caso.
    /// </summary>
    private void Aviso(string texto, Sprite icone = null)
    {
        if (aviso == null) aviso = gameObject.AddComponent<AvisoRodape>();
        aviso.Mostrar(texto, icone);
    }

    private void Fechar()
    {
        UIScreen tela = GetComponent<UIScreen>();
        if (tela != null) tela.RequestClose();
    }

    // ================================================================== atualização

    private void Atualizar()
    {
        if (msgTxt == null) return;

        // Na etapa de CORTAR o cabo em pé ainda não aparece: só a caixa e o cabo saindo de lado.
        bool faseDoCabo = sessao.Etapa == RedeCrimpEtapa.Descascar
                       || sessao.Etapa == RedeCrimpEtapa.Ordenar;
        bool descascado = sessao.Etapa == RedeCrimpEtapa.Ordenar;

        if (caboTela.gameObject.activeSelf != faseDoCabo) caboTela.gameObject.SetActive(faseDoCabo);

        faseCortar.gameObject.SetActive(sessao.Etapa == RedeCrimpEtapa.Cortar);
        faseDescascar.gameObject.SetActive(sessao.Etapa == RedeCrimpEtapa.Descascar);
        faseFileira.gameObject.SetActive(descascado);

        if (faseDoCabo)
        {
            float topo = descascado ? BaseFios : TopoCapaInteira;   // topo da capa
            capaCabo.sizeDelta = new Vector2(LarguraCapa, topo - FundoCapa);
            capaCabo.anchoredPosition = new Vector2(0f, (topo + FundoCapa) * 0.5f);

            faixaCabo.gameObject.SetActive(descascado);
            marcaCorte.gameObject.SetActive(sessao.Etapa == RedeCrimpEtapa.Descascar);
            fiosCabo.gameObject.SetActive(descascado);
        }

        // Os "pontos certos" (sem botão) só ficam clicáveis na etapa a que pertencem. A caixa (e o cabo que
        // sai de lado) fica na fase "Cortar", então some sozinha depois do corte.
        alvoCorte.gameObject.SetActive(sessao.Etapa == RedeCrimpEtapa.Cortar);
        alvoCapa.gameObject.SetActive(sessao.Etapa == RedeCrimpEtapa.Descascar);
        sinalCapaImg.gameObject.SetActive(sessao.Etapa == RedeCrimpEtapa.Descascar);

        if (descascado)
        {
            for (int i = 0; i < 8; i++)
            {
                bool colocado = sessao.Fila[i] != null;

                RedeFio? fio = sessao.Fila[i];
                if (fio == null)
                {
                    int k = IndiceSoltoNaColuna(i);
                    if (k >= 0 && k < sessao.Soltos.Count) fio = sessao.Soltos[k];
                }

                bool tem = fio.HasValue;
                RectTransform rt = (RectTransform)fiosImg[i].transform;
                rt.sizeDelta = new Vector2(LarguraFio, colocado ? AlturaFioColocado : AlturaFioSolto);
                rt.anchoredPosition = new Vector2(0f, colocado ? FioColocadoY : FioSoltoY);

                fiosImg[i].enabled = tem;
                if (tem) PintarFio(i, fio.Value);
                else if (fiosListras[i] != null) fiosListras[i].gameObject.SetActive(false);

                fiosPonta[i].gameObject.SetActive(tem);
                if (tem) ((RectTransform)fiosPonta[i].transform).anchoredPosition = new Vector2(0f, colocado ? PontaColocadoY : PontaSoltoY);

                // O soquete aparece só nas posições que ainda estão vazias.
                fiosSoquete[i].gameObject.SetActive(!tem);
            }
        }

        // O RJ45 vazio só "acende" quando os 8 fios já estão na fileira: é o ponto certo para fechar o cabo.
        if (sinalRjVazioImg != null)
            sinalRjVazioImg.gameObject.SetActive(descascado && sessao.TodasPreenchidas);

        // Efeitos do protótipo: cada etapa entra com uma "batida" (o alicate que segue o mouse é o AnimarAlicate).
        if (sessao.Etapa != etapaAnterior) { etapaAnterior = sessao.Etapa; EtapaMudou(sessao.Etapa); }
    }

    /// <summary>Pinta o fio: cor cheia do par + listras brancas nos "branco-X" (como no protótipo).</summary>
    private void PintarFio(int i, RedeFio fio)
    {
        bool listrado = listrarFios && RedeCores.EhListrado(fio);
        fiosImg[i].color = CorDoFioNaTela(fio);
        if (fiosListras[i] != null) fiosListras[i].gameObject.SetActive(listrado);
    }

    /// <summary>Cor do fio como ele aparece na tela (a mesma dos fios e das amostras da tabela).</summary>
    private Color CorDoFioNaTela(RedeFio fio) =>
        listrarFios && RedeCores.EhListrado(fio) ? RedeCores.CorDaListra(fio) : RedeCores.Cor(fio);

    // ================================================================== efeitos do protótipo

    /// <summary>
    /// Etapa nova = uma "batida" de entrada, como no protótipo: a caixa cai na etapa de cortar, o cabo sobe na
    /// de descascar e o RJ45 cai na de ordenar.
    /// </summary>
    private void EtapaMudou(RedeCrimpEtapa etapa)
    {
        PararAnimacao(true);
        if (!isActiveAndEnabled) return;

        if (etapa == RedeCrimpEtapa.Cortar && caixaCortar != null)
            animacao = StartCoroutine(Entrar(caixaCortar, new Vector2(CaixaCortarX, CaixaCortarY), new Vector2(CaixaCortarX, CaixaCortarY + 620f)));
        else if (etapa == RedeCrimpEtapa.Descascar && caboTela != null)
            animacao = StartCoroutine(Entrar(caboTela, new Vector2(0f, CaboCentroY), new Vector2(0f, CaboCentroY - 640f)));
        else if (etapa == RedeCrimpEtapa.Ordenar && rjVazio != null)
            animacao = StartCoroutine(Entrar(rjVazio, new Vector2(0f, centroMacho), new Vector2(0f, centroMacho + 250f)));
    }

    private System.Collections.IEnumerator Entrar(RectTransform rt, Vector2 fim, Vector2 comeco)
    {
        PararAnimacao(true);
        animando = rt;
        animandoFinal = fim;
        rt.anchoredPosition = comeco;

        const float duracao = 0.42f;
        float t = 0f;
        while (t < duracao)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duracao);
            // "passa um pouco e volta" (out-back): a mesma batida do protótipo.
            float e = 1f + 2.70158f * Mathf.Pow(k - 1f, 3f) + 1.70158f * Mathf.Pow(k - 1f, 2f);
            rt.anchoredPosition = Vector2.LerpUnclamped(comeco, fim, e);
            yield return null;
        }

        rt.anchoredPosition = fim;
        animando = null;
        animacao = null;
    }

    /// <summary>Para a animação da etapa (e, se pedido, encaixa o elemento na posição final).</summary>
    private void PararAnimacao(bool encaixar)
    {
        if (animacao != null) { StopCoroutine(animacao); animacao = null; }
        if (encaixar && animando != null) animando.anchoredPosition = animandoFinal;
        animando = null;
    }

    /// <summary>
    /// O alicate da arte do MinigameTest segue o mouse nas etapas que usam o alicate: CORTAR (o cabo que sai
    /// da caixa) e DESCASCAR (o corte da capa azul, a "fimose", que libera os 8 fios). É uma imagem com mola
    /// (o mesmo movimento do protótipo) e o cursor do sistema escondido. Nas outras etapas ele some e o
    /// cursor padrão das telas 2D volta (<see cref="UIScreenManager.ApplyDefaultCursor"/>).
    /// </summary>
    private void AnimarAlicate()
    {
        if (alicateFechando > 0f) alicateFechando = Mathf.Max(0f, alicateFechando - Time.unscaledDeltaTime);

        bool etapaDoAlicate = sessao.Etapa == RedeCrimpEtapa.Cortar || sessao.Etapa == RedeCrimpEtapa.Descascar;
        // Em cima do celular (aberto ou fechado) quem manda no ponteiro é a mãozinha (TicketTabletPrototype): o
        // alicate sai da frente para não aparecer junto com ela nem ficar desenhado por baixo do aparelho. Fora do
        // celular o alicate é o cursor, como sempre.
        bool mostrar = alicateImg != null && etapaDoAlicate && !TicketTabletPrototype.PonteiroSobreOAparelho
                    && Application.isFocused && (!exigirAlicate || TemAlicate());
        if (mostrar && !alicateImg.gameObject.activeSelf) alicateImg.gameObject.SetActive(true);
        if (!mostrar)
        {
            if (alicateImg != null && alicateImg.gameObject.activeSelf) alicateImg.gameObject.SetActive(false);
            RestaurarCursor();
            return;
        }

        RectTransform rt = alicateImg.rectTransform;
        Vector2 alvo = AlvoAlicate();

        if (!cursorSobControle)
        {
            cursorEraVisivel = Cursor.visible;
            cursorSobControle = true;
            Cursor.visible = false;
            rt.anchoredPosition = alvo;
            velocidadeAlicate = Vector2.zero;
            anguloAlicate = velocidadeAngular = 0f;
        }

        // Mola + amortecimento do protótipo, em passos pequenos para não "explodir" com quadros longos.
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        float k = Mathf.Max(30f, molaAlicate);
        int passos = Mathf.Max(1, Mathf.CeilToInt(dt / 0.008f));
        float h = dt / passos;
        Vector2 pos = rt.anchoredPosition;
        for (int i = 0; i < passos; i++)
        {
            velocidadeAlicate += ((alvo - pos) * k - velocidadeAlicate * (2f * Mathf.Sqrt(k) * amortecimentoAlicate)) * h;
            pos += velocidadeAlicate * h;
        }
        rt.anchoredPosition = pos;

        float giro = Mathf.Clamp(-velocidadeAlicate.x * 0.02f, -30f, 30f) * giroAlicate;
        anguloAlicate = Mathf.SmoothDampAngle(anguloAlicate, giro, ref velocidadeAngular, 0.12f, Mathf.Infinity, dt);
        rt.localRotation = Quaternion.Euler(0f, 0f, anguloAlicate);

        bool fechado = alicateFechando > 0f;
        alicateImg.sprite = fechado && spriteAlicateFechado != null ? spriteAlicateFechado : spriteAlicateAberto;
    }

    /// <summary>Posição (no espaço da tela) onde o alicate deve ficar: no mouse, com o deslocamento do protótipo.</summary>
    private Vector2 AlvoAlicate()
    {
        if (!PonteiroNaTela(out Vector2 local)) return alicateImg.rectTransform.anchoredPosition;
        return local + deslocamentoAlicate;
    }

    /// <summary>Posição do mouse no espaço local DESTA tela (o mesmo espaço dos filhos do painel).</summary>
    private bool PonteiroNaTela(out Vector2 local)
    {
        local = Vector2.zero;
        if (Mouse.current == null) return false;
        if (canvasTela == null) canvasTela = GetComponentInParent<Canvas>();
        Camera cam = canvasTela != null && canvasTela.renderMode != RenderMode.ScreenSpaceOverlay ? canvasTela.worldCamera : null;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, Mouse.current.position.ReadValue(), cam, out local);
    }

    /// <summary>Um clique com o alicate: ele aparece fechado por um instante (o "tec" do protótipo).</summary>
    private void FecharAlicate()
    {
        alicateFechando = 0.16f;
    }

    private void RestaurarCursor()
    {
        if (alicateFechando > 0f) alicateFechando = 0f;
        if (!cursorSobControle) return;
        cursorSobControle = false;
        Cursor.visible = cursorEraVisivel;
    }

    /// <summary>Toca o som do protótipo; sem ele, usa o som genérico do jogo.</summary>
    private void Tocar(AudioClip clipe, InteractionSound reserva)
    {
        if (clipe != null && somUi != null) somUi.PlayOneShot(clipe);
        else InteractionSounds.Raise(reserva);
    }

    /// <summary>Toca o som do protótipo só se estiver configurado (não tem som genérico equivalente).</summary>
    private void TocarSe(AudioClip clipe)
    {
        if (clipe != null && somUi != null) somUi.PlayOneShot(clipe);
    }

    private void Mensagem(string texto, Color cor)
    {
        if (msgTxt == null) return;
        msgTxt.text = texto;
        msgTxt.color = cor;
    }
}
