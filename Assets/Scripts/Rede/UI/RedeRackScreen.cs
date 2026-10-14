using EstagiarIo.Rede;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Tela 2D do rack de rede (patch panel + switch) feita IGUAL à conexão de rede da tela do PC:
/// as portas são tomadas (mesma arte) e o cabo é uma curva com PLUGUE na ponta, seguindo o mouse.
/// <para>
/// O cabo é SÓ o que o jogador traz na mão — não existe cabo infinito. Com um cabo de rede crimpado na mão,
/// ele aparece na tela seguindo o mouse; clicar numa tomada encaixa a primeira ponta (o cabo SAI da mão);
/// clicar na tomada de MESMO número do outro lado liga o par e o cabo é GASTO; clicar de novo na mesma
/// tomada tira o cabo e ele VOLTA pra mão do jogador. Sem cabo na mão, não dá pra encaixar em nada.
/// </para>
/// Monta a própria interface por código (as artes vêm do setup) e usa o estado de <see cref="RedeGame"/>.
/// </summary>
[RequireComponent(typeof(UIScreen))]
public class RedeRackScreen : MonoBehaviour
{
    private static readonly Color CorPainel = new Color(0.09f, 0.11f, 0.14f, 1f);
    private static readonly Color CorModulo = new Color(0.16f, 0.18f, 0.23f, 1f);
    private static readonly Color CorModuloTopo = new Color(0.21f, 0.24f, 0.30f, 1f);
    private static readonly Color CorTrilho = new Color(0.10f, 0.12f, 0.16f, 1f);
    private static readonly Color CorTomada = new Color(0.13f, 0.15f, 0.19f, 1f);
    private static readonly Color CorTomadaBorda = new Color(0.30f, 0.34f, 0.41f, 1f);
    private static readonly Color CorContato = new Color(0.83f, 0.72f, 0.36f);
    private static readonly Color AzulCabo = new Color(0.22f, 0.50f, 0.90f, 1f);
    private static readonly Color TextoClaro = new Color(0.92f, 0.94f, 0.97f);
    private static readonly Color TextoFraco = new Color(0.66f, 0.71f, 0.78f);
    private static readonly Color VerdeOk = new Color(0.22f, 0.60f, 0.36f);
    private static readonly Color VermelhoErro = new Color(0.85f, 0.30f, 0.28f);
    private static readonly Color AmareloOk = new Color(0.97f, 0.84f, 0.30f);

    [Header("Artes (as mesmas da tela do PC) — o setup preenche")]
    [Tooltip("Tomada/ponto de rede: Assets/Art/UI/ui_render_ponto_rede.png")]
    [SerializeField] private Sprite spriteTomada;
    [Tooltip("Plugue RJ45: Assets/Art/UI/ui_render_plugue_rj45.png")]
    [SerializeField] private Sprite spritePlug;

    // Geometria (pixels da referência 1920x1080; coordenadas LOCAIS do canvas, centro = 0).
    private const float EspacamentoPorta = 220f;
    private const float TomadaLado = 92f;         // tamanho da arte da tomada
    private const float TomadaClique = 132f;      // área clicável em volta dela
    private const float BarraLargura = 1500f;
    private const float BarraAltura = 250f;
    private const float PatchY = 175f;            // centro do patch panel (em cima)
    private const float SwitchY = -215f;          // centro do switch (embaixo)
    private const float BarrigaCabo = 78f;        // quanto o cabo ligado "sai" para o lado
    private const float EspessuraCabo = 10f;
    private const float BordaDeBaixo = -640f;     // por onde o cabo entra na tela (abaixo do canvas)

    private bool construido;
    private int numPortas;
    private RedeRackSession sessao;

    private RectTransform bancada, cabos;
    private RectTransform[] tomadaPatch, tomadaSwitch;
    private Image[] plugPatch, plugSwitch;
    private UICableCurve[] curvaLigada;
    private UICableCurve curvaLivre;
    private Image plugLivre, brilhoAlvo;
    private Text statusTxt, instrTxt, msgTxt;

