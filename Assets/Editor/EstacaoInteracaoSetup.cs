using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interação com a estação de trabalho (idempotente; Tools > E-stagiar.io > Interação da estação de trabalho):
///  - <c>PC_Escritorio</c>: monitor, teclado e mouse pegáveis (<see cref="PickupInteractable"/> + Rigidbody, pose na mão
///    e giro ao colocar), <see cref="WallConnectionPoint"/> no <c>PontoEletrico</c> (aponta para o gabinete do mesmo PC)
///    e alcance do jogador no <see cref="MonitorDisplay"/>;
///  - superfícies de trabalho (<see cref="WorkSurface"/>): <c>OF_Mesa</c> e <c>PH_Bancada</c> (só os móveis que são
///    claramente mesa/bancada; gaveteiros, rack, cadeira etc. NÃO);
///  - <c>UI_Screen_Gabinete</c>: tomadas e ponto de rede num painel horizontal (<c>Traseira/PainelConexoes</c>, na ordem do
///    ponto elétrico 3D visto de frente: rede, tomada 2, tomada 1), painel ligado ao alcance (<c>wallViews</c>) e sem a
///    faixa de avisos de texto (<c>Aviso</c>);
///  - camada <c>ItemPreview</c> (só usada durante a miniatura do item da mão);
///  - cena <c>Office_Placeholder</c>: painel <c>ScreenCanvas/MaoAtual</c> (<see cref="HeldItemPreview"/>), objeto
///    <c>Sons_Interacao</c> (<see cref="InteractionSoundPlayer"/> com uma linha por ação, sem áudio) e câmera/braço
///    (<see cref="JogadorBracoSetup"/>).
/// </summary>
public static class EstacaoInteracaoSetup
{
    private const string PcPath = "Assets/Prefabs/Gameplay/PC_Escritorio.prefab";
    private const string MesaPath = "Assets/Prefabs/Props/OF_Mesa.prefab";
    private const string BancadaPath = "Assets/Prefabs/Props/PH_Bancada.prefab";
    private const string ScreenPath = "Assets/Prefabs/UI/UI_Screen_Gabinete.prefab";
    private const string ScenePath = "Assets/Scenes/Office_Placeholder.unity";
    public const string PreviewLayer = "ItemPreview";

    /// <summary>Distância máxima (m, horizontal) entre o gabinete e o ponto elétrico do PC.</summary>
    public const float WallPointCaseRange = 2f;

    // pose na mão (espaço do ponto de mão) e giro ao colocar: objetos grandes menores e mais baixos, virados para o jogador
    private struct Peripheral
    {
        public string child; public Vector3 offset, euler; public float scale, mass;
    }

    private static readonly Peripheral[] Peripherals =
    {
        // apoiados na mão: a base do objeto fica logo acima do ponto de mão (a mão vem por baixo)
        // (a mão fica em HoldPoint + (0,01; -0,035; -0,04); y = -0,035 + metade da altura do objeto na mão)
        // monitor e gabinete em TAMANHO REAL (só posição/rotação): base apoiada na mão, sem atravessar a câmera
        new Peripheral { child = "Monitor", offset = new Vector3(-0.02f, 0.33f, 0.18f), euler = new Vector3(0f, 0f, 0f),   scale = 1f, mass = 4f },
        new Peripheral { child = "Teclado", offset = new Vector3(0.03f, -0.02f, 0.03f), euler = new Vector3(-25f, 0f, 0f), scale = 0.42f, mass = 0.8f },
        new Peripheral { child = "Mouse",   offset = new Vector3(-0.04f, 0f, 0.04f),     euler = new Vector3(-35f, 0f, 0f), scale = 0.7f,  mass = 0.1f },
    };

    // gabinete (Gabinete.prefab): carregado menor e mais baixo, sem cobrir a visão
    private static readonly Peripheral CaseHold =
        new Peripheral { offset = new Vector3(0.0f, 0.27f, 0.3f), euler = Vector3.zero, scale = 1f };
    private const string CasePath = "Assets/Prefabs/Gameplay/Gabinete.prefab";

