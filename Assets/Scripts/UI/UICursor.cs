using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cursor do mouse nas telas 2D: aplica uma textura numa ALTURA em pixels (redimensionada uma vez e guardada em cache)
/// com o hotspot proporcional (a ponta continua na ponta). Usa cursor por software para o tamanho ser respeitado em
/// qualquer plataforma (cursores de hardware costumam ser limitados a 32/64 px).
/// <para>
/// São DUAS camadas: o cursor da tela (<see cref="Set"/>, o de baixo) e um cursor por CIMA (<see cref="Sobrepor"/>).
/// Enquanto o de cima estiver posto é ele que aparece; ao sair (<see cref="LimparSobreposicao"/>), o de baixo volta
/// sozinho, sem a tela precisar reaplicá-lo. Quem usa a camada de cima é o celular: em cima do aparelho aberto o
/// ponteiro vira a mãozinha, mesmo com o crimpador ou a chave de fenda "na mão" na tela de trás.
/// </para>
/// </summary>
public static class UICursor
{
    private static readonly Dictionary<(Texture2D, int), Texture2D> cache = new Dictionary<(Texture2D, int), Texture2D>();

    /// <summary>Textura atualmente aplicada (null = cursor padrão do sistema).</summary>
    public static Texture2D Current { get; private set; }

    // Camada de baixo: o cursor da tela (ou do jogo).
    private static Texture2D texturaDaTela;
    private static Vector2 pontaDaTela;
    private static int alturaDaTela;

    // Camada de cima: o cursor que fica por cima do da tela enquanto estiver posto.
    private static Texture2D texturaDeCima;
    private static Vector2 pontaDeCima;
    private static int alturaDeCima;

    /// <summary>Cursor da TELA (o de baixo): a textura, a ponta e a altura em pixels da tela.</summary>
    /// <param name="hotspotPixels">Hotspot em pixels da textura ORIGINAL (origem no canto superior esquerdo).</param>
    public static void Set(Texture2D texture, Vector2 hotspotPixels, int height)
    {
        texturaDaTela = texture;
        pontaDaTela = hotspotPixels;
        alturaDaTela = height;
        Aplicar();
    }

    /// <summary>Sem cursor da tela: o do sistema (a camada de cima, se estiver posta, continua aparecendo).</summary>
    public static void Reset()
    {
        texturaDaTela = null;
        Aplicar();
    }

    /// <summary>
    /// Põe um cursor POR CIMA do atual (ex.: a mãozinha em cima do celular aberto). Não mexe no cursor da tela: ao
    /// limpar a sobreposição é o cursor de baixo que volta a aparecer.
    /// </summary>
    public static void Sobrepor(Texture2D texture, Vector2 hotspotPixels, int height)
    {
        texturaDeCima = texture;
        pontaDeCima = hotspotPixels;
        alturaDeCima = height;
        Aplicar();
    }

    /// <summary>Tira o cursor de cima (sem sobreposição posta, não muda nada).</summary>
    public static void LimparSobreposicao()
    {
        if (texturaDeCima == null) return;
        texturaDeCima = null;
        Aplicar();
    }

    /// <summary>Aplica a camada de cima se houver; senão a de baixo; senão o cursor do sistema.</summary>
    private static void Aplicar()
    {
        bool deCima = texturaDeCima != null;
        Texture2D textura = deCima ? texturaDeCima : texturaDaTela;

        if (textura == null)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            Current = null;
            return;
        }

        int height = Mathf.Clamp(deCima ? alturaDeCima : alturaDaTela, 16, AlturaMaxima);
        Vector2 hotspotPixels = deCima ? pontaDeCima : pontaDaTela;

        float k = height / (float)textura.height;
        int width = Mathf.Max(1, Mathf.RoundToInt(textura.width * k));
        Texture2D scaled = Scaled(textura, width, height);
        Vector2 hotspot = new Vector2(Mathf.Clamp(hotspotPixels.x * k, 0, width - 1), Mathf.Clamp(hotspotPixels.y * k, 0, height - 1));
        Cursor.SetCursor(scaled, hotspot, Modo);
        Current = textura;
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    // Web: o navegador desenha o cursor (CSS). Cursor por software não aparece, e acima de 128 px ele é ignorado.
    private const CursorMode Modo = CursorMode.Auto;
    private const int AlturaMaxima = 128;
#else
    private const CursorMode Modo = CursorMode.ForceSoftware;
    private const int AlturaMaxima = 512;
#endif

    private static Texture2D Scaled(Texture2D source, int width, int height)
    {
        if (cache.TryGetValue((source, height), out Texture2D t) && t != null) return t;
        if (source.isReadable)
        {
            // textura legível: redimensiona na CPU (sem depender de leitura da GPU, que varia por plataforma)
            t = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = source.name + "_" + height };
            var px = new Color[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    px[y * width + x] = source.GetPixelBilinear((x + 0.5f) / width, (y + 0.5f) / height);
            t.SetPixels(px);
            t.Apply();
            cache[(source, height)] = t;
            return t;
        }
        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(source, rt);
        RenderTexture.active = rt;
        t = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = source.name + "_" + height };
        t.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        t.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        cache[(source, height)] = t;
        return t;
    }
}
