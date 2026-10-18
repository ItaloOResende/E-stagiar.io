using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Tela 2D do testador de cabos de rede, no padrão das telas de manutenção do gabinete: o MODELO 3D do próprio
/// testador aparece num ângulo fixo (<see cref="ModeloNaTela"/>), com as duas entradas RJ45 clicáveis por cima.
/// Fluxo: com um cabo de rede na mão, clicar em cada entrada encaixa uma ponta (o plugue anima até a entrada e o
/// cabo é desenhado entre as unidades) → TESTAR (só com as duas pontas) → os pinos 1..8 são percorridos: os LEDs do
/// modelo (o da tela e o do mundo) acendem verde (continuidade) ou vermelho (falha), pelo estado
/// REAL do cabo (<see cref="CaboRedeEstado"/>) → resultado APROVADO ou FALHA. Cabo reprovado pode ser descartado.
/// Clicar numa ponta encaixada a solta. O cabo não sai da mão (o teste não liga nada na rede).
/// Sem o modelo (cenas antigas), as unidades são desenhadas por código.
/// </summary>
[RequireComponent(typeof(UIScreen))]
public class TestadorCaboScreen : MonoBehaviour
{
    private static readonly Color CorFundo = new Color(0.07f, 0.09f, 0.12f, 1f);
    private static readonly Color CorCartao = new Color(0.12f, 0.15f, 0.19f, 0.96f);
    private static readonly Color LedApagado = new Color(0.2f, 0.23f, 0.22f, 1f);
    private static readonly Color LedVerde = new Color(0.3f, 0.95f, 0.42f, 1f);
    private static readonly Color LedVermelho = new Color(0.98f, 0.3f, 0.26f, 1f);
    private static readonly Color LedAmbar = new Color(1f, 0.76f, 0.22f, 1f);
    private static readonly Color TextoClaro = new Color(0.92f, 0.94f, 0.97f);
    private static readonly Color TextoFraco = new Color(0.62f, 0.68f, 0.76f);
    private static readonly Color AzulBotao = new Color(0.2f, 0.5f, 0.92f, 1f);
    private static readonly Color CinzaBotao = new Color(0.25f, 0.28f, 0.33f, 1f);
    private static readonly Color VermelhoBotao = new Color(0.72f, 0.22f, 0.2f, 1f);
    private static readonly Color AzulCabo = new Color(0.22f, 0.5f, 0.9f, 1f);

    [Tooltip("Segundos que cada pino leva no teste.")]
    [Min(0.02f)] [SerializeField] private float segundosPorPino = 0.22f;
    [Tooltip("Plugue RJ45 (Art/UI/ui_render_plugue_rj45). Vazio = desenhado por código.")]
    [SerializeField] private Sprite plugueRj45;

    private UIScreen tela;
    private bool construido;
    private RectTransform palco, areaModelo;
    private ModeloNaTela modelo;
    private GameObject origemModelo;
    private TestadorCaboLeds ledsMundo, ledsCopia;
    private Transform entradaPrincipal, entradaRemota, botaoModelo;
    private RectTransform alvoPrincipal, alvoRemota;
    private RectTransform plugA, plugB, dock;
    private UICableCurve cabo;
    private readonly List<Vector2> pontos = new List<Vector2>();
    private Text status, resultado;
    private RectTransform botaoTestar, botaoDescartar, faixaResultado;
    private Image faixaFundo;
    private bool pontaA, pontaB, posicionar;
    private Vector2 posA, posB;
    private Coroutine teste;
    private Image[] ledsDesenhoA, ledsDesenhoB;   // sem modelo: unidades desenhadas por código

    private UIScreen Tela => tela != null ? tela : tela = GetComponent<UIScreen>();
    private ItemHolder Mao => Tela.Interactor != null ? Tela.Interactor.Holder : null;
    private CableItem CaboNaMao => TestadorCaboInteractable.CaboNaMao(Tela.Interactor);

    private void Awake() => Construir();

