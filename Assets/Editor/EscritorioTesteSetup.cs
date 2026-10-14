using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Amplia o escritório TEMPORÁRIO da <c>Office_Placeholder</c> para testar a ronda do supervisor (idempotente;
/// Tools > E-stagiar.io > Ampliar escritório de teste). Não é o mapa definitivo.
///  - Sala de 8 x 6 m para 14 x 8 m: piso, teto e paredes (primitivos) redimensionados; a parede norte (onde ficam as
///    estações) não sai do lugar, a sul desce 2 m, leste e oeste abrem 3 m cada.
///  - Estações mais afastadas (x ±2 → ±3,5): o prefab inteiro se move, com PC, monitor, ponto elétrico e cabos juntos
///    (todas as referências são internas ao prefab).
///  - Objetos encostados nas paredes leste/oeste (bancada, gaveta, vaso, janela, porta, rack) acompanham a parede.
/// Depois rode <see cref="SupervisorSetup"/> (rota e NavMesh); o menu deste script já faz isso.
/// </summary>
public static class EscritorioTesteSetup
{
    private const string ScenePath = "Assets/Scenes/Office_Placeholder.unity";
    private const float HalfWidth = 7f, North = 3f, South = -5f, Height = 3f, Wall = 0.2f;
    private const float StationX = 3.5f, SideShift = 3f;

    [MenuItem("Tools/E-stagiar.io/Ampliar escritório de teste")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        Transform wallW = GameObject.Find("Wall_W").transform;
        bool already = Mathf.Abs(wallW.position.x + HalfWidth + Wall * 0.5f) < 0.01f;

        float depth = North - South, midZ = (North + South) * 0.5f, width = HalfWidth * 2f + Wall * 2f;
        Place("Floor", new Vector3(0f, -0.05f, midZ), new Vector3(width, 0.1f, depth + Wall * 2f));
        Place("Ceiling", new Vector3(0f, Height + 0.05f, midZ), new Vector3(width, 0.1f, depth + Wall * 2f));
        Place("Wall_W", new Vector3(-HalfWidth - Wall * 0.5f, Height * 0.5f, midZ), new Vector3(Wall, Height, depth));
        Place("Wall_E", new Vector3(HalfWidth + Wall * 0.5f, Height * 0.5f, midZ), new Vector3(Wall, Height, depth));
        Place("Wall_N", new Vector3(0f, Height * 0.5f, North + Wall * 0.5f), new Vector3(width, Height, Wall));
        Place("Wall_S", new Vector3(0f, Height * 0.5f, South - Wall * 0.5f), new Vector3(width, Height, Wall));

        SetX("Estacao_1", -StationX);
        SetX("Estacao_2", StationX);
        if (!already)
        {
            foreach (string n in new[] { "Bancada", "Gaveta", "Vaso", "Janela" }) ShiftX(n, -SideShift);
            foreach (string n in new[] { "Porta", "RackInternet" }) ShiftX(n, SideShift);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        SupervisorSetup.Run();
        Debug.Log("EscritorioTesteSetup: pronto");
    }

    private static Transform Get(string name)
    {
        GameObject g = GameObject.Find(name);
        if (g == null) throw new System.Exception("EscritorioTesteSetup: não achei " + name);
        return g.transform;
    }

    private static void Place(string name, Vector3 pos, Vector3 scale)
    {
        Transform t = Get(name);
        t.position = pos;
        t.localScale = scale;
    }

    private static void SetX(string name, float x)
    {
        Transform t = Get(name);
        Vector3 p = t.position; p.x = x; t.position = p;
    }

    private static void ShiftX(string name, float dx) => Get(name).position += Vector3.right * dx;
}
