using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// A divisa NÃO faz parte da estação de trabalho (a estação é só a mesa, o PC e o ponto elétrico).
/// Este comando: (1) guarda onde estão as divisas das estações na cena de teste integrado; (2) tira a divisa do prefab
/// <c>Estacao_Trabalho</c> (base de todas as estações); (3) recoloca essas divisas na cena como objetos SEPARADOS
/// (grupo <c>Divisas_Estacoes</c>), nas mesmas posições, para mover ou apagar à vontade.
/// Também deixa a mesa da estação SÓ como mesa: o <c>mesa_2.fbx</c> é uma malha única com papéis, porta-lápis e caneca;
/// uma cópia sem esses enfeites é gravada em <c>Art/Meshes/Mesa_Estacao.asset</c> (o .fbx do Matheus não muda) e é ela
/// que a estação usa. O cubículo decorado do escritório continua com o modelo original.
/// </summary>
public static class SepararDivisaSetup
{
    private const string BasePath = "Assets/Prefabs/Gameplay/Estacao_Trabalho.prefab";
    private const string Grupo = "Divisas_Estacoes";

    private const string MeshPath = "Assets/Art/Meshes/Mesa_Estacao.asset";

    // Troca a mesa do cubículo (fbx com enfeites) por um objeto com a malha limpa, no mesmo lugar.
    private static string LimparMesa(GameObject pref)
    {
        Transform cub = pref.transform.Find("Mesa/Cubiculo");
        Transform atual = cub != null ? cub.Find("Mesa_Cubiculo") : null;
        if (atual == null) return "mesa do cubículo não encontrada";
        MeshFilter mf = atual.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return "mesa sem malha";
        Mesh fonte = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath) != null && mf.sharedMesh.name == "Mesa_Estacao"
            ? null : mf.sharedMesh;
        if (fonte == null) return "mesa já estava limpa";

        Mesh limpa = SemEnfeites(fonte, out int tirados);
        limpa.name = "Mesa_Estacao";
        var existente = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (existente == null) AssetDatabase.CreateAsset(limpa, MeshPath);
        else { EditorUtility.CopySerialized(limpa, existente); limpa = existente; }