    // O cabo: só existe o cabo que o jogador trouxe na mão — não tem cabo infinito.
    // temCabo = o jogador tem um cabo de rede na mão (ou já encaixado numa tomada).
    // pontaPorta = 0 (cabo na mão, sem tomada ainda) ou o número da tomada onde a primeira ponta está encaixada.
    // pontaItem = o cabo físico que SAIU da mão pra ficar encaixado (null enquanto ele está na mão).
    private bool temCabo;
    private bool pontaEhPatch;
    private int pontaPorta;
    private PickupInteractable pontaItem;

    private readonly List<Vector2> pontos = new List<Vector2>();
    private Vector2[] posPatch, posSwitch;
    private Canvas canvasTela;

    private void Awake() => Construir();

    private void OnEnable()
    {
        sessao = new RedeRackSession(RedeGame.Garantir().PortasDoRack);
        foreach (int p in RedeGame.Garantir().ParesLigados) sessao.ParesLigados.Add(p);

        // O cabo é SÓ o que o jogador traz na mão. Sem cabo crimpado na mão, não dá pra encaixar em nada.
        pontaItem = null;
        pontaPorta = 0;
        pontaEhPatch = false;
        temCabo = CaboNaMao() != null;

        Mensagem(temCabo ? "" : "Você precisa de um cabo de rede crimpado na mão pra ligar as portas.", temCabo ? TextoClaro : VermelhoErro);
        Atualizar();
    }

    /// <summary>Fechou a tela? O cabo tirado da mão e ainda não usado volta pra mão (nunca some).</summary>
    private void OnDisable() => DevolverCabo(false);

    // ================================================================== construção

    private void Construir()
    {
        if (construido) return;
        construido = true;

        numPortas = Mathf.Max(1, RedeGame.Garantir().PortasDoRack);

        RedeUi.Esticar((RectTransform)transform);

        // UMA região só, de ponta a ponta do canvas.
        bancada = RedeUi.Cheio(transform, "Painel", CorPainel);

        // Mesmo botão de fechar da tela do PC (quadrado escuro com "X").
        RedeUi.BotaoFechar(bancada, Fechar);

        tomadaPatch = new RectTransform[numPortas];
        tomadaSwitch = new RectTransform[numPortas];

        Modulo("PatchPanel", "PATCH PANEL", PatchY, true);
        Modulo("Switch", "SWITCH", SwitchY, false);

        // Os cabos ficam ACIMA dos equipamentos (é um cabo por cima do rack, como na traseira do PC).
        cabos = RedeUi.Cheio(bancada, "Cabos", Color.clear);

        // As CURVAS ficam atrás dos plugues; os plugues ficam atrás só do plugue que segue o mouse.
        curvaLigada = new UICableCurve[numPortas];
        for (int i = 0; i < numPortas; i++) curvaLigada[i] = NovaCurva("Cabo" + (i + 1));
        curvaLivre = NovaCurva("CaboNaMao");

        // Destaque amarelo que pulsa na tomada de mesmo número (sem texto, como na crimpagem).
        brilhoAlvo = RedeUi.Imagem(cabos, "Alvo", new Color(AmareloOk.r, AmareloOk.g, AmareloOk.b, 0.3f)).GetComponent<Image>();
        RedeUi.Ancorar(brilhoAlvo.rectTransform, new Vector2(0.5f, 0.5f),
            new Vector2(TomadaClique + 28f, TomadaClique + 28f), new Vector2(0.5f, 0.5f));

        // Plugues das tomadas (depois das curvas para ficarem na frente, como no PC).
        plugPatch = new Image[numPortas];
        plugSwitch = new Image[numPortas];
        for (int i = 0; i < numPortas; i++)
        {
            plugPatch[i] = Plugue(cabos, "PlugP" + (i + 1));
            plugSwitch[i] = Plugue(cabos, "PlugS" + (i + 1));
        }

        // O plugue da ponta que segue o mouse fica na frente de tudo.
        plugLivre = Plugue(cabos, "PlugNaMao");

        posPatch = new Vector2[numPortas];
        posSwitch = new Vector2[numPortas];

        statusTxt = RedeUi.Texto(bancada, "Status", "", 24, TextAnchor.UpperLeft, TextoFraco);
        RedeUi.Ancorar((RectTransform)statusTxt.transform, new Vector2(0f, 1f), new Vector2(1400f, 40f), new Vector2(0f, 1f));
        ((RectTransform)statusTxt.transform).anchoredPosition = new Vector2(36f, -34f);

        instrTxt = RedeUi.Texto(bancada, "Instrucao", "", 24, TextAnchor.MiddleCenter, TextoClaro);
        RedeUi.Ancorar((RectTransform)instrTxt.transform, new Vector2(0.5f, 0f), new Vector2(1500f, 40f), new Vector2(0.5f, 0f));
        ((RectTransform)instrTxt.transform).anchoredPosition = new Vector2(0f, 132f);

        msgTxt = RedeUi.Texto(bancada, "Mensagem", "", 24, TextAnchor.MiddleCenter, TextoClaro);
        RedeUi.Ancorar((RectTransform)msgTxt.transform, new Vector2(0.5f, 0f), new Vector2(1500f, 60f), new Vector2(0.5f, 0f));
        ((RectTransform)msgTxt.transform).anchoredPosition = new Vector2(0f, 70f);
    }

