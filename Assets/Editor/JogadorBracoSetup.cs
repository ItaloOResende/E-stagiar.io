using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Câmera, ponto de mão e braço em 1ª pessoa do Player (Office_Placeholder). Idempotente.
/// Menu: Tools > E-stagiar.io > Ajustar câmera e braço do jogador.
///
/// Antes: câmera nos olhos, mas o HoldPoint quase na linha dos olhos (-0,12) e o braço SUBINDO do ombro (-0,19) para a
/// mão (-0,14): a peça ficava no meio da tela e o braço parecia na altura da cabeça.
/// Agora (espaço local da câmera, antes da escala 0,85 do Player): câmera nos olhos do modelo, HoldPoint na parte de
/// baixo da visão e o braço posicionado medindo a própria malha: ombro abaixo da câmera e a mão chegando por baixo
/// da peça segurada.
/// </summary>
public static class JogadorBracoSetup
{
    private const float CameraHeight = 0.85f;                                // local do Player (era 0,80)
    private static readonly Vector3 HoldPoint = new Vector3(0.26f, -0.22f, 0.50f);
    private static readonly Vector3 Shoulder = new Vector3(0.30f, -0.42f, 0.10f);
    private static readonly Vector3 HandOffset = new Vector3(0.01f, -0.035f, -0.04f); // mão logo abaixo/atrás do centro da peça

    [MenuItem("Tools/E-stagiar.io/Ajustar câmera e braço do jogador")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Office_Placeholder.unity");
        GameObject player = GameObject.Find("Player");
        Camera cam = player.GetComponentInChildren<Camera>();
        Transform c = cam.transform, hold = c.Find("HoldPoint"), arm = c.Find("Arm_Personagem");

        Vector3 lp = c.localPosition; lp.y = CameraHeight; c.localPosition = lp;
        hold.localPosition = HoldPoint;

        // ombro e mão atuais do braço (malha), no espaço da câmera
        MeshFilter mf = arm.GetComponentInChildren<MeshFilter>();
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
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"JogadorBracoSetup: câmera y {CameraHeight}, HoldPoint {HoldPoint}, braço pos {arm.localPosition:F3} rot {arm.localEulerAngles:F1}");
    }
}
