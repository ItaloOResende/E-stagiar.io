using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Aviso curto no RODAPÉ da tela: aparece, fica um instante e sai sozinho. Serve para dizer o que IMPEDIU a ação
/// ("Falta ferramenta", "Ordem errada") sem caixa de diálogo e sem parar o jogo.
///
/// É o mesmo espírito do cartão do <see cref="SupervisorAlertHUD"/> — fundo arredondado, ícone à esquerda e uma
/// linha de texto — mas embaixo na tela e SEM o "!" vermelho: a flagra do supervisor é que custa ponto; aqui é só
/// um recado do próprio jogo. Nada dele recebe cliques, então não atrapalha a tela por baixo.
///
/// Monta tudo por código (sem prefab): ponha o componente num RectTransform que cubra a tela e chame
/// <see cref="Mostrar"/>.
/// </summary>
public class AvisoRodape : MonoBehaviour
{
    [Tooltip("Fonte do texto. Vazio = a fonte padrão da interface do projeto.")]
    [SerializeField] private Font font;
    [Tooltip("Quanto tempo o cartão fica na tela, em segundos.")]
    [Min(0.5f)] [SerializeField] private float duracao = 2.6f;
    [Tooltip("Distância do cartão até a borda de baixo da tela, em pixels.")]
    [Min(0f)] [SerializeField] private float margem = 96f;
    [Tooltip("De quanto abaixo da borda o cartão entra deslizando, em pixels.")]
    [Min(0f)] [SerializeField] private float deslize = 130f;
    [Tooltip("Tamanho do texto do aviso (pixels).")]
    [Min(1f)] [SerializeField] private int tamanhoDoTexto = 28;
    [SerializeField] private Color corFundo = new Color(0.10f, 0.12f, 0.16f, 0.95f);
    [SerializeField] private Color corBorda = new Color(0.97f, 0.84f, 0.30f);
    [SerializeField] private Color corTexto = new Color(0.96f, 0.96f, 0.94f);
    [Tooltip("Lado do ícone (o desenho da ferramenta que falta), em pixels.")]
    [Min(0f)] [SerializeField] private float ladoIcone = 62f;
    [Tooltip("Espaço entre o ícone e o texto, em pixels.")]
    [Min(0f)] [SerializeField] private float espacoIcone = 14f;

    private RectTransform raiz, cartao, areaIcone;
    private Image fundo, icone;
    private Text texto;
    private float mostradoEm = -1000f;
    private readonly System.Collections.Generic.Dictionary<Color, Sprite> sprites = new System.Collections.Generic.Dictionary<Color, Sprite>();

    /// <summary>O cartão está na tela agora?</summary>
    public bool Visivel => cartao != null && cartao.gameObject.activeSelf;

    /// <summary>O texto que está no cartão (vazio se ele nunca apareceu).</summary>
    public string TextoAtual => texto != null ? texto.text : "";

    /// <summary>Mostra o aviso por <see cref="duracao"/> segundos. Chamar de novo reinicia o tempo e troca o texto.</summary>
    /// <param name="mensagem">Texto curto, ex.: "Falta ferramenta".</param>
    /// <param name="icone">Desenho opcional à esquerda (ex.: a ferramenta que falta).</param>
    public void Mostrar(string mensagem, Sprite icone = null) => Mostrar(mensagem, icone, corBorda);

    /// <summary>Como <see cref="Mostrar(string, Sprite)"/>, com a cor da borda do cartão (ex.: verde = deu certo).</summary>
    public void Mostrar(string mensagem, Sprite icone, Color borda)
    {
        Construir();
        if (!sprites.TryGetValue(borda, out Sprite sp)) sprites[borda] = sp = Arredondado(borda, corFundo);
        fundo.sprite = sp;

        texto.text = mensagem;
        areaIcone.gameObject.SetActive(icone != null);
        if (icone != null) this.icone.sprite = icone;

        mostradoEm = Time.unscaledTime;
        cartao.anchoredPosition = new Vector2(0f, -deslize);   // entra de baixo: o LateUpdate sobe
        Pintar(0f);
        cartao.gameObject.SetActive(true);
    }

    /// <summary>Tira o cartão da tela na hora (ao fechar a tela, por exemplo).</summary>
    public void Esconder()
    {
        mostradoEm = -1000f;
        if (cartao != null) cartao.gameObject.SetActive(false);
    }

