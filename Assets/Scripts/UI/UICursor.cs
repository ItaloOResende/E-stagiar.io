using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cursor do mouse nas telas 2D: aplica uma textura numa ALTURA em pixels (redimensionada uma vez e guardada em cache)
/// com o hotspot proporcional (a ponta continua na ponta). Usa cursor por software para o tamanho ser respeitado em
/// qualquer plataforma (cursores de hardware costumam ser limitados a 32/64 px).
/// </summary>
public static class UICursor
{
    private static readonly Dictionary<(Texture2D, int), Texture2D> cache = new Dictionary<(Texture2D, int), Texture2D>();

    /// <summary>Textura atualmente aplicada (null = cursor padrão do sistema).</summary>
    public static Texture2D Current { get; private set; }

    /// <param name="hotspotPixels">Hotspot em pixels da textura ORIGINAL (origem no canto superior esquerdo).</param>
    public static void Set(Texture2D texture, Vector2 hotspotPixels, int height)
    {
        if (texture == null) { Reset(); return; }
        height = Mathf.Clamp(height, 16, 512);
        float k = height / (float)texture.height;
        int width = Mathf.Max(1, Mathf.RoundToInt(texture.width * k));
        Texture2D scaled = Scaled(texture, width, height);
        Vector2 hotspot = new Vector2(Mathf.Clamp(hotspotPixels.x * k, 0, width - 1), Mathf.Clamp(hotspotPixels.y * k, 0, height - 1));
        Cursor.SetCursor(scaled, hotspot, CursorMode.ForceSoftware);
        Current = texture;
    }

    public static void Reset()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        Current = null;
    }

    private static Texture2D Scaled(Texture2D source, int width, int height)
    {
        if (cache.TryGetValue((source, height), out Texture2D t) && t != null) return t;
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