    /// <summary>Um equipamento do rack (patch panel ou switch) com a fileira de tomadas numeradas.</summary>
    private void Modulo(string nome, string rotulo, float y, bool ehPatch)
    {
        RectTransform modulo = RedeUi.Imagem(bancada, nome, CorModulo);
        RedeUi.Ancorar(modulo, new Vector2(0.5f, 0.5f), new Vector2(BarraLargura, BarraAltura), new Vector2(0.5f, 0.5f));
        modulo.anchoredPosition = new Vector2(0f, y);

        Caixa(modulo, "Topo", CorModuloTopo, new Vector2(0f, BarraAltura * 0.5f - 10f), new Vector2(BarraLargura, 20f));
        Caixa(modulo, "Trilho", CorTrilho, new Vector2(0f, -BarraAltura * 0.5f + 12f), new Vector2(BarraLargura, 24f));

        Text titulo = RedeUi.Texto(modulo, "Rotulo", rotulo, 24, TextAnchor.MiddleLeft, TextoFraco);
        RedeUi.Ancorar((RectTransform)titulo.transform, new Vector2(0f, 1f), new Vector2(600f, 32f), new Vector2(0f, 1f));
        ((RectTransform)titulo.transform).anchoredPosition = new Vector2(28f, -12f);

        for (int i = 0; i < numPortas; i++)
        {
            int porta = i + 1;
            bool patch = ehPatch;
            float x = (i - (numPortas - 1) * 0.5f) * EspacamentoPorta;

            RectTransform tomada = BotaoTomada(modulo, (ehPatch ? "P" : "S") + porta, new Vector2(x, 0f),
                () => ClicarPorta(patch, porta));
            if (ehPatch) tomadaPatch[i] = tomada; else tomadaSwitch[i] = tomada;

            Text numero = RedeUi.Texto(modulo, "Numero" + porta, (ehPatch ? "P" : "S") + porta, 22, TextAnchor.MiddleCenter, TextoClaro);
            RedeUi.Ancorar((RectTransform)numero.transform, new Vector2(0.5f, 0.5f), new Vector2(EspacamentoPorta - 20f, 28f), new Vector2(0.5f, 0.5f));
            ((RectTransform)numero.transform).anchoredPosition = new Vector2(x, -80f);
        }
    }

