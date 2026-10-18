using EstagiarIo.Rede;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Tela 2D do rack de rede: um gabinete de rack de 19" visto de frente, com o PATCH PANEL (portas P1..Pn, etiquetas
/// brancas: cada uma é o ponto de parede de uma estação), o organizador de cabos e o SWITCH (portas S1..Sn, com o LED de
/// link de cada porta). Mesmo padrão visual das outras telas internas (fundo escuro, equipamentos desenhados com cantos
/// arredondados, a mesma arte de tomada/plugue da tela do PC).
/// <para>
/// O cabo é SÓ o que o jogador traz na mão — não existe cabo infinito. Com um cabo de rede na mão: clicar numa porta
/// livre encaixa a primeira ponta (o cabo SAI da mão); clicar em QUALQUER outra porta livre encaixa a outra ponta.
/// <b>Nenhuma combinação é proibida nem corrigida</b>: P N ↔ S N (certa), P N ↔ S M, P ↔ P e S ↔ S são ligações
/// reais na topologia (<see cref="RedeGame.Topologia"/>), e são os PCs e os chamados que dizem se a rede funciona.
/// Clicar de novo na porta da primeira ponta solta o cabo (volta para a mão); clicar numa porta ocupada tira SÓ aquela
/// ponta: a outra continua encaixada e o cabo fica pendurado nela. Porta ocupada com outra ponta pendurada = não
/// encaixa (pisca vermelho).
/// </para>
/// Os LEDs do switch são o retorno do próprio equipamento: verde = porta com cabo bom vindo do patch panel; apagado =
/// sem link; todos piscando âmbar = laço (S ↔ S), como num switch de verdade. Nada diz "número errado".
/// Monta a própria interface por código (as artes vêm do setup).
/// </summary>
[RequireComponent(typeof(UIScreen))]
public class RedeRackScreen : MonoBehaviour
{
    private static readonly Color CorPainel = new Color(0.09f, 0.11f, 0.14f, 1f);       // mesmo fundo da impressora
    private static readonly Color CorGabinete = new Color(0.13f, 0.15f, 0.19f, 1f);
    private static readonly Color CorTrilho = new Color(0.22f, 0.24f, 0.28f, 1f);
    private static readonly Color CorFuro = new Color(0.07f, 0.08f, 0.1f, 1f);
    private static readonly Color CorPatch = new Color(0.8f, 0.82f, 0.85f, 1f);          // carcaça clara (como a impressora)
    private static readonly Color CorPatchSombra = new Color(0.62f, 0.65f, 0.7f, 1f);
    private static readonly Color CorSwitch = new Color(0.2f, 0.22f, 0.26f, 1f);
    private static readonly Color CorSwitchClaro = new Color(0.26f, 0.29f, 0.34f, 1f);
    private static readonly Color CorOrganizador = new Color(0.1f, 0.11f, 0.13f, 1f);
    private static readonly Color CorAnel = new Color(0.3f, 0.33f, 0.38f, 1f);
    private static readonly Color CorEtiqueta = new Color(0.97f, 0.97f, 0.94f, 1f);
    private static readonly Color TextoEtiqueta = new Color(0.12f, 0.13f, 0.16f, 1f);
    private static readonly Color TextoGravado = new Color(0.6f, 0.66f, 0.74f, 1f);
    private static readonly Color CorTomada = new Color(0.13f, 0.15f, 0.19f, 1f);
    private static readonly Color AzulCabo = new Color(0.22f, 0.50f, 0.90f, 1f);
    private static readonly Color VermelhoErro = new Color(0.85f, 0.30f, 0.28f);
    private static readonly Color LedApagado = new Color(0.16f, 0.18f, 0.2f);
    private static readonly Color LedVerde = new Color(0.36f, 0.95f, 0.45f);
    private static readonly Color LedAmbar = new Color(1f, 0.7f, 0.15f);

