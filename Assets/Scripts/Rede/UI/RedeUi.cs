using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Ajudas para montar as telas 2D do minigame de rede por código (retângulos, textos e botões).
/// Assim as telas não dependem de um prefab cheio de fios amarrados à mão.
/// </summary>
public static class RedeUi
{
    private static Font fonte;

    /// <summary>
    /// Largura do botão do tablet do jogador ("TABLET [T]", <see cref="TicketTabletPrototype"/>), em pixels da
    /// referência 1920 x 1080. Ele fica ancorado no canto inferior direito, a <see cref="CelularMargem"/> das bordas.
    /// É a ÚNICA fonte desse tamanho: o tablet limita o botão a ela e as setas das telas se afastam dela.
    /// </summary>
    public const float CelularLargura = 170f;

    /// <summary>Altura do botão do tablet.</summary>
    public const float CelularAltura = 54f;

    /// <summary>Distância do botão do tablet até as bordas direita e de baixo.</summary>
    public const float CelularMargem = 24f;

    /// <summary>Respiro entre a seta "►" e a lateral esquerda do celular.</summary>
    public const float FolgaDoCelular = 20f;

    /// <summary>
    /// Distância da borda DIREITA em que a seta "►" fica: o botão do tablet ocupa os 194 px do canto inferior direito
    /// (24 de margem + 170 do botão), então a seta vai para a ESQUERDA dele, lado a lado. É o mesmo valor gravado no
    /// RectTransform do BotaoAvancar do prefab <c>UI_Screen_Gabinete</c> — se um mudar, mude o outro.
    /// </summary>
    public const float BordaSetaProxima = CelularMargem + CelularLargura + FolgaDoCelular;   // 214

    /// <summary>
    /// Setas de navegação das telas de vista (◄ ►), no padrão do canvas do PC (UI_Screen_Gabinete): os cantos de
    /// baixo, 110 x 90. A "►" nasce À ESQUERDA do celular do jogador para ele não cobrir o botão.
    /// </summary>
    public static void Setas(Transform pai, UnityAction aoVoltar, UnityAction aoAvancar)
    {
        var cor = new Color(0.22f, 0.25f, 0.3f, 0.95f);

        RectTransform anterior = Botao(pai, "Anterior", "\u25C4", cor, aoVoltar, 40);
        Ancorar(anterior, new Vector2(0f, 0f), new Vector2(110f, 90f), new Vector2(0f, 0f));
        anterior.anchoredPosition = new Vector2(24f, 24f);

        // O canto inferior direito é do celular: a seta "►" fica na lateral esquerda dele.
        RectTransform proxima = Botao(pai, "Proxima", "\u25BA", cor, aoAvancar, 40);
        Ancorar(proxima, new Vector2(1f, 0f), new Vector2(110f, 90f), new Vector2(1f, 0f));
        proxima.anchoredPosition = new Vector2(-BordaSetaProxima, 24f);
    }

    public static Font Fonte
    {
        get
        {
            if (fonte == null)
            {
                fonte = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (fonte == null) fonte = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            return fonte;
        }
    }

    /// <summary>Retângulo filho que ocupa todo o pai.</summary>
    public static RectTransform Cheio(Transform pai, string nome, Color cor)
    {
        RectTransform rt = Imagem(pai, nome, cor);
        Esticar(rt);
        return rt;
    }

    /// <summary>Retângulo colorido (Image) com RectTransform, sem posicionar.</summary>
    public static RectTransform Imagem(Transform pai, string nome, Color cor)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(pai, false);
        var img = go.AddComponent<Image>();
        img.color = cor;
        img.raycastTarget = false;
        return (RectTransform)go.transform;
    }

    /// <summary>
    /// Mesmo retângulo, mas com uma ARTE (sprite) por cima. Sprite nulo = só o retângulo colorido, como antes.
    /// A arte é esticada no tamanho do retângulo (sem preservar proporção), como o protótipo da cena MinigameTest.
    /// </summary>
    public static RectTransform Imagem(Transform pai, string nome, Color cor, Sprite sprite)
    {
        RectTransform rt = Imagem(pai, nome, cor);
        if (sprite != null)
        {
            Image img = rt.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = false;
        }
        return rt;
    }

