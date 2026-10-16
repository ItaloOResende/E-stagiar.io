using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Câmera, ponto de mão e braço em 1ª pessoa do Player. Idempotente.
/// Menu: Tools > E-stagiar.io > Ajustar câmera e braço do jogador (cena aberta)
///       Tools > E-stagiar.io > Ajustar câmera e braço do jogador (Office_Placeholder)
///
/// Antes: câmera nos olhos, mas o HoldPoint quase na linha dos olhos (-0,12) e o braço SUBINDO do ombro (-0,19) para a
/// mão (-0,14): a peça ficava no meio da tela e o braço parecia na altura da cabeça.
/// Agora (espaço local da câmera, antes da escala 0,9 do Player): câmera nos olhos do modelo, HoldPoint na parte de
/// baixo da visão e o braço posicionado medindo a própria malha: ombro abaixo da câmera e a mão chegando por baixo
/// da peça segurada.
/// 2026-10-13: a câmera em 0,85 ficava quase no TOPO da cabeça (a cabeça do personagem vai de ~-0,22 a 0,94 no espaço
/// do Player); desceu para 0,62, mais perto do centro da cabeça (~0,36) sem exagero. Ponto de mão e ombro um pouco
/// mais à frente (o braço aparece mais e a mão não fica colada na câmera).
/// 2026-10-16: a mão agora sai da QUINA DIREITA da tela. O <see cref="HoldPoint"/> e o <see cref="Shoulder"/> andaram
/// JUNTOS +0,22 em X — como este script alinha a ponta da malha (a mão) ao HoldPoint + HandOffset, deslocar os dois
/// igual preserva a pose e simplesmente desliza o braço para a direita (o antebraço sai pelo canto).
/// </summary>
public static class JogadorBracoSetup
{
    private const string CenaDoEscritorio = "Assets/Scenes/Office_Placeholder.unity";

    // 2026-10-14: personagem um pouco maior (0,85 → 0,9: mesas e equipamentos na proporção natural) e câmera mais
    // perto do centro da cabeça (olhos a ~1,40 m); braço mais à frente, mostrando mais antebraço.
    private const float PlayerScale = 0.9f;                                   // era 0,85
    private const float CameraHeight = 0.55f;                                 // local do Player (era 0,62; antes 0,85)
    private static readonly Vector3 HoldPoint = new Vector3(0.44f, -0.19f, 0.55f);   // era (0,22; -0,19; 0,55)
    private static readonly Vector3 Shoulder = new Vector3(0.52f, -0.30f, 0.30f);    // era (0,30; -0,30; 0,30)

    // Pose de referência do braço (a do commit 9cab91b, correta). O cálculo abaixo SEMPRE parte dela: antes ele partia da
    // pose atual, e rodar o setup de novo acumulava rotações; com o ombro quase de lado, a "ponta" detectada passou a ser a
    // manga e o braço inverteu (manga para fora, mão para dentro).
    private static readonly Vector3 RefArmPosition = new Vector3(0.29636273f, -0.38724905f, 0.23579757f);
    private static readonly Quaternion RefArmRotation = new Quaternion(-0.27711797f, -0.06888593f, -0.029837541f, 0.95789886f);
    private static readonly Vector3 HandOffset = new Vector3(0.01f, -0.035f, -0.04f); // mão logo abaixo/atrás do centro da peça

    [MenuItem("Tools/E-stagiar.io/Ajustar câmera e braço do jogador (cena aberta)")]
    public static void RunNaCenaAberta()
    {
        Aplicar(SceneManager.GetActiveScene());
    }

    [MenuItem("Tools/E-stagiar.io/Ajustar câmera e braço do jogador (Office_Placeholder)")]
    public static void Run()
    {
        Aplicar(EditorSceneManager.OpenScene(CenaDoEscritorio));
    }

    private static void Aplicar(Scene scene)
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogError("JogadorBracoSetup: não achei um objeto 'Player' na cena '" + scene.name + "'.");
            return;
        }

        Camera cam = player.GetComponentInChildren<Camera>();
        Transform c = cam != null ? cam.transform : null;
        Transform hold = c != null ? c.Find("HoldPoint") : null;
        Transform arm = c != null ? c.Find("Arm_Personagem") : null;
        if (hold == null || arm == null)
        {
            Debug.LogError("JogadorBracoSetup: 'HoldPoint' e 'Arm_Personagem' precisam ser filhos da câmera do Player.");
            return;
        }

        MeshFilter mf = arm.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogError("JogadorBracoSetup: o braço está sem malha (MeshFilter) — não dá para medir a mão.");
            return;
        }

        // escala do personagem: a cápsula (altura 2, centro 0) continua com a base no chão
        float floorY = player.transform.position.y - player.transform.localScale.y;
        player.transform.localScale = Vector3.one * PlayerScale;
        Vector3 pp = player.transform.position; pp.y = floorY + PlayerScale; player.transform.position = pp;
        Vector3 lp = c.localPosition; lp.y = CameraHeight; c.localPosition = lp;
        hold.localPosition = HoldPoint;

        arm.localPosition = RefArmPosition;
        arm.localRotation = RefArmRotation;

        // ombro e mão do braço (malha) na pose de referência, no espaço da câmera
        Vector3[] pts = mf.sharedMesh.vertices.Select(v => c.InverseTransformPoint(mf.transform.TransformPoint(v))).ToArray();
        Vector3 fwd = Vector3.forward;
        Vector3 tip = pts.OrderByDescending(p => Vector3.Dot(p, fwd)).First();
        Vector3 back = pts.OrderBy(p => Vector3.Dot(p, fwd)).First();
        Vector3 hand = HoldPoint + HandOffset;
        // gira o braço em torno do ombro atual para apontar do ombro novo à mão, mantendo o comprimento
        Quaternion turn = Quaternion.FromToRotation(tip - back, hand - Shoulder);
        arm.localRotation = turn * arm.localRotation;
        Vector3 newBack = turn * (arm.localPosition - back) ;  // posição do pivô relativa ao ombro, girada
        arm.localPosition = Shoulder + newBack;
        // o comprimento do braço é o da malha: alinha a ponta da mão com o alvo deslizando ao longo do braço
        Vector3[] after = mf.sharedMesh.vertices.Select(v => c.InverseTransformPoint(mf.transform.TransformPoint(v))).ToArray();
        Vector3 tip2 = after.OrderByDescending(p => Vector3.Dot(p, (hand - Shoulder).normalized)).First();
        arm.localPosition += hand - tip2;

        EditorSceneManager.MarkSceneDirty(scene);
        if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
        else Debug.LogWarning("JogadorBracoSetup: a cena '" + scene.name + "' está sem caminho — salve você mesmo.");
        Debug.Log($"JogadorBracoSetup [{scene.name}]: câmera y {CameraHeight}, HoldPoint {HoldPoint}, braço pos {arm.localPosition:F3} rot {arm.localEulerAngles:F1}");
    }
}