    private void OnEnable()
    {
        Construir();
        PrepararModelo();
        pontaA = pontaB = false;
        Apagar();
        MostrarResultado(null, Color.clear);
        posicionar = true;
        Atualizar();
    }

    private void OnDisable()
    {
        if (teste != null) StopCoroutine(teste);
        teste = null;   // os LEDs 3D ficam com o último resultado até o próximo teste
    }

    // ================================================================== construção

    private void Construir()
    {
        if (construido) return;
        construido = true;
        RedeUi.Esticar((RectTransform)transform);
        RectTransform painel = RedeUi.Cheio(transform, "Painel", CorFundo);
        RedeUi.BotaoFechar(painel, () => Tela.RequestClose());

        // centralizado no alto: fica entre o selo de pontos (alto à esquerda) e o relógio (alto à direita), sem encostar
        Text titulo = RedeUi.Texto(painel, "Titulo", "TESTADOR DE CABO DE REDE", 30, TextAnchor.MiddleCenter, TextoFraco);
        titulo.fontStyle = FontStyle.Bold;
        RedeUi.Ancorar((RectTransform)titulo.transform, new Vector2(0.5f, 1f), new Vector2(700f, 56f), new Vector2(0.5f, 1f));
        ((RectTransform)titulo.transform).anchoredPosition = new Vector2(0f, -28f);

        // palco: o modelo 3D (ou o desenho) com as entradas clicáveis, o cabo e os plugues por cima
        palco = RedeUi.Imagem(painel, "Palco", Color.clear);
        palco.GetComponent<Image>().raycastTarget = false;
        palco.anchorMin = new Vector2(0.08f, 0.2f);
        palco.anchorMax = new Vector2(0.92f, 0.9f);
        palco.offsetMin = palco.offsetMax = Vector2.zero;
        areaModelo = RedeUi.Cheio(palco, "Modelo", Color.clear);
        areaModelo.GetComponent<Image>().raycastTarget = false;

        var curva = new GameObject("Cabo", typeof(RectTransform), typeof(CanvasRenderer));
        curva.transform.SetParent(palco, false);
        RedeUi.Esticar((RectTransform)curva.transform);
        cabo = curva.AddComponent<UICableCurve>();
        cabo.color = AzulCabo;
        cabo.Thickness = 14f;
        cabo.raycastTarget = false;

        // "dock": de onde as pontas saem (o cabo na mão), embaixo do palco
        dock = RedeUi.Imagem(palco, "CaboNaMao", Color.clear);
        dock.GetComponent<Image>().raycastTarget = false;
        RedeUi.Ancorar(dock, new Vector2(0.5f, 0f), new Vector2(10f, 10f), new Vector2(0.5f, 0.5f));
        dock.anchoredPosition = new Vector2(0f, -40f);

        alvoPrincipal = Alvo(palco, "Entrada_Principal", () => ClicarEntrada(true));
        alvoRemota = Alvo(palco, "Entrada_Remota", () => ClicarEntrada(false));
        plugA = Plugue(palco, "PlugueA");
        plugB = Plugue(palco, "PlugueB");

        status = RedeUi.Texto(painel, "Status", "", 30, TextAnchor.MiddleCenter, TextoClaro);
        RedeUi.Ancorar((RectTransform)status.transform, new Vector2(0.5f, 0.15f), new Vector2(1400f, 44f), new Vector2(0.5f, 0.5f));

        faixaResultado = RedeUi.Imagem(painel, "Resultado", Color.clear);
        RedeUi.Ancorar(faixaResultado, new Vector2(0.5f, 0.15f), new Vector2(1100f, 64f), new Vector2(0.5f, 0.5f));
        faixaFundo = faixaResultado.GetComponent<Image>();
        faixaFundo.raycastTarget = false;
        resultado = RedeUi.Texto(faixaResultado, "Texto", "", 32, TextAnchor.MiddleCenter, Color.white);
        resultado.fontStyle = FontStyle.Bold;
        RedeUi.Esticar((RectTransform)resultado.transform);

        // TESTAR = o botão do próprio testador no modelo (área clicável em cima dele, como as entradas)
        botaoTestar = Alvo(palco, "Botao_Testar", Testar);
        botaoDescartar = RedeUi.Botao(painel, "BotaoDescartar", "DESCARTAR CABO", VermelhoBotao, Descartar, 26);
        RedeUi.Ancorar(botaoDescartar, new Vector2(0.5f, 0.07f), new Vector2(320f, 84f), new Vector2(0.5f, 0.5f));
    }

