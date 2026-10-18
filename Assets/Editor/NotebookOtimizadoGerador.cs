using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Gera <c>Notebook_Toon_Otimizado.prefab</c> a partir do <c>Notebook_Toon.prefab</c> SEM alterar o original
/// (o original só é lido; nada é salvo nele). O notebook atual tem ~224 renderers e ~170 mil vértices porque cada
/// peça é um RoundedBox de 600 vértices, de qualquer tamanho.
///  1. Malhas: toda malha RoundedBox (600 vértices) com menos de <see cref="Pequena"/> m vira uma caixa simples de 24
///     vértices com os MESMOS bounds; o teclado (77 RoundedBox juntados) vira 77 caixas, cada uma com os bounds da tecla.
///  2. Junção: renderers de detalhe (sem nome usado pelos scripts) são juntados por material DENTRO do mesmo pai
///     protegido (o ancestral mais próximo cujo nome os scripts usam, ou a raiz). Assim a subárvore de cada peça
///     nomeada continua com a mesma geometria: bounds de enquadramento e dos pontos clicáveis da tela 2D não mudam,
///     SetActive/forceRenderingOff dos grupos (TampaInferior, Interior, RAM, SSD, parafusos, pendrive) continuam valendo.
///     Projeção de sombra e recepção são preservadas (um objeto por modo de sombra).
///  3. Validação antes de salvar: nomes, posições locais, MeshFilters e bounds (em relação à raiz) das peças usadas
///     pelo NotebookModeloEstado/NotebookTela iguais ao original (tolerância 1 mm); campos do NotebookModeloEstado iguais.
///     Se algo falhar, NADA é salvo. Também não sobrescreve: se o prefab ou a pasta de malhas já existirem, para.
/// Materiais: os MESMOS do original (não são copiados nem alterados). Relatório: Logs/NotebookOtimizado.txt.
/// </summary>
public static class NotebookOtimizadoGerador
{
    private const string Original = "Assets/Prefabs/Props/Notebook_Toon.prefab";
    private const string Destino = "Assets/Prefabs/Props/Notebook_Toon_Otimizado.prefab";
    private const string PastaMalhas = "Assets/Art/Meshes/NotebookOtimizado";
    private const float Pequena = 0.03f;
    private const int VertsRoundedBox = 600;

    // nomes que os scripts procuram (NotebookModeloEstado.Awake/Parte e NotebookTela: vistas, enquadramento, alvos)
    private static readonly string[] Usados =
    {
        "Base", "Tela", "LedEnergia", "TampaInferior", "RAM_Instalada", "SSD_Instalado", "Pendrive",
        "Parafuso_0", "Parafuso_1", "Parafuso_2", "Parafuso_3", "Parafuso_4", "Parafuso_5",
        "Tampa", "Dobradica", "SlotRAM", "SlotSSD", "Porta_USB", "BotaoPower", "Interior",
    };
    // peças cujo renderer é pintado por MaterialPropertyBlock: a subárvore inteira fica como está
    private static readonly string[] Intocaveis = { "Tela", "LedEnergia" };

