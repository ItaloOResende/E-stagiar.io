#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gera as fotos dos itens dos aplicativos do tablet (Resources/TutorialIcons/&lt;id&gt;.png) renderizando os
/// próprios prefabs usados na cena, com fundo transparente e recorte justo. Não altera prefab nem cena: cada
/// prefab é instanciado numa cena de preview, fotografado e descartado.
/// Menu: Tools > E-stagiar.io > Tablet > Gerar fotos dos itens.
/// </summary>
[InitializeOnLoad]
public static class TabletFotosGerador
{
    private const string Pasta = "Assets/Resources/TutorialIcons";
    private const string Gatilho = "Temp/gerar_fotos_tablet.trigger";
    private const string Log = "Temp/gerar_fotos_tablet.log";
    private const int Render = 1024, Maximo = 512;

    // id da foto, prefab, giro em Y (graus) e inclinação da câmera (graus).
    private struct Foto
    {
        public string Id, Prefab; public float Giro, Inclinacao;
        public Foto(string id, string prefab, float giro, float inclinacao = 18f) { Id = id; Prefab = prefab; Giro = giro; Inclinacao = inclinacao; }
    }

    private const string G = "Assets/Prefabs/Gameplay/", P = "Assets/Prefabs/Props/";
    private static readonly Foto[] Fotos = {
        new Foto("gabinete", G + "Gabinete.prefab", -30),
        new Foto("gabinete-traseira", G + "Gabinete.prefab", 150),
        new Foto("placa-mae", G + "Pickup_PlacaMae.prefab", 160, 45),
        new Foto("fonte", G + "Pickup_Fonte.prefab", 150),
        new Foto("ram", G + "Pickup_MemoriaRAM.prefab", 160, 25),
        new Foto("ssd", G + "Pickup_HD.prefab", 150, 30),
        new Foto("cooler", G + "Pickup_Cooler.prefab", 150, 25),
        new Foto("monitor", P + "OF_Monitor.prefab", 160, 10),
        new Foto("monitor-traseira", P + "OF_Monitor.prefab", -20, 10),
        new Foto("sistema", G + "Pickup_Pendrive.prefab", 150, 30),
        new Foto("energia", G + "Pickup_CaboEnergia.prefab", 150, 40),
        new Foto("energia-monitor", G + "Pickup_CaboEnergia.prefab", 150, 40),
        new Foto("hdmi", G + "Pickup_CaboHDMI.prefab", 150, 40),
        new Foto("vga", G + "Pickup_CaboVideo.prefab", 150, 40),
        new Foto("rede", G + "Pickup_CaboRede.prefab", 150, 40),
        new Foto("chave", G + "Pickup_Ferramenta.prefab", 150, 40),
        new Foto("alicate", G + "Pickup_Alicate.prefab", 150, 40),
        new Foto("pendrive", G + "Pickup_Pendrive.prefab", 150, 30),
        new Foto("testador", G + "Testador_CaboRede.prefab", 160, 30),
        new Foto("impressora", G + "Impressora_Estacao.prefab", 150),
        new Foto("toner", G + "Pickup_Toner.prefab", 150, 25),
        new Foto("resma", G + "Pickup_ResmaPapel.prefab", 150, 25),
        new Foto("impressora-rede", G + "Impressora_Estacao.prefab", 150),
        new Foto("notebook", P + "Notebook_Toon_Otimizado.prefab", 160, 20),
        new Foto("ram-notebook", G + "Pickup_RamSodimm.prefab", 160, 30),
        new Foto("ssd-notebook", G + "Pickup_Ssd.prefab", 160, 30),
        new Foto("sistema-notebook", G + "Pickup_Pendrive.prefab", 150, 30),
        new Foto("rack", G + "RackInternet.prefab", 160, 12),
        new Foto("crimpagem", G + "Caixa_CaboRede.prefab", 150, 25),
    };

