using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// Tela de teste de impressão (ImpressoraTela, vista Teste) na cena Teste_Integrado: com papel a página sai e conta
/// em ImpressoraEstacao.TestesOk; sem papel não sai. Com CAPTURAS_DIR, salva imagens das duas situações.
/// </summary>
public class TesteImpressaoTests
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type T(string n) => Type.GetType(n + ", Assembly-CSharp", true);

    [UnityTest]
    public IEnumerator TesteDeImpressao_ComPapelImprime_SemPapelFalha()
    {
#if UNITY_EDITOR
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
            "Assets/Scenes/Teste_Integrado.unity", new UnityEngine.SceneManagement.LoadSceneParameters());
#endif
        for (int i = 0; i < 5; i++) yield return null;

        Component est = null;
        foreach (Object o in Object.FindObjectsByType(T("ImpressoraEstacao"), FindObjectsSortMode.None))
        {
            est = (Component)o;
            if (est.GetComponent(T("UIScreenInteractable")) != null) break;
        }
        Assert.IsNotNull(est, "impressora na cena");
        object sessao = est.GetType().GetProperty("Sessao").GetValue(est);
        Assert.IsTrue((bool)sessao.GetType().GetProperty("TemPapel").GetValue(sessao), "começa com papel (estado funcional)");

        var manager = (Component)T("UIScreenManager").GetProperty("Active").GetValue(null);
        var interactable = est.GetComponent(T("UIScreenInteractable"));
        var screen = (Component)interactable.GetType().GetField("screen").GetValue(interactable);
        var detector = Object.FindAnyObjectByType(T("InteractionDetector"));
        Assert.IsTrue((bool)manager.GetType().GetMethod("Open").Invoke(manager, new object[] { screen, est.gameObject, detector }));
        yield return new WaitForSeconds(0.5f);

        var tela = Object.FindAnyObjectByType(T("ImpressoraTela"));
        Clicar((Component)tela, "BotaoImprimir");
        yield return new WaitForSeconds(2.8f);
        Assert.AreEqual(1, (int)est.GetType().GetProperty("TestesOk").GetValue(est), "página de teste saiu");
        Assert.IsNull(Achar((Component)tela, "Sucesso"), "sem selo de ✓ (pedido de 2026-10-17)");
        Assert.IsTrue(Achar((Component)tela, "Folha").gameObject.activeSelf, "a folha impressa é o resultado");
        Capturar((Component)tela, "impressora_teste_ok");

        // tira o papel (como o defeito do chamado) e tenta de novo
        object toner = est.GetType().GetProperty("TonerGuardado").GetValue(est);
        string ip = (string)sessao.GetType().GetProperty("IpSalvo").GetValue(sessao);
        est.GetType().GetMethod("DefinirEstado").Invoke(est, new object[] { toner, null, ip });
        Clicar((Component)tela, "BotaoImprimir");
        yield return new WaitForSeconds(1.0f);
        Assert.AreEqual(1, (int)est.GetType().GetProperty("TestesOk").GetValue(est), "sem papel: não imprime");
        Assert.IsFalse(Achar((Component)tela, "Folha").gameObject.activeSelf, "nenhuma folha sai");
        Capturar((Component)tela, "impressora_teste_falha");
        manager.GetType().GetMethod("Close").Invoke(manager, null);
    }

    private static Transform Achar(Component root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    private static void Clicar(Component root, string name)
    {
        Transform t = Achar(root, name);
        Assert.IsNotNull(t, name);
        t.GetComponentInChildren<UnityEngine.UI.Button>(true).onClick.Invoke();
    }

    private static void Capturar(Component tela, string nome)
    {
        string dir = Environment.GetEnvironmentVariable("CAPTURAS_DIR");
        if (string.IsNullOrEmpty(dir)) return;
        Canvas canvas = tela.GetComponentInParent<Canvas>().rootCanvas;
        RenderMode modo = canvas.renderMode;
        var camGo = new GameObject("CamCaptura");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.cullingMask = 1 << canvas.gameObject.layer;
        var rt = new RenderTexture(1920, 1080, 24);
        cam.targetTexture = rt;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;
        Canvas.ForceUpdateCanvases();
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        tex.Apply();
        System.IO.Directory.CreateDirectory(dir);
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, nome + ".png"), tex.EncodeToPNG());
        RenderTexture.active = null;
        canvas.renderMode = modo;
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
    }
}