    [MenuItem("Tools/E-stagiar.io/Interação da estação de trabalho")]
    public static void Run()
    {
        EnsureLayer();
        MarkSurface(MesaPath);
        MarkSurface(BancadaPath);
        ConfigurePc();
        ConfigureCaseHold();
        ConfigureScreen();
        AssetDatabase.SaveAssets();
        ConfigureScene();
        JogadorBracoSetup.Run();
        Debug.Log("EstacaoInteracaoSetup: pronto");
    }

    // ------------------------------------------------------------------ camada da miniatura
    private static void EnsureLayer()
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
            if (layers.GetArrayElementAtIndex(i).stringValue == PreviewLayer) return;
        for (int i = 9; i < layers.arraySize; i++)
        {
            SerializedProperty l = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(l.stringValue)) continue;
            l.stringValue = PreviewLayer;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
            return;
        }
        Debug.LogWarning("EstacaoInteracaoSetup: sem camada livre para " + PreviewLayer + " (a miniatura usa a 31).");
    }

    // ------------------------------------------------------------------ mesas e bancadas
    private static void MarkSurface(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root.GetComponent<WorkSurface>() == null) root.AddComponent<WorkSurface>();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // ------------------------------------------------------------------ PC
    private static void ConfigurePc()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PcPath);
        Transform r = root.transform;
        ComputerPower power = root.GetComponentInChildren<ComputerPower>(true);

        foreach (Peripheral p in Peripherals)
        {
            GameObject go = Find(r, p.child).gameObject;
            var pickup = go.GetComponent<PickupInteractable>();
            if (pickup == null) pickup = go.AddComponent<PickupInteractable>();   // traz o Rigidbody (RequireComponent)
            pickup.SetHoldPose(p.offset, p.euler, p.scale);
            pickup.PlacedYawOffset = 180f;   // ao colocar na mesa, a frente fica virada para o jogador
            var body = go.GetComponent<Rigidbody>();
            body.mass = p.mass;
            body.isKinematic = true;          // parado até ser pego (startsFrozen faz o mesmo em jogo)
            // sem interpolação: preso à mão, um corpo interpolado seria "puxado" de volta para a pose da física
            body.interpolation = RigidbodyInterpolation.None;
        }

        GameObject ponto = Find(r, "PontoEletrico").gameObject;
        var wall = ponto.GetComponent<WallConnectionPoint>();
        if (wall == null) wall = ponto.AddComponent<WallConnectionPoint>();
        var so = new SerializedObject(wall);
        so.FindProperty("source").objectReferenceValue = power;
        so.FindProperty("caseRange").floatValue = WallPointCaseRange;
        so.ApplyModifiedPropertiesWithoutUndo();

        // (o alcance do monitor é a distância até o gabinete: ComputerPower.Monitor Range)

        PrefabUtility.SaveAsPrefabAsset(root, PcPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // Só a pose na mão do gabinete; o resto do Gabinete.prefab (parafusos, peças, sorteio) não é tocado.
    private static void ConfigureCaseHold()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CasePath);
        root.GetComponent<PickupInteractable>().SetHoldPose(CaseHold.offset, CaseHold.euler, CaseHold.scale);
        PrefabUtility.SaveAsPrefabAsset(root, CasePath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // ------------------------------------------------------------------ tela 2D
    // Frente: monitor e gabinete centralizados juntos e um pouco mais baixos (lado fixo). Traseira: layout original
    // (monitor + gabinete à esquerda; ponto elétrico à direita com as três conexões uma abaixo da outra) e a miniatura do
    // item da mão embaixo, no centro (HeldItemSlot). Interior: não muda. Âncoras absolutas: rodar de novo dá o mesmo.
    private static readonly (string path, Rect anchors)[] Equipment =
    {
        ("Frente/Monitor", Rect.MinMaxRect(0.4182f, 0.20f, 0.7995f, 0.8769f)),
        ("Frente/Gabinete", Rect.MinMaxRect(0.2005f, 0.20f, 0.3557f, 0.6806f)),
        ("Traseira/Monitor", Rect.MinMaxRect(0.1302f, 0.1574f, 0.5115f, 0.8343f)),
        ("Traseira/ContainerTraseira_Area", Rect.MinMaxRect(0.574f, 0.1574f, 0.7292f, 0.638f)),
    };
    // (parte, eixo do espelhamento): a Frente em torno do centro da tela; na Traseira, monitor e gabinete em torno do centro
    // da área deles e o ponto elétrico vai para a borda do lado do gabinete
    private static readonly (string path, float center)[] SideViews =
    {
        ("Frente/Monitor", 0.5f), ("Frente/Gabinete", 0.5f),
        ("Traseira/Monitor", 0.455f), ("Traseira/ContainerTraseira_Area", 0.455f), ("Traseira/PainelConexoes", 0.5f),
    };
    private static readonly Rect PanelAnchors = Rect.MinMaxRect(0.85f, 0.16f, 0.97f, 0.88f);   // coluna original
    // de cima para baixo, como antes: tomada 1, tomada 2, rede (âncoras dentro do painel)
    private static readonly (string name, float yMin, float yMax)[] PanelTiles =
    {
        ("Tomada", 0.6806f, 1f), ("Tomada_Monitor", 0.3403f, 0.6597f), ("PontoRede", 0f, 0.3194f),
    };
    private const float PlugScale = 0.8f;   // plugues das tomadas/rede um pouco menores (cabem no conector)

    private static void ConfigureScreen()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ScreenPath);
        Transform r = root.transform;

        Transform aviso = r.Find("Aviso");
        if (aviso != null) UnityEngine.Object.DestroyImmediate(aviso.gameObject);   // avisos de texto removidos

        foreach ((string path, Rect a) in Equipment)
        {
            var rt = (RectTransform)Find(r, path);
            rt.anchorMin = a.min;
            rt.anchorMax = a.max;
        }

        Transform rear = Find(r, "Traseira");
        Transform panel = rear.Find("PainelConexoes");
        if (panel == null)
        {
            var go = new GameObject("PainelConexoes", typeof(RectTransform), typeof(Image));
            panel = go.transform;
            panel.SetParent(rear, false);
        }
        var prt = (RectTransform)panel;
        prt.anchorMin = PanelAnchors.min;
        prt.anchorMax = PanelAnchors.max;
        prt.offsetMin = prt.offsetMax = Vector2.zero;
        var plate = panel.GetComponent<Image>();
        if (plate != null) UnityEngine.Object.DestroyImmediate(plate);           // só agrupa (cada placa tem o seu desenho)

        for (int i = 0; i < PanelTiles.Length; i++)
        {
            Transform t = panel.Find(PanelTiles[i].name);
            if (t == null) t = Find(rear, PanelTiles[i].name);
            t.SetParent(panel, false);
            t.SetSiblingIndex(i);
            var rt = (RectTransform)t;
            rt.anchorMin = new Vector2(0f, PanelTiles[i].yMin);
            rt.anchorMax = new Vector2(1f, PanelTiles[i].yMax);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = t.GetComponent<Image>();
            if (img != null) { img.color = Color.white; img.raycastTarget = false; }
            foreach (ComputerPortView port in t.GetComponentsInChildren<ComputerPortView>(true))
            {
                Transform plug = port.transform.Find("Plugue");
                if (plug != null) plug.localScale = Vector3.one * PlugScale;
            }
        }
        Transform cabos = Find(rear, "Cabos");
        if (panel.GetSiblingIndex() > cabos.GetSiblingIndex()) panel.SetSiblingIndex(cabos.GetSiblingIndex());

        // lugar da miniatura do item da mão na Traseira: embaixo, no centro (onde estava o painel)
        Transform slot = rear.Find("SlotMao");
        if (slot == null) slot = new GameObject("SlotMao", typeof(RectTransform), typeof(HeldItemSlot)).transform;
        slot.SetParent(rear, false);
        var srt = (RectTransform)slot;
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0f);
        srt.pivot = new Vector2(0.5f, 0f);
        srt.anchoredPosition = new Vector2(0f, 12f);
        srt.sizeDelta = new Vector2(150f, 150f);

        var ctrl = root.GetComponent<ComputerScreenController>();
        var so = new SerializedObject(ctrl);
        SerializedProperty list = so.FindProperty("wallViews");
        list.arraySize = 1;
        list.GetArrayElementAtIndex(0).objectReferenceValue = panel.gameObject;
        SerializedProperty sides = so.FindProperty("sideViews");
        sides.arraySize = SideViews.Length;
        SerializedProperty centers = so.FindProperty("sideMirrorCenters");
        centers.arraySize = SideViews.Length;
        for (int i = 0; i < SideViews.Length; i++)
        {
            sides.GetArrayElementAtIndex(i).objectReferenceValue = Find(r, SideViews[i].path);
            centers.GetArrayElementAtIndex(i).floatValue = SideViews[i].center;
        }
        so.FindProperty("mirrorCenterX").floatValue = 0.5f;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, ScreenPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // sons das ações (Tools/audio/gerar_sfx.py; volumes discretos)
    private const string SfxFolder = "Assets/Audio/SFX/";
    private static readonly (InteractionSound sound, string clip, float volume)[] Sounds =
    {
        (InteractionSound.ScrewRemoved, "sfx_parafuso_retirar", 0.45f),
        (InteractionSound.ScrewInstalled, "sfx_parafuso_recolocar", 0.45f),
        (InteractionSound.CaseOpened, "sfx_gabinete_abrir", 0.5f),
        (InteractionSound.CaseClosed, "sfx_gabinete_fechar", 0.5f),
        (InteractionSound.CablePlugged, "sfx_cabo_conectar", 0.45f),
        (InteractionSound.CableUnplugged, "sfx_cabo_desconectar", 0.45f),
        (InteractionSound.PartInstalled, "sfx_cabo_conectar", 0.4f),
        (InteractionSound.PartRemoved, "sfx_cabo_desconectar", 0.4f),
        (InteractionSound.PendrivePlugged, "sfx_cabo_conectar", 0.35f),
        (InteractionSound.PendriveUnplugged, "sfx_cabo_desconectar", 0.35f),
        (InteractionSound.PowerButton, "sfx_cabo_conectar", 0.3f),
        (InteractionSound.ItemPickedUp, "sfx_pegar", 0.35f),
        (InteractionSound.ItemPlaced, "sfx_colocar", 0.4f),
        (InteractionSound.ItemDropped, "sfx_soltar", 0.4f),
        (InteractionSound.ItemStoredInDrawer, "sfx_colocar", 0.35f),
        (InteractionSound.DrawerOpened, "sfx_gaveta_abrir", 0.4f),
        (InteractionSound.DrawerClosed, "sfx_gaveta_fechar", 0.4f),
        (InteractionSound.SupervisorAlert, "sfx_supervisor_alerta", 0.55f),
    };

    // ------------------------------------------------------------------ cena
    private static void ConfigureScene()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        GameObject player = GameObject.Find("Player");
        var holder = player.GetComponent<ItemHolder>();
        var manager = player.GetComponent<UIScreenManager>();

        // miniatura do item da mão nas telas 2D: à direita, no meio da altura (área livre ao lado do gabinete aberto)
        Transform canvas = GameObject.Find("ScreenCanvas").transform;
        Transform mao = canvas.Find("MaoAtual");
        if (mao == null) mao = new GameObject("MaoAtual", typeof(RectTransform)).transform;
        mao.SetParent(canvas, false);
        var mrt = (RectTransform)mao;
        mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one; mrt.offsetMin = mrt.offsetMax = Vector2.zero;
        Transform frame = mao.Find("Moldura");
        if (frame == null) frame = new GameObject("Moldura", typeof(RectTransform), typeof(Image)).transform;
        frame.SetParent(mao, false);
        var frt = (RectTransform)frame;
        frt.anchorMin = frt.anchorMax = new Vector2(1f, 0.5f);
        frt.pivot = new Vector2(1f, 0.5f);
        frt.anchoredPosition = new Vector2(-50f, 30f);
        frt.sizeDelta = new Vector2(190f, 190f);
        var bg = frame.GetComponent<Image>();
        bg.color = new Color(0.3f, 0.34f, 0.4f, 1f);   // moldura (mesmo tom dos botões)
        bg.raycastTarget = false;
        Transform pic = frame.Find("Item");
        if (pic == null) pic = new GameObject("Item", typeof(RectTransform), typeof(RawImage)).transform;
        pic.SetParent(frame, false);
        var irt = (RectTransform)pic;
        irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
        irt.offsetMin = new Vector2(6f, 6f); irt.offsetMax = new Vector2(-6f, -6f);
        var raw = pic.GetComponent<RawImage>();
        raw.raycastTarget = false;
        frame.gameObject.SetActive(false);
        var preview = mao.GetComponent<HeldItemPreview>();
        if (preview == null) preview = mao.gameObject.AddComponent<HeldItemPreview>();
        var ps = new SerializedObject(preview);
        ps.FindProperty("holder").objectReferenceValue = holder;
        ps.FindProperty("screens").objectReferenceValue = manager;
        ps.FindProperty("panel").objectReferenceValue = frame.gameObject;
        ps.FindProperty("image").objectReferenceValue = raw;
        ps.FindProperty("background").colorValue = new Color(0.86f, 0.9f, 0.92f, 1f);
        ps.ApplyModifiedPropertiesWithoutUndo();
        mao.SetAsLastSibling();

        // sons: uma linha por ação, sem clipes (o time de áudio só arrasta os arquivos)
        GameObject sons = GameObject.Find("Sons_Interacao");
        if (sons == null) sons = new GameObject("Sons_Interacao");
        var src = sons.GetComponent<AudioSource>();
        if (src == null) src = sons.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        var soundPlayer = sons.GetComponent<InteractionSoundPlayer>();
        if (soundPlayer == null) soundPlayer = sons.AddComponent<InteractionSoundPlayer>();
        var ss = new SerializedObject(soundPlayer);
        ss.FindProperty("uiSource").objectReferenceValue = src;
        SerializedProperty entries = ss.FindProperty("entries");
        var present = new HashSet<int>();
        for (int i = 0; i < entries.arraySize; i++) present.Add(entries.GetArrayElementAtIndex(i).FindPropertyRelative("sound").enumValueIndex);
        foreach (InteractionSound s in Enum.GetValues(typeof(InteractionSound)))
        {
            if (present.Contains((int)s)) continue;
            entries.arraySize++;
            SerializedProperty e = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            e.FindPropertyRelative("sound").enumValueIndex = (int)s;
            e.FindPropertyRelative("clips").arraySize = 0;
            e.FindPropertyRelative("volume").floatValue = 1f;
            e.FindPropertyRelative("pitchVariation").floatValue = 0.05f;
        }
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty e = entries.GetArrayElementAtIndex(i);
            var sound = (InteractionSound)e.FindPropertyRelative("sound").enumValueIndex;
            foreach ((InteractionSound snd, string clip, float volume) in Sounds)
            {
                if (snd != sound) continue;
                var audio = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxFolder + clip + ".wav");
                if (audio == null) throw new Exception("EstacaoInteracaoSetup: falta " + clip);
                SerializedProperty clips = e.FindPropertyRelative("clips");
                clips.arraySize = 1;
                clips.GetArrayElementAtIndex(0).objectReferenceValue = audio;
                e.FindPropertyRelative("volume").floatValue = volume;
            }
        }
        ss.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static Transform Find(Transform root, string path)
    {
        Transform t = root.Find(path);
        if (t == null) throw new Exception("EstacaoInteracaoSetup: não achei " + path + " em " + root.name);
        return t;
    }
}