    static TabletFotosGerador()
    {
        if (!File.Exists(Gatilho)) return;
        File.Delete(Gatilho);
        EditorApplication.delayCall += Gerar;
    }

    [MenuItem("Tools/E-stagiar.io/Tablet/Gerar fotos dos itens")]
    public static void Gerar()
    {
        var log = new StringBuilder();
        Directory.CreateDirectory(Pasta);
        var preview = new PreviewRenderUtility();
        try
        {
            Camera cam = preview.camera;
            cam.fieldOfView = 22f; cam.nearClipPlane = .01f; cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.allowHDR = false; cam.allowMSAA = true;
            preview.lights[0].intensity = 1.25f; preview.lights[0].transform.rotation = Quaternion.Euler(40f, -35f, 0f);
            preview.lights[1].intensity = .8f; preview.lights[1].transform.rotation = Quaternion.Euler(-20f, 150f, 0f);
            preview.ambientColor = new Color(.42f, .42f, .46f);
            foreach (Foto f in Fotos)
            {
                try { log.AppendLine(f.Id + ": " + Fotografar(preview, f)); }
                catch (Exception e) { log.AppendLine(f.Id + ": ERRO " + e.Message); }
            }
        }
        finally { preview.Cleanup(); }
        AssetDatabase.Refresh();
        foreach (Foto f in Fotos) Importar(Pasta + "/" + f.Id + ".png");
        File.WriteAllText(Log, log.ToString());
        Debug.Log("[Tablet] Fotos dos itens geradas:\n" + log);
    }