    // Área clicável sobre uma entrada: invisível, com anel amarelo ao passar o mouse (como os encaixes do gabinete).
    private RectTransform Alvo(RectTransform pai, string nome, UnityAction acao)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(pai, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(120f, 120f);
        Image img = MiniUi.ImagemCircular(rt, "Anel", Color.white, 120f);
        RedeUi.Esticar((RectTransform)img.transform);
        img.raycastTarget = true;   // RedeUi.Imagem cria sem raycast: sem isto o botão não recebia o clique (as pontas não encaixavam)
        var b = go.AddComponent<Button>();
        b.targetGraphic = img;
        ColorBlock c = b.colors;
        c.normalColor = new Color(1f, 0.88f, 0.2f, 0.12f);
        c.highlightedColor = new Color(1f, 0.88f, 0.2f, 0.5f);
        c.pressedColor = new Color(1f, 0.95f, 0.5f, 0.7f);
        c.selectedColor = c.normalColor;
        c.fadeDuration = 0.08f;
        b.colors = c;
        img.canvasRenderer.SetColor(c.normalColor);
        b.onClick.AddListener(acao);
        string legenda = nome.EndsWith("Principal") ? "PRINCIPAL" : nome.EndsWith("Remota") ? "REMOTA" : "TESTAR";
        Text rotulo = RedeUi.Texto(rt, "Rotulo", legenda, 20, TextAnchor.MiddleCenter, TextoFraco);
        rotulo.raycastTarget = false;
        rotulo.fontStyle = FontStyle.Bold;
        RedeUi.Ancorar((RectTransform)rotulo.transform, new Vector2(0.5f, 0f), new Vector2(200f, 28f), new Vector2(0.5f, 1f));
        ((RectTransform)rotulo.transform).anchoredPosition = new Vector2(0f, -6f);
        return rt;
    }

    private RectTransform Plugue(RectTransform pai, string nome)
    {
        RectTransform rt;
        if (plugueRj45 != null)
        {
            Image img = RedeUi.ImagemSprite(pai, nome, plugueRj45, Color.white);
            img.preserveAspect = true;
            rt = (RectTransform)img.transform;
            rt.localRotation = Quaternion.Euler(0f, 0f, 180f);   // cabo para baixo, plugue para cima (entrando na entrada)
        }
        else
        {
            rt = RedeUi.Imagem(pai, nome, new Color(0.2f, 0.45f, 0.85f));
            RectTransform ponta = RedeUi.Imagem(rt, "Ponta", new Color(0.85f, 0.9f, 0.95f, 0.85f));
            RedeUi.Ancorar(ponta, new Vector2(0.5f, 1f), new Vector2(50f, 26f), new Vector2(0.5f, 0f));
        }
        rt.GetComponent<Image>().raycastTarget = false;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(78f, 70f);
        return rt;
    }

    // ================================================================== modelo 3D

