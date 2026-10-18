using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Decoração das estações do escritório (só visual, sem lógica, sem colisor).
///  - <see cref="GerarModelos"/>: malhas low-poly geradas por código (o Blender não está disponível no ambiente),
///    UMA malha e UM renderer por objeto, todas com o MESMO material toon (M_Decoracao) e a mesma textura
///    (T_Decoracao_Atlas: paleta de cores + post-its com texto, desenhados aqui com uma fonte de pixels própria).
///    Prefabs em Prefabs/Props/Decoracao (root + filho Visual), sem sombra projetada (recebem sombra), estáticos.
///  - <see cref="Posicionar"/>: põe composições variadas nas mesas e post-its nas divisórias da Office_Placeholder,
///    dentro de UM grupo novo ("Decoracao_Estacoes"); nada mais da cena é alterado. Rodar de novo recria só esse grupo.
///    Cada peça é apoiada no tampo por raycast e recusada se encostar em notebook, xícara, plaquinha, PC, nas áreas
///    onde os cabos caem ao lado do gabinete ou em decorações já existentes (inclusive as do Matheus).
/// Relatório: Logs/DecoracaoEscritorio.txt.
/// </summary>
public static class DecoracaoEscritorioSetup
{
    private const string Pasta = "Assets/Art/Meshes/Decoracao";
    private const string PastaPrefabs = "Assets/Prefabs/Props/Decoracao";
    private const string TexPath = "Assets/Art/Textures/Decoracao/T_Decoracao_Atlas.png";
    private const string MatPath = "Assets/Art/Materials/Decoracao/M_Decoracao.mat";
    private const string BaseToon = "Assets/Art/Materials/M_Character_Tex1_1.mat";
    private const string Cena = "Assets/Scenes/Office_Placeholder.unity";
    private const string Grupo = "Decoracao_Estacoes";

    // ------------------------------------------------------------------ paleta (célula 16x16 px, 16 por linha)
    private static readonly Color[] Paleta =
    {
        C("1b1d22"), C("f4f1e8"), C("d9443b"), C("2f6fd6"), C("f2c230"), C("3fa65a"), C("f08a2c"), C("c96f4a"),   // 0-7
        C("7a4a2b"), C("4f9e3f"), C("2f6b34"), C("8cc56a"), C("e86aa0"), C("6fc2c9"), C("9b6bd1"), C("f7e7c4"),   // 8-15
        C("b8bcc6"), C("5b6170"), C("fff176"), C("ff9ec7"), C("a5e3a0"), C("9fd3ff"), C("f6d36b"), C("e8ecef"),   // 16-23
        C("8fd1c0"), C("3a3f4b"), C("ffb35c"), C("6b4a9e"), C("d7c4a3"), C("87b8e8"), C("c2e3a7"), C("f0f0f0"),   // 24-31
    };
    private const int Preto = 0, Branco = 1, Vermelho = 2, Azul = 3, Amarelo = 4, Verde = 5, Laranja = 6, Terracota = 7,
        Terra = 8, Folha = 9, FolhaEscura = 10, FolhaClara = 11, Rosa = 12, Turquesa = 13, Roxo = 14, Creme = 15,
        Metal = 16, Grafite = 17, PostAmarelo = 18, PostRosa = 19, PostVerde = 20, PostAzul = 21, Madeira = 22, Gelo = 23,
        Menta = 24, Escuro = 25, Pessego = 26, RoxoEscuro = 27, Areia = 28, Ceu = 29, Grama = 30, Papel = 31;

    private const int Tam = 512, Cel = 16, NotaPx = 128, NotaY0 = 64;   // paleta em y 0-63; post-its 128 px, 4 por linha

    private static readonly (string texto, int cor)[] Notas =
    {
        ("BACKUP", PostAmarelo), ("CTRL+S", PostRosa), ("PING:\nOK", PostVerde), ("SENHA\nFORTE", PostAzul),
        ("TICKET\n#42", PostAmarelo), ("REBOOT?", PostVerde), ("RAM\n8GB", PostRosa), ("LOGOFF\n18H", PostAzul),
    };

