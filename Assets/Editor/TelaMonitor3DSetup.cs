using UnityEditor;
using UnityEngine;

/// <summary>
/// Tela do monitor na Frente da manutenção (<c>UI_Screen_Gabinete</c>, <c>Frente/Monitor/Area/Tela</c>) com as MESMAS
/// proporções do monitor 3D (referência visual): imagem retangular, bordas grossas, sem deformação.
/// Medido num render frontal ortográfico do <c>OF_Monitor</c> (2026-10-16): painel x 207–991 / y 59–649 px, tela
/// x 298–902 / y 143–570 px. Como <c>ui_render_monitor_frente.png</c> é render do mesmo modelo (painel = 1602 × 1208 px
/// no topo da imagem de 1603 × 1600), a tela fica em x 186–1420 / y 172–1046 px → âncoras abaixo (Y de baixo para cima).
/// Remove o <c>UIQuadWarp</c> (deformação anterior, descartada). O monitor 3D não muda. Idempotente.
/// </summary>
public static class TelaMonitor3DSetup
{
    private const string ScreenPath = "Assets/Prefabs/UI/UI_Screen_Gabinete.prefab";
    private const string TelaPath = "Frente/Monitor/Area/Tela";

    public static readonly Vector2 AnchorMin = new Vector2(0.1160f, 0.3463f);
    public static readonly Vector2 AnchorMax = new Vector2(0.8858f, 0.8925f);

    [MenuItem("Tools/E-stagiar.io/Tela do monitor nas proporções do monitor 3D")]
    public static void Run()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ScreenPath);
        try
        {
            Transform tela = root.transform.Find(TelaPath);
            if (tela == null)
            {
                Debug.LogError($"TelaMonitor3DSetup: '{TelaPath}' não encontrado em {ScreenPath}.");
                return;
            }
            foreach (MonoBehaviour mb in tela.GetComponents<MonoBehaviour>())
                if (mb != null && mb.GetType().Name == "UIQuadWarp") Object.DestroyImmediate(mb, true);
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(tela.gameObject);

            var rt = (RectTransform)tela;
            rt.anchorMin = AnchorMin;
            rt.anchorMax = AnchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            PrefabUtility.SaveAsPrefabAsset(root, ScreenPath);
            Debug.Log("TelaMonitor3DSetup: tela do monitor nas proporções do 3D.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