    /// <summary>Tomada clicável: a MESMA arte do ponto de rede do PC (com um desenho simples de reserva).</summary>
    private RectTransform BotaoTomada(Transform pai, string nome, Vector2 pos, UnityAction acao)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(pai, false);

        var img = go.AddComponent<Image>();
        img.raycastTarget = true;
        if (spriteTomada != null)
        {
            img.sprite = spriteTomada;
            img.preserveAspect = true;
            img.color = Color.white;
        }
        else img.color = CorTomada;

        var b = go.AddComponent<Button>();
        b.targetGraphic = img;
        ButtonColors(b, Color.white);
        if (acao != null) b.onClick.AddListener(acao);

        if (spriteTomada == null)
        {
            Caixa(go.transform, "Borda", CorTomadaBorda, Vector2.zero, new Vector2(TomadaLado, TomadaLado));
            Caixa(go.transform, "Miolo", new Color(0.08f, 0.09f, 0.12f), Vector2.zero, new Vector2(TomadaLado - 16f, TomadaLado - 16f));
            for (int k = 0; k < 4; k++)
                Caixa(go.transform, "Contato" + k, CorContato, new Vector2((k - 1.5f) * 14f, 2f), new Vector2(7f, TomadaLado - 36f));
        }

        RedeUi.Ancorar((RectTransform)go.transform, new Vector2(0.5f, 0.5f), new Vector2(TomadaClique, TomadaClique), new Vector2(0.5f, 0.5f));
        ((RectTransform)go.transform).anchoredPosition = pos;
        return (RectTransform)go.transform;
    }

    /// <summary>Plugue (a mesma arte do PC) com um desenho simples de reserva. Já é criado desligado.</summary>
    private Image Plugue(RectTransform pai, string nome)
    {
        Image img = spritePlug != null
            ? RedeUi.ImagemSprite(pai, nome, spritePlug, Color.white)
            : RedeUi.Imagem(pai, nome, new Color(0.80f, 0.86f, 0.93f)).GetComponent<Image>();

        if (spritePlug == null)
        {
            Caixa(img.transform, "Corpo", new Color(0.80f, 0.86f, 0.93f), Vector2.zero, new Vector2(TomadaLado * 1.25f, TomadaLado * 0.8f));
            Caixa(img.transform, "Lingua", new Color(0.60f, 0.66f, 0.74f), new Vector2(0f, TomadaLado * 0.5f), new Vector2(TomadaLado * 0.55f, TomadaLado * 0.22f));
        }

        RedeUi.Ancorar(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(TomadaLado * 1.3f, TomadaLado * 1.5f), new Vector2(0.5f, 0.5f));
        img.enabled = false;
        return img;
    }

    private UICableCurve NovaCurva(string nome)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(cabos, false);
        RedeUi.Esticar((RectTransform)go.transform);
        var curva = go.AddComponent<UICableCurve>();
        curva.Thickness = EspessuraCabo;
        curva.color = AzulCabo;
        curva.raycastTarget = false;
        go.SetActive(false);
        return curva;
    }

    private static void ButtonColors(Button b, Color normal)
    {
        ColorBlock cores = b.colors;
        cores.normalColor = normal;
        cores.highlightedColor = new Color(1f, 0.95f, 0.58f, 1f);
        cores.pressedColor = new Color(0.85f, 0.80f, 0.50f, 1f);
        cores.selectedColor = normal;
        cores.fadeDuration = 0.06f;
        b.colors = cores;
    }

    private static RectTransform Caixa(Transform pai, string nome, Color cor, Vector2 pos, Vector2 tamanho)
    {
        RectTransform rt = RedeUi.Imagem(pai, nome, cor);
        RedeUi.Ancorar(rt, new Vector2(0.5f, 0.5f), tamanho, new Vector2(0.5f, 0.5f));
        rt.anchoredPosition = pos;
        return rt;
    }

    // ================================================================== interação

    /// <summary>
    /// Clique numa tomada, com o cabo na mão: encaixa a ponta ali. Já com uma ponta encaixada: a tomada de
    /// mesmo número do outro lado liga o par; a mesma tomada solta o cabo; outra tomada do mesmo lado muda a
    /// ponta de lugar; tomada de número diferente do outro lado avisa que os números precisam bater.
    /// </summary>
    private void ClicarPorta(bool ehPatch, int porta)
    {
        if (sessao == null) return;
        if (porta < 1 || porta > numPortas) return;

        if (sessao.PortaLigada(porta))
        {
            Mensagem("A porta " + Nome(ehPatch, porta) + " já está ligada.", TextoFraco);
            return;
        }

        // Sem cabo na mão não dá pra encaixar em nada.
        if (!temCabo)
        {
            Mensagem("Você precisa de um cabo de rede crimpado na mão pra encaixar numa tomada.", VermelhoErro);
            return;
        }

        // Cabo na mão, sem nenhuma ponta encaixada ainda: encaixa a primeira ponta aqui (o cabo SAI da mão).
        if (pontaPorta == 0)
        {
            ItemHolder mao = Mao();
            pontaItem = mao != null ? mao.Stow(transform.position) : null;
            pontaEhPatch = ehPatch;
            pontaPorta = porta;
            Escolher(ehPatch, porta);
            Mensagem("Ponta encaixada em " + Nome(ehPatch, porta) + " — clique na tomada de mesmo número do outro lado.", TextoFraco);
            Atualizar();
            return;
        }

        // Clicou de novo na tomada onde a ponta está: solta o cabo (ele volta pra mão).
        if (porta == pontaPorta && ehPatch == pontaEhPatch)
        {
            SoltarPonta();
            return;
        }

        // Tomada de MESMO número do outro lado: encaixa a outra ponta e liga o par.
        if (porta == pontaPorta)
        {
            FecharLigacao(ehPatch, porta);
            return;
        }

        // Mesmo lado, outra tomada: a ponta encaixada só muda de lugar.
        if (ehPatch == pontaEhPatch)
        {
            sessao.LimparEscolhas();
            Escolher(ehPatch, porta);
            pontaPorta = porta;
            Mensagem("Ponta encaixada agora em " + Nome(ehPatch, porta) + ".", TextoFraco);
            Atualizar();
            return;
        }

        Mensagem("Os números precisam ser iguais: a ponta está em " + Nome(pontaEhPatch, pontaPorta) +
                 " e você clicou em " + Nome(ehPatch, porta) + ".", VermelhoErro);
    }

    /// <summary>Encaixa a outra ponta: se os números batem, a porta fica ligada e o cabo é GASTO (acabou).</summary>
    private void FecharLigacao(bool ehPatch, int porta)
    {
        Escolher(ehPatch, porta);
        bool ok = sessao.Confirmar();

        if (ok)
        {
            RedeGame.Garantir().RegistrarParLigado(porta);
            pontaItem = null;        // o cabo ficou no rack (gasto)
            pontaPorta = 0;
            pontaEhPatch = false;
            temCabo = false;         // e a mão voltou a ficar vazia: precisa de outro cabo
            Mensagem("Porta " + porta + " ligada! Pegue outro cabo crimpado na mão pra ligar a próxima.", VerdeOk);
            if (sessao.Completo) Mensagem("Rack completo! Todas as portas estão ligadas.", VerdeOk);
        }
        else Mensagem("Não deu para ligar a porta " + porta + ".", VermelhoErro);

        Atualizar();
    }

    /// <summary>Tira a ponta da tomada: o cabo VOLTA pra mão do jogador (ele termina com o cabo na mão).</summary>
    private void SoltarPonta()
    {
        if (pontaItem != null)
        {
            ItemHolder mao = Mao();
            if (mao != null) mao.Retrieve(pontaItem.gameObject);
            pontaItem = null;
        }

        pontaPorta = 0;
        pontaEhPatch = false;
        temCabo = true;
        if (sessao != null) sessao.LimparEscolhas();
        Mensagem("Cabo de volta na sua mão — clique noutra tomada pra encaixar.", TextoFraco);
        Atualizar();
    }

    /// <summary>Ao fechar a tela, um cabo encaixado volta pra mão do jogador (nunca some).</summary>
    private void DevolverCabo(bool atualizar = true)
    {
        if (pontaItem != null)
        {
            ItemHolder mao = Mao();
            if (mao != null) mao.Retrieve(pontaItem.gameObject);
            pontaItem = null;
        }

        pontaPorta = 0;
        pontaEhPatch = false;
        if (sessao != null) sessao.LimparEscolhas();
        if (atualizar) Atualizar();
    }

    private void Escolher(bool ehPatch, int porta)
    {
        if (ehPatch) sessao.EscolherPatch(porta);
        else sessao.EscolherSwitch(porta);
    }

    private static string Nome(bool ehPatch, int porta) => (ehPatch ? "P" : "S") + porta;

    /// <summary>A mão do jogador que abriu a tela (null se a tela não estiver ligada a um jogador).</summary>
    private ItemHolder Mao()
    {
        UIScreen tela = GetComponent<UIScreen>();
        return tela != null && tela.Interactor != null ? tela.Interactor.Holder : null;
    }

    /// <summary>O cabo de REDE que o jogador está segurando agora (null se a mão está vazia ou tem outra coisa).</summary>
    private PickupInteractable CaboNaMao()
    {
        ItemHolder mao = Mao();
        PickupInteractable item = mao != null ? mao.Current : null;
        if (item == null) return null;

        CableItem cabo = item.GetComponentInParent<CableItem>(true);
        return cabo != null && cabo.Type == CableType.Network ? item : null;
    }

    private void Fechar()
    {
        DevolverCabo();
        UIScreen tela = GetComponent<UIScreen>();
        if (tela != null) tela.RequestClose();
    }

    // ================================================================== desenho

    private void LateUpdate()
    {
        if (sessao == null || cabos == null) return;

        for (int i = 0; i < numPortas; i++)
        {
            posPatch[i] = Local(tomadaPatch[i]);
            posSwitch[i] = Local(tomadaSwitch[i]);
            if (plugPatch[i].enabled) plugPatch[i].rectTransform.anchoredPosition = posPatch[i];
            if (plugSwitch[i].enabled) plugSwitch[i].rectTransform.anchoredPosition = posSwitch[i];
        }

        bool presaNumaTomada = pontaPorta >= 1 && pontaPorta <= numPortas;

        if (presaNumaTomada)
        {
            float pulso = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
            brilhoAlvo.color = new Color(AmareloOk.r, AmareloOk.g, AmareloOk.b, Mathf.Lerp(0.10f, 0.45f, pulso));
            brilhoAlvo.rectTransform.anchoredPosition = pontaEhPatch ? posSwitch[pontaPorta - 1] : posPatch[pontaPorta - 1];
        }

        for (int i = 0; i < numPortas; i++)
            if (curvaLigada[i].gameObject.activeSelf)
                Desenhar(curvaLigada[i], posPatch[i], posSwitch[i], true);

        if (!temCabo)
        {
            if (curvaLivre.gameObject.activeSelf) curvaLivre.gameObject.SetActive(false);
            return;
        }

        // A outra ponta do cabo (com plugue) segue o mouse. Sem tomada ainda, o cabo sobe da borda de baixo.
        Vector2 livre = new Vector2(0f, BordaDeBaixo + 320f);
        if (TentarPonteiro(out Vector2 mouse)) livre = mouse;

        Vector2 presa = presaNumaTomada
            ? (pontaEhPatch ? posPatch[pontaPorta - 1] : posSwitch[pontaPorta - 1])
            : new Vector2(livre.x, BordaDeBaixo);

        if (!curvaLivre.gameObject.activeSelf) curvaLivre.gameObject.SetActive(true);
        plugLivre.rectTransform.anchoredPosition = livre;
        Desenhar(curvaLivre, presa, livre, false);
    }

    /// <summary>
    /// Pontos do cabo. Ligado: sai da tomada de cima e faz a "barriga" para o lado, como um patch cord.
    /// Solto (seguindo o mouse): desce/sobe em curva até a ponta livre, como o cabo pendurado do PC.
    /// </summary>
    private void Desenhar(UICableCurve curva, Vector2 a, Vector2 b, bool ligado)
    {
        Vector2 c1, c2;
        if (ligado)
        {
            c1 = a + Vector2.right * BarrigaCabo;
            c2 = b + Vector2.right * BarrigaCabo;
        }
        else
        {
            float queda = Mathf.Max(80f, Vector2.Distance(a, b) * 0.32f);
            c1 = a + Vector2.down * queda;
            c2 = b + Vector2.down * queda * 0.6f;
        }

        pontos.Clear();
        const int n = 26;
        for (int k = 0; k <= n; k++)
        {
            float t = k / (float)n, u = 1f - t;
            pontos.Add(u * u * u * a + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * b);
        }
        curva.SetPoints(pontos);
    }

    /// <summary>Posição do centro de um elemento em coordenadas locais do container dos cabos.</summary>
    private Vector2 Local(RectTransform t) =>
        ((RectTransform)cabos).InverseTransformPoint(t.TransformPoint(t.rect.center));

    private bool TentarPonteiro(out Vector2 local)
    {
        local = Vector2.zero;
        if (Mouse.current == null) return false;
        if (canvasTela == null) canvasTela = GetComponentInParent<Canvas>();
        Camera cam = canvasTela != null && canvasTela.renderMode != RenderMode.ScreenSpaceOverlay ? canvasTela.worldCamera : null;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)cabos, Mouse.current.position.ReadValue(), cam, out local);
    }

    // ================================================================== atualização

    private void Atualizar()
    {
        if (sessao == null || bancada == null) return;

        for (int i = 0; i < numPortas; i++)
        {
            int porta = i + 1;
            bool ligada = sessao.PortaLigada(porta);
            bool presaAqui = pontaPorta == porta;

            bool plugP = ligada || (presaAqui && pontaEhPatch);
            bool plugS = ligada || (presaAqui && !pontaEhPatch);
            if (plugPatch[i].enabled != plugP) plugPatch[i].enabled = plugP;
            if (plugSwitch[i].enabled != plugS) plugSwitch[i].enabled = plugS;

            if (curvaLigada[i].gameObject.activeSelf != ligada) curvaLigada[i].gameObject.SetActive(ligada);
        }

        plugLivre.enabled = temCabo;
        brilhoAlvo.enabled = temCabo && pontaPorta >= 1;
        if (!temCabo && curvaLivre.gameObject.activeSelf) curvaLivre.gameObject.SetActive(false);

        RedeGame jogo = RedeGame.Garantir();
        statusTxt.text = "Cabos crimpados: " + jogo.CabosDisponiveis + "     Portas ligadas: " + sessao.TotalLigado + " / " + numPortas;

        if (sessao.Completo) instrTxt.text = "Rack completo. Bom trabalho!";
        else if (!temCabo) instrTxt.text = "Pegue um cabo de rede crimpado na mão (na caixa de cabo de rede) pra ligar as portas.";
        else if (pontaPorta >= 1) instrTxt.text = "Clique na tomada de mesmo número do outro lado — ou clique de novo na mesma tomada pra tirar o cabo.";
        else instrTxt.text = "O cabo está na sua mão: clique numa tomada do patch panel ou do switch pra encaixar a primeira ponta.";
    }

    private void Mensagem(string texto, Color cor)
    {
        if (msgTxt == null) return;
        msgTxt.text = texto;
        msgTxt.color = cor;
    }
}