    private static string Fotografar(PreviewRenderUtility preview, Foto f)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(f.Prefab);
        if (prefab == null) return "prefab não encontrado (" + f.Prefab + ")";
        var go = UnityEngine.Object.Instantiate(prefab);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, f.Giro, 0f));
        // Só o modelo: telas de minigame, indicadores e efeitos ficam fora da foto.
        foreach (var c in go.GetComponentsInChildren<Canvas>(true)) c.gameObject.SetActive(false);
        foreach (var p in go.GetComponentsInChildren<ParticleSystem>(true)) p.gameObject.SetActive(false);
        foreach (var l in go.GetComponentsInChildren<Light>(true)) l.enabled = false;
        preview.AddSingleGO(go);

        Bounds b = default; bool tem = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || !(r is MeshRenderer || r is SkinnedMeshRenderer || r is LineRenderer)) continue;
            if (!tem) { b = r.bounds; tem = true; } else b.Encapsulate(r.bounds);
        }
        if (!tem) { UnityEngine.Object.DestroyImmediate(go); return "sem renderers"; }

        Camera cam = preview.camera;
        float raio = b.extents.magnitude;
        float dist = raio / Mathf.Sin(cam.fieldOfView * .5f * Mathf.Deg2Rad) * 1.02f;
        Quaternion rot = Quaternion.Euler(f.Inclinacao, 0f, 0f);
        cam.transform.SetPositionAndRotation(b.center - rot * Vector3.forward * dist, rot);

        Color[] preto = Renderizar(preview, Color.black), branco = Renderizar(preview, Color.white);
        UnityEngine.Object.DestroyImmediate(go);

        var px = new Color[preto.Length];
        int x0 = Render, y0 = Render, x1 = -1, y1 = -1;
        for (int i = 0; i < px.Length; i++)
        {
            Color p = preto[i], w = branco[i];
            float a = 1f - ((w.r - p.r) + (w.g - p.g) + (w.b - p.b)) / 3f;
            a = Mathf.Clamp01(a);
            px[i] = a > .003f ? new Color(Mathf.Clamp01(p.r / a), Mathf.Clamp01(p.g / a), Mathf.Clamp01(p.b / a), a) : Color.clear;
            if (a > .04f)
            {
                int x = i % Render, y = i / Render;
                if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
            }
        }
        if (x1 < 0) return "imagem vazia";
        int margem = Mathf.RoundToInt(Mathf.Max(x1 - x0, y1 - y0) * .03f) + 2;
        x0 = Mathf.Max(0, x0 - margem); y0 = Mathf.Max(0, y0 - margem);
        x1 = Mathf.Min(Render - 1, x1 + margem); y1 = Mathf.Min(Render - 1, y1 + margem);
        int cw = x1 - x0 + 1, ch = y1 - y0 + 1;
        float esc = Mathf.Min(1f, (float)Maximo / Mathf.Max(cw, ch));
        int ow = Mathf.Max(1, Mathf.RoundToInt(cw * esc)), oh = Mathf.Max(1, Mathf.RoundToInt(ch * esc));

        var recorte = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
        var linha = new Color[cw * ch];
        for (int y = 0; y < ch; y++) Array.Copy(px, (y0 + y) * Render + x0, linha, y * cw, cw);
        recorte.SetPixels(linha); recorte.Apply();
        var saida = new Texture2D(ow, oh, TextureFormat.RGBA32, false);
        var outPx = new Color[ow * oh];
        for (int y = 0; y < oh; y++)
            for (int x = 0; x < ow; x++)
                outPx[y * ow + x] = Amostra(linha, cw, ch, (x + .5f) / ow, (y + .5f) / oh, 1f / esc);
        saida.SetPixels(outPx); saida.Apply();
        File.WriteAllBytes(Pasta + "/" + f.Id + ".png", saida.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(recorte); UnityEngine.Object.DestroyImmediate(saida);
        return ow + "x" + oh + " de " + f.Prefab;
    }

    // Redução por média de caixa com alfa pré-multiplicado (sem halo escuro nas bordas).
    private static Color Amostra(Color[] src, int w, int h, float u, float v, float passo)
    {
        int r = Mathf.Max(0, Mathf.FloorToInt(passo * .5f));
        int cx = Mathf.Clamp((int)(u * w), 0, w - 1), cy = Mathf.Clamp((int)(v * h), 0, h - 1);
        float sr = 0, sg = 0, sb = 0, sa = 0; int n = 0;
        for (int y = Mathf.Max(0, cy - r); y <= Mathf.Min(h - 1, cy + r); y++)
            for (int x = Mathf.Max(0, cx - r); x <= Mathf.Min(w - 1, cx + r); x++)
            { Color c = src[y * w + x]; sr += c.r * c.a; sg += c.g * c.a; sb += c.b * c.a; sa += c.a; n++; }
        if (sa <= 0f) return Color.clear;
        return new Color(sr / sa, sg / sa, sb / sa, sa / n);
    }

    private static Color[] Renderizar(PreviewRenderUtility preview, Color fundo)
    {
        preview.camera.backgroundColor = fundo;
        preview.BeginStaticPreview(new Rect(0, 0, Render, Render));
        preview.camera.Render();
        Texture2D t = preview.EndStaticPreview();
        Color[] px;
        if (t.width == Render && t.height == Render) px = t.GetPixels();
        else
        {
            var rt = RenderTexture.GetTemporary(Render, Render, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(t, rt); var antes = RenderTexture.active; RenderTexture.active = rt;
            var c = new Texture2D(Render, Render, TextureFormat.RGBA32, false);
            c.ReadPixels(new Rect(0, 0, Render, Render), 0, 0); c.Apply(); px = c.GetPixels();
            RenderTexture.active = antes; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.DestroyImmediate(c);
        }
        UnityEngine.Object.DestroyImmediate(t);
        return px;
    }

    private static void Importar(string caminho)
    {
        var imp = AssetImporter.GetAtPath(caminho) as TextureImporter;
        if (imp == null) return;
        imp.textureType = TextureImporterType.Default; imp.alphaIsTransparency = true; imp.mipmapEnabled = false;
        imp.npotScale = TextureImporterNPOTScale.None; imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.wrapMode = TextureWrapMode.Clamp; imp.maxTextureSize = 1024;
        imp.SaveAndReimport();
    }
}
#endif

