using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Vistas da impressora (a mesma tela, trocando de vista pelos botões Anterior/Próxima).</summary>
/// Valores novos no fim; a ORDEM de navegação é <see cref="ImpressoraTela.Ordem"/> (teste, toner, papel, painel).
public enum ImpressoraVista { Toner, Papel, Painel, Teste }

/// <summary>
/// Tela 2D da impressora, no padrão da manutenção do gabinete e do notebook: o compartimento desenhado e a PEÇA REAL
/// (o modelo 3D do cartucho/da resma, <see cref="ModeloNaTela"/>) aparecendo encaixada quando está instalada; vazio,
/// fica o contorno tracejado do lugar dela. A área clicável é invisível, com moldura amarela só com o mouse em cima.
///
/// Quatro vistas, nesta ordem:
///  - <b>Teste</b>: a impressora de frente (modelo 3D da própria impressora) e o botão de imprimir uma página de teste:
///    pronta (toner, papel e rede) a folha sai da impressora e o LED acende verde (sem selo de "✓"); faltando algo nada
///    sai, o LED pisca vermelho e aparece o X (aviso discreto, sem dizer o motivo). Os chamados validam pelo teste (<see cref="ImpressoraEstacao.TestesOk"/>).
///  - <b>Toner</b>: a frente aberta, com o compartimento do cartucho.
///  - <b>Papel</b>: a bandeja de papel puxada para fora.
///  - <b>Painel</b>: configuração de rede (só o campo do endereço IP + teclado numérico), com máscara de ENTRADA IPv4:
///    os pontos entram sozinhos e o visor mostra o IP como o cartaz (192.168.0.61, <see cref="ImpressoraSession.FormatarVisor"/>).
///    Também aceita o TECLADO FÍSICO: números, ponto (fecha o grupo antes dos 3 dígitos), Backspace (apaga, voltando
///    pelos grupos), Delete (limpa) e Enter (confirma).
/// Os textos são neutros: a tela não diz o que está faltando.
///
/// O estado NÃO fica aqui: fica no <see cref="ImpressoraEstacao"/> do objeto do mundo (<c>UIScreen.Source</c>).
/// </summary>
[RequireComponent(typeof(UIScreen))]
public class ImpressoraTela : MonoBehaviour
{
    private static readonly Color CorPainel = new Color(0.09f, 0.11f, 0.14f);
    private static readonly Color CorCorpo = new Color(0.2f, 0.22f, 0.26f);
    private static readonly Color CorCorpoClaro = new Color(0.26f, 0.29f, 0.34f);
    private static readonly Color CorBorda = new Color(0.3f, 0.35f, 0.42f);
    private static readonly Color CorVazio = new Color(0.1f, 0.12f, 0.15f);
    private static readonly Color CorLcd = new Color(0.06f, 0.14f, 0.1f);
    private static readonly Color CorToner = new Color(0.25f, 0.28f, 0.34f);
    private static readonly Color CorPapel = new Color(0.85f, 0.86f, 0.82f);
    private static readonly Color CorTecla = new Color(0.3f, 0.34f, 0.4f);
    private static readonly Color TextoClaro = new Color(0.9f, 0.93f, 0.97f);
    private static readonly Color TextoFraco = new Color(0.6f, 0.66f, 0.74f);
    private static readonly Color TextoLcd = new Color(0.55f, 0.95f, 0.6f);
    private static readonly Color Verde = new Color(0.36f, 0.85f, 0.45f);
    private static readonly Color Amarelo = new Color(1f, 0.82f, 0.25f);
    private static readonly Color Vermelho = new Color(0.95f, 0.4f, 0.4f);

    private static readonly string[] Teclas = { "1", "2", "3", "4", "5", "6", "7", "8", "9", ".", "0", "Apagar" };
    private static readonly Color CorCarcaca = new Color(0.8f, 0.82f, 0.85f);
    private static readonly Color CorCarcacaSombra = new Color(0.62f, 0.65f, 0.7f);
    private static readonly Color CorCavidade = new Color(0.13f, 0.14f, 0.17f);
    private static readonly Color CorCartao = new Color(0.14f, 0.17f, 0.22f);
    private static readonly Color CorCampo = new Color(0.06f, 0.08f, 0.1f);

    [Header("Modelos das peças (o desenho 3D na tela)")]
    [Tooltip("Prefab do cartucho (Pickup_Toner): o modelo aparece encaixado no compartimento.")]
    [SerializeField] private GameObject modeloToner;
    [Tooltip("Prefab da resma (Pickup_ResmaPapel): o modelo aparece na bandeja.")]
    [SerializeField] private GameObject modeloPapel;

    private bool construido;
    private bool vistaInicial;      // já escolheu a vista da primeira abertura? (depois a vista é lembrada)
    private UIScreen tela;

    /// <summary>Ordem de navegação das vistas (◄ ►).</summary>
    public static readonly ImpressoraVista[] Ordem = { ImpressoraVista.Teste, ImpressoraVista.Toner, ImpressoraVista.Papel, ImpressoraVista.Painel };

    private ImpressoraVista vista = ImpressoraVista.Teste;