    private static Color C(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out Color c); return c; }

    // ================================================================== modelos

    [MenuItem("Tools/E-stagiar.io/Decoração: gerar modelos")]
    public static void GerarModelos()
    {
        Pastas();
        Texture2D tex = GerarTextura();
        Material mat = GerarMaterial(tex);
        foreach (var (nome, malha) in Modelos()) Prefab(nome, Salvar(malha, nome), mat);
        AssetDatabase.SaveAssets();
        Debug.Log("Decoração: modelos gerados.");
    }

    private static IEnumerable<(string, Mesh)> Modelos()
    {
        yield return ("DEC_CuboMagico", CuboMagico());
        yield return ("DEC_Suculenta", Suculenta());
        yield return ("DEC_Cacto", Cacto());
        yield return ("DEC_PortaRetrato", PortaRetrato());
        yield return ("DEC_PortaCanetas", PortaCanetas());
        yield return ("DEC_Caderno", Caderno());
        yield return ("DEC_BlocoPostit", BlocoPostit());
        yield return ("DEC_Relogio", Relogio());
        yield return ("DEC_Patinho", Patinho());
        for (int i = 0; i < Notas.Length; i++) yield return ("DEC_Postit_" + i, Postit(i));
    }

    private static Mesh CuboMagico()
    {
        var m = new MB();
        const float a = 0.057f, s = a / 3f, g = 0.0016f;
        m.Box(new Vector3(0, a / 2, 0), Vector3.one * a, Quaternion.identity, Preto);
        int[] cores = { Vermelho, Azul, Amarelo, Verde, Laranja, Branco };
        var rnd = new System.Random(7);
        // 5 faces à vista (a de baixo fica na mesa): 3x3 adesivos levemente salientes
        (Vector3 n, Vector3 u, Vector3 v)[] faces =
        {
            (Vector3.up, Vector3.right, Vector3.forward), (Vector3.forward, Vector3.right, Vector3.up),
            (Vector3.back, Vector3.left, Vector3.up), (Vector3.right, Vector3.back, Vector3.up), (Vector3.left, Vector3.forward, Vector3.up),
        };
        foreach (var f in faces)
            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                {
                    Vector3 c = new Vector3(0, a / 2, 0) + f.n * (a / 2 + 0.0004f) + f.u * (i * s) + f.v * (j * s);
                    Quaternion r = Quaternion.LookRotation(f.n, f.v == Vector3.up ? Vector3.up : f.v);
                    m.Box(c, new Vector3(s - g * 2, s - g * 2, 0.0012f), r, cores[rnd.Next(cores.Length)]);
                }
        return m.Mesh("DEC_CuboMagico");
    }

    private static Mesh Suculenta()
    {
        var m = new MB();
        m.Cyl(new Vector3(0, 0.025f, 0), 0.036f, 0.032f, 0.05f, 10, Terracota);              // vaso (afunila para baixo)
        m.Cyl(new Vector3(0, 0.053f, 0), 0.04f, 0.04f, 0.008f, 10, Terracota);               // borda
        m.Cyl(new Vector3(0, 0.0555f, 0), 0.034f, 0.034f, 0.006f, 10, Terra);                // terra (acima da borda)
        for (int anel = 0; anel < 3; anel++)
        {
            int n = 7 - anel * 2;
            float incl = 62f - anel * 18f, comp = 0.034f - anel * 0.007f;
            for (int i = 0; i < n; i++)
            {
                Quaternion r = Quaternion.Euler(0, i * 360f / n + anel * 25f, 0) * Quaternion.Euler(-incl, 0, 0);
                Vector3 c = new Vector3(0, 0.059f + anel * 0.006f, 0) + r * new Vector3(0, 0, comp * 0.5f);
                m.Box(c, new Vector3(0.014f - anel * 0.002f, 0.006f, comp), r, anel == 0 ? FolhaEscura : anel == 1 ? Folha : FolhaClara);
            }
        }
        return m.Mesh("DEC_Suculenta");
    }

    private static Mesh Cacto()
    {
        var m = new MB();
        m.Cyl(new Vector3(0, 0.027f, 0), 0.032f, 0.028f, 0.054f, 10, Branco);
        m.Cyl(new Vector3(0, 0.0525f, 0), 0.029f, 0.029f, 0.006f, 10, Terra);
        m.Cyl(new Vector3(0, 0.095f, 0), 0.017f, 0.017f, 0.085f, 8, Folha);                  // tronco
        m.Sphere(new Vector3(0, 0.138f, 0), 0.017f, 8, 5, Folha);
        m.Cyl(new Vector3(0.022f, 0.092f, 0), 0.008f, 0.008f, 0.012f, 6, Folha, Quaternion.Euler(0, 0, 90));   // braço
        m.Cyl(new Vector3(0.03f, 0.105f, 0), 0.008f, 0.008f, 0.03f, 6, Folha);
        m.Sphere(new Vector3(0.03f, 0.12f, 0), 0.008f, 6, 4, Folha);
        m.Sphere(new Vector3(0, 0.157f, 0), 0.007f, 6, 4, Rosa);                             // florzinha
        return m.Mesh("DEC_Cacto");
    }

    private static Mesh PortaRetrato()
    {
        var m = new MB();
        Quaternion t = Quaternion.Euler(-12f, 0, 0);                                          // inclinado para trás
        Vector3 c = new Vector3(0, 0.052f, 0.004f);
        m.Box(c, new Vector3(0.13f, 0.1f, 0.012f), t, Madeira);
        m.Box(c + t * new Vector3(0, 0, 0.0065f), new Vector3(0.108f, 0.078f, 0.002f), t, Ceu);           // foto: céu
        m.Box(c + t * new Vector3(0, -0.022f, 0.0078f), new Vector3(0.108f, 0.034f, 0.002f), t, Grama);  // morro
        m.Box(c + t * new Vector3(0.03f, 0.02f, 0.0078f), new Vector3(0.016f, 0.016f, 0.002f), t, Amarelo); // sol
        m.Box(c + t * new Vector3(-0.022f, -0.005f, 0.0088f), new Vector3(0.012f, 0.026f, 0.002f), t, Vermelho); // pessoinha
        m.Box(c + t * new Vector3(-0.022f, 0.012f, 0.0088f), new Vector3(0.01f, 0.01f, 0.002f), t, Pessego);
        m.Box(new Vector3(0, 0.035f, -0.022f), new Vector3(0.02f, 0.07f, 0.006f), Quaternion.Euler(28f, 0, 0), Madeira); // pé
        return m.Mesh("DEC_PortaRetrato");
    }

    private static Mesh PortaCanetas()
    {
        var m = new MB();
        m.Cyl(new Vector3(0, 0.045f, 0), 0.034f, 0.034f, 0.09f, 10, Turquesa);
        m.Cyl(new Vector3(0, 0.0905f, 0), 0.03f, 0.03f, 0.002f, 10, Escuro);                 // boca
        (Vector3 p, Vector3 e, int cor, int ponta)[] canetas =
        {
            (new Vector3(-0.01f, 0.1f, 0.006f), new Vector3(-9, 0, 8), Azul, Branco),
            (new Vector3(0.011f, 0.1f, -0.004f), new Vector3(7, 0, -10), Vermelho, Branco),
            (new Vector3(0.002f, 0.105f, 0.012f), new Vector3(12, 0, 3), Amarelo, Rosa),
            (new Vector3(-0.004f, 0.098f, -0.013f), new Vector3(-11, 0, -4), Preto, Metal),
        };
        foreach (var k in canetas)
        {
            Quaternion r = Quaternion.Euler(k.e);
            m.Cyl(k.p, 0.0045f, 0.0045f, 0.12f, 6, k.cor, r);
            m.Cyl(k.p + r * new Vector3(0, 0.064f, 0), 0.0047f, 0.0047f, 0.012f, 6, k.ponta, r);
        }
        return m.Mesh("DEC_PortaCanetas");
    }

    private static Mesh Caderno()
    {
        var m = new MB();
        m.Box(new Vector3(0, 0.0015f, 0), new Vector3(0.152f, 0.003f, 0.212f), Quaternion.identity, Roxo);   // capa de baixo
        m.Box(new Vector3(0.002f, 0.0085f, 0), new Vector3(0.144f, 0.011f, 0.204f), Quaternion.identity, Creme); // folhas
        m.Box(new Vector3(0, 0.0155f, 0), new Vector3(0.152f, 0.003f, 0.212f), Quaternion.identity, Roxo);   // capa de cima
        m.Box(new Vector3(0.05f, 0.0175f, 0), new Vector3(0.008f, 0.001f, 0.212f), Quaternion.identity, Escuro); // elástico
        m.Box(new Vector3(-0.02f, 0.0172f, 0.05f), new Vector3(0.07f, 0.0008f, 0.03f), Quaternion.identity, Papel); // etiqueta
        return m.Mesh("DEC_Caderno");
    }

    private static Mesh BlocoPostit()
    {
        var m = new MB();
        m.Box(new Vector3(0, 0.007f, 0), new Vector3(0.076f, 0.014f, 0.076f), Quaternion.identity, PostAmarelo);
        m.Box(new Vector3(0.004f, 0.0145f, 0.003f), new Vector3(0.076f, 0.001f, 0.076f), Quaternion.Euler(0, 6, 0), PostRosa);
        return m.Mesh("DEC_BlocoPostit");
    }

    private static Mesh Relogio()
    {
        var m = new MB();
        Quaternion pe = Quaternion.Euler(90, 0, 0);                                           // disco em pé, face para +Z
        Vector3 c = new Vector3(0, 0.058f, 0);
        m.Cyl(c, 0.046f, 0.046f, 0.032f, 14, Menta, pe);
        m.Cyl(c + new Vector3(0, 0, 0.0165f), 0.038f, 0.038f, 0.002f, 14, Gelo, pe);
        m.Box(c + new Vector3(0, 0.011f, 0.019f), new Vector3(0.004f, 0.024f, 0.0015f), Quaternion.identity, Preto);
        m.Box(c + new Vector3(0.012f, 0.004f, 0.019f), new Vector3(0.026f, 0.003f, 0.0015f), Quaternion.Euler(0, 0, 18), Preto);
        foreach (float x in new[] { -0.026f, 0.026f })
        {
            m.Sphere(c + new Vector3(x, 0.046f, -0.004f), 0.012f, 8, 4, Menta);                 // sininhos
            m.Cyl(new Vector3(x * 0.9f, 0.007f, 0), 0.005f, 0.004f, 0.014f, 6, Escuro);       // pés
        }
        return m.Mesh("DEC_Relogio");
    }

    private static Mesh Patinho()
    {
        var m = new MB();
        m.Sphere(new Vector3(0, 0.024f, -0.004f), 0.027f, 10, 6, Amarelo, new Vector3(1f, 0.85f, 1.15f));
        m.Sphere(new Vector3(0, 0.055f, 0.012f), 0.017f, 10, 6, Amarelo);
        m.Box(new Vector3(0, 0.052f, 0.032f), new Vector3(0.016f, 0.006f, 0.014f), Quaternion.identity, Laranja);
        foreach (float x in new[] { -0.008f, 0.008f }) m.Sphere(new Vector3(x, 0.061f, 0.026f), 0.0028f, 6, 4, Preto);
        m.Box(new Vector3(0, 0.035f, -0.034f), new Vector3(0.014f, 0.01f, 0.014f), Quaternion.Euler(-35, 0, 0), Amarelo); // rabinho
        return m.Mesh("DEC_Patinho");
    }

    private static Mesh Postit(int i)
    {
        var m = new MB();
        int col = i % 4, lin = i / 4;
        var rect = new Rect((col * NotaPx + 3f) / Tam, (NotaY0 + lin * NotaPx + 3f) / Tam, (NotaPx - 6f) / Tam, (NotaPx - 6f) / Tam);
        // origem no verso (encostado na divisória), frente = +Z
        m.Box(new Vector3(0, 0, 0.001f), new Vector3(0.076f, 0.076f, 0.002f), Quaternion.identity, Notas[i].cor, rect);
        return m.Mesh("DEC_Postit_" + i);
    }

    // ------------------------------------------------------------------ textura, material, prefab

    private static Texture2D GerarTextura()
    {
        var t = new Texture2D(Tam, Tam, TextureFormat.RGBA32, false);
        var px = new Color32[Tam * Tam];
        for (int i = 0; i < px.Length; i++) px[i] = Color.white;
        for (int k = 0; k < Paleta.Length; k++)
        {
            int x0 = (k % 16) * Cel, y0 = (k / 16) * Cel;
            for (int y = 0; y < Cel; y++) for (int x = 0; x < Cel; x++) px[(y0 + y) * Tam + x0 + x] = Paleta[k];
        }
        for (int i = 0; i < Notas.Length; i++)
        {
            int x0 = (i % 4) * NotaPx, y0 = NotaY0 + (i / 4) * NotaPx;
            Color fundo = Paleta[Notas[i].cor];
            for (int y = 0; y < NotaPx; y++) for (int x = 0; x < NotaPx; x++)
                px[(y0 + y) * Tam + x0 + x] = y > NotaPx - 20 ? (Color32)(fundo * 0.9f) : (Color32)fundo;   // faixa da cola
            Escrever(px, x0, y0, Notas[i].texto, Paleta[Escuro]);
        }
        t.SetPixels32(px);
        t.Apply();
        System.IO.File.WriteAllBytes(TexPath, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(TexPath);
        var imp = (TextureImporter)AssetImporter.GetAtPath(TexPath);
        imp.textureType = TextureImporterType.Default;
        imp.filterMode = FilterMode.Bilinear;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.mipmapEnabled = false;   // UVs das cores apontam o centro da célula: sem mip, nada vaza entre células
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
    }

    // Texto centralizado, fonte 5x7 própria em escala 2 (cada linha 16 px).
    private static void Escrever(Color32[] px, int x0, int y0, string texto, Color32 cor)
    {
        string[] linhas = texto.Split('\n');
        const int esc = 3, larg = 6 * esc, alt = 9 * esc;   // até 6 letras por linha
        int centro = y0 + (NotaPx - 20) / 2, total = linhas.Length * alt - (alt - 7 * esc);
        int topo = centro + total / 2;   // centro vertical (abaixo da faixa da cola)
        for (int l = 0; l < linhas.Length; l++)
        {
            string s = linhas[l];
            int x = x0 + (NotaPx - s.Length * larg + esc) / 2, yTop = topo - l * alt;
            foreach (char ch in s)
            {
                if (Fonte.TryGetValue(ch, out string[] g))
                    for (int r = 0; r < 7; r++) for (int c = 0; c < 5; c++)
                        if (g[r][c] == '#')
                            for (int dy = 0; dy < esc; dy++) for (int dx = 0; dx < esc; dx++)
                            {
                                int X = x + c * esc + dx, Y = yTop - r * esc - dy;
                                if (X >= 0 && X < Tam && Y >= 0 && Y < Tam) px[Y * Tam + X] = cor;
                            }
                x += larg;
            }
        }
    }

    private static Material GerarMaterial(Texture2D tex)
    {
        var src = AssetDatabase.LoadAssetAtPath<Material>(BaseToon);
        var m = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        bool novo = m == null;
        if (novo) m = src != null ? new Material(src) : new Material(Shader.Find("Toon/Toon"));
        foreach (string p in new[] { "_MainTex", "_BaseMap", "_1st_ShadeMap", "_2nd_ShadeMap" }) if (m.HasProperty(p)) m.SetTexture(p, tex);
        Cor(m, "_BaseColor", Color.white); Cor(m, "_Color", Color.white);
        Cor(m, "_1st_ShadeColor", new Color(0.82f, 0.82f, 0.88f)); Cor(m, "_2nd_ShadeColor", new Color(0.66f, 0.66f, 0.74f));
        if (m.HasProperty("_Outline_Width")) m.SetFloat("_Outline_Width", 0f);   // peças pequenas: contorno vira riscos
        m.SetShaderPassEnabled("SRPDefaultUnlit", true);
        m.enableInstancing = true;
        m.name = "M_Decoracao";
        if (novo) AssetDatabase.CreateAsset(m, MatPath); else EditorUtility.SetDirty(m);
        return m;
    }

    private static void Cor(Material m, string p, Color c) { if (m.HasProperty(p)) m.SetColor(p, c); }

    private static Mesh Salvar(Mesh m, string nome)
    {
        string path = $"{Pasta}/{nome}.asset";
        var velho = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (velho == null) { AssetDatabase.CreateAsset(m, path); return m; }
        EditorUtility.CopySerialized(m, velho);
        Object.DestroyImmediate(m);
        return velho;
    }

    private static void Prefab(string nome, Mesh malha, Material mat)
    {
        var root = new GameObject(nome);
        var vis = new GameObject("Visual");
        vis.transform.SetParent(root.transform, false);
        vis.AddComponent<MeshFilter>().sharedMesh = malha;
        var r = vis.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // desempenho (ver otimização dos notebooks)
        r.receiveShadows = true;
        GameObjectUtility.SetStaticEditorFlags(vis, StaticEditorFlags.BatchingStatic);
        PrefabUtility.SaveAsPrefabAsset(root, $"{PastaPrefabs}/{nome}.prefab");
        Object.DestroyImmediate(root);
    }

    private static void Pastas()
    {
        foreach (string p in new[] { Pasta, PastaPrefabs, System.IO.Path.GetDirectoryName(TexPath).Replace('\\', '/'), System.IO.Path.GetDirectoryName(MatPath).Replace('\\', '/') })
        {
            if (AssetDatabase.IsValidFolder(p)) continue;
            string pai = p.Substring(0, p.LastIndexOf('/')), nome = p.Substring(p.LastIndexOf('/') + 1);
            AssetDatabase.CreateFolder(pai, nome);
        }
    }

    // ================================================================== posicionamento

    private static readonly string[][] Composicoes =
    {
        new[] { "DEC_Suculenta", "DEC_CuboMagico" },
        new[] { "DEC_Caderno", "DEC_PortaCanetas" },
        new[] { "DEC_PortaRetrato", "DEC_Cacto" },
        new[] { "DEC_Relogio", "DEC_BlocoPostit" },
        new[] { "DEC_Patinho", "DEC_Caderno" },
        new[] { "DEC_PortaCanetas", "DEC_Suculenta", "DEC_BlocoPostit" },
        new[] { "DEC_CuboMagico", "DEC_PortaRetrato" },
        new[] { "DEC_Cacto", "DEC_Relogio" },
        new[] { "DEC_PortaRetrato", "DEC_Patinho", "DEC_PortaCanetas" },
    };

    [MenuItem("Tools/E-stagiar.io/Decoração: posicionar na Office_Placeholder")]
    public static void Posicionar()
    {
        var log = new StringBuilder();
        var cena = EditorSceneManager.OpenScene(Cena);
        foreach (GameObject r in cena.GetRootGameObjects()) if (r.name == Grupo) Object.DestroyImmediate(r);
        Physics.SyncTransforms();
        var grupo = new GameObject(Grupo);
        Transform mesas = new GameObject("Mesas").transform, divs = new GameObject("Divisorias").transform;
        mesas.SetParent(grupo.transform, false);
        divs.SetParent(grupo.transform, false);

        // tudo que já tem desenho na cena (fora o próprio tampo e as divisórias) é obstáculo para as decorações
        var ocupado = new List<Bounds>();
        Transform[] estacoes = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude)
            .Where(t => t.name.StartsWith("Estacao_") && t.name.Length == 10 && t.Find("Mesa") != null).OrderBy(t => t.name).ToArray();
        Collider[] divisorias = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude).Where(t => t.name == "Divisorias")
            .SelectMany(t => t.GetComponentsInChildren<Collider>()).ToArray();
        var divSet = new HashSet<Collider>(divisorias);

        int pecas = 0, notas = 0, recusas = 0;
        for (int e = 0; e < estacoes.Length; e++)
        {
            Transform est = estacoes[e];
            Transform mesa = est.Find("Mesa");
            Collider tampo = mesa.GetComponentInChildren<Collider>();
            if (tampo == null) { log.AppendLine($"{est.name}: mesa sem collider, pulada"); continue; }
            Transform cadeiraEst = est.Find("Cadeira");
            var obst = new List<Bounds>();
            foreach (Renderer r in est.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r.transform.IsChildOf(mesa)) continue;
                if (cadeiraEst != null && r.transform.IsChildOf(cadeiraEst)) continue;   // a cadeira fica fora do tampo
                Bounds b = r.bounds;
                bool notebook = r.GetComponentInParent<NotebookEstacao>() != null;
                b.Expand(notebook ? 0.3f : 0.06f);   // notebook: espaço para abrir, virar e usar
                obst.Add(b);
            }
            // onde os cabos caem ao lado do gabinete (PcTicketBridge.RemoveCable: +X do gabinete, 0,32 m + 0,09 m por cabo)
            foreach (ComputerCase gab in est.GetComponentsInChildren<ComputerCase>())
                for (int k = 0; k < 5; k++) obst.Add(new Bounds(gab.transform.position + gab.transform.right * (0.32f + k * 0.09f), new Vector3(0.22f, 0.4f, 0.22f)));

            Bounds tb = tampo.bounds;
            float topoY = tb.max.y;
            string[] comp = Composicoes[(e * 5 + 3) % Composicoes.Length];
            var rnd = new System.Random(1000 + e);
            Transform pai = new GameObject("Deco_" + est.name).transform;
            pai.SetParent(mesas, false);
            // lado da mesa longe da cadeira = "fundo" (perto da divisória)
            Transform cadeira = est.Find("Cadeira");
            Vector3 frente = cadeira != null ? Vector3.ProjectOnPlane(cadeira.position - tb.center, Vector3.up).normalized : -mesa.forward;
            Vector3 lado = Vector3.Cross(Vector3.up, frente);
            foreach (string nome in comp)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PastaPrefabs}/{nome}.prefab");
                if (prefab == null) continue;
                Bounds pb = BoundsDe(prefab);
                bool ok = false;
                for (int tent = 0; tent < 160 && !ok; tent++)
                {
                    float u = (float)rnd.NextDouble() * 2f - 1f, v = (float)rnd.NextDouble();
                    Vector3 p = tb.center + lado * (u * (tb.extents.magnitude * 0.62f)) - frente * (0.12f + v * 0.3f);
                    float yaw = Quaternion.LookRotation(frente).eulerAngles.y + (float)(rnd.NextDouble() * 50 - 25);
                    if (!Apoio(p, pb, yaw, tampo, out float y)) { recusas++; continue; }
                    Bounds w = Mundo(pb, new Vector3(p.x, y, p.z), yaw);
                    if (Mathf.Abs(y - topoY) > 0.02f || obst.Concat(ocupado).Any(o => o.Intersects(w))) { recusas++; continue; }
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pai);
                    inst.transform.SetPositionAndRotation(new Vector3(p.x, y, p.z), Quaternion.Euler(0, yaw, 0));
                    w.Expand(0.03f);
                    ocupado.Add(w);
                    pecas++;
                    ok = true;
                    log.AppendLine($"{est.name}: {nome} em {inst.transform.position:F3}");
                }
                if (!ok) log.AppendLine($"{est.name}: {nome} SEM LUGAR LIVRE");
            }

            // post-its na divisória à frente do funcionário (raio da cadeira para o fundo da mesa, na altura dos olhos)
            if (cadeira == null) continue;
            Vector3 o = new Vector3(tb.center.x, topoY + 0.27f, tb.center.z) + frente * 0.5f;
            int qtd = 1 + (e % 3 == 0 ? 1 : 0) + (e % 4 == 1 ? 1 : 0);
            float[] desloc = { -0.35f, 0.42f, 0.05f };
            for (int k = 0; k < qtd; k++)
            {
                Vector3 orig = o + lado * desloc[k] + Vector3.up * (k == 2 ? 0.1f : 0f);
                if (!Physics.Raycast(orig, -frente, out RaycastHit hit, 2.2f, ~0, QueryTriggerInteraction.Ignore) || !divSet.Contains(hit.collider)
                    || Vector3.Dot(hit.normal, frente) < 0.8f) { log.AppendLine($"{est.name}: post-it {k} sem divisória à frente"); continue; }
                int idx = (e * 3 + k * 5) % Notas.Length;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PastaPrefabs}/DEC_Postit_{idx}.prefab");
                Quaternion rot = Quaternion.LookRotation(hit.normal) * Quaternion.Euler(0, 0, (float)(rnd.NextDouble() * 14 - 7));
                Vector3 pos = hit.point + hit.normal * 0.0015f;
                var w = new Bounds(pos, new Vector3(0.1f, 0.1f, 0.1f));
                if (OutrosDesenhos(w, divSet, grupo.transform) || ocupado.Any(b => b.Intersects(w))) { log.AppendLine($"{est.name}: post-it {k} sobre decoração existente, pulado"); continue; }
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, divs);
                inst.name = $"Postit_{est.name}_{k}";
                inst.transform.SetPositionAndRotation(pos, rot);
                ocupado.Add(w);
                notas++;
            }
        }
        log.Insert(0, $"Decoração: {pecas} peças em {estacoes.Length} mesas, {notas} post-its; {recusas} posições recusadas (colisão/apoio)\n");
        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        System.IO.Directory.CreateDirectory("Logs");
        System.IO.File.WriteAllText("Logs/DecoracaoEscritorio.txt", log.ToString());
        Debug.Log(log.ToString().Split('\n')[0]);
    }

    private static bool OutrosDesenhos(Bounds w, HashSet<Collider> divs, Transform nosso)
    {
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            if (!r.enabled || r.transform.IsChildOf(nosso)) continue;
            if (divs.Any(d => r.transform.IsChildOf(d.transform) || d.transform.IsChildOf(r.transform))) continue;
            Bounds b = r.bounds;
            if (b.size.x > 1.2f || b.size.z > 1.2f) continue;   // móveis, paredes, piso (o volume da mesa cobre o espaço acima do tampo)
            if (b.Intersects(w)) return true;
        }
        return false;
    }

    // As 4 quinas e o centro da base precisam cair no MESMO tampo, na mesma altura.
    private static bool Apoio(Vector3 p, Bounds pb, float yaw, Collider tampo, out float y)
    {
        y = 0f;
        Quaternion r = Quaternion.Euler(0, yaw, 0);
        Vector3 e = pb.extents;
        Vector3[] pts = { Vector3.zero, new Vector3(e.x, 0, e.z), new Vector3(-e.x, 0, e.z), new Vector3(e.x, 0, -e.z), new Vector3(-e.x, 0, -e.z) };
        float? h = null;
        foreach (Vector3 d in pts)
        {
            Vector3 q = p + r * (d + new Vector3(pb.center.x, 0, pb.center.z));
            if (!tampo.Raycast(new Ray(new Vector3(q.x, 3f, q.z), Vector3.down), out RaycastHit hit, 4f) || hit.normal.y < 0.95f) return false;
            if (h != null && Mathf.Abs(h.Value - hit.point.y) > 0.005f) return false;
            h = hit.point.y;
        }
        y = h.Value;
        return true;
    }

    private static Bounds BoundsDe(GameObject prefab)
    {
        var mf = prefab.GetComponentInChildren<MeshFilter>();
        return mf != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one * 0.1f);
    }

    private static Bounds Mundo(Bounds local, Vector3 pos, float yaw)
    {
        Quaternion r = Quaternion.Euler(0, yaw, 0);
        var w = new Bounds(pos + r * local.center, Vector3.zero);
        for (int i = 0; i < 8; i++)
            w.Encapsulate(pos + r * new Vector3((i & 1) == 0 ? local.min.x : local.max.x, (i & 2) == 0 ? local.min.y : local.max.y, (i & 4) == 0 ? local.min.z : local.max.z));
        return w;
    }

    // ================================================================== construtor de malha (flat, UV = célula da paleta)

    private class MB
    {
        private readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
        private readonly List<Vector2> uv = new List<Vector2>();
        private readonly List<int> t = new List<int>();

        private static Vector2 UV(int cor) => new Vector2(((cor % 16) * Cel + Cel * 0.5f) / Tam, ((cor / 16) * Cel + Cel * 0.5f) / Tam);

        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 nn, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = v.Count;
            v.AddRange(new[] { a, b, c, d });
            n.AddRange(new[] { nn, nn, nn, nn });
            uv.AddRange(new[] { ua, ub, uc, ud });
            t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }

        public void Box(Vector3 c, Vector3 s, Quaternion r, int cor, Rect? frente = null)
        {
            Vector3 h = s * 0.5f;
            Vector2 u = UV(cor);
            (Vector3 nn, Vector3 a, Vector3 b)[] f =
            {
                (Vector3.forward, Vector3.right, Vector3.up), (Vector3.back, Vector3.left, Vector3.up),
                (Vector3.right, Vector3.back, Vector3.up), (Vector3.left, Vector3.forward, Vector3.up),
                (Vector3.up, Vector3.right, Vector3.back), (Vector3.down, Vector3.right, Vector3.forward),
            };
            foreach (var (nn, a, b) in f)
            {
                Vector3 cc = Vector3.Scale(nn, h), aa = Vector3.Scale(a, h), bb = Vector3.Scale(b, h);
                Vector3 p0 = c + r * (cc - aa - bb), p1 = c + r * (cc + aa - bb), p2 = c + r * (cc + aa + bb), p3 = c + r * (cc - aa + bb);
                if (frente.HasValue && nn == Vector3.forward)
                {
                    Rect q = frente.Value;
                    // visto de frente, o -X local fica à DIREITA: o texto não sai espelhado
                    Quad(p0, p1, p2, p3, r * nn, new Vector2(q.xMax, q.yMin), new Vector2(q.xMin, q.yMin), new Vector2(q.xMin, q.yMax), new Vector2(q.xMax, q.yMax));
                }
                else Quad(p0, p1, p2, p3, r * nn, u, u, u, u);
            }
        }

        /// <summary>Tronco de cone em pé (eixo Y local, girado por <paramref name="r"/>), centro em <paramref name="c"/>.</summary>
        public void Cyl(Vector3 c, float rTopo, float rBase, float alt, int seg, int cor, Quaternion? r = null)
        {
            Quaternion q = r ?? Quaternion.identity;
            Vector2 u = UV(cor);
            float y0 = -alt / 2, y1 = alt / 2;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 b0 = d0 * rBase + Vector3.up * y0, b1 = d1 * rBase + Vector3.up * y0, t0 = d0 * rTopo + Vector3.up * y1, t1 = d1 * rTopo + Vector3.up * y1;
                Vector3 nn = q * ((d0 + d1).normalized * alt + Vector3.up * (rBase - rTopo)).normalized;
                Quad(c + q * b1, c + q * b0, c + q * t0, c + q * t1, nn, u, u, u, u);
                Tri(c + q * (Vector3.up * y1), c + q * t1, c + q * t0, q * Vector3.up, u);
                Tri(c + q * (Vector3.up * y0), c + q * b0, c + q * b1, q * Vector3.down, u);
            }
        }

        public void Sphere(Vector3 c, float raio, int seg, int aneis, int cor, Vector3? escala = null)
        {
            Vector3 s = escala ?? Vector3.one;
            Vector2 u = UV(cor);
            for (int a = 0; a < aneis; a++)
            {
                float p0 = Mathf.PI * a / aneis - Mathf.PI / 2, p1 = Mathf.PI * (a + 1) / aneis - Mathf.PI / 2;
                for (int i = 0; i < seg; i++)
                {
                    float l0 = i * Mathf.PI * 2 / seg, l1 = (i + 1) * Mathf.PI * 2 / seg;
                    Vector3 P(float p, float l) => new Vector3(Mathf.Cos(p) * Mathf.Cos(l), Mathf.Sin(p), Mathf.Cos(p) * Mathf.Sin(l));
                    Vector3 q00 = P(p0, l0), q01 = P(p0, l1), q10 = P(p1, l0), q11 = P(p1, l1);
                    Vector3 nn = (q00 + q01 + q10 + q11).normalized;
                    Vector3 W(Vector3 d) => c + Vector3.Scale(d * raio, s);
                    Quad(W(q01), W(q00), W(q10), W(q11), Vector3.Scale(nn, new Vector3(1 / s.x, 1 / s.y, 1 / s.z)).normalized, u, u, u, u);
                }
            }
        }

        private void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 nn, Vector2 u)
        {
            int i = v.Count;
            v.AddRange(new[] { a, b, c });
            n.AddRange(new[] { nn, nn, nn });
            uv.AddRange(new[] { u, u, u });
            t.AddRange(new[] { i, i + 1, i + 2 });
        }

        public Mesh Mesh(string nome)
        {
            var m = new Mesh { name = nome };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }
    }

    // ================================================================== fonte 5x7 (própria)

    private static readonly Dictionary<char, string[]> Fonte = new Dictionary<char, string[]>
    {
        ['A'] = new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
        ['B'] = new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." },
        ['C'] = new[] { ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###." },
        ['D'] = new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." },
        ['E'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" },
        ['F'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." },
        ['G'] = new[] { ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".###." },
        ['H'] = new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
        ['I'] = new[] { ".###.", "..#..", "..#..", "..#..", "..#..", "..#..", ".###." },
        ['K'] = new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" },
        ['L'] = new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" },
        ['M'] = new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" },
        ['N'] = new[] { "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#" },
        ['O'] = new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
        ['P'] = new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." },
        ['R'] = new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" },
        ['S'] = new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." },
        ['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
        ['U'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
        ['0'] = new[] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." },
        ['1'] = new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." },
        ['2'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" },
        ['4'] = new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." },
        ['8'] = new[] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." },
        ['#'] = new[] { ".#.#.", ".#.#.", "#####", ".#.#.", "#####", ".#.#.", ".#.#." },
        ['+'] = new[] { ".....", "..#..", "..#..", "#####", "..#..", "..#..", "....." },
        [':'] = new[] { ".....", "..#..", "..#..", ".....", "..#..", "..#..", "....." },
        ['!'] = new[] { "..#..", "..#..", "..#..", "..#..", "..#..", ".....", "..#.." },
        ['?'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.." },
        [' '] = new[] { ".....", ".....", ".....", ".....", ".....", ".....", "....." },
    };

}