    [Header("Artes (as mesmas da tela do PC) — o setup preenche")]
    [Tooltip("Tomada/ponto de rede: Assets/Art/UI/ui_render_ponto_rede.png")]
    [SerializeField] private Sprite spriteTomada;
    [Tooltip("Plugue RJ45: Assets/Art/UI/ui_render_plugue_rj45.png")]
    [SerializeField] private Sprite spritePlug;
    [Tooltip("Fundo antigo (bg1). Não é mais usado: o fundo segue o padrão escuro das outras telas internas.")]
    [SerializeField] private Sprite spriteFundo;
    [Tooltip("Sem uso desde a reformulação visual (2026-10-17); mantido para não perder a configuração do prefab.")]
    [Range(0f, 1f)] [SerializeField] private float veuDoFundo = 0.62f;

    [Header("Grossuras (pixels da referência)")]
    [Tooltip("Espessura do cabo de rede desenhado no rack.")]
    [SerializeField] private float espessuraCabo = 22f;
    [Tooltip("Lado do retângulo do plugue RJ45 (a arte mantém a proporção dentro dele).")]
    [SerializeField] private float ladoMacho = 74f;
    [Tooltip("Cabo de rede que vai para a mão ao tirar do rack uma ligação SEM objeto físico (a do estado inicial ou a " +
             "trocada por um chamado). Vazio = a ligação só é desfeita, como antes.")]
    [SerializeField] private PickupInteractable caboSemObjetoPrefab;

    // Geometria (pixels da referência 1920x1080; coordenadas LOCAIS do canvas, centro = 0).
    private const float EspacamentoPorta = 200f;
    private const float TomadaClique = 124f;
    private const float LarguraEquip = 1460f;
    private const float PatchY = 250f, PatchAltura = 200f;
    private const float OrganizadorY = 70f, OrganizadorAltura = 96f;
    private const float SwitchY = -150f, SwitchAltura = 240f;
    private const float CegaY = -360f, CegaAltura = 110f;
    private const float BordaDeBaixo = -640f;

    private bool construido;
    private int numPortas;

    private RectTransform bancada, cabos;
    private readonly Dictionary<PortaRack, PortaUi> portas = new Dictionary<PortaRack, PortaUi>();
    private readonly List<UICableCurve> curvas = new List<UICableCurve>();
    private readonly List<Image> plugues = new List<Image>();
    private UICableCurve curvaLivre;
    private Image plugLivre;
    private static Sprite arredondado;

    private class PortaUi
    {
        public RectTransform Rt;
        public Image Img, Led;
        public Color Base;
        public float Flash;
    }

    // O cabo da mão: temCabo = o jogador tem um cabo de rede na mão (ou com uma ponta encaixada).
    private bool temCabo;
    private PortaRack? ponta;              // porta onde a primeira ponta está encaixada (null = cabo ainda na mão)
    private PickupInteractable pontaItem;  // o cabo físico que SAIU da mão para ficar encaixado
    private LigacaoRack puxada;            // ligação de onde a ponta pendurada foi puxada (null = veio da mão)
    private bool puxadaMaterializada;      // o cabo dela foi criado ao puxar (a ligação não tinha objeto)

    private readonly List<Vector2> pontos = new List<Vector2>();
    private Canvas canvasTela;

    /// <summary>Topologia do rack que esta tela mostra (para testes).</summary>
    public RedeRackTopologia Topologia => RedeGame.Garantir().Topologia;

    private void Awake() => Construir();

    private void OnEnable()
    {
        pontaItem = null;
        ponta = null;
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
        bancada = RedeUi.Cheio(transform, "Painel", CorPainel);
        RedeUi.BotaoFechar(bancada, Fechar);

        // gabinete do rack: fundo, trilhos laterais com a furação e a base
        Caixa(bancada, "Gabinete", CorGabinete, new Vector2(0f, -40f), new Vector2(LarguraEquip + 240f, 980f), true);
        foreach (float x in new[] { -(LarguraEquip * 0.5f + 60f), LarguraEquip * 0.5f + 60f })
        {
            RectTransform trilho = Caixa(bancada, "Trilho", CorTrilho, new Vector2(x, -40f), new Vector2(46f, 940f), true);
            for (int i = 0; i < 22; i++)
                Caixa(trilho, "Furo", CorFuro, new Vector2(0f, 440f - i * 41f), new Vector2(14f, 14f), true);
        }

        Equipamento("PatchPanel", PatchY, PatchAltura, CorPatch, CorPatchSombra, "PATCH PANEL  ·  CAT6", true);
        Organizador();
        Equipamento("Switch", SwitchY, SwitchAltura, CorSwitch, CorSwitchClaro, "SWITCH  ·  GIGABIT", false);
        PainelCego();

        // cabos por cima dos equipamentos (curvas atrás dos plugues; o plugue que segue o mouse na frente de tudo)
        cabos = RedeUi.Cheio(bancada, "Cabos", Color.clear);
        curvaLivre = NovaCurva("CaboNaMao");
        plugLivre = Plugue("PlugNaMao");
    }

