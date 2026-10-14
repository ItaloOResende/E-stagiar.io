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
    private static readonly Color TextoFraco = new Color(0.66f, 0.71f, 0.78f);
    private static readonly Color VermelhoErro = new Color(0.85f, 0.30f, 0.28f);
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
    private const float RjVazioY = 285f;          // y do desenho do RJ45 vazio, logo acima dos fios
    private const float CentroColuna = 60f;       // centro da área clicável da coluna
    private const float AlturaColuna = 360f;
    // posições (y) DENTRO da coluna
    private const float AlturaFioSolto = 64f;                                        // fio ainda solto
    private const float FioSoltoY = BaseFios - CentroColuna + AlturaFioSolto * 0.5f;
    private const float PontaSoltoY = BaseFios - CentroColuna + AlturaFioSolto + 8f;
    private const float TopoFioColocado = RailY - 15f;                               // onde o fio encaixado encosta no trilho
    private const float AlturaFioColocado = TopoFioColocado - BaseFios;
    private const float FioColocadoY = (TopoFioColocado + BaseFios) * 0.5f - CentroColuna;
    private const float PontaColocadoY = TopoFioColocado - CentroColuna + 8f;
    private const float SoqueteY = RailY - CentroColuna;
    private const float NumeroY = 180f - CentroColuna;

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
    // O padrão de cores (T568B) começa ESCONDIDO: o jogador o abre no "celular" do canto inferior direito.
    private bool mostrarTabela;

    [Tooltip("Prefab do cabo que aparece na mão do jogador quando a crimpagem dá certo (ex.: Pickup_CaboRede).")]
    [SerializeField] private GameObject caboPrefab;

    private Text msgTxt;
    private RectTransform faseCortar, faseDescascar, faseFileira;
    private RectTransform caboTela, capaCabo, faixaCabo, marcaCorte, fiosCabo, tabelaPainel, pontaMao, botaoCelular;
    private RectTransform alvoCorte, alvoCapa;
    private Image faixaAlvoImg, sinalCapaImg, sinalRjVazioImg;
    private readonly Image[] fiosImg = new Image[8];
    private readonly Image[] fiosPonta = new Image[8];
    private readonly RectTransform[] fiosSoquete = new RectTransform[8];
    private UICableCurve curvaMao;
    private Image pontaMaoImg;
    private Canvas canvasTela;

    private void Awake() => Construir();

    private void OnEnable() => NovaPeca();

    private void NovaPeca()
    {
        sessao.Reset();
        EmbaralharFios();
        mostrarTabela = false;   // o celular (padrão de cores) sempre começa fechado
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

        // UMA região só, de ponta a ponta do canvas (nada de bordas/faixas escuras).
        RectTransform painel = RedeUi.Cheio(transform, "Painel", CorPainel);

        // Mesmo botão de fechar da tela do PC (quadrado escuro com "X").
        RedeUi.BotaoFechar(painel, Fechar);

        // A "bancada" agora é o canvas INTEIRO: por isso o cabo pode sair da tela por baixo.
        RectTransform bancada = RedeUi.Cheio(painel, "Bancada", Color.clear);

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

    private static RectTransform Caixa(Transform pai, string nome, Color cor, Vector2 pos, Vector2 tamanho)
    {
        RectTransform rt = RedeUi.Imagem(pai, nome, cor);
        RedeUi.Ancorar(rt, new Vector2(0.5f, 0.5f), tamanho, new Vector2(0.5f, 0.5f));
        rt.anchoredPosition = pos;
        return rt;
    }

    private static Text Texto(RectTransform pai, string nome, string valor, int tam, TextAnchor alinhamento, Color cor, Vector2 ancora, Vector2 pos, Vector2 size)
    {
        Text t = RedeUi.Texto(pai, nome, valor, tam, alinhamento, cor);
        RedeUi.Ancorar((RectTransform)t.transform, ancora, size, ancora);
        ((RectTransform)t.transform).anchoredPosition = pos;
        return t;
    }

    private static RectTransform Botao(RectTransform pai, string nome, string rotulo, Color cor, UnityEngine.Events.UnityAction acao, int tam, Vector2 ancora, Vector2 pos, Vector2 size)
    {
        RectTransform rt = RedeUi.Botao(pai, nome, rotulo, cor, acao, tam);
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

    private void ConstruirCabo(RectTransform bancada)
    {
        caboTela = Caixa(bancada, "CaboRede", Color.clear, new Vector2(0f, CaboCentroY), new Vector2(900f, 760f));

        // O cabo azul: LARGO e comprido, para parecer que TODOS os fios saem dele. O tamanho muda entre
        // inteiro e descascado em Atualizar().
        capaCabo = Caixa(caboTela, "CapaAzul", AzulCapa,
            new Vector2(0f, (TopoCapaInteira + FundoCapa) * 0.5f), new Vector2(LarguraCapa, TopoCapaInteira - FundoCapa));
        RectTransform brilho = RedeUi.Imagem(capaCabo, "Brilho", new Color(1f, 1f, 1f, 0.10f));
        RedeUi.Ancorar(brilho, new Vector2(0f, 1f), new Vector2(22f, 160f), new Vector2(0f, 1f));
        brilho.anchoredPosition = new Vector2(20f, -18f);

        // Borda clara = o lugar onde a capa foi cortada (só aparece descascado).
        faixaCabo = Caixa(caboTela, "BordaCapa", new Color(0.72f, 0.78f, 0.86f), new Vector2(0f, BaseFios - 6f), new Vector2(LarguraCapa, 12f));

        // Marca de onde o alicate vai cortar a capa (etapa "Descascar").
        marcaCorte = Caixa(caboTela, "MarcaCorte", new Color(0.95f, 0.82f, 0.28f), new Vector2(0f, BaseFios + 10f), new Vector2(LarguraCapa + 30f, 8f));

        fiosCabo = RedeUi.Imagem(caboTela, "Fios", Color.clear);
        RedeUi.Esticar(fiosCabo);

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

            // Ponta de cobre exposta, na extremidade de cima.
            Image ponta = RedeUi.Imagem(coluna, "Ponta", new Color(0.86f, 0.78f, 0.60f)).GetComponent<Image>();
            RedeUi.Ancorar((RectTransform)ponta.transform, new Vector2(0.5f, 0.5f), new Vector2(LarguraFio, 16f), new Vector2(0.5f, 0.5f));
            ((RectTransform)ponta.transform).anchoredPosition = new Vector2(0f, PontaSoltoY);
            fiosPonta[i] = ponta;

            Text numero = RedeUi.Texto(coluna, "Numero", (i + 1).ToString(), 22, TextAnchor.MiddleCenter, TextoClaro);
            RedeUi.Ancorar((RectTransform)numero.transform, new Vector2(0.5f, 0.5f), new Vector2(EspacamentoFio - 4f, 28f), new Vector2(0.5f, 0.5f));
            ((RectTransform)numero.transform).anchoredPosition = new Vector2(0f, NumeroY);
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
        // Brilho em volta: só aparece quando os 8 fios já estão na fileira.
        sinalRjVazioImg = RedeUi.Imagem(pai, "SinalRj", new Color(0.97f, 0.84f, 0.30f, 0.5f)).GetComponent<Image>();
        RedeUi.Ancorar((RectTransform)sinalRjVazioImg.transform, new Vector2(0.5f, 0.5f), new Vector2(340f, 200f), new Vector2(0.5f, 0.5f));
        ((RectTransform)sinalRjVazioImg.transform).anchoredPosition = new Vector2(0f, RjVazioY);

        RectTransform rj = RedeUi.Imagem(pai, "Rj45Vazio", Color.clear);
        RedeUi.Ancorar(rj, new Vector2(0.5f, 0.5f), new Vector2(280f, 130f), new Vector2(0.5f, 0.5f));
        rj.anchoredPosition = new Vector2(0f, RjVazioY);

        Caixa(rj, "Trava", new Color(0.72f, 0.78f, 0.86f), new Vector2(0f, 60f), new Vector2(70f, 20f));
        Caixa(rj, "Corpo", new Color(0.84f, 0.89f, 0.95f), new Vector2(0f, 14f), new Vector2(250f, 72f));

        // 8 encaixes VAZIOS (todos na mesma cor: nada conectado).
        for (int i = 0; i < 8; i++)
            Caixa(rj, "Vazio" + i, new Color(0.17f, 0.19f, 0.24f), new Vector2((i - 3.5f) * 30f, -30f), new Vector2(16f, 32f));

        // Ponto certo para fechar o cabo (sem botão): clicar no RJ45 encaixa e já crimpa.
        Botao(pai, "PontoRj", "", Transparente, ClicarRj, 16, new Vector2(0.5f, 0.5f),
            new Vector2(0f, RjVazioY), new Vector2(310f, 200f)).SetAsLastSibling();
    }

    private void ConstruirCortar()
    {
        // A caixa é GRANDE, DEITADA e fica no canto esquerdo (pode até sair da tela). O cabo sai DE LADO,
        // na horizontal, bem mais fino que o cabo em pé.
        RectTransform caixa = Caixa(faseCortar, "CaixaCabo", new Color(0.60f, 0.45f, 0.29f),
            new Vector2(CaixaCortarX, CaixaCortarY), new Vector2(CaixaCortarLargura, CaixaCortarAltura));
        Caixa(caixa, "Tampa", new Color(0.52f, 0.39f, 0.24f), new Vector2(0f, CaixaCortarAltura * 0.5f - 22f), new Vector2(CaixaCortarLargura, 44f));
        Caixa(caixa, "Base", new Color(0.46f, 0.34f, 0.20f), new Vector2(0f, -CaixaCortarAltura * 0.5f + 24f), new Vector2(CaixaCortarLargura, 48f));
        Caixa(caixa, "Etiqueta", new Color(0.68f, 0.53f, 0.34f), new Vector2(290f, 40f), new Vector2(560f, 330f));
        Caixa(caixa, "Faixa", new Color(0.30f, 0.22f, 0.14f), new Vector2(290f, 40f), new Vector2(460f, 60f));
        // "Boca" escura NA LATERAL direita, por onde o cabo sai.
        Caixa(caixa, "Boca", new Color(0.15f, 0.11f, 0.07f),
            new Vector2(CaixaCortarLargura * 0.5f, SaidaCaixaY - CaixaCortarY), new Vector2(96f, SaidaCaboEspessura + 96f));

        // O cabo que sai de lado e vai para a direita.
        Caixa(faseCortar, "CaboDaLateral", AzulCapa,
            new Vector2(SaidaCaboX, SaidaCaixaY), new Vector2(SaidaCaboComprimento, SaidaCaboEspessura));

        // CORTAR não tem botão: o ponto certo é a faixa amarela que pulsa sobre o cabo.
        alvoCorte = Botao(faseCortar, "PontoCorte", "", Transparente, () =>
        {
            if (sessao.Cortar()) { InteractionSounds.Raise(InteractionSound.CableUnplugged); Mensagem("", TextoClaro); Atualizar(); }
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
            if (sessao.Descascar()) { InteractionSounds.Raise(InteractionSound.CableUnplugged); Mensagem("", TextoClaro); Atualizar(); }
        }, 16, new Vector2(0.5f, 0.5f), new Vector2(0f, centroSali), new Vector2(LarguraCapa + 40f, TopoCapaInteira - BaseFios + 40f));

        // Brilho suave sobre a saliência, só para o jogador achar onde clicar (nenhum texto).
        sinalCapaImg = RedeUi.Imagem(caboTela, "SinalCapa", new Color(1f, 1f, 1f, 0.12f)).GetComponent<Image>();
        RedeUi.Ancorar((RectTransform)sinalCapaImg.transform, new Vector2(0.5f, 0.5f),
            new Vector2(LarguraCapa, TopoCapaInteira - BaseFios), new Vector2(0.5f, 0.5f));
        ((RectTransform)sinalCapaImg.transform).anchoredPosition = new Vector2(0f, (BaseFios + TopoCapaInteira) * 0.5f);
    }

    private void ConstruirFileira()
    {
        ConstruirTabela();

        // A ordem dos fios é SORTEADA a cada abertura da tela — não existe botão de "soltar tudo de novo".
        // O celular do canto inferior direito é a única ajuda: abre a tabela do padrão de cores.
        ConstruirCelular();
    }

    /// <summary>Botão do celular (canto inferior direito, sem texto): abre a tabela do padrão T568B.</summary>
    private void ConstruirCelular()
    {
        botaoCelular = Botao(faseFileira, "BotaoCelular", "", new Color(0.22f, 0.25f, 0.3f, 0.95f),
            () => { mostrarTabela = true; Atualizar(); }, 22,
            new Vector2(1f, 0f), new Vector2(-16f, 16f), new Vector2(96f, 150f));

        // Desenho do aparelho: corpo escuro, a "tela" e o alto-falante (nada de texto).
        RectTransform tela = RedeUi.Imagem(botaoCelular, "TelaCelular", new Color(0.13f, 0.16f, 0.21f));
        RedeUi.Ancorar(tela, new Vector2(0.5f, 0.5f), new Vector2(78f, 122f), new Vector2(0.5f, 0.5f));
        tela.anchoredPosition = new Vector2(0f, -5f);

        RectTransform altoFalante = RedeUi.Imagem(botaoCelular, "AltoFalante", new Color(0.55f, 0.6f, 0.68f));
        RedeUi.Ancorar(altoFalante, new Vector2(0.5f, 1f), new Vector2(34f, 7f), new Vector2(0.5f, 1f));
        altoFalante.anchoredPosition = new Vector2(0f, -12f);
    }

    /// <summary>
    /// O celular com a ordem e as cores do RJ45 (1 a 8). Ele abre NO MESMO LUGAR do botão (mesmo canto e mesmo
    /// pivô, só maior): cobre o botão e, clicado de novo (em qualquer ponto), fecha e o botão volta a aparecer.
    /// </summary>
    private void ConstruirTabela()
    {
        tabelaPainel = Botao(faseFileira, "Celular", "", new Color(0.10f, 0.12f, 0.16f, 0.98f),
            () => { mostrarTabela = false; Atualizar(); }, 22,
            new Vector2(1f, 0f), new Vector2(-16f, 16f), new Vector2(430f, 640f));

        RectTransform tela = RedeUi.Imagem(tabelaPainel, "Tela", new Color(0.13f, 0.15f, 0.20f));
        RedeUi.Esticar(tela, 16f);

        Texto(tela, "Titulo", "PADR\u00C3O T568B", 26, TextAnchor.MiddleCenter, TextoClaro,
            new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(360f, 36f));

        for (int i = 0; i < 8; i++)
        {
            RedeFio fio = RedeCores.OrdemT568B[i];
            float y = -78f - i * 58f;

            Texto(tela, "Numero" + i, (i + 1).ToString(), 26, TextAnchor.MiddleLeft, TextoFraco,
                new Vector2(0f, 1f), new Vector2(18f, y), new Vector2(40f, 40f));

            RectTransform amostra = RedeUi.Imagem(tela, "Amostra" + i, RedeCores.Cor(fio));
            RedeUi.Ancorar(amostra, new Vector2(0f, 1f), new Vector2(46f, 30f), new Vector2(0f, 1f));
            amostra.anchoredPosition = new Vector2(58f, y - 4f);

            Texto(tela, "Nome" + i, RedeCores.Nome(fio), 22, TextAnchor.MiddleLeft, TextoClaro,
                new Vector2(0f, 1f), new Vector2(116f, y), new Vector2(260f, 40f));
        }
    }

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
                Mensagem("Fio na mão — clique na posição onde ele deve ficar.", TextoClaro);
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
        PulsarPontosCertos();

        if (curvaMao == null) return;

        bool naMao = sessao.Etapa == RedeCrimpEtapa.Ordenar && sessao.NaMao.HasValue;
        if (curvaMao.gameObject.activeSelf != naMao) curvaMao.gameObject.SetActive(naMao);
        if (pontaMao.gameObject.activeSelf != naMao) pontaMao.gameObject.SetActive(naMao);
        if (!naMao) return;

        Color cor = RedeCores.Cor(sessao.NaMao.Value);
        curvaMao.color = cor;
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
            Mensagem("Coloque os 8 fios na fileira antes de encaixar o RJ45.", VermelhoErro);
            return;
        }

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
            // Errou a ordem: o cabo é descartado e volta a descascar (o estado já voltou para Descascar).
            EmbaralharFios();
            Mensagem("Ordem errada (" + sessao.FiosErradosNaUltimaCrimpagem +
                     " fio(s) fora de lugar)! O cabo foi descartado — descasque outro e monte de novo.", VermelhoErro);
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

        Transform olhos = mao.HoldPoint != null && mao.HoldPoint.parent != null ? mao.HoldPoint.parent : null;
        if (olhos != null) item.OnReleased(olhos.position);
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

        // O celular fica no lugar do botão: aberto, ele cobre o botão; fechado, só o botão aparece.
        tabelaPainel.gameObject.SetActive(mostrarTabela);
        botaoCelular.gameObject.SetActive(!mostrarTabela);

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
                if (tem) fiosImg[i].color = RedeCores.Cor(fio.Value);

                fiosPonta[i].gameObject.SetActive(tem);
                if (tem) ((RectTransform)fiosPonta[i].transform).anchoredPosition = new Vector2(0f, colocado ? PontaColocadoY : PontaSoltoY);

                // O soquete aparece só nas posições que ainda estão vazias.
                fiosSoquete[i].gameObject.SetActive(!tem);
            }
        }

        // O RJ45 vazio só "acende" quando os 8 fios já estão na fileira: é o ponto certo para fechar o cabo.
        if (sinalRjVazioImg != null)
            sinalRjVazioImg.gameObject.SetActive(descascado && sessao.TodasPreenchidas);
    }

    private void Mensagem(string texto, Color cor)
    {
        if (msgTxt == null) return;
        msgTxt.text = texto;
        msgTxt.color = cor;
    }
}