    // Vista "Teste".
    private ModeloNaTela impressoraModelo;
    private ImpressoraEstacao copiaDe;
    private RectTransform folha, folhaJanela, falha;
    private Image ledTeste;
    private Coroutine imprimindo;
    private RectTransform[] vistas;
    private Text msgTxt;

    // Vista "Toner".
    private ModeloNaTela tonerModelo;
    private RectTransform tonerVazio;

    // Vista "Papel".
    private ModeloNaTela papelModelo;
    private RectTransform papelVazio;

    // Vista "Painel".
    private Text lcdStatus, lcdIp, ipSalvoTxt;
    private Image lcdLed;
    private static Sprite arredondado;
    private bool modelosProntos;

    private void Awake() => Construir();

    private void OnEnable()
    {
        if (tela == null) tela = GetComponent<UIScreen>();
        // A vista é LEMBRADA entre as aberturas (a tela é o mesmo objeto: só desliga e liga de novo). Saiu do
        // painel do IP? Ao voltar, volta no painel do IP. Na primeira vez, abre na vista que ainda falta
        // resolver; se estiver tudo pronto, vai direto ao painel.
        if (!vistaInicial)
        {
            vista = ImpressoraVista.Teste;   // primeiro o teste: ver se imprime, depois toner e papel
            vistaInicial = true;
        }

        // Aberta pelo painel 3D da impressora: começa no teclado do IP (pedido feito pela própria estação).
        ImpressoraEstacao pedida = Estacao();
        if (pedida != null && pedida.ConsumirVistaPedida(out ImpressoraVista vistaPedida)) vista = vistaPedida;

        Mensagem("", TextoFraco);
        Atualizar();
    }

    private void Update()
    {
        if (!construido) return;

        // As setas do teclado fazem o MESMO que os botões ◄ ► (que continuam funcionando).
        Keyboard teclado = Keyboard.current;
        if (teclado != null)
        {
            if (teclado.leftArrowKey.wasPressedThisFrame) TrocarVista(-1);
            else if (teclado.rightArrowKey.wasPressedThisFrame) TrocarVista(1);
        }

        if (vista == ImpressoraVista.Painel)
        {
            TecladoFisico(teclado);
            Atualizar();
        }
    }

