using UnityEngine;

/// <summary>
/// Visual comum dos selos do HUD de jogo (pontuação <see cref="ScoreHUD"/> e cronômetro <see cref="StageTimerHUD"/>):
/// cartão arredondado azul-marinho com borda verde-água (as cores do tablet), sombra leve, um rótulo pequeno em cima
/// ("PONTOS", "TEMPO") e o valor grande embaixo, na fonte do jogo (Varela Round, a mesma do tablet).
/// Também guarda o LAYOUT do topo da tela, em pixels da referência 1920 x 1080: a notificação de chamado novo do
/// tablet fica logo abaixo do selo da pontuação (<see cref="BaseDosSelos"/>), sem passar por baixo dele.
/// Os selos são IMGUI (desenhados por cima de qualquer Canvas), por isso o resto do HUD se posiciona em volta deles.
/// </summary>
public static class HudEstilo
{
    public static readonly Color Fundo = new Color(0.06f, 0.10f, 0.16f, 0.90f);
    public static readonly Color Borda = new Color(0.25f, 0.85f, 0.77f, 0.95f);
    public static readonly Color Rotulo = new Color(0.62f, 0.92f, 0.86f, 1f);
    public static readonly Color Valor = new Color(0.96f, 0.98f, 1f, 1f);
    public static readonly Color Sombra = new Color(0f, 0f, 0f, 0.35f);
    public static readonly Color FundoUrgente = new Color(0.42f, 0.07f, 0.08f, 0.94f);
    public static readonly Color BordaUrgente = new Color(1f, 0.42f, 0.38f, 1f);

    /// <summary>Faixa de tempo no topo da tela (referência 1080p).</summary>
    public const float AlturaFaixa = 10f;
    /// <summary>Distância entre a faixa do tempo e o topo dos selos.</summary>
    public const float MargemTopo = 12f;
    /// <summary>Altura dos selos: rótulo (16) + valor (34) + respiros.</summary>
    public const float AlturaSelo = 70f;
    /// <summary>Onde os selos terminam (do topo da tela): o que vier abaixo deles começa depois disso.</summary>
    public const float BaseDosSelos = AlturaFaixa + MargemTopo + AlturaSelo;   // 92

    public const int TamanhoRotulo = 15;
    public const int TamanhoValor = 32;
    private const float Raio = 14f, Espessura = 2f, PaddingX = 18f;

    private static GUIStyle rotulo, valor;
    private static float escalaEstilo = -1f;
    private static Font fonteEstilo;

    /// <summary>A mesma escala que o CanvasScaler das telas do jogo (referência 1920 x 1080, match 0,5).</summary>
    public static float Escala => Mathf.Sqrt((Screen.width / 1920f) * (Screen.height / 1080f));

    /// <summary>Largura do selo para o rótulo e o valor dados (pixels de tela).</summary>
    public static float Largura(string textoRotulo, string textoValor, float e, Font fonte)
    {
        Garantir(e, fonte);
        float w = Mathf.Max(rotulo.CalcSize(new GUIContent(textoRotulo)).x, valor.CalcSize(new GUIContent(textoValor)).x);
        return Mathf.Ceil(w + 2f * PaddingX * e);
    }

    /// <summary>Desenha o selo: sombra, fundo, borda, rótulo (alinhado à esquerda) e valor.</summary>
    public static void Selo(Rect r, string textoRotulo, string textoValor, float e, Font fonte, bool urgente = false, Color? corValor = null)
    {
        Garantir(e, fonte);
        Retangulo(new Rect(r.x + 3f * e, r.y + 4f * e, r.width, r.height), Sombra, Raio * e, 0f);
        Retangulo(r, urgente ? FundoUrgente : Fundo, Raio * e, 0f);
        Retangulo(r, urgente ? BordaUrgente : Borda, Raio * e, Mathf.Max(1f, Espessura * e));
        float x = r.x + PaddingX * e, w = r.width - 2f * PaddingX * e;
        rotulo.normal.textColor = urgente ? BordaUrgente : Rotulo;
        GUI.Label(new Rect(x, r.y + 8f * e, w, 18f * e), textoRotulo, rotulo);
        valor.normal.textColor = corValor ?? Valor;
        GUI.Label(new Rect(x, r.y + 24f * e, w, r.height - 28f * e), textoValor, valor);
    }

    /// <summary>Texto solto no estilo do valor (ex.: o +N/−N ao lado da pontuação).</summary>
    public static void Texto(Rect r, string texto, Color cor, float e, Font fonte)
    {
        Garantir(e, fonte);
        valor.normal.textColor = cor;
        GUI.Label(r, texto, valor);
    }

    /// <summary>Retângulo arredondado: <paramref name="borda"/> 0 = preenchido; maior que 0 = só o contorno.</summary>
    public static void Retangulo(Rect r, Color cor, float raio, float borda)
    {
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, cor,
            borda > 0f ? Vector4.one * borda : Vector4.zero, Vector4.one * raio);
    }

    private static void Garantir(float e, Font fonte)
    {
        if (rotulo != null && Mathf.Approximately(escalaEstilo, e) && fonteEstilo == fonte) return;
        escalaEstilo = e;
        fonteEstilo = fonte;
        rotulo = new GUIStyle(GUI.skin.label)
        {
            font = fonte, fontSize = Mathf.Max(8, Mathf.RoundToInt(TamanhoRotulo * e)), fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft, wordWrap = false, clipping = TextClipping.Overflow
        };
        valor = new GUIStyle(GUI.skin.label)
        {
            font = fonte, fontSize = Mathf.Max(10, Mathf.RoundToInt(TamanhoValor * e)), fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Overflow
        };
    }
}
