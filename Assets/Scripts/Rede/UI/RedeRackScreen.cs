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
/// clicar na tomada de MESMO número do outro lado liga o par; clicar de novo na mesma tomada (ou em qualquer
/// tomada já ligada) tira o cabo, que VOLTA pra mão do jogador — dá pra colocar e tirar quantas vezes quiser.
/// Sem cabo na mão, não dá pra encaixar em nada.
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
    private static readonly Color VermelhoErro = new Color(0.85f, 0.30f, 0.28f);
    private static readonly Color AmareloOk = new Color(0.97f, 0.84f, 0.30f);

    [Header("Artes (as mesmas da tela do PC) — o setup preenche")]
    [Tooltip("Tomada/ponto de rede: Assets/Art/UI/ui_render_ponto_rede.png")]
    [SerializeField] private Sprite spriteTomada;
    [Tooltip("Plugue RJ45: Assets/Art/UI/ui_render_plugue_rj45.png")]
    [SerializeField] private Sprite spritePlug;
    [Tooltip("Fundo da tela, o mesmo da crimpagem: Assets/Art/Textures/Sprites/bg1.png")]
    [SerializeField] private Sprite spriteFundo;
    [Tooltip("O quanto o fundo (bg1) é escurecido para os textos claros ficarem legíveis. 0 = arte pura, 1 = quase preto.")]
    [Range(0f, 1f)] [SerializeField] private float veuDoFundo = 0.62f;

    [Header("Grossuras (pixels da referência)")]
    [Tooltip("Espessura do cabo de rede desenhado no rack. O 'macho' (plugue) fica com ~3x esta grossura, como um " +
             "RJ45 de verdade em relação ao cabo.")]
    [SerializeField] private float espessuraCabo = 26f;
    [Tooltip("Lado do retângulo do plugue RJ45 (a arte mantém a proporção dentro dele). Ficava com 1,3x a tomada " +
             "e parecia um plugue gigante; ~0,9x a tomada fica do tamanho de um conector.")]
    [SerializeField] private float ladoMacho = 82f;

    // Geometria (pixels da referência 1920x1080; coordenadas LOCAIS do canvas, centro = 0).
    private const float EspacamentoPorta = 220f;
    private const float TomadaLado = 92f;         // tamanho da arte da tomada
    private const float TomadaClique = 132f;      // área clicável em volta dela
    private const float BarraLargura = 1500f;
    private const float BarraAltura = 250f;
    private const float PatchY = 175f;            // centro do patch panel (em cima)
    private const float SwitchY = -215f;          // centro do switch (embaixo)
    private const float BarrigaCabo = 78f;        // quanto o cabo ligado "sai" para o lado
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

    // Sem texto nenhum na tela: o retorno é só visual. A tomada clicada pisca VERMELHA quando o clique não
    // vale (sem cabo na mão, par de números diferente, tomada já ligada) e o cabo ligado aparece entre as duas.
    private Image[] tomadaImgPatch, tomadaImgSwitch;
    private Color[] corBasePatch, corBaseSwitch;
    private float[] flashPatch, flashSwitch;

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

        // UMA região só, de ponta a ponta do canvas. Com a arte do protótipo (bg1) ela é o fundo da tela e leva
        // por cima o mesmo véu escuro da crimpagem, para os textos e as tomadas continuarem legíveis.
        bancada = RedeUi.Cheio(transform, "Painel", CorPainel, spriteFundo);
        if (spriteFundo != null)
            RedeUi.Cheio(bancada, "Veu", new Color(0.035f, 0.05f, 0.075f, Mathf.Clamp01(veuDoFundo)));

        // Mesmo botão de fechar da tela do PC (quadrado escuro com "X").
        RedeUi.BotaoFechar(bancada, Fechar);

        tomadaPatch = new RectTransform[numPortas];
        tomadaSwitch = new RectTransform[numPortas];
        tomadaImgPatch = new Image[numPortas];
        tomadaImgSwitch = new Image[numPortas];
        corBasePatch = new Color[numPortas];
        corBaseSwitch = new Color[numPortas];
        flashPatch = new float[numPortas];
        flashSwitch = new float[numPortas];

        Modulo("PatchPanel", PatchY, true);
        Modulo("Switch", SwitchY, false);

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
    }

    /// <summary>Um equipamento do rack (patch panel ou switch) com a fileira de tomadas numeradas.</summary>
    private void Modulo(string nome, float y, bool ehPatch)
    {
        RectTransform modulo = RedeUi.Imagem(bancada, nome, CorModulo);
        RedeUi.Ancorar(modulo, new Vector2(0.5f, 0.5f), new Vector2(BarraLargura, BarraAltura), new Vector2(0.5f, 0.5f));
        modulo.anchoredPosition = new Vector2(0f, y);

        Caixa(modulo, "Topo", CorModuloTopo, new Vector2(0f, BarraAltura * 0.5f - 10f), new Vector2(BarraLargura, 20f));
        Caixa(modulo, "Trilho", CorTrilho, new Vector2(0f, -BarraAltura * 0.5f + 12f), new Vector2(BarraLargura, 24f));

        for (int i = 0; i < numPortas; i++)
        {
            int porta = i + 1;
            bool patch = ehPatch;
            float x = (i - (numPortas - 1) * 0.5f) * EspacamentoPorta;

            RectTransform tomada = BotaoTomada(modulo, (ehPatch ? "P" : "S") + porta, new Vector2(x, 0f),
                () => ClicarPorta(patch, porta), out Image imagem, out Color corBase);

            if (ehPatch)
            {
                tomadaPatch[i] = tomada;
                tomadaImgPatch[i] = imagem;
                corBasePatch[i] = corBase;
            }
            else
            {
                tomadaSwitch[i] = tomada;
                tomadaImgSwitch[i] = imagem;
                corBaseSwitch[i] = corBase;
            }

            // O número da tomada (P1..Pn no patch panel, S1..Sn no switch) é o que casa os pares.
            Text numero = RedeUi.Texto(modulo, "Numero" + porta, (ehPatch ? "P" : "S") + porta, 22, TextAnchor.MiddleCenter, CorTomadaBorda);
            RedeUi.Ancorar((RectTransform)numero.transform, new Vector2(0.5f, 0.5f), new Vector2(EspacamentoPorta - 20f, 28f), new Vector2(0.5f, 0.5f));
            ((RectTransform)numero.transform).anchoredPosition = new Vector2(x, -80f);
        }
    }

    /// <summary>Tomada clicável: a MESMA arte do ponto de rede do PC (com um desenho simples de reserva).</summary>
    private RectTransform BotaoTomada(Transform pai, string nome, Vector2 pos, UnityAction acao,
        out Image imagem, out Color corBase)
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

        imagem = img;
        corBase = img.color;

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
            Caixa(img.transform, "Corpo", new Color(0.80f, 0.86f, 0.93f), Vector2.zero, new Vector2(ladoMacho, ladoMacho * 0.82f));
            Caixa(img.transform, "Lingua", new Color(0.60f, 0.66f, 0.74f), new Vector2(0f, ladoMacho * 0.34f), new Vector2(ladoMacho * 0.45f, ladoMacho * 0.18f));
        }

        // O "macho" fica com a grossura de um conector: um quadrado do lado <see cref="ladoMacho"/> — a arte
        // (deitada) mantém a proporção dentro dele e o desenho sai do tamanho de um plugue de cabo de rede.
        RedeUi.Ancorar(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(ladoMacho, ladoMacho), new Vector2(0.5f, 0.5f));
        img.enabled = false;
        return img;
    }

    private UICableCurve NovaCurva(string nome)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(cabos, false);
        RedeUi.Esticar((RectTransform)go.transform);
        var curva = go.AddComponent<UICableCurve>();
        curva.Thickness = espessuraCabo;
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
    /// Tomada JÁ LIGADA: o clique tira o cabo dela (veja <see cref="Desligar"/>).
    /// </summary>
    private void ClicarPorta(bool ehPatch, int porta)
    {
        if (sessao == null) return;
        if (porta < 1 || porta > numPortas) return;

        if (sessao.PortaLigada(porta))
        {
            // Tomada já ligada: o clique TIRA o cabo (ele volta pra mão) — dá pra colocar e tirar quantas vezes quiser.
            // Com uma ponta na mão (meio caminho de outra ligação) o clique não vale: primeiro termine/desfaça aquela.
            if (pontaPorta == 0) Desligar(porta, ehPatch);
            else Erro(ehPatch, porta);
            return;
        }

        // Sem cabo na mão não dá pra encaixar em nada.
        if (!temCabo)
        {
            Erro(ehPatch, porta);
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
            InteractionSounds.Raise(InteractionSound.CablePlugged);   // primeira ponta encaixada
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
            Atualizar();
            return;
        }

        // Números diferentes: a outra ponta tem de ser a tomada de MESMO número do outro lado.
        Erro(ehPatch, porta);
    }

    /// <summary>Encaixa a outra ponta: se os números batem, a porta fica ligada e o cabo fica guardado com ela
    /// (o jogador pode tirá-lo depois e ligar de novo — veja <see cref="Desligar"/>).</summary>
    private void FecharLigacao(bool ehPatch, int porta)
    {
        Escolher(ehPatch, porta);
        bool ok = sessao.Confirmar();

        if (ok)
        {
            InteractionSounds.Raise(InteractionSound.CablePlugged);   // par ligado: a outra ponta encaixou
            RedeGame jogo = RedeGame.Garantir();
            jogo.RegistrarParLigado(porta);
            // O cabo NÃO é gasto: fica guardado com a porta (o jogador pode tirá-lo depois e ligar de novo).
            jogo.GuardarCaboLigado(porta, pontaItem);
            pontaItem = null;
            pontaPorta = 0;
            pontaEhPatch = false;
            temCabo = false;         // e a mão ficou vazia: o cabo está no rack
        }
        else Erro(ehPatch, porta);

        Atualizar();
    }

    /// <summary>
    /// Tira o cabo de uma tomada já ligada: o par desliga e o cabo VOLTA pra mão do jogador, que pode encaixar de
    /// novo (na mesma tomada ou em outra), quantas vezes quiser. Sem cabo físico guardado (ligação feita sem mão,
    /// ex.: sem jogador) a tomada simplesmente fica livre. Mão ocupada = o clique não vale: o cabo nunca some.
    /// </summary>
    private void Desligar(int porta, bool ehPatch)
    {
        RedeGame jogo = RedeGame.Garantir();
        PickupInteractable cabo = jogo.RetirarCaboLigado(porta);

        if (cabo != null)
        {
            ItemHolder mao = Mao();
            if (mao == null || !mao.Retrieve(cabo.gameObject))
            {
                jogo.GuardarCaboLigado(porta, cabo);   // não deu pra devolver: o cabo continua onde estava
                Erro(ehPatch, porta);
                return;
            }
        }

        sessao.Desligar(porta);
        jogo.RemoverParLigado(porta);
        temCabo = CaboNaMao() != null;   // o cabo voltou pra mão: já dá pra encaixar de novo
        InteractionSounds.Raise(InteractionSound.CableUnplugged);
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
        InteractionSounds.Raise(InteractionSound.CableUnplugged);
        if (sessao != null) sessao.LimparEscolhas();
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

    /// <summary>Aviso SEM texto: a tomada clicada pisca vermelha quando o clique não vale.</summary>
    private void Erro(bool ehPatch, int porta)
    {
        InteractionSounds.Raise(InteractionSound.Error);   // clique que não vale: som de erro (o vermelho é o visual)
        int i = porta - 1;
        if (i < 0 || i >= numPortas) return;
        if (ehPatch) flashPatch[i] = 0.5f;
        else flashSwitch[i] = 0.5f;
    }

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

        // Piscada vermelha da tomada clicada sem sucesso (retorno sem nenhum texto).
        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < numPortas; i++)
        {
            if (flashPatch[i] > 0f)
            {
                flashPatch[i] = Mathf.Max(0f, flashPatch[i] - dt);
                tomadaImgPatch[i].color = Color.Lerp(corBasePatch[i], VermelhoErro, Mathf.Clamp01(flashPatch[i] * 2f));
            }
            if (flashSwitch[i] > 0f)
            {
                flashSwitch[i] = Mathf.Max(0f, flashSwitch[i] - dt);
                tomadaImgSwitch[i].color = Color.Lerp(corBaseSwitch[i], VermelhoErro, Mathf.Clamp01(flashSwitch[i] * 2f));
            }
        }

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
    }
}
