using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Formas extras para as telas 2D dos minigames (círculos, botões redondos). O resto continua em
/// <see cref="RedeUi"/> (retângulos, textos, botões). O círculo é gerado em tempo de execução: não
/// depende de nenhuma arte no projeto.
/// </summary>
public static class MiniUi
{
    private static Sprite circulo;

    /// <summary>Sprite de um círculo branco (borda suave), reutilizado por todas as telas.</summary>
    public static Sprite Circulo
    {
        get
        {
            if (circulo == null) circulo = GerarCirculo(128);
            return circulo;
        }
    }

    /// <summary>Imagem circular (não clicável) já ancorada no centro do pai.</summary>
    public static Image ImagemCircular(Transform pai, string nome, Color cor, float diametro)
    {
        RectTransform rt = RedeUi.Imagem(pai, nome, cor);
        rt.GetComponent<Image>().sprite = Circulo;
        RedeUi.Ancorar(rt, new Vector2(0.5f, 0.5f), new Vector2(diametro, diametro), new Vector2(0.5f, 0.5f));
        return rt.GetComponent<Image>();
    }

    /// <summary>Botão circular clicável (sem rótulo), ancorado no centro do pai.</summary>
    public static Image BotaoCircular(Transform pai, string nome, Color cor, float diametro, UnityAction aoClicar)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(pai, false);

        var img = go.AddComponent<Image>();
        img.sprite = Circulo;
        img.color = cor;
        img.raycastTarget = true;

        var b = go.AddComponent<Button>();
        b.targetGraphic = img;
        ColorBlock cores = b.colors;
        cores.normalColor = Color.white;
        cores.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
        cores.pressedColor = new Color(0.82f, 0.82f, 0.82f);
        cores.selectedColor = Color.white;
        cores.fadeDuration = 0.06f;
        b.colors = cores;
        if (aoClicar != null) b.onClick.AddListener(aoClicar);

        RedeUi.Ancorar((RectTransform)go.transform, new Vector2(0.5f, 0.5f), new Vector2(diametro, diametro), new Vector2(0.5f, 0.5f));
        return img;
    }

    /// <summary>Retângulo com "borda" (uma caixa maior por baixo de uma menor) — usado nas tomadas e encaixes.</summary>
    public static RectTransform CaixaComBorda(Transform pai, string nome, Color borda, Color miolo, Vector2 pos, Vector2 tamanho, float espessura = 6f)
    {
        RectTransform fora = RedeUi.Imagem(pai, nome, borda);
        RedeUi.Ancorar(fora, new Vector2(0.5f, 0.5f), tamanho, new Vector2(0.5f, 0.5f));
        fora.anchoredPosition = pos;

        RectTransform dentro = RedeUi.Imagem(fora, "Miolo", miolo);
        RedeUi.Ancorar(dentro, new Vector2(0.5f, 0.5f), tamanho - new Vector2(espessura * 2f, espessura * 2f), new Vector2(0.5f, 0.5f));
        dentro.anchoredPosition = Vector2.zero;
        return fora;
    }

    /// <summary>Barra de progresso simples: devolve o preenchimento (Imagem) e ajusta o pai para a largura dada.</summary>
    public static Image BarraProgresso(Transform pai, string nome, Color fundo, Color preenchimento, Vector2 pos, Vector2 tamanho, out RectTransform raiz)
    {
        raiz = RedeUi.Imagem(pai, nome, fundo);
        RedeUi.Ancorar(raiz, new Vector2(0.5f, 0.5f), tamanho, new Vector2(0.5f, 0.5f));
        raiz.anchoredPosition = pos;

        RectTransform fill = RedeUi.Imagem(raiz, "Preenchimento", preenchimento);
        RedeUi.Esticar(fill, 4f);

        // Tipo "Filled" horizontal: é o que faz o fillAmount valer (em Simple ele é ignorado).
        Image img = fill.GetComponent<Image>();
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left;
        img.fillAmount = 0f;
        return img;
    }

    private static Sprite GerarCirculo(int tamanho)
    {
        var tex = new Texture2D(tamanho, tamanho, TextureFormat.RGBA32, false);
        tex.name = "MiniUi_Circulo";
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float raio = tamanho * 0.5f - 1f;
        var pixels = new Color32[tamanho * tamanho];
        for (int y = 0; y < tamanho; y++)
        {
            for (int x = 0; x < tamanho; x++)
            {
                float dx = x + 0.5f - tamanho * 0.5f;
                float dy = y + 0.5f - tamanho * 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(raio - d + 0.5f);
                pixels[y * tamanho + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, tamanho, tamanho), new Vector2(0.5f, 0.5f), 100f);
    }
}
