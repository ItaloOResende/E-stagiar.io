using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Diagnóstico de vídeo, monitor por proximidade, cabos e abertura do gabinete (idempotente;
/// Tools > E-stagiar.io > Diagnóstico de vídeo e abertura do gabinete):
///  - importa <c>UI/PH_ui_tela_sem_sinal.png</c> (gerada por Tools/screens/gerar_telas_monitor.py: tela preta com "Sem sinal",
///    na proporção da área do display) como as outras telas;
///  - <c>UI_Screen_Gabinete</c>: tela "Sem sinal" (<c>noSignalScreen</c>), partes do monitor que somem com ele longe
///    (<c>monitorViews</c>), <c>PontoRede</c> desenhado ANTES dos cabos (o cabo de rede ficava escondido atrás da placa
///    da parede; agora fica na frente do plugue, como nas tomadas) e CanvasGroup no Avançar (opacidade do bloqueio).
///    A faixa de avisos de texto (<c>Aviso</c>) que este script criava foi removida em 2026-10-13
///    (<see cref="EstacaoInteracaoSetup"/>): o feedback do bloqueio agora é só visual;
///  - <c>PC_Escritorio</c>: textura "Sem sinal" no <c>MonitorDisplay</c> da tela 3D.
/// Não mexe na cena nem no Gabinete.prefab.
/// </summary>
public static class DiagnosticoVideoSetup
{
    private const string ScreenPath = "Assets/Prefabs/UI/UI_Screen_Gabinete.prefab";
    private const string PcPath = "Assets/Prefabs/Gameplay/PC_Escritorio.prefab";
    private const string NoSignal = "Assets/Art/UI/PH_ui_tela_sem_sinal.png";
    private const string Template = "Assets/Art/UI/PH_ui_tela_bios.png";

    [MenuItem("Tools/E-stagiar.io/Diagnóstico de vídeo e abertura do gabinete")]
    public static void Run()
    {
        ImportScreen();
        ConfigureScreen();
        ConfigurePc();
        AssetDatabase.SaveAssets();
        Debug.Log("DiagnosticoVideoSetup: pronto");
    }

    // ------------------------------------------------------------------ imagem
    private static void ImportScreen()
    {
        AssetDatabase.ImportAsset(NoSignal);
        var template = (TextureImporter)AssetImporter.GetAtPath(Template);
        var imp = (TextureImporter)AssetImporter.GetAtPath(NoSignal);
        var settings = new TextureImporterSettings();
        template.ReadTextureSettings(settings);
        imp.SetTextureSettings(settings);
        imp.textureCompression = template.textureCompression;
        imp.SaveAndReimport();
    }

    // ------------------------------------------------------------------ tela 2D
    private static void ConfigureScreen()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ScreenPath);
        Transform r = root.transform;
        var ctrl = root.GetComponent<ComputerScreenController>();
        var so = new SerializedObject(ctrl);

        so.FindProperty("noSignalScreen").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(NoSignal);

        var monitors = new List<GameObject> { Find(r, "Frente/Monitor").gameObject, Find(r, "Traseira/Monitor").gameObject };
        SerializedProperty list = so.FindProperty("monitorViews");
        list.arraySize = monitors.Count;
        for (int i = 0; i < monitors.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = monitors[i];

        // ordem de desenho: placa do ponto de rede antes dos cabos (como Tomada e Tomada_Monitor). Depois de 2026-10-13
        // ela fica dentro de Traseira/PainelConexoes (EstacaoInteracaoSetup), que já vem antes dos cabos.
        Transform cabos = Find(r, "Traseira/Cabos"), rede = r.Find("Traseira/PontoRede");
        if (rede != null && rede.GetSiblingIndex() > cabos.GetSiblingIndex()) rede.SetSiblingIndex(cabos.GetSiblingIndex());

        GameObject next = Find(r, "Navegacao/BotaoAvancar").gameObject;
        if (next.GetComponent<CanvasGroup>() == null) next.AddComponent<CanvasGroup>();

        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, ScreenPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // ------------------------------------------------------------------ monitor 3D
    private static void ConfigurePc()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PcPath);
        var display = root.GetComponentInChildren<MonitorDisplay>(true);
        var so = new SerializedObject(display);
        so.FindProperty("noSignalTexture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(NoSignal);
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, PcPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static Transform Find(Transform root, string path)
    {
        Transform t = root.Find(path);
        if (t == null) throw new System.Exception("UI_Screen_Gabinete: não achei " + path);
        return t;
    }
}