    // Um equipamento de rack (1U/2U): carcaça, orelhas com parafusos, nome gravado e a fileira de portas.
    private void Equipamento(string nome, float y, float altura, Color cor, Color sombra, string gravado, bool patch)
    {
        RectTransform sombraRt = Caixa(bancada, nome + "_Sombra", sombra, new Vector2(0f, y - 6f), new Vector2(LarguraEquip + 100f, altura), true);
        sombraRt.SetAsLastSibling();
        RectTransform eq = Caixa(bancada, nome, cor, new Vector2(0f, y), new Vector2(LarguraEquip + 100f, altura - 8f), true);
        foreach (float x in new[] { -(LarguraEquip * 0.5f + 20f), LarguraEquip * 0.5f + 20f })   // parafusos das orelhas
            foreach (float yy in new[] { altura * 0.3f, -altura * 0.3f })
                Caixa(eq, "Parafuso", CorFuro, new Vector2(x, yy), new Vector2(18f, 18f), true);

        Text marca = RedeUi.Texto(eq, "Gravado", gravado, 18, TextAnchor.MiddleLeft, patch ? Color.Lerp(CorPatchSombra, Color.black, 0.35f) : TextoGravado);
        RedeUi.Ancorar((RectTransform)marca.transform, new Vector2(0f, 1f), new Vector2(500f, 26f), new Vector2(0f, 1f));
        ((RectTransform)marca.transform).anchoredPosition = new Vector2(70f, -10f);
        marca.fontStyle = FontStyle.Bold;
        marca.raycastTarget = false;

        if (!patch)
        {
            // LED de energia do switch
            Image pwr = Circulo(eq, "LedEnergia", LedVerde, 16f);
            ((RectTransform)pwr.transform).anchorMin = ((RectTransform)pwr.transform).anchorMax = new Vector2(1f, 1f);
            ((RectTransform)pwr.transform).anchoredPosition = new Vector2(-80f, -22f);
        }

        for (int i = 0; i < numPortas; i++)
        {
            int numero = i + 1;
            var p = new PortaRack(patch, numero);
            float x = (i - (numPortas - 1) * 0.5f) * EspacamentoPorta;
            var ui = new PortaUi();

            if (patch)
            {
                // etiqueta branca acima da porta: é ela que diz qual ponto de parede (estação) a porta atende
                RectTransform etiqueta = Caixa(eq, "Etiqueta" + numero, CorEtiqueta, new Vector2(x, 62f), new Vector2(96f, 36f), true);
                Text t = RedeUi.Texto(etiqueta, "Texto", p.Rotulo, 24, TextAnchor.MiddleCenter, TextoEtiqueta);
                RedeUi.Esticar((RectTransform)t.transform);
                t.fontStyle = FontStyle.Bold;
                t.raycastTarget = false;
            }
            else
            {
                // número da porta impresso embaixo e o LED de link acima
                Text t = RedeUi.Texto(eq, "Numero" + numero, p.Rotulo, 24, TextAnchor.MiddleCenter, TextoGravado);
                RedeUi.Ancorar((RectTransform)t.transform, new Vector2(0.5f, 0.5f), new Vector2(120f, 30f), new Vector2(0.5f, 0.5f));
                ((RectTransform)t.transform).anchoredPosition = new Vector2(x, -84f);
                t.fontStyle = FontStyle.Bold;
                t.raycastTarget = false;
                ui.Led = Circulo(eq, "Led" + numero, LedApagado, 18f);
                ((RectTransform)ui.Led.transform).anchoredPosition = new Vector2(x, 78f);
            }

            PortaRack alvo = p;
            ui.Rt = BotaoTomada(eq, p.Rotulo, new Vector2(x, patch ? -12f : 0f), () => ClicarPorta(alvo), out ui.Img, out ui.Base);
            portas[p] = ui;
        }
    }