    // Digitação do IP pelo teclado físico (o mesmo que clicar nas teclas da tela).
    private void TecladoFisico(Keyboard k)
    {
        if (k == null) return;
        for (int d = 0; d <= 9; d++)
        {
            Key normal = (Key)((int)Key.Digit0 + d), numerico = (Key)((int)Key.Numpad0 + d);
            if (k[normal].wasPressedThisFrame || k[numerico].wasPressedThisFrame) ClicarTecla(d.ToString());
        }
        if (k.periodKey.wasPressedThisFrame || k.numpadPeriodKey.wasPressedThisFrame || k.commaKey.wasPressedThisFrame) ClicarTecla(".");
        if (k.backspaceKey.wasPressedThisFrame) ClicarTecla("Apagar");
        if (k.deleteKey.wasPressedThisFrame) Limpar();
        if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) ClicarConfirmar();
    }

    private ImpressoraEstacao Estacao()
    {
        if (tela == null) tela = GetComponent<UIScreen>();
        return tela != null && tela.Source != null ? tela.Source.GetComponent<ImpressoraEstacao>() : null;
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
        vistas = new RectTransform[4];
        for (int i = 0; i < 4; i++) vistas[i] = RedeUi.Cheio(area, "Vista" + (ImpressoraVista)i, Color.clear);

        ConstruirTeste(vistas[(int)ImpressoraVista.Teste]);
        ConstruirToner(vistas[(int)ImpressoraVista.Toner]);
        ConstruirPapel(vistas[(int)ImpressoraVista.Papel]);
        ConstruirPainel(vistas[(int)ImpressoraVista.Painel]);

        // Setas IGUAIS às do canvas do PC (UI_Screen_Gabinete): os cantos de baixo, sem título de vista e sem
        // dica escrita — a própria tela mostra o que fazer. A "►" fica à ESQUERDA do celular (RedeUi.Setas).
        RedeUi.Setas(painel, () => TrocarVista(-1), () => TrocarVista(1));

        msgTxt = RedeUi.Texto(painel, "Mensagem", "", 26, TextAnchor.MiddleCenter, TextoFraco);
        RedeUi.Ancorar((RectTransform)msgTxt.transform, new Vector2(0.5f, 0.5f), new Vector2(1300f, 40f), new Vector2(0.5f, 0.5f));
        ((RectTransform)msgTxt.transform).anchoredPosition = new Vector2(0f, -498f);
        msgTxt.fontStyle = FontStyle.Bold;
    }

    // Cantos arredondados (9-slice gerado uma vez): carcaça, cartões, campos e teclas.
    private static Sprite Arredondado
    {
        get
        {
            if (arredondado != null) return arredondado;
            const int n = 48, raio = 16;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(raio - x - 0.5f, x + 0.5f - (n - raio)));
                    float dy = Mathf.Max(0f, Mathf.Max(raio - y - 0.5f, y + 0.5f - (n - raio)));
                    float a = Mathf.Clamp01(raio + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            arredondado = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                                        new Vector4(raio, raio, raio, raio));
            return arredondado;
        }
    }

    private static RectTransform Caixa(Transform pai, string nome, Color cor, Vector2 pos, Vector2 tam)
    {
        RectTransform rt = RedeUi.Imagem(pai, nome, cor, Arredondado);
        Image img = rt.GetComponent<Image>();
        img.type = Image.Type.Sliced;
        img.color = cor;
        img.raycastTarget = false;
        RedeUi.Ancorar(rt, new Vector2(0.5f, 0.5f), tam, new Vector2(0.5f, 0.5f));
        rt.anchoredPosition = pos;
        return rt;
    }

    // Contorno tracejado do lugar vazio de uma peça.
    private static RectTransform Tracejado(Transform pai, Vector2 pos, Vector2 tam)
    {
        RectTransform grupo = RedeUi.Imagem(pai, "LugarVazio", Color.clear);
        grupo.GetComponent<Image>().raycastTarget = false;
        RedeUi.Ancorar(grupo, new Vector2(0.5f, 0.5f), tam, new Vector2(0.5f, 0.5f));
        grupo.anchoredPosition = pos;
        var cor = new Color(0.75f, 0.8f, 0.88f, 0.45f);
        for (int lado = 0; lado < 4; lado++)
        {
            bool horizontal = lado < 2;
            float comp = horizontal ? tam.x : tam.y;
            int n = Mathf.Max(2, Mathf.RoundToInt(comp / 34f));
            for (int i = 0; i < n; i++)
            {
                RectTransform tr = RedeUi.Imagem(grupo, "Traco", cor);
                tr.GetComponent<Image>().raycastTarget = false;
                float f = (i + 0.5f) / n;
                Vector2 a = horizontal ? new Vector2(f, lado == 0 ? 1f : 0f) : new Vector2(lado == 2 ? 0f : 1f, f);
                RedeUi.Ancorar(tr, a, horizontal ? new Vector2(comp / n * 0.55f, 4f) : new Vector2(4f, comp / n * 0.55f), new Vector2(0.5f, 0.5f));
            }
        }
        return grupo;
    }

    // Área com o modelo 3D da peça (a cópia é feita na primeira atualização: Preparar3D).
    private static ModeloNaTela Area3D(Transform pai, string nome, Vector2 pos, Vector2 tam)
    {
        RectTransform area = RedeUi.Imagem(pai, nome, Color.clear);
        area.GetComponent<Image>().raycastTarget = false;
        RedeUi.Ancorar(area, new Vector2(0.5f, 0.5f), tam, new Vector2(0.5f, 0.5f));
        area.anchoredPosition = pos;
        return ModeloNaTela.Criar(area, "Modelo3D", new Vector2Int(Mathf.RoundToInt(tam.x), Mathf.RoundToInt(tam.y)));
    }

    private void ConstruirTeste(RectTransform vista)
    {
        // a impressora de frente (cópia do modelo 3D desta impressora) e, saindo dela, a folha de teste
        impressoraModelo = Area3D(vista, "Impressora", new Vector2(0f, 90f), new Vector2(1100f, 640f));

        // janela que recorta a folha: ela "nasce" na saída de papel (topo da janela) e desce para fora
        folhaJanela = RedeUi.Imagem(vista, "SaidaFolha", Color.clear);
        folhaJanela.GetComponent<Image>().raycastTarget = false;
        RedeUi.Ancorar(folhaJanela, new Vector2(0.5f, 0.5f), new Vector2(360f, 420f), new Vector2(0.5f, 1f));
        folhaJanela.anchoredPosition = new Vector2(0f, 170f);
        folhaJanela.gameObject.AddComponent<RectMask2D>();
        folha = Caixa(folhaJanela, "Folha", Color.white, Vector2.zero, new Vector2(300f, 390f));
        folha.anchorMin = folha.anchorMax = new Vector2(0.5f, 1f);
        folha.pivot = new Vector2(0.5f, 0f);   // a borda de baixo sai primeiro
        folha.anchoredPosition = Vector2.zero;
        Caixa(folha, "Titulo", new Color(0.24f, 0.46f, 0.8f), new Vector2(-50f, 160f), new Vector2(150f, 18f));
        for (int i = 0; i < 6; i++)   // linhas de "texto"
            Caixa(folha, "Linha" + i, new Color(0.7f, 0.72f, 0.76f), new Vector2(i % 2 == 0 ? -10f : -30f, 115f - i * 24f), new Vector2(i % 2 == 0 ? 220f : 180f, 9f));
        float[] barras = { 40f, 75f, 55f, 95f, 70f };
        Color[] cores = { new Color(0.24f, 0.46f, 0.8f), new Color(0.95f, 0.6f, 0.18f), new Color(0.36f, 0.75f, 0.45f) };
        for (int i = 0; i < barras.Length; i++)   // gráfico de barras simples
            Caixa(folha, "Barra" + i, cores[i % 3], new Vector2(-90f + i * 45f, -150f + barras[i] * 0.5f), new Vector2(30f, barras[i]));
        folha.gameObject.SetActive(false);

        // LED da impressora: verde quando a página sai, pisca vermelho quando a impressão falha (sem selo de "✓": o
        // resultado é a própria folha impressa, o LED e a mensagem)
        ledTeste = MiniUi.ImagemCircular(vista, "LedTeste", new Color(0.3f, 0.34f, 0.4f), 26f);
        ((RectTransform)ledTeste.transform).anchoredPosition = new Vector2(-300f, -330f);
        ledTeste.raycastTarget = false;
        Image erro = MiniUi.ImagemCircular(vista, "Falha", Vermelho, 120f);
        erro.raycastTarget = false;
        falha = (RectTransform)erro.transform;
        falha.anchoredPosition = new Vector2(330f, -60f);
        Text xis = RedeUi.Texto(falha, "X", "X", 76, TextAnchor.MiddleCenter, Color.white);
        xis.fontStyle = FontStyle.Bold;
        RedeUi.Esticar((RectTransform)xis.transform);
        falha.gameObject.SetActive(false);

        RectTransform botao = Caixa(vista, "BotaoImprimir", new Color(0.24f, 0.46f, 0.8f), new Vector2(0f, -330f), new Vector2(520f, 86f));
        Text rot = RedeUi.Texto(botao, "Rotulo", "Imprimir página de teste", 32, TextAnchor.MiddleCenter, Color.white);
        rot.fontStyle = FontStyle.Bold;
        RedeUi.Esticar((RectTransform)rot.transform);
        TornarClicavel(botao, ClicarImprimirTeste);
    }

    private void ClicarImprimirTeste()
    {
        ImpressoraEstacao est = Estacao();
        if (est == null || imprimindo != null) return;
        falha.gameObject.SetActive(false);
        ledTeste.color = LedApagado;
        bool ok = est.ImprimirTeste();
        InteractionSounds.Raise(InteractionSound.PowerButton);
        imprimindo = StartCoroutine(ok ? AnimarImpressao() : AnimarFalha());
    }

    private System.Collections.IEnumerator AnimarImpressao()
    {
        Mensagem("Imprimindo...", TextoFraco);
        folha.anchoredPosition = Vector2.zero;
        folha.gameObject.SetActive(true);
        float h = folha.sizeDelta.y;
        yield return new WaitForSecondsRealtime(0.35f);
        InteractionSounds.Raise(InteractionSound.DrawerClosed);   // som curto de papel passando (o do gaveteiro)
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 1.6f)
        {
            // a folha desce aos trancos, como o papel puxado pelos roletes
            float k = Mathf.Clamp01(Mathf.SmoothStep(0f, 1f, t) + Mathf.Sin(t * 40f) * 0.004f);
            folha.anchoredPosition = new Vector2(0f, -h * k);
            yield return null;
        }
        folha.anchoredPosition = new Vector2(0f, -h);
        InteractionSounds.Raise(InteractionSound.PartInstalled);
        Mensagem("Página de teste impressa.", Verde);
        ledTeste.color = Verde;   // LED da impressora: pronta, impressão concluída
        imprimindo = null;
    }

    // Falta toner, papel ou IP: nada sai; o mecanismo "engasga" (som curto de papel + erro), o LED pisca, aparece o X e
    // a mensagem é neutra (o jogador investiga o motivo nas outras vistas).
    private System.Collections.IEnumerator AnimarFalha()
    {
        folha.gameObject.SetActive(false);
        Mensagem("Imprimindo...", TextoFraco);
        yield return new WaitForSecondsRealtime(0.5f);
        InteractionSounds.Raise(InteractionSound.DrawerClosed);   // roletes tentam puxar...
        yield return new WaitForSecondsRealtime(0.35f);
        InteractionSounds.Raise(InteractionSound.Error);          // ...e travam
        falha.gameObject.SetActive(true);
        Mensagem("A impressão não foi concluída.", Vermelho);
        Color apagado = LedApagado;
        for (int i = 0; i < 3; i++)
        {
            ledTeste.color = Vermelho;
            yield return new WaitForSecondsRealtime(0.18f);
            ledTeste.color = apagado;
            yield return new WaitForSecondsRealtime(0.14f);
        }
        imprimindo = null;
    }

    private void OnDisable()
    {
        // fechou no meio da impressão: a animação para e a próxima abertura começa limpa
        if (imprimindo != null) { StopCoroutine(imprimindo); imprimindo = null; }
        if (folha != null) folha.gameObject.SetActive(false);
        if (ledTeste != null) ledTeste.color = LedApagado;
        if (falha != null) falha.gameObject.SetActive(false);
    }

    private void ConstruirToner(RectTransform vista)
    {
        // a impressora de frente com a tampa da frente aberta (baixada): o compartimento do cartucho no meio
        Caixa(vista, "Carcaca", CorCarcacaSombra, new Vector2(0f, 26f), new Vector2(1180f, 640f));
        Caixa(vista, "CarcacaFrente", CorCarcaca, new Vector2(0f, 40f), new Vector2(1160f, 612f));
        Caixa(vista, "SaidaPapel", CorCarcacaSombra, new Vector2(0f, 300f), new Vector2(760f, 40f));
        RectTransform cavidade = Caixa(vista, "Compartimento", CorCavidade, new Vector2(0f, 40f), new Vector2(900f, 360f));
        Caixa(cavidade, "SombraTopo", new Color(0f, 0f, 0f, 0.35f), new Vector2(0f, 150f), new Vector2(880f, 50f));
        foreach (float x in new[] { -380f, 380f })   // trilhos laterais onde o cartucho desliza
            Caixa(cavidade, "Trilho", new Color(0.32f, 0.35f, 0.4f), new Vector2(x, -10f), new Vector2(26f, 280f));
        Caixa(vista, "TampaAberta", CorCarcacaSombra, new Vector2(0f, -310f), new Vector2(1080f, 110f));
        Caixa(vista, "Dobradica", new Color(0.45f, 0.48f, 0.53f), new Vector2(0f, -250f), new Vector2(1000f, 14f));
        tonerVazio = Tracejado(cavidade, new Vector2(0f, -10f), new Vector2(700f, 220f));
        tonerModelo = Area3D(cavidade, "Cartucho", new Vector2(0f, -10f), new Vector2(820f, 300f));
        Alvo(cavidade, new Vector2(0f, -10f), new Vector2(760f, 280f), () => ClicarEncaixe(ImpressoraParte.Toner));
    }

    private void ConstruirPapel(RectTransform vista)
    {
        // a frente da impressora em cima e a bandeja puxada para fora, vista de cima
        Caixa(vista, "Carcaca", CorCarcacaSombra, new Vector2(0f, 300f), new Vector2(1180f, 200f));
        Caixa(vista, "CarcacaFrente", CorCarcaca, new Vector2(0f, 306f), new Vector2(1160f, 184f));
        Caixa(vista, "Abertura", CorCavidade, new Vector2(0f, 230f), new Vector2(900f, 40f));
        RectTransform bandeja = Caixa(vista, "Bandeja", CorCarcacaSombra, new Vector2(0f, -110f), new Vector2(960f, 600f));
        RectTransform fundo = Caixa(bandeja, "Fundo", new Color(0.55f, 0.58f, 0.63f), new Vector2(0f, 20f), new Vector2(900f, 500f));
        foreach (float x in new[] { -420f, 420f })   // guias azuis das laterais do papel
            Caixa(fundo, "Guia", new Color(0.24f, 0.46f, 0.8f), new Vector2(x, 0f), new Vector2(22f, 220f));
        Caixa(bandeja, "Puxador", CorCavidade, new Vector2(0f, -268f), new Vector2(220f, 26f));
        papelVazio = Tracejado(fundo, Vector2.zero, new Vector2(700f, 400f));
        papelModelo = Area3D(fundo, "Resma", Vector2.zero, new Vector2(800f, 470f));
        Alvo(fundo, Vector2.zero, new Vector2(760f, 440f), () => ClicarEncaixe(ImpressoraParte.Papel));
    }

    private void ConstruirPainel(RectTransform vista)
    {
        RectTransform cartao = Caixa(vista, "Cartao", CorCartao, new Vector2(0f, 10f), new Vector2(880f, 900f));

        Text titulo = RedeUi.Texto(cartao, "Titulo", "CONFIGURAÇÃO DE REDE", 30, TextAnchor.MiddleLeft, TextoClaro);
        titulo.fontStyle = FontStyle.Bold;
        RedeUi.Ancorar((RectTransform)titulo.transform, new Vector2(0f, 1f), new Vector2(560f, 50f), new Vector2(0f, 1f));
        ((RectTransform)titulo.transform).anchoredPosition = new Vector2(48f, -40f);

        // estado da impressora (o mesmo aviso neutro do painel 3D) com o LED ao lado
        RectTransform chip = Caixa(cartao, "Estado", CorCampo, new Vector2(0f, 330f), new Vector2(784f, 70f));
        lcdLed = MiniUi.ImagemCircular(chip, "Led", Amarelo, 22f);
        var led = (RectTransform)lcdLed.transform;
        led.anchorMin = led.anchorMax = new Vector2(0f, 0.5f);
        led.anchoredPosition = new Vector2(34f, 0f);
        lcdLed.raycastTarget = false;
        lcdStatus = RedeUi.Texto(chip, "Status", "", 28, TextAnchor.MiddleLeft, TextoLcd);
        var st = (RectTransform)lcdStatus.transform;
        st.anchorMin = Vector2.zero; st.anchorMax = Vector2.one; st.offsetMin = new Vector2(64f, 0f); st.offsetMax = new Vector2(-20f, 0f);
        lcdStatus.fontStyle = FontStyle.Bold;

        // um único campo: o endereço IP (recebe as teclas; os pontos entram sozinhos)
        lcdIp = CampoRede(cartao, "Ip", "Endereço IP");
        ipSalvoTxt = RedeUi.Texto(cartao, "IpSalvo", "", 22, TextAnchor.MiddleCenter, TextoFraco);
        RedeUi.Ancorar((RectTransform)ipSalvoTxt.transform, new Vector2(0.5f, 0.5f), new Vector2(784f, 30f), new Vector2(0.5f, 0.5f));
        ((RectTransform)ipSalvoTxt.transform).anchoredPosition = new Vector2(0f, 138f);

        // teclado numérico 3 x 4
        for (int i = 0; i < Teclas.Length; i++)
        {
            string tecla = Teclas[i];
            int coluna = i % 3, linha = i / 3;
            RectTransform b = RedeUi.Botao(cartao, "Tecla_" + tecla, tecla == "Apagar" ? "Apagar" : tecla, CorTecla,
                                           () => ClicarTecla(tecla), tecla == "Apagar" ? 26 : 38);
            Arredondar(b);
            RedeUi.Ancorar(b, new Vector2(0.5f, 0.5f), new Vector2(200f, 84f), new Vector2(0.5f, 0.5f));
            b.anchoredPosition = new Vector2(-216f + coluna * 216f, 50f - linha * 98f);
        }

        RectTransform limpar = RedeUi.Botao(cartao, "Limpar", "Limpar", new Color(0.42f, 0.28f, 0.3f), Limpar, 26);
        Arredondar(limpar);
        RedeUi.Ancorar(limpar, new Vector2(0.5f, 0.5f), new Vector2(300f, 80f), new Vector2(0.5f, 0.5f));
        limpar.anchoredPosition = new Vector2(-242f, -362f);
        RectTransform confirmar = RedeUi.Botao(cartao, "Confirmar", "Confirmar", Verde, ClicarConfirmar, 28);
        Arredondar(confirmar);
        RedeUi.Ancorar(confirmar, new Vector2(0.5f, 0.5f), new Vector2(460f, 80f), new Vector2(0.5f, 0.5f));
        confirmar.anchoredPosition = new Vector2(162f, -362f);
        Text rc = confirmar.GetComponentInChildren<Text>();
        if (rc != null) { rc.color = new Color(0.05f, 0.1f, 0.06f); rc.fontStyle = FontStyle.Bold; }

        Text dica = RedeUi.Texto(vista, "Dica", "Digite pelos botões ou pelo teclado  ·  os pontos entram sozinhos  ·  Enter confirma  ·  Backspace apaga", 20,
                                 TextAnchor.MiddleCenter, TextoFraco);
        RedeUi.Ancorar((RectTransform)dica.transform, new Vector2(0.5f, 0.5f), new Vector2(1100f, 30f), new Vector2(0.5f, 0.5f));
        ((RectTransform)dica.transform).anchoredPosition = new Vector2(0f, -462f);
    }

    private static readonly Color LedApagado = new Color(0.3f, 0.34f, 0.4f);
    private static readonly Color BordaAtiva = new Color(0.36f, 0.6f, 0.95f, 0.95f);

    // Campo do painel de rede (rótulo + visor + borda), na largura do cartão.
    private Text CampoRede(RectTransform cartao, string nome, string rotuloTxt)
    {
        Text rotulo = RedeUi.Texto(cartao, "Rotulo" + nome, rotuloTxt, 22, TextAnchor.MiddleLeft, TextoFraco);
        RedeUi.Ancorar((RectTransform)rotulo.transform, new Vector2(0.5f, 0.5f), new Vector2(784f, 30f), new Vector2(0.5f, 0.5f));
        ((RectTransform)rotulo.transform).anchoredPosition = new Vector2(0f, 262f);
        RectTransform campo = Caixa(cartao, "Campo" + nome, CorCampo, new Vector2(0f, 200f), new Vector2(784f, 84f));
        Caixa(campo, "Borda", BordaAtiva, new Vector2(0f, -40f), new Vector2(784f, 6f));
        Text visor = RedeUi.Texto(campo, nome, "", 36, TextAnchor.MiddleCenter, TextoClaro);
        RedeUi.Esticar((RectTransform)visor.transform);
        visor.fontStyle = FontStyle.Bold;
        visor.raycastTarget = false;
        return visor;
    }

    private static void Arredondar(RectTransform botao)
    {
        Image img = botao.GetComponent<Image>();
        img.sprite = Arredondado;
        img.type = Image.Type.Sliced;
    }

    private void Limpar()
    {
        ImpressoraEstacao e = Estacao();
        if (e == null) return;
        e.Sessao.Limpar();
        InteractionSounds.Raise(InteractionSound.PowerButton);
        Mensagem("Campo do IP limpo.", TextoFraco);
        Atualizar();
    }

    // Área clicável invisível com moldura amarela só com o mouse em cima (como os encaixes do gabinete e do notebook).
    private static void Alvo(Transform pai, Vector2 pos, Vector2 tam, UnityAction acao)
    {
        var go = new GameObject("Alvo", typeof(RectTransform));
        go.transform.SetParent(pai, false);
        var rt = (RectTransform)go.transform;
        RedeUi.Ancorar(rt, new Vector2(0.5f, 0.5f), tam, new Vector2(0.5f, 0.5f));
        rt.anchoredPosition = pos;
        var img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        var b = go.AddComponent<Button>();
        b.targetGraphic = img;
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(acao);
        RectTransform moldura = RedeUi.Imagem(rt, "Moldura", Color.clear);
        RedeUi.Esticar(moldura);
        moldura.GetComponent<Image>().raycastTarget = false;
        var amarelo = new Color(1f, 0.86f, 0.2f, 0.95f);
        foreach ((Vector2 min, Vector2 max) in new[] { (new Vector2(0f, 1f), new Vector2(1f, 1f)), (Vector2.zero, new Vector2(1f, 0f)),
                                                       (Vector2.zero, new Vector2(0f, 1f)), (new Vector2(1f, 0f), Vector2.one) })
        {
            RectTransform l = RedeUi.Imagem(moldura, "Linha", amarelo);
            l.anchorMin = min; l.anchorMax = max;
            l.sizeDelta = min.y == max.y ? new Vector2(0f, 4f) : new Vector2(4f, 0f);
            l.anchoredPosition = Vector2.zero;
            l.GetComponent<Image>().raycastTarget = false;
        }
        moldura.gameObject.SetActive(false);
        var hover = go.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        foreach (var (tipo, ativo) in new[] { (UnityEngine.EventSystems.EventTriggerType.PointerEnter, true),
                                              (UnityEngine.EventSystems.EventTriggerType.PointerExit, false) })
        {
            var e = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = tipo };
            bool v = ativo;
            e.callback.AddListener(_ => moldura.gameObject.SetActive(v));
            hover.triggers.Add(e);
        }
    }

    // Copia os modelos das peças para as vistas (uma vez) e enquadra: o cartucho de frente, a resma de cima.
    private void Preparar3D()
    {
        // a impressora de frente: cópia (só visual) DESTA impressora, refeita se a tela abrir em outra
        ImpressoraEstacao est = Estacao();
        if (est != null && est != copiaDe && impressoraModelo != null)
        {
            copiaDe = est;
            Transform copia = impressoraModelo.Copiar(est.gameObject);
            if (copia != null)
            {
                Bounds b = impressoraModelo.BoundsLocal();
                // a frente é o lado do painel de controle (ImpressoraPainel3D fica na face da frente)
                Vector3 frente = Vector3.back;
                Transform painel = null;
                foreach (Transform t in copia.GetComponentsInChildren<Transform>(true)) if (t.name == "PainelControle") painel = t;
                if (painel != null)
                {
                    Vector3 d = impressoraModelo.Modelo.InverseTransformPoint(painel.position) - b.center;
                    d.y = 0f;
                    // o painel fica num canto da face: vale a face (±X ou ±Z) em que ele encosta, relativa ao tamanho da caixa
                    if (d.sqrMagnitude > 1e-6f)
                        frente = Mathf.Abs(d.x) / Mathf.Max(b.extents.x, 1e-4f) > Mathf.Abs(d.z) / Mathf.Max(b.extents.z, 1e-4f) ? new Vector3(Mathf.Sign(d.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(d.z));
                }
                impressoraModelo.Enquadrar(-frente + Vector3.down * 0.3f, Vector3.up, b, 0.08f);
            }
        }
        if (modelosProntos) return;
        modelosProntos = true;
        if (modeloToner != null && tonerModelo.Copiar(modeloToner) != null)
            tonerModelo.Enquadrar(new Vector3(0f, -0.35f, -1f), Vector3.up, tonerModelo.BoundsLocal(), 0.06f);
        if (modeloPapel != null && papelModelo.Copiar(modeloPapel) != null)
            papelModelo.Enquadrar(Vector3.down, Vector3.left, papelModelo.BoundsLocal(), 0.06f);
    }

    private void Rotulo(Transform pai, string texto, Vector2 pos)
    {
        Text t = RedeUi.Texto(pai, "Rotulo_" + texto, texto, 24, TextAnchor.MiddleCenter, TextoClaro);
        RedeUi.Ancorar((RectTransform)t.transform, new Vector2(0.5f, 0.5f), new Vector2(620f, 32f), new Vector2(0.5f, 0.5f));
        ((RectTransform)t.transform).anchoredPosition = pos;
    }

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

    private void TrocarVista(int passo)
    {
        int n = Ordem.Length;
        int i = System.Array.IndexOf(Ordem, vista);
        vista = Ordem[((i < 0 ? 0 : i) + passo % n + n) % n];
        Mensagem("", TextoFraco);
        Atualizar();
    }

    private void ClicarEncaixe(ImpressoraParte encaixe)
    {
        ImpressoraEstacao est = Estacao();
        if (est == null) return;

        ItemHolder mao = Mao();
        PickupInteractable naMao = mao != null ? mao.Current : null;

        ImpressoraEncaixeResultado r = est.UsarEncaixe(encaixe, naMao, out GameObject devolver);
        switch (r)
        {
            case ImpressoraEncaixeResultado.Instalado:
                if (mao != null) mao.Stow(est.transform.position);
                InteractionSounds.Raise(InteractionSound.PartInstalled);
                Mensagem(encaixe == ImpressoraParte.Toner ? "Cartucho de toner instalado." : "Papel carregado na bandeja.", Verde);
                break;
            case ImpressoraEncaixeResultado.Removido:
                if (mao != null && devolver != null) mao.Retrieve(devolver);
                InteractionSounds.Raise(InteractionSound.PartRemoved);
                Mensagem("Peça retirada.", TextoFraco);
                break;
            case ImpressoraEncaixeResultado.PrecisaPeca:
                Mensagem(encaixe == ImpressoraParte.Toner
                    ? "O compartimento precisa de um item compatível."
                    : "A bandeja precisa de um item compatível.", Amarelo);
                break;
            case ImpressoraEncaixeResultado.PecaErrada:
                InteractionSounds.Raise(InteractionSound.Error);   // peça no encaixe errado
                Mensagem("Este item não é compatível com este encaixe.", Vermelho);
                break;
            case ImpressoraEncaixeResultado.MaoCheia:
                Mensagem("Com as mãos ocupadas não é possível retirar a peça.", Amarelo);
                break;
            case ImpressoraEncaixeResultado.PecaQuebrada:
                Mensagem("Este item está danificado e não pode ser instalado.", Vermelho);
                break;
        }
        Atualizar();
    }

    private void ClicarTecla(string tecla)
    {
        ImpressoraEstacao est = Estacao();
        if (est == null) return;

        if (tecla == "Apagar") est.Sessao.Apagar();
        else est.Sessao.Digitar(tecla);
        InteractionSounds.Raise(InteractionSound.PowerButton);   // tecla do painel: mesmo "clique" dos botões do PC

        Mensagem("", TextoFraco);
        Atualizar();
    }

    private void ClicarConfirmar()
    {
        ImpressoraEstacao est = Estacao();
        if (est == null) return;

        ImpressoraSession s = est.Sessao;
        if (s.Digitado.Length == 0) return;   // nada digitado: o IP salvo continua como está
        if (!s.ConfirmarIp())
        {
            InteractionSounds.Raise(InteractionSound.Error);
            // não diz qual é o IP certo: só se o formato está incompleto ou se não confere com o exigido
            Mensagem(s.UltimaRecusa == ImpressoraSession.RecusaIp.NaoConfere
                ? "A configuração informada não corresponde aos requisitos da rede."
                : "Endereço incompleto: preencha os quatro grupos.", Vermelho);
        }
        else
        {
            InteractionSounds.Raise(InteractionSound.PowerButton);
            Mensagem($"IP configurado: {s.IpSalvo}.", Verde);
        }
        Atualizar();
    }

    // ================================================================== desenho

    private void Atualizar()
    {
        if (!construido) return;
        ImpressoraEstacao est = Estacao();

        for (int i = 0; i < vistas.Length; i++) vistas[i].gameObject.SetActive(i == (int)vista);

        if (est == null) return;

        Preparar3D();
        if (vista == ImpressoraVista.Toner)
        {
            bool tem = est.Sessao.TemToner;
            tonerModelo.gameObject.SetActive(tem && modeloToner != null);
            tonerVazio.gameObject.SetActive(!tem);
        }
        else if (vista == ImpressoraVista.Papel)
        {
            bool tem = est.Sessao.TemPapel;
            papelModelo.gameObject.SetActive(tem && modeloPapel != null);
            papelVazio.gameObject.SetActive(!tem);
        }
        else if (vista == ImpressoraVista.Painel) AtualizarPainel(est.Sessao);
    }

    private void AtualizarPainel(ImpressoraSession s)
    {
        string status;
        // Mesmo aviso genérico do painel 3D (ImpressoraPainel3D.Status): não revela o que falta.
        status = ImpressoraPainel3D.Status(s);
        lcdStatus.color = s.Pronta ? TextoLcd : Amarelo;
        lcdStatus.text = status;
        lcdLed.color = s.Pronta ? Verde : Amarelo;

        bool cursor = Mathf.Repeat(Time.unscaledTime, 1f) < 0.55f;
        Visor(lcdIp, s.IpDigitado, s.IpSalvo, cursor);
        ipSalvoTxt.text = s.TemIp ? "Configurado: " + s.IpSalvo : "Nenhum endereço configurado";
    }

    // O visor do IP: em digitação, a máscara de entrada (192.168.0._) com o cursor piscando; parado, o IP salvo como o
    // cartaz o escreve (verde) ou o molde vazio.
    private void Visor(Text visor, string digitado, string salvo, bool cursor)
    {
        bool digitando = digitado.Length > 0;
        string txt = digitando ? ImpressoraSession.FormatarVisor(digitado)
                   : salvo.Length > 0 ? salvo : ImpressoraSession.FormatarVisor("");
        visor.text = txt + (cursor ? "|" : " ");
        visor.color = digitando ? (ImpressoraSession.IpEhValido(digitado) ? Verde : TextoClaro)
                    : salvo.Length > 0 ? Verde : TextoFraco;
    }


    // Mesmo padrão dos minigames (FeedbackManutencao): sucesso, requisito e erro vão para o cartão do rodapé; o texto
    // fraco ("Imprimindo...", "Peça retirada.") continua na linha de status.
    private void Mensagem(string texto, Color cor)
    {
        if (msgTxt == null) return;
        bool cartao = !string.IsNullOrEmpty(texto) && (cor == Verde || cor == Amarelo || cor == Vermelho);
        msgTxt.text = cartao ? "" : texto;
        msgTxt.color = cor;
        if (cartao) FeedbackManutencao.Mostrar(this, texto, FeedbackManutencao.TipoDaCor(cor == Verde, cor == Vermelho));
    }

    private void Fechar()
    {
        if (tela != null) tela.RequestClose();
    }
}