        var novo = new GameObject("Mesa_Cubiculo");
        novo.transform.SetParent(cub, false);
        novo.transform.localPosition = atual.localPosition;
        novo.transform.localRotation = atual.localRotation;
        novo.transform.localScale = atual.localScale;
        if (mf.transform != atual)   // a malha num filho do fbx: mantém a pose dele
        {
            novo.transform.SetPositionAndRotation(mf.transform.position, mf.transform.rotation);
            novo.transform.localScale = Vector3.Scale(atual.localScale, mf.transform.localScale);
        }
        novo.AddComponent<MeshFilter>().sharedMesh = limpa;
        novo.AddComponent<MeshRenderer>().sharedMaterials = mf.GetComponent<MeshRenderer>().sharedMaterials;
        Object.DestroyImmediate(atual.gameObject);
        return $"mesa limpa ({tirados} enfeite(s) removido(s))";
    }

    /// <summary>
    /// Cópia da malha só com a mesa: acha o tampo (maior área de faces viradas para cima) e separa a malha em partes
    /// soltas; fica o que desce abaixo do tampo (tampo e pernas) ou ocupa boa parte da área; saem as partes pequenas
    /// apoiadas em cima (papéis, porta-lápis, caneca).
    /// </summary>
    private static Mesh SemEnfeites(Mesh m, out int tirados)
    {
        Vector3[] v = m.vertices;
        // vértices na mesma posição = mesmo nó (a malha pode ter vértices duplicados nas quinas)
        var id = new Dictionary<Vector3Int, int>();
        int[] no = new int[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            var k = Vector3Int.RoundToInt(v[i] * 1000f);
            if (!id.TryGetValue(k, out int n)) { n = id.Count; id[k] = n; }
            no[i] = n;
        }
        int[] pai = new int[id.Count];
        for (int i = 0; i < pai.Length; i++) pai[i] = i;
        int Raiz(int x) { while (pai[x] != x) x = pai[x] = pai[pai[x]]; return x; }
        var area = new Dictionary<int, float>();
        for (int sm = 0; sm < m.subMeshCount; sm++)
        {
            int[] t = m.GetTriangles(sm);
            for (int i = 0; i < t.Length; i += 3)
            {
                pai[Raiz(no[t[i + 1]])] = Raiz(no[t[i]]);
                pai[Raiz(no[t[i + 2]])] = Raiz(no[t[i]]);
                Vector3 c = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (c.magnitude > 1e-9f && c.normalized.y > 0.95f)
                {
                    int y = Mathf.RoundToInt(v[t[i]].y * 20f);
                    area[y] = (area.TryGetValue(y, out float a) ? a : 0f) + c.magnitude * 0.5f;
                }
            }
        }
        float tampo = 0f, melhor = -1f;
        foreach (var kv in area) if (kv.Value > melhor) { melhor = kv.Value; tampo = kv.Key / 20f; }

        // extensão de cada parte
        var min = new Dictionary<int, Vector3>();
        var max = new Dictionary<int, Vector3>();
        for (int i = 0; i < v.Length; i++)
        {
            int r = Raiz(no[i]);
            min[r] = min.TryGetValue(r, out Vector3 a) ? Vector3.Min(a, v[i]) : v[i];
            max[r] = max.TryGetValue(r, out Vector3 b) ? Vector3.Max(b, v[i]) : v[i];
        }
        Bounds tudo = m.bounds;
        float folga = tudo.size.y * 0.03f, pegada = tudo.size.x * tudo.size.z;
        var fica = new HashSet<int>();
        foreach (int r in min.Keys)
        {
            Vector3 s = max[r] - min[r];
            if (min[r].y < tampo - folga || s.x * s.z > pegada * 0.25f) fica.Add(r);
        }
        tirados = min.Count - fica.Count;

        var limpa = new Mesh { indexFormat = m.indexFormat };
        limpa.vertices = v;
        limpa.normals = m.normals;
        limpa.uv = m.uv;
        if (m.tangents.Length == v.Length) limpa.tangents = m.tangents;
        if (m.colors.Length == v.Length) limpa.colors = m.colors;
        limpa.subMeshCount = m.subMeshCount;
        for (int sm = 0; sm < m.subMeshCount; sm++)
        {
            var tri = new List<int>();
            int[] t = m.GetTriangles(sm);
            for (int i = 0; i < t.Length; i += 3)
                if (fica.Contains(Raiz(no[t[i]]))) { tri.Add(t[i]); tri.Add(t[i + 1]); tri.Add(t[i + 2]); }
            limpa.SetTriangles(tri, sm);
        }
        limpa.RecalculateBounds();
        return limpa;
    }

    [MenuItem("Tools/E-stagiar.io/Estações: separar divisa e limpar a mesa")]
    public static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(TesteIntegradoSetup.ScenePath, OpenSceneMode.Single);

        // 1. poses das divisas que hoje vêm de dentro das estações
        var poses = new List<(Vector3 pos, Quaternion rot, Vector3 escala, string estacao)>();
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "Divisa" && t.parent != null && t.parent.name == "Cubiculo")
                {
                    Transform st = t;
                    while (st.parent != null && !st.name.StartsWith("Estacao")) st = st.parent;
                    poses.Add((t.position, t.rotation, t.lossyScale, st.name));
                }

        // 2. tira a divisa do prefab base
        GameObject pref = PrefabUtility.LoadPrefabContents(BasePath);
        Transform divisa = pref.transform.Find("Mesa/Cubiculo/Divisa");
        bool tirou = divisa != null;
        if (tirou) Object.DestroyImmediate(divisa.gameObject);
        string mesaInfo = LimparMesa(pref);
        PrefabUtility.SaveAsPrefabAsset(pref, BasePath);
        PrefabUtility.UnloadPrefabContents(pref);

        // 3. divisas separadas na cena, nas mesmas posições
        Transform grupo = GameObject.Find(Grupo)?.transform;
        if (grupo == null)
        {
            grupo = new GameObject(Grupo).transform;
            Transform escritorio = GameObject.Find("Escritorio")?.transform;
            if (escritorio != null) grupo.SetParent(escritorio, false);
        }
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/divisa.fbx");
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/M_Escritorio_Claro.mat");
        foreach (var p in poses)
        {
            string nome = "Divisa_" + p.estacao;
            if (grupo.Find(nome) != null) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(modelo, scene);
            go.name = nome;
            go.transform.SetParent(grupo, false);
            go.transform.SetPositionAndRotation(p.pos, p.rot);
            Vector3 s = grupo.lossyScale;
            go.transform.localScale = new Vector3(p.escala.x / s.x, p.escala.y / s.y, p.escala.z / s.z);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>()) mf.gameObject.AddComponent<MeshCollider>();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"SepararDivisaSetup: {mesaInfo}; divisa {(tirou ? "removida" : "já não estava")} no Estacao_Trabalho; " +
                  $"{poses.Count} divisa(s) recolocada(s) na cena como objetos separados em '{Grupo}'.");
    }
}