    private void Organizador()
    {
        RectTransform org = Caixa(bancada, "Organizador", CorOrganizador, new Vector2(0f, OrganizadorY), new Vector2(LarguraEquip + 100f, OrganizadorAltura), true);
        for (int i = 0; i < numPortas; i++)
        {
            float x = (i - (numPortas - 1) * 0.5f) * EspacamentoPorta;
            RectTransform anel = Caixa(org, "Anel", CorAnel, new Vector2(x, 0f), new Vector2(96f, 60f), true);
            Caixa(anel, "Vao", CorOrganizador, new Vector2(0f, -6f), new Vector2(70f, 40f), true);
        }
    }

    private void PainelCego()
    {
        RectTransform cega = Caixa(bancada, "PainelCego", CorSwitch, new Vector2(0f, CegaY), new Vector2(LarguraEquip + 100f, CegaAltura), true);
        for (int i = 0; i < 18; i++)   // ventilação
            Caixa(cega, "Ventilacao", CorOrganizador, new Vector2(-595f + i * 70f, 0f), new Vector2(46f, 12f), true);
    }

    /// <summary>Tomada clicável: a MESMA arte do ponto de rede do PC (com um desenho simples de reserva).</summary>
    private RectTransform BotaoTomada(Transform pai, string nome, Vector2 pos, UnityAction acao, out Image imagem, out Color corBase)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(pai, false);
        var img = go.AddComponent<Image>();
        img.raycastTarget = true;
        if (spriteTomada != null) { img.sprite = spriteTomada; img.preserveAspect = true; img.color = Color.white; }
        else img.color = CorTomada;
        imagem = img;
        corBase = img.color;
        var b = go.AddComponent<Button>();
        b.targetGraphic = img;
        ColorBlock cores = b.colors;
        cores.normalColor = Color.white;
        cores.highlightedColor = new Color(1f, 0.95f, 0.58f, 1f);
        cores.pressedColor = new Color(0.85f, 0.80f, 0.50f, 1f);
        cores.selectedColor = Color.white;
        cores.fadeDuration = 0.06f;
        b.colors = cores;
        if (acao != null) b.onClick.AddListener(acao);
        RedeUi.Ancorar((RectTransform)go.transform, new Vector2(0.5f, 0.5f), new Vector2(TomadaClique, TomadaClique), new Vector2(0.5f, 0.5f));
        ((RectTransform)go.transform).anchoredPosition = pos;
        return (RectTransform)go.transform;
    }

    private Image Plugue(string nome)
    {
        Image img = spritePlug != null
            ? RedeUi.ImagemSprite(cabos, nome, spritePlug, Color.white)
            : RedeUi.Imagem(cabos, nome, new Color(0.80f, 0.86f, 0.93f)).GetComponent<Image>();
        RedeUi.Ancorar(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(ladoMacho, ladoMacho), new Vector2(0.5f, 0.5f));
        img.raycastTarget = false;
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

    private static RectTransform Caixa(Transform pai, string nome, Color cor, Vector2 pos, Vector2 tamanho, bool redondo)
    {
        RectTransform rt = RedeUi.Imagem(pai, nome, cor);
        RedeUi.Ancorar(rt, new Vector2(0.5f, 0.5f), tamanho, new Vector2(0.5f, 0.5f));
        rt.anchoredPosition = pos;
        Image img = rt.GetComponent<Image>();
        img.raycastTarget = false;
        if (redondo) { img.sprite = Arredondado; img.type = Image.Type.Sliced; }
        return rt;
    }

    private static Image Circulo(Transform pai, string nome, Color cor, float d)
    {
        Image img = MiniUi.ImagemCircular(pai, nome, cor, d);
        img.raycastTarget = false;
        return img;
    }

    // Cantos arredondados (9-slice gerado uma vez), o mesmo desenho das outras telas internas.
    private static Sprite Arredondado
    {
        get
        {
            if (arredondado != null) return arredondado;
            const int n = 32, raio = 10;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Max(raio - x - 0.5f, x + 0.5f - (n - raio)));
                    float dy = Mathf.Max(0f, Mathf.Max(raio - y - 0.5f, y + 0.5f - (n - raio)));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(raio + 0.5f - Mathf.Sqrt(dx * dx + dy * dy))));
                }
            tex.Apply();
            arredondado = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                                        new Vector4(raio, raio, raio, raio));
            return arredondado;
        }
    }

    // ================================================================== interação

    /// <summary>
    /// Clique numa porta. Sem ponta encaixada: porta ocupada = tira só essa ponta (a outra fica); porta livre com cabo na mão =
    /// encaixa a primeira ponta. Com uma ponta encaixada: a mesma porta solta o cabo; qualquer outra porta LIVRE fecha a
    /// ligação (qualquer combinação: a topologia guarda o que o jogador fez); porta ocupada não aceita.
    /// </summary>
    private void ClicarPorta(PortaRack p)
    {
        RedeGame jogo = RedeGame.Garantir();
        LigacaoRack ligacao = jogo.Topologia.LigacaoEm(p);

        if (ponta == null)
        {
            if (ligacao != null) { PuxarPonta(p, ligacao); return; }
            if (!temCabo) { Erro(p); return; }   // sem cabo na mão não dá pra encaixar em nada
            ItemHolder mao = Mao();
            pontaItem = mao != null ? mao.Stow(transform.position) : null;
            ponta = p;
            InteractionSounds.Raise(InteractionSound.CablePlugged);   // primeira ponta encaixada
            Atualizar();
            return;
        }

        if (p.Equals(ponta.Value)) { SoltarPonta(); return; }
        if (ligacao != null) { Erro(p); return; }   // porta ocupada: uma porta, um cabo

        LigacaoRack nova = jogo.Ligar(ponta.Value, p, pontaItem);
        if (nova == null) { Erro(p); return; }
        InteractionSounds.Raise(InteractionSound.CablePlugged);   // a outra ponta encaixou
        pontaItem = null;
        ponta = null;
        puxada = null;
        temCabo = CaboNaMao() != null;   // a mão ficou vazia: o cabo está no rack
        Atualizar();
    }

    /// <summary>
    /// Clique numa porta OCUPADA (sem ponta pendurada): só a ponta CLICADA sai. A outra continua encaixada onde estava e
    /// o cabo fica pendurado nela (o mesmo estado de "primeira ponta encaixada"): clicar noutra porta livre encaixa de
    /// novo; clicar na ponta que ficou tira o cabo de vez (volta pra mão). Antes as duas pontas saíam juntas.
    /// Ligação sem objeto físico (estado inicial / trocada por chamado): o cabo dela vira um cabo real
    /// (<see cref="caboSemObjetoPrefab"/>), guardado como o cabo físico que sai da mão ao encaixar.
    /// </summary>
    private void PuxarPonta(PortaRack p, LigacaoRack l)
    {
        RedeGame jogo = RedeGame.Garantir();
        PickupInteractable cabo = l.Cabo as PickupInteractable;
        bool materializa = cabo == null && caboSemObjetoPrefab != null;
        if (!jogo.Desligar(p, out _)) { Erro(p); return; }
        if (materializa)
        {
            cabo = Instantiate(caboSemObjetoPrefab, transform.position, Quaternion.identity);
            cabo.gameObject.SetActive(false);   // como o cabo físico encaixado (Stow): o Retrieve ativa e põe na mão
        }
        puxada = l;
        puxadaMaterializada = materializa;
        ponta = l.Outra(p);
        pontaItem = cabo;
        temCabo = true;
        InteractionSounds.Raise(InteractionSound.CableUnplugged);
        Atualizar();
    }

    /// <summary>Tira a ponta da porta: o cabo VOLTA pra mão do jogador (mão ocupada = não sai, pisca vermelho).</summary>
    private void SoltarPonta()
    {
        if (pontaItem != null)
        {
            ItemHolder mao = Mao();
            if (mao == null || !mao.Retrieve(pontaItem.gameObject)) { Erro(ponta.Value); return; }
            pontaItem = null;
        }
        ponta = null;
        puxada = null;
        temCabo = CaboNaMao() != null;
        InteractionSounds.Raise(InteractionSound.CableUnplugged);
        Atualizar();
    }

    /// <summary>
    /// Ao fechar a tela, um cabo com uma ponta só encaixada volta pra mão do jogador (nunca some). Se a mão estiver
    /// ocupada, o cabo puxado de uma ligação volta para as duas portas em que estava.
    /// </summary>
    private void DevolverCabo(bool atualizar = true)
    {
        if (pontaItem != null)
        {
            ItemHolder mao = Mao();
            if ((mao == null || !mao.Retrieve(pontaItem.gameObject)) && puxada != null)
            {
                LigacaoRack l = puxada;
                if (puxadaMaterializada) { Destroy(pontaItem.gameObject); pontaItem = null; }
                RedeGame.Garantir().Religar(l, pontaItem);
            }
            pontaItem = null;
        }
        else if (puxada != null && ponta != null)
        {
            // ligação sem objeto e sem cabo para materializar: a ponta solta não tem para onde ir, volta para as portas
            LigacaoRack l = puxada;
            RedeGame.Garantir().Religar(l, null);
        }
        ponta = null;
        puxada = null;
        if (atualizar) Atualizar();
    }

    /// <summary>Aviso SEM texto: a porta clicada pisca vermelha quando o clique não vale.</summary>
    private void Erro(PortaRack p)
    {
        InteractionSounds.Raise(InteractionSound.Error);
        if (portas.TryGetValue(p, out PortaUi ui)) ui.Flash = 0.5f;
    }

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
        if (cabos == null) return;
        RedeRackTopologia topo = RedeGame.Garantir().Topologia;
        float dt = Time.unscaledDeltaTime;
        bool laco = topo.LacoNoSwitch;
        bool piscaLaco = Mathf.Repeat(Time.unscaledTime * 6f, 1f) < 0.5f;

        foreach (KeyValuePair<PortaRack, PortaUi> kv in portas)
        {
            PortaUi ui = kv.Value;
            if (ui.Flash > 0f)
            {
                ui.Flash = Mathf.Max(0f, ui.Flash - dt);
                ui.Img.color = Color.Lerp(ui.Base, VermelhoErro, Mathf.Clamp01(ui.Flash * 2f));
            }
            if (ui.Led != null)
            {
                // LED de link do switch: verde = cabo bom vindo do patch panel; laço = todos piscando âmbar
                LigacaoRack l = topo.LigacaoEm(kv.Key);
                bool link = l != null && !l.Defeituoso && l.Outra(kv.Key).Patch;
                ui.Led.color = laco ? (l != null && piscaLaco ? LedAmbar : LedApagado) : link ? LedVerde : LedApagado;
            }
        }

        // cabos ligados (um por ligação da topologia)
        IReadOnlyList<LigacaoRack> ligs = topo.Ligacoes;
        while (curvas.Count < ligs.Count) curvas.Add(NovaCurva("Cabo" + (curvas.Count + 1)));
        while (plugues.Count < ligs.Count * 2 + 1) plugues.Add(Plugue("Plug" + (plugues.Count + 1)));
        for (int i = 0; i < plugues.Count; i++) plugues[i].enabled = i < ligs.Count * 2;
        for (int i = 0; i < curvas.Count; i++)
        {
            bool usa = i < ligs.Count;
            if (curvas[i].gameObject.activeSelf != usa) curvas[i].gameObject.SetActive(usa);
            if (!usa) continue;
            Vector2 a = Pos(ligs[i].A), b = Pos(ligs[i].B);
            Desenhar(curvas[i], a, b, ligs[i]);
            plugues[i * 2].rectTransform.anchoredPosition = a;
            plugues[i * 2 + 1].rectTransform.anchoredPosition = b;
        }
        plugLivre.transform.SetAsLastSibling();

        if (!temCabo)
        {
            if (curvaLivre.gameObject.activeSelf) curvaLivre.gameObject.SetActive(false);
            plugLivre.enabled = false;
            return;
        }

        // a outra ponta do cabo (com plugue) segue o mouse; sem ponta encaixada, o cabo sobe da borda de baixo
        Vector2 livre = new Vector2(0f, BordaDeBaixo + 320f);
        if (TentarPonteiro(out Vector2 mouse)) livre = mouse;
        Vector2 presa = ponta.HasValue ? Pos(ponta.Value) : new Vector2(livre.x, BordaDeBaixo);
        if (!curvaLivre.gameObject.activeSelf) curvaLivre.gameObject.SetActive(true);
        plugLivre.enabled = true;
        plugLivre.rectTransform.anchoredPosition = livre;
        DesenharSolto(curvaLivre, presa, livre);
        if (ponta.HasValue)
        {
            // a ponta encaixada também mostra o plugue (o primeiro plugue livre da lista)
            Image p0 = plugues[ligs.Count * 2];
            p0.enabled = true;
            p0.rectTransform.anchoredPosition = presa;
        }
    }

    private Vector2 Pos(PortaRack p) => portas.TryGetValue(p, out PortaUi ui) ? Local(ui.Rt) : Vector2.zero;

    /// <summary>
    /// Patch cord ligado: P ↔ S desce pelo organizador; P ↔ P e S ↔ S fazem uma alça para baixo (o laço fica visível).
    /// </summary>
    private void Desenhar(UICableCurve curva, Vector2 a, Vector2 b, LigacaoRack l)
    {
        Vector2 c1, c2;
        if (l.A.Patch != l.B.Patch)
        {
            Vector2 cima = a.y >= b.y ? a : b, baixo = a.y >= b.y ? b : a;
            float d = Mathf.Max(90f, (cima.y - baixo.y) * 0.45f);
            c1 = cima + Vector2.down * d;
            c2 = baixo + Vector2.up * d;
            a = cima; b = baixo;
        }
        else
        {
            float queda = 150f + Mathf.Abs(a.x - b.x) * 0.12f;
            c1 = a + Vector2.down * queda;
            c2 = b + Vector2.down * queda;
        }
        Curva(curva, a, c1, c2, b);
    }

    private void DesenharSolto(UICableCurve curva, Vector2 a, Vector2 b)
    {
        float queda = Mathf.Max(80f, Vector2.Distance(a, b) * 0.32f);
        Curva(curva, a, a + Vector2.down * queda, b + Vector2.down * queda * 0.6f, b);
    }

    private void Curva(UICableCurve curva, Vector2 a, Vector2 c1, Vector2 c2, Vector2 b)
    {
        pontos.Clear();
        const int n = 26;
        for (int k = 0; k <= n; k++)
        {
            float t = k / (float)n, u = 1f - t;
            pontos.Add(u * u * u * a + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * b);
        }
        curva.SetPoints(pontos);
    }

    private Vector2 Local(RectTransform t) => cabos.InverseTransformPoint(t.TransformPoint(t.rect.center));

    private bool TentarPonteiro(out Vector2 local)
    {
        local = Vector2.zero;
        if (Mouse.current == null) return false;
        if (canvasTela == null) canvasTela = GetComponentInParent<Canvas>();
        Camera cam = canvasTela != null && canvasTela.renderMode != RenderMode.ScreenSpaceOverlay ? canvasTela.worldCamera : null;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(cabos, Mouse.current.position.ReadValue(), cam, out local);
    }

    // ================================================================== atualização

    private void Atualizar()
    {
        if (bancada == null) return;
        if (!temCabo && curvaLivre != null && curvaLivre.gameObject.activeSelf) curvaLivre.gameObject.SetActive(false);
        if (plugLivre != null) plugLivre.enabled = temCabo;
    }
}