    private void OnDisable() { if (cartao != null) cartao.gameObject.SetActive(false); }

    private void LateUpdate()
    {
        if (cartao == null || !cartao.gameObject.activeSelf) return;

        float t = Time.unscaledTime - mostradoEm;
        if (t > duracao) { cartao.gameObject.SetActive(false); return; }

        float entra = Mathf.Clamp01(t / 0.22f);
        float sai = Mathf.Clamp01((duracao - t) / 0.35f);
        float k = Mathf.Min(Suavizar(entra), Suavizar(sai));
        cartao.anchoredPosition = new Vector2(0f, Mathf.Lerp(-deslize, margem, k));
        Pintar(k);
    }

    private static float Suavizar(float x) => 1f - (1f - x) * (1f - x);

    private void Pintar(float alfa)
    {
        alfa = Mathf.Clamp01(alfa);
        fundo.color = new Color(1f, 1f, 1f, alfa);
        if (icone != null) icone.color = new Color(1f, 1f, 1f, alfa);
        if (texto != null) texto.color = new Color(corTexto.r, corTexto.g, corTexto.b, corTexto.a * alfa);
    }

    // ------------------------------------------------------------------ montagem (uma vez)

    private void Construir()
    {
        if (raiz != null) return;
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        raiz = Nova("AvisoRodape", transform);
        Esticar(raiz);

        // O cartão: encosta na base, cresce com o texto (é o retângulo justo ao conteúdo) e desliza de baixo.
        cartao = Nova("Cartao", raiz);
        cartao.anchorMin = cartao.anchorMax = new Vector2(0.5f, 0f);
        cartao.pivot = new Vector2(0.5f, 0f);
        cartao.anchoredPosition = new Vector2(0f, -deslize);

        fundo = cartao.gameObject.AddComponent<Image>();
        fundo.sprite = Arredondado(corBorda, corFundo);
        fundo.type = Image.Type.Sliced;
        fundo.raycastTarget = false;

        var linha = cartao.gameObject.AddComponent<HorizontalLayoutGroup>();
        linha.padding = new RectOffset(30, 34, 12, 12);
        linha.spacing = espacoIcone;
        linha.childAlignment = TextAnchor.MiddleCenter;
        linha.childForceExpandWidth = false;
        linha.childForceExpandHeight = false;

        var tamanho = cartao.gameObject.AddComponent<ContentSizeFitter>();
        tamanho.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        tamanho.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        areaIcone = Nova("Icone", cartao);
        icone = areaIcone.gameObject.AddComponent<Image>();
        icone.preserveAspect = true;
        icone.raycastTarget = false;
        var tamanhoIcone = areaIcone.gameObject.AddComponent<LayoutElement>();
        tamanhoIcone.preferredWidth = tamanhoIcone.preferredHeight = ladoIcone;

        RectTransform areaTexto = Nova("Texto", cartao);
        texto = areaTexto.gameObject.AddComponent<Text>();
        texto.font = font;
        texto.fontSize = tamanhoDoTexto;
        texto.alignment = TextAnchor.MiddleCenter;
        texto.fontStyle = FontStyle.Bold;
        texto.horizontalOverflow = HorizontalWrapMode.Overflow;
        texto.verticalOverflow = VerticalWrapMode.Overflow;
        texto.raycastTarget = false;
        var tamanhoTexto = areaTexto.gameObject.AddComponent<LayoutElement>();
        tamanhoTexto.preferredHeight = tamanhoDoTexto + 6f;

        Pintar(0f);
        cartao.gameObject.SetActive(false);
    }

    private static RectTransform Nova(string nome, Transform pai)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(pai, false);
        return (RectTransform)go.transform;
    }

    private static void Esticar(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // Retângulo arredondado com borda colorida (9-slice), no mesmo estilo cartunizado do cartão do supervisor.
    private static Sprite Arredondado(Color borda, Color fundo)
    {
        const int n = 64, r = 20, bw = 5;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float cx = Mathf.Clamp(x, r, n - 1 - r), cy = Mathf.Clamp(y, r, n - 1 - r);
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                float fora = Mathf.Clamp01(r - d + 0.5f);
                Color c = d > r - bw ? borda : fundo;
                c.a *= fora;
                t.SetPixel(x, y, c);
            }
        t.Apply();
        return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
    }
}