    [MenuItem("Tools/E-stagiar.io/Notebook: gerar versão otimizada (não altera o original)")]
    public static void Gerar()
    {
        var log = new StringBuilder();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Destino) != null || AssetDatabase.IsValidFolder(PastaMalhas))
        {
            Debug.LogError($"NotebookOtimizado: {Destino} ou {PastaMalhas} já existe; nada foi feito (não sobrescreve).");
            return;
        }
        GameObject orig = PrefabUtility.LoadPrefabContents(Original);
        GameObject novo = PrefabUtility.LoadPrefabContents(Original);   // cópia de trabalho; o arquivo original nunca é salvo
        var criadas = new List<Mesh>();
        bool salvo = false;
        try
        {
            log.AppendLine("ANTES: " + Contagem(orig));
            int trocadas = Simplificar(novo, criadas, log);
            int juntados = Juntar(novo, criadas, log);
            log.AppendLine($"malhas simplificadas: {trocadas}; renderers juntados: {juntados}");
            log.AppendLine("DEPOIS: " + Contagem(novo));

            List<string> erros = Validar(orig, novo, log);
            if (erros.Count > 0)
            {
                log.AppendLine("VALIDAÇÃO FALHOU, nada salvo:\n  " + string.Join("\n  ", erros));
                Debug.LogError(log.ToString());
                return;
            }
            AssetDatabase.CreateFolder("Assets/Art/Meshes", "NotebookOtimizado");
            foreach (Mesh m in criadas) AssetDatabase.CreateAsset(m, $"{PastaMalhas}/{m.name}.asset");
            novo.name = "Notebook_Toon_Otimizado";
            PrefabUtility.SaveAsPrefabAsset(novo, Destino);
            AssetDatabase.SaveAssets();
            salvo = true;
            log.AppendLine($"SALVO: {Destino} + {criadas.Count} malhas em {PastaMalhas}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(orig);
            PrefabUtility.UnloadPrefabContents(novo);
            if (!salvo) foreach (Mesh m in criadas) if (m != null && !AssetDatabase.Contains(m)) Object.DestroyImmediate(m);
            System.IO.Directory.CreateDirectory("Logs");
            System.IO.File.WriteAllText("Logs/NotebookOtimizado.txt", log.ToString());
        }
        Debug.Log(log.ToString());
    }

    // ------------------------------------------------------------------ 1. malhas

    private static int Simplificar(GameObject raiz, List<Mesh> criadas, StringBuilder log)
    {
        var cache = new Dictionary<Mesh, Mesh>();
        int n = 0;
        foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh m = mf.sharedMesh;
            if (m == null || DentroDe(mf.transform, Intocaveis)) continue;
            if (!cache.TryGetValue(m, out Mesh troca))
            {
                troca = null;
                if (m.name == "Notebook_Teclado" && m.vertexCount % VertsRoundedBox == 0) troca = TecladoEmCaixas(m);
                else if (m.vertexCount == VertsRoundedBox && m.subMeshCount == 1 && MaiorMedida(mf) < Pequena) troca = Caixa(m.name + "_Simples", m.bounds);
                if (troca != null) { criadas.Add(troca); log.AppendLine($"  malha {m.name} ({m.vertexCount} v) -> {troca.name} ({troca.vertexCount} v)"); }
                cache[m] = troca;
            }
            if (troca != null) { mf.sharedMesh = troca; n++; }
        }
        return n;
    }

    private static float MaiorMedida(MeshFilter mf)
    {
        Vector3 s = Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.lossyScale);
        return Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
    }

    // CombineMeshes empilha as teclas em ordem: cada bloco de 600 vértices é uma tecla; a caixa nova tem os bounds dela.
    private static Mesh TecladoEmCaixas(Mesh teclado)
    {
        Vector3[] v = teclado.vertices;
        var partes = new List<CombineInstance>();
        for (int i = 0; i < v.Length; i += VertsRoundedBox)
        {
            var b = new Bounds(v[i], Vector3.zero);
            for (int k = i; k < i + VertsRoundedBox; k++) b.Encapsulate(v[k]);
            partes.Add(new CombineInstance { mesh = Caixa("tecla", b), transform = Matrix4x4.identity });
        }
        var m = new Mesh { name = "Notebook_Teclado_Simples" };
        m.CombineMeshes(partes.ToArray(), true, true);
        foreach (CombineInstance c in partes) Object.DestroyImmediate(c.mesh);
        m.RecalculateBounds();
        return m;
    }

    private static Mesh Caixa(string nome, Bounds b)
    {
        Vector3 c = b.center, h = b.extents;
        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        (Vector3 n, Vector3 a, Vector3 bb)[] faces =
        {
            (Vector3.right, Vector3.forward, Vector3.up), (Vector3.left, Vector3.back, Vector3.up),
            (Vector3.up, Vector3.right, Vector3.forward), (Vector3.down, Vector3.right, Vector3.back),
            (Vector3.forward, Vector3.left, Vector3.up), (Vector3.back, Vector3.right, Vector3.up),
        };
        foreach (var (n, a, bb) in faces)
        {
            Vector3 cc = Vector3.Scale(n, h), aa = Vector3.Scale(a, h), b2 = Vector3.Scale(bb, h);
            int i = verts.Count;
            verts.AddRange(new[] { c + cc - aa - b2, c + cc - aa + b2, c + cc + aa + b2, c + cc + aa - b2 });
            norms.AddRange(new[] { n, n, n, n });
            uvs.AddRange(new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
            tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        var m = new Mesh { name = nome };
        m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        m.RecalculateTangents();
        return m;
    }

    // ------------------------------------------------------------------ 2. junção

    private static int Juntar(GameObject raiz, List<Mesh> criadas, StringBuilder log)
    {
        var grupos = new Dictionary<(Transform pai, ShadowCastingMode sombra, bool recebe), List<MeshRenderer>>();
        foreach (MeshRenderer r in raiz.GetComponentsInChildren<MeshRenderer>(true))
        {
            Transform t = r.transform;
            if (!Juntavel(t, raiz.transform)) continue;
            Transform pai = AncestralProtegido(t, raiz.transform);
            var chave = (pai, r.shadowCastingMode, r.receiveShadows);
            if (!grupos.TryGetValue(chave, out var lista)) grupos[chave] = lista = new List<MeshRenderer>();
            lista.Add(r);
        }
        int total = 0;
        foreach (var kv in grupos)
        {
            if (kv.Value.Count < 2) continue;   // nada a ganhar
            Transform pai = kv.Key.pai;
            var porMaterial = kv.Value.GroupBy(r => r.sharedMaterial).ToList();
            var submalhas = new List<Mesh>();
            foreach (var g in porMaterial)
            {
                var ci = g.Select(r => new CombineInstance { mesh = r.GetComponent<MeshFilter>().sharedMesh, transform = pai.worldToLocalMatrix * r.transform.localToWorldMatrix }).ToArray();
                var sm = new Mesh { indexFormat = IndexFormat.UInt32 };
                sm.CombineMeshes(ci, true, true);
                submalhas.Add(sm);
            }
            var junta = new Mesh { name = $"NbOt_{Caminho(pai, raiz.transform).Replace('/', '_')}_{(kv.Key.sombra == ShadowCastingMode.Off ? "SemSombra" : "Sombra")}", indexFormat = IndexFormat.UInt32 };
            junta.CombineMeshes(submalhas.Select(s => new CombineInstance { mesh = s, transform = Matrix4x4.identity }).ToArray(), false, false);
            foreach (Mesh s in submalhas) Object.DestroyImmediate(s);
            junta.RecalculateBounds();
            criadas.Add(junta);

            var go = new GameObject(kv.Key.sombra == ShadowCastingMode.Off ? "Detalhes" : "Detalhes_Sombra");
            go.transform.SetParent(pai, false);
            go.AddComponent<MeshFilter>().sharedMesh = junta;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = porMaterial.Select(g => g.Key).ToArray();
            mr.shadowCastingMode = kv.Key.sombra;
            mr.receiveShadows = kv.Key.recebe;
            log.AppendLine($"  {Caminho(pai, raiz.transform)}: {kv.Value.Count} renderers -> 1 ({porMaterial.Count} submalhas, sombra {kv.Key.sombra})");

            foreach (MeshRenderer r in kv.Value)
            {
                GameObject alvo = r.gameObject;
                Object.DestroyImmediate(r.GetComponent<MeshFilter>());
                Object.DestroyImmediate(r);
                Remover(alvo, raiz.transform);
                total++;
            }
        }
        return total;
    }

    // Renderer de detalhe: nem ele nem nenhum filho tem nome usado; não está em peça pintada por bloco de propriedades;
    // só tem Transform + MeshFilter + MeshRenderer.
    private static bool Juntavel(Transform t, Transform raiz)
    {
        if (t == raiz || Usados.Contains(t.name) || DentroDe(t, Intocaveis)) return false;
        if (t.GetComponentsInChildren<Transform>(true).Any(f => f != t && Usados.Contains(f.name))) return false;
        return t.GetComponents<Component>().All(c => c is Transform || c is MeshFilter || c is MeshRenderer);
    }

    private static Transform AncestralProtegido(Transform t, Transform raiz)
    {
        for (Transform p = t.parent; p != null; p = p.parent)
            if (p == raiz || Usados.Contains(p.name)) return p;
        return raiz;
    }

    // apaga o objeto que ficou vazio (sem componentes nem filhos) e sobe enquanto o pai também ficar vazio
    private static void Remover(GameObject go, Transform raiz)
    {
        while (go != null && go.transform != raiz && !Usados.Contains(go.name) && go.transform.childCount == 0 &&
               go.GetComponents<Component>().Length == 1)
        {
            Transform pai = go.transform.parent;
            Object.DestroyImmediate(go);
            go = pai != null ? pai.gameObject : null;
        }
    }

    private static bool DentroDe(Transform t, string[] nomes)
    {
        for (Transform p = t; p != null; p = p.parent) if (nomes.Contains(p.name)) return true;
        return false;
    }

    // ------------------------------------------------------------------ 3. validação e contagem

    private static List<string> Validar(GameObject orig, GameObject novo, StringBuilder log)
    {
        var erros = new List<string>();
        foreach (string nome in Usados)
        {
            Transform a = Achar(orig.transform, nome), b = Achar(novo.transform, nome);
            if (a == null) { log.AppendLine($"  (aviso) {nome} não existe no original"); continue; }
            if (b == null) { erros.Add($"{nome} sumiu"); continue; }
            if (Caminho(a, orig.transform) != Caminho(b, novo.transform)) erros.Add($"{nome}: caminho {Caminho(a, orig.transform)} -> {Caminho(b, novo.transform)}");
            if ((a.localPosition - b.localPosition).magnitude > 1e-4f || Quaternion.Angle(a.localRotation, b.localRotation) > 0.01f || (a.localScale - b.localScale).magnitude > 1e-4f)
                erros.Add($"{nome}: transform local mudou");
            bool mfA = a.GetComponentsInChildren<MeshFilter>(true).Length > 0, mfB = b.GetComponentsInChildren<MeshFilter>(true).Length > 0;
            if (mfA != mfB) erros.Add($"{nome}: MeshFilter {mfA} -> {mfB}");
            if (a.GetComponent<MeshFilter>() != null && b.GetComponent<MeshFilter>() == null) erros.Add($"{nome}: perdeu o próprio MeshFilter");
            if (a.gameObject.activeSelf != b.gameObject.activeSelf) erros.Add($"{nome}: ativo mudou");
            Bounds ba = BoundsLocal(a, orig.transform), bb = BoundsLocal(b, novo.transform);
            float d = Mathf.Max((ba.min - bb.min).magnitude, (ba.max - bb.max).magnitude);
            log.AppendLine($"  {nome}: bounds Δ {d * 1000f:0.000} mm, renderers {a.GetComponentsInChildren<Renderer>(true).Length} -> {b.GetComponentsInChildren<Renderer>(true).Length}");
            if (d > 0.001f) erros.Add($"{nome}: bounds mudaram {d * 1000f:0.0} mm");
        }
        // a cópia da tela 2D procura o primeiro Porta_USB por nome; os dois continuam existindo?
        int usbA = orig.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Porta_USB"), usbB = novo.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Porta_USB");
        if (usbA != usbB) erros.Add($"Porta_USB: {usbA} -> {usbB}");
        // NotebookModeloEstado com os mesmos campos (texturas e cores)
        var ea = orig.GetComponent<NotebookModeloEstado>(); var eb = novo.GetComponent<NotebookModeloEstado>();
        if (ea == null || eb == null) erros.Add("NotebookModeloEstado ausente");
        else if (EditorJsonUtility.ToJson(ea) != EditorJsonUtility.ToJson(eb)) erros.Add("NotebookModeloEstado: campos diferentes");
        // componentes não visuais preservados
        var compA = orig.GetComponentsInChildren<Component>(true).Where(c => !(c is Transform || c is MeshFilter || c is MeshRenderer)).Select(c => c.GetType().Name).OrderBy(s => s);
        var compB = novo.GetComponentsInChildren<Component>(true).Where(c => !(c is Transform || c is MeshFilter || c is MeshRenderer)).Select(c => c.GetType().Name).OrderBy(s => s);
        if (!compA.SequenceEqual(compB)) erros.Add($"outros componentes mudaram: [{string.Join(",", compA)}] -> [{string.Join(",", compB)}]");
        // materiais: nenhum novo, nenhum a menos
        var ma = new HashSet<Material>(orig.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials));
        var mb = new HashSet<Material>(novo.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials));
        if (!ma.SetEquals(mb)) erros.Add("conjunto de materiais mudou");
        return erros;
    }

    // mesmo cálculo do ModeloNaTela.Cantos/BoundsLocal (MeshFilters da subárvore, incluindo inativos), no espaço da raiz
    private static Bounds BoundsLocal(Transform alvo, Transform raiz)
    {
        var b = new Bounds(); bool tem = false;
        foreach (MeshFilter mf in alvo.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Bounds mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = raiz.InverseTransformPoint(mf.transform.TransformPoint(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1))));
                if (!tem) { b = new Bounds(p, Vector3.zero); tem = true; } else b.Encapsulate(p);
            }
        }
        return b;
    }

    private static string Contagem(GameObject r)
    {
        var rs = r.GetComponentsInChildren<Renderer>(true);
        int verts = rs.Sum(x => x.TryGetComponent(out MeshFilter f) && f.sharedMesh != null ? f.sharedMesh.vertexCount : 0);
        int sub = rs.Sum(x => x.TryGetComponent(out MeshFilter f) && f.sharedMesh != null ? f.sharedMesh.subMeshCount : 0);
        int mats = rs.SelectMany(x => x.sharedMaterials).Distinct().Count();
        return $"objetos {r.GetComponentsInChildren<Transform>(true).Length}, renderers {rs.Length}, submalhas {sub}, materiais {mats}, vértices {verts}, " +
               $"projetam sombra {rs.Count(x => x.shadowCastingMode != ShadowCastingMode.Off)}";
    }

    private static Transform Achar(Transform raiz, string nome)
    {
        if (raiz.name == nome) return raiz;
        foreach (Transform f in raiz) { Transform r = Achar(f, nome); if (r != null) return r; }
        return null;
    }

    private static string Caminho(Transform t, Transform raiz)
    {
        if (t == raiz) return "(raiz)";
        string c = t.name;
        for (Transform p = t.parent; p != null && p != raiz; p = p.parent) c = p.name + "/" + c;
        return c;
    }
}