    private void PrepararModelo()
    {
        GameObject origem = Tela.Source;
        ledsMundo = origem != null ? origem.GetComponentInChildren<TestadorCaboLeds>(true) : null;
        Transform visual = origem != null ? origem.transform.Find("Visual") : null;
        if (visual == null || ledsMundo == null)
        {
            if (modelo != null) modelo.gameObject.SetActive(false);
            DesenharUnidades();
            return;
        }
        if (modelo == null) modelo = ModeloNaTela.Criar(areaModelo, "Modelo3D", new Vector2Int(1700, 900));
        modelo.gameObject.SetActive(true);
        if (origemModelo != origem)
        {
            Transform copia = modelo.Copiar(origem, typeof(TestadorCaboLeds));
            ledsCopia = copia != null ? copia.GetComponent<TestadorCaboLeds>() : null;
            Transform v = copia != null ? copia.Find("Visual") : null;
            entradaPrincipal = v != null ? v.Find("Principal/EntradaRJ45") : null;
            entradaRemota = v != null ? v.Find("Remota/EntradaRJ45") : null;
            botaoModelo = v != null ? v.Find("Principal/BotaoTeste") : null;
            // de frente e um pouco de cima: os LEDs em cima e as entradas na frente aparecem juntos
            modelo.Enquadrar(new Vector3(0f, -0.9f, -1f), Vector3.up, modelo.BoundsLocal(v), 0.14f);
            origemModelo = origem;
        }
        if (ledsDesenhoA != null) ledsDesenhoA[0].transform.parent.parent.gameObject.SetActive(false);
    }

    // Sem o modelo: duas unidades simples desenhadas por código (mesmas posições das entradas).
    private void DesenharUnidades()
    {
        if (ledsDesenhoA == null)
        {
            RectTransform grupo = RedeUi.Imagem(areaModelo, "Unidades", Color.clear);
            RedeUi.Esticar(grupo);
            ledsDesenhoA = Unidade(grupo, new Vector2(-300f, 30f), new Vector2(380f, 300f));
            ledsDesenhoB = Unidade(grupo, new Vector2(300f, 30f), new Vector2(300f, 240f));
        }
        ledsDesenhoA[0].transform.parent.parent.gameObject.SetActive(true);
        entradaPrincipal = entradaRemota = botaoModelo = null;
    }

    private Image[] Unidade(RectTransform pai, Vector2 pos, Vector2 tam)
    {
        RectTransform corpo = RedeUi.Imagem(pai, "Unidade", new Color(0.98f, 0.74f, 0.16f));
        RedeUi.Ancorar(corpo, new Vector2(0.5f, 0.5f), tam, new Vector2(0.5f, 0.5f));
        corpo.anchoredPosition = pos;
        RectTransform visor = RedeUi.Imagem(corpo, "Visor", new Color(0.1f, 0.13f, 0.12f));
        RedeUi.Ancorar(visor, new Vector2(0.5f, 0.65f), new Vector2(tam.x * 0.8f, 70f), new Vector2(0.5f, 0.5f));
        var leds = new Image[8];
        for (int i = 0; i < 8; i++)
        {
            Image l = MiniUi.ImagemCircular(visor, "Led" + (i + 1), LedApagado, 26f);
            ((RectTransform)l.transform).anchoredPosition = new Vector2((i - 3.5f) * tam.x * 0.095f, 0f);
            leds[i] = l;
        }
        RectTransform entrada = RedeUi.Imagem(corpo, "EntradaRJ45", new Color(0.06f, 0.07f, 0.08f));
        RedeUi.Ancorar(entrada, new Vector2(0.5f, 0.15f), new Vector2(80f, 60f), new Vector2(0.5f, 0.5f));
        return leds;
    }

    // Posições das entradas e dos plugues (depois do layout; a câmera do modelo já está enquadrada).
    private void LateUpdate()
    {
        // áreas clicáveis em cima das peças do modelo, do tamanho delas (todo quadro: o layout pode mudar)
        posA = Cobrir(alvoPrincipal, entradaPrincipal, new Vector2(-300f, -60f));
        posB = Cobrir(alvoRemota, entradaRemota, new Vector2(300f, -60f));
        Cobrir(botaoTestar, botaoModelo, new Vector2(-300f, 120f));
        // pronto para testar: o anel TESTAR pulsa de leve (é a orientação do próximo passo, sem frase na tela)
        float pulso = botaoTestar.gameObject.activeSelf ? 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 5f) : 1f;
        botaoTestar.localScale = new Vector3(pulso, pulso, 1f);

