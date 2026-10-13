using UnityEditor;
using UnityEngine;

/// <summary>
/// Passa os materiais do gabinete e do monitor (M_PC_*, URP/Lit, criados pelo projeto) para o Unity Toon Shader,
/// com a mesma configuração dos materiais do Matheus (M_Escritorio_Escuro = modelo: contorno, degraus de sombra).
/// Mantém a cor de cada material (base), usa uma versão mais escura dela como sombra e conserva o brilho do LED.
/// Os modelos (gabinete.fbx/monitor.fbx) não mudam. Idempotente. Menu: Tools > E-stagiar.io > Toon Shader no gabinete e monitor.
/// Backup dos .mat anteriores: Art/_Backup/2026-10-13_matheus_toon/Materials.
/// </summary>
public static class ToonShaderSetup
{
    private const string Mats = "Assets/Art/Materials/";
    private const string Template = Mats + "M_Escritorio_Escuro.mat";
    private static readonly string[] PcMaterials = { "Chapa", "HDMI", "LED", "LED_Off", "Painel", "Parafuso", "Pino", "Porta", "VGA" };
    private const float ShadeFactor = 0.7f;

    [MenuItem("Tools/E-stagiar.io/Toon Shader no gabinete e monitor")]
    public static void Run()
    {
        var template = AssetDatabase.LoadAssetAtPath<Material>(Template);
        if (template == null || template.shader == null || !template.shader.isSupported || template.shader.name.StartsWith("Hidden/"))
            throw new System.Exception("ToonShaderSetup: o material modelo não está com o Toon Shader válido (pacote com.unity.toonshader instalado?)");

        foreach (string n in PcMaterials)
        {
            string path = Mats + "M_PC_" + n + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m.shader == template.shader) continue;                     // já convertido
            Color baseColor = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
            Color emission = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
            Texture baseMap = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;

            m.shader = template.shader;
            m.CopyPropertiesFromMaterial(template);
            m.shaderKeywords = template.shaderKeywords;
            baseColor.a = 1f;
            Color shade = new Color(baseColor.r * ShadeFactor, baseColor.g * ShadeFactor, baseColor.b * ShadeFactor, 1f);
            foreach (string p in new[] { "_BaseColor", "_Color" }) if (m.HasProperty(p)) m.SetColor(p, baseColor);
            if (m.HasProperty("_1st_ShadeColor")) m.SetColor("_1st_ShadeColor", shade);
            if (m.HasProperty("_2nd_ShadeColor")) m.SetColor("_2nd_ShadeColor", shade * ShadeFactor);
            if (m.HasProperty("_Emissive_Color")) m.SetColor("_Emissive_Color", emission);
            foreach (string p in new[] { "_MainTex", "_BaseMap", "_1st_ShadeMap", "_2nd_ShadeMap" })
                if (m.HasProperty(p)) m.SetTexture(p, baseMap);
            EditorUtility.SetDirty(m);
            Debug.Log($"ToonShaderSetup: {path} -> {m.shader.name} (cor {baseColor}, emissão {emission})");
        }
        AssetDatabase.SaveAssets();
        FixShading();
    }

    /// <summary>
    /// Sombra coerente com a aparência anterior (URP/Lit) em TODOS os materiais com Toon Shader de Assets/Art.
    /// Causa do visual esbranquiçado/azulado: nos materiais do Matheus a sombra não usava a textura nem a cor da peça
    /// (_Use_BaseAs1st = 0, _1st_ShadeMap vazio, _1st_ShadeColor lavanda clara) e a luz ambiente era ignorada
    /// (_GI_Intensity = 0). No interior do escritório quase tudo fica "na sombra" do Toon: peças pretas ficavam lilás e a
    /// planta perdia a textura. Agora a sombra = textura/cor da própria peça escurecida, e o ambiente ilumina como antes.
    /// Cor base, texturas, contorno e demais propriedades não mudam. Idempotente.
    /// </summary>
    [MenuItem("Tools/E-stagiar.io/Toon Shader: corrigir sombras")]
    public static void FixShading()
    {
        var template = AssetDatabase.LoadAssetAtPath<Material>(Template);
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Art/Materials" }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m == null || m.shader != template.shader) continue;
            Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
            Set(m, "_Use_BaseAs1st", 1f);                       // sombra 1 = textura base x cor da sombra
            Set(m, "_Use_1stAs2nd", 1f);                        // sombra 2 = idem
            if (m.HasProperty("_1st_ShadeColor")) m.SetColor("_1st_ShadeColor", new Color(c.r * ShadeFactor, c.g * ShadeFactor, c.b * ShadeFactor, 1f));
            if (m.HasProperty("_2nd_ShadeColor")) m.SetColor("_2nd_ShadeColor", new Color(c.r * ShadeFactor * ShadeFactor, c.g * ShadeFactor * ShadeFactor, c.b * ShadeFactor * ShadeFactor, 1f));
            // Cor da peça igual à textura/cor original na parte iluminada: sem multiplicar pela intensidade da luz
            // direcional (2,0 na sala), que saturava as cores (planta e vaso "lavados").
            Set(m, "_Is_LightColor_Base", 0f);
            Set(m, "_Is_LightColor_1st_Shade", 0f);
            Set(m, "_Is_LightColor_2nd_Shade", 0f);
            Set(m, "_GI_Intensity", 0f);
            // Luz de borda branca e larga (_RimLight_Power 0,1): halo claro nas peças pretas (a "luz estranha" perto do
            // monitor, na cadeira). O URP/Lit anterior não tinha esse efeito.
            Set(m, "_RimLight", 0f);
            EditorUtility.SetDirty(m);
            Debug.Log("ToonShaderSetup: sombra corrigida em " + m.name);
        }
        AssetDatabase.SaveAssets();
    }

    private static void Set(Material m, string p, float v) { if (m.HasProperty(p)) m.SetFloat(p, v); }
}