    /// <summary>Retângulo filho que ocupa todo o pai, com uma arte (sprite) opcional.</summary>
    public static RectTransform Cheio(Transform pai, string nome, Color cor, Sprite sprite)
    {
        RectTransform rt = Imagem(pai, nome, cor, sprite);
        Esticar(rt);
        return rt;
    }

    public static Image ImagemSprite(Transform pai, string nome, Sprite sprite, Color cor)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(pai, false);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = cor;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }

    public static Text Texto(Transform pai, string nome, string valor, int tamanho, TextAnchor alinhamento, Color cor)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(pai, false);
        var t = go.AddComponent<Text>();
        t.font = Fonte;
        t.text = valor;
        t.fontSize = tamanho;
        t.alignment = alinhamento;
        t.color = cor;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>
    /// Cria um botão com rótulo e devolve o RectTransform dele (use GetComponent&lt;Button&gt;() se precisar).
    /// Com uma arte (sprite), o botão vira a própria arte (e o "apertado" pode ser outro sprite).
    /// </summary>
    public static RectTransform Botao(Transform pai, string nome, string rotulo, Color fundo, UnityAction aoClicar,
        int tamanhoFonte = 26, Sprite sprite = null, Sprite spriteApertado = null)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(pai, false);
        var img = go.AddComponent<Image>();
        img.color = fundo;
        img.raycastTarget = true;
        if (sprite != null)
        {
            img.sprite = sprite;
            img.color = Color.white;   // a arte já vem colorida: nada de tingir por cima
        }
        var b = go.AddComponent<Button>();
        b.targetGraphic = img;
        var cores = b.colors;
        cores.normalColor = Color.white;
        cores.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
        cores.pressedColor = new Color(0.82f, 0.82f, 0.82f);
        cores.selectedColor = Color.white;
        b.colors = cores;
        if (spriteApertado != null)
        {
            SpriteState estados = b.spriteState;
            estados.highlightedSprite = spriteApertado;
            estados.pressedSprite = spriteApertado;
            b.spriteState = estados;
        }
        if (aoClicar != null) b.onClick.AddListener(aoClicar);

        Text t = Texto(go.transform, "Texto", rotulo, tamanhoFonte, TextAnchor.MiddleCenter, Color.white);
        Esticar((RectTransform)t.transform);
        return (RectTransform)go.transform;
    }

    /// <summary>
    /// Botão de fechar IGUAL ao da tela do PC (UI_Screen_Gabinete): um quadrado escuro 64x64 com um "X"
    /// branco em negrito e contorno preto, ancorado no canto superior direito.
    /// </summary>
    public static RectTransform BotaoFechar(Transform pai, UnityAction aoClicar)
    {
        RectTransform rt = Botao(pai, "BotaoFechar", "X", new Color(0.22f, 0.25f, 0.3f, 0.95f), aoClicar, 40);
        Ancorar(rt, new Vector2(1f, 1f), new Vector2(64f, 64f), new Vector2(1f, 1f));
        rt.anchoredPosition = new Vector2(-24f, -24f);

        Text t = rt.GetComponentInChildren<Text>();
        if (t != null)
        {
            t.fontStyle = FontStyle.Bold;
            Outline contorno = t.gameObject.AddComponent<Outline>();
            contorno.effectColor = new Color(0f, 0f, 0f, 0.8f);
            contorno.effectDistance = new Vector2(2f, -2f);
        }
        return rt;
    }

    /// <summary>Troca a cor de fundo de um botão criado por <see cref="Botao"/>.</summary>
    public static void PintarBotao(RectTransform botao, Color cor)
    {
        if (botao != null && botao.TryGetComponent(out Image img)) img.color = cor;
    }

    /// <summary>Faz o RectTransform preencher todo o pai (com margens opcionais).</summary>
    public static void Esticar(RectTransform rt, float margem = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(margem, margem);
        rt.offsetMax = new Vector2(-margem, -margem);
    }

    /// <summary>Ancora o elemento num ponto do pai (0..1) com tamanho fixo.</summary>
    public static RectTransform Ancorar(RectTransform rt, Vector2 ancora, Vector2 tamanho, Vector2 pivor)
    {
        rt.anchorMin = rt.anchorMax = ancora;
        rt.pivot = pivor;
        rt.sizeDelta = tamanho;
        return rt;
    }
}