        // plugues deslizam até a entrada (encaixado) ou até o cabo na mão; o cabo é desenhado entre eles
        float k = 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime);
        bool temCabo = CaboNaMao != null;
        Vector2 doca = dock.anchoredPosition + new Vector2(0f, -((RectTransform)palco).rect.height * 0.5f);
        Mover(plugA, pontaA ? posA + new Vector2(0f, -36f) : doca + new Vector2(-70f, 0f), temCabo || pontaA, k);
        Mover(plugB, pontaB ? posB + new Vector2(0f, -36f) : doca + new Vector2(70f, 0f), temCabo || pontaB, k);
        DesenharCabo(temCabo || pontaA || pontaB);
    }

    private void OnRectTransformDimensionsChange() => posicionar = true;

    private Vector2 Cobrir(RectTransform alvo, Transform peca, Vector2 semModelo)
    {
        if (peca == null || modelo == null || !modelo.isActiveAndEnabled)
        {
            alvo.anchoredPosition = semModelo;
            alvo.sizeDelta = new Vector2(110f, 110f);
            return semModelo;
        }
        Rect r = modelo.Retangulo(peca, 18f);
        alvo.anchoredPosition = r.center;
        alvo.sizeDelta = new Vector2(Mathf.Max(90f, r.width), Mathf.Max(90f, r.height));
        return r.center;
    }

    private static void Mover(RectTransform plug, Vector2 alvo, bool visivel, float k)
    {
        if (plug.gameObject.activeSelf != visivel) { plug.gameObject.SetActive(visivel); plug.anchoredPosition = alvo; }
        plug.anchoredPosition = Vector2.Lerp(plug.anchoredPosition, alvo, k);
    }

    private void DesenharCabo(bool visivel)
    {
        cabo.enabled = visivel;
        if (!visivel) return;
        Vector2 a = plugA.anchoredPosition + new Vector2(0f, -30f), b = plugB.anchoredPosition + new Vector2(0f, -30f);
        float queda = 160f + Vector2.Distance(a, b) * 0.15f;
        Vector2 c1 = a + Vector2.down * queda, c2 = b + Vector2.down * queda;
        pontos.Clear();
        for (int i = 0; i <= 32; i++)
        {
            float t = i / 32f, u = 1f - t;
            pontos.Add(u * u * u * a + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * b);
        }
        cabo.SetPoints(pontos);
    }

    // ================================================================== ações

    private void ClicarEntrada(bool principal)
    {
        if (teste != null) return;
        bool ligada = principal ? pontaA : pontaB;
        if (!ligada && CaboNaMao == null)
        {
            Status("Segure um cabo de rede para testar.", LedAmbar);
            InteractionSounds.Raise(InteractionSound.PickupBlocked);
            return;
        }
        if (principal) pontaA = !pontaA; else pontaB = !pontaB;
        InteractionSounds.Raise(ligada ? InteractionSound.CableUnplugged : InteractionSound.CablePlugged);
        Apagar();
        MostrarResultado(null, Color.clear);
        Atualizar();
    }

    public void Testar()
    {
        if (teste != null || !pontaA || !pontaB) return;
        CableItem atual = CaboNaMao;
        if (atual == null) { pontaA = pontaB = false; Atualizar(); return; }
        teste = StartCoroutine(Executar(atual.gameObject));
    }

    private IEnumerator Executar(GameObject alvo)
    {
        Apagar();
        MostrarResultado(null, Color.clear);
        Status("Testando...", LedAmbar);
        Atualizar();
        CaboRedeEstado estado = alvo.GetComponentInParent<CaboRedeEstado>(true);
        var falhas = new List<int>();
        for (int i = 0; i < 8; i++)
        {
            bool falha = estado != null && estado.Defeituoso && Contem(estado, i);
            Pino(i, LedAmbar, false, LedApagado);
            yield return new WaitForSecondsRealtime(segundosPorPino * 0.5f);
            Pino(i, LedVerde, true, falha ? LedVermelho : LedVerde);
            if (falha) falhas.Add(i + 1);
            yield return new WaitForSecondsRealtime(segundosPorPino * 0.5f);
        }

        bool aprovado = falhas.Count == 0;
        if (aprovado) MostrarResultado("APROVADO  ·  8 de 8 pinos com continuidade", new Color(0.16f, 0.5f, 0.26f, 0.95f));
        else MostrarResultado("FALHA  ·  pino(s) " + string.Join(", ", falhas) + " sem continuidade", new Color(0.62f, 0.18f, 0.16f, 0.95f));
        Status("", TextoClaro);
        InteractionSounds.Raise(aprovado ? InteractionSound.CablePlugged : InteractionSound.PickupBlocked);
        TestadorCabo.RegistrarTeste(alvo, aprovado);
        teste = null;
        Atualizar(aprovado);
    }

    // Pino i: LED da unidade principal (a), da remota (b) e o círculo da fileira (cor final da remota).
    private void Pino(int i, Color a, bool fim, Color b)
    {
        foreach (TestadorCaboLeds l in new[] { ledsMundo, ledsCopia })
            if (l != null) { l.Acender(false, i, a); if (fim) l.Acender(true, i, b); }
        if (ledsDesenhoA != null) { ledsDesenhoA[i].color = a; if (fim) ledsDesenhoB[i].color = b; }
    }

    private static bool Contem(CaboRedeEstado e, int pino)
    {
        foreach (int p in e.PinosComFalha) if (p == pino) return true;
        return false;
    }

    /// <summary>Joga fora o cabo reprovado que está na mão (ele sai do jogo).</summary>
    public void Descartar()
    {
        CableItem atual = CaboNaMao;
        if (atual == null || TestadorCabo.Aprovado(atual.gameObject)) return;
        ItemHolder mao = Mao;
        PickupInteractable item = mao != null ? mao.Stow(transform.position) : null;
        if (item == null) return;
        Destroy(item.gameObject);
        InteractionSounds.Raise(InteractionSound.CableUnplugged);
        pontaA = pontaB = false;
        Apagar();
        MostrarResultado(null, Color.clear);
        Status("Cabo descartado.", TextoFraco);
        Atualizar();
    }

    // ================================================================== desenho

    private void Atualizar(bool? aprovado = null)
    {
        bool temCabo = CaboNaMao != null;
        bool testando = teste != null;
        bool pronto = pontaA && pontaB && temCabo && !testando;
        botaoTestar.gameObject.SetActive(pronto);   // o botão do testador só responde com as duas pontas encaixadas
        botaoDescartar.gameObject.SetActive(aprovado == false && temCabo);
        if (testando || faixaResultado.gameObject.activeSelf) return;
        if (!temCabo && !pontaA && !pontaB) Status("Segure um cabo de rede e encaixe as pontas nas entradas.", TextoFraco);
        else if (!pontaA || !pontaB) Status("Encaixe " + (pontaA || pontaB ? "a outra ponta" : "as duas pontas") + " do cabo nas entradas.", TextoClaro);
        else Status("", TextoClaro);   // pontas encaixadas: o anel TESTAR pulsando no botão do testador já orienta
    }

    private void Apagar()
    {
        if (ledsMundo != null) ledsMundo.Apagar();
        if (ledsCopia != null) ledsCopia.Apagar();
        if (ledsDesenhoA != null) for (int i = 0; i < 8; i++) { ledsDesenhoA[i].color = LedApagado; ledsDesenhoB[i].color = LedApagado; }
    }

    private void Status(string texto, Color cor)
    {
        status.text = texto;
        status.color = cor;
    }

    private void MostrarResultado(string texto, Color fundo)
    {
        bool tem = !string.IsNullOrEmpty(texto);
        faixaResultado.gameObject.SetActive(tem);
        status.gameObject.SetActive(!tem);
        if (!tem) return;
        resultado.text = texto;
        faixaFundo.color = fundo;
    }
}
