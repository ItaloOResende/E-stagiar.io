#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Prepara a <c>Office_Placeholder</c> para o chamado de estação nova (<c>pc-montagem-completa</c>), em que vale qualquer
/// gabinete, monitor, teclado e mouse da sala de TI:
///  1. <c>PC_Escritorio.prefab</c>: o teclado e o mouse ganham o marcador <see cref="Periferico"/>.
///  2. Gabinetes da sala de TI sem chamado próprio (estoque e bancada) começam VAZIOS (sem sorteio de peças).
///  3. Estantes do estoque como no tutorial: os teclados ficam pegáveis e a caixa ao lado deles vira dois mouses.
/// Idempotente: rodar de novo não duplica nada. Menu: Tools &gt; E-stagiar.io &gt; Estação nova &gt; Preparar sala de TI.
/// </summary>
public static class EstacaoNovaSetup
{
    private const string Cena = "Assets/Scenes/Office_Placeholder.unity";
    private const string PrefabPc = "Assets/Prefabs/Gameplay/PC_Escritorio.prefab";

    [MenuItem("Tools/E-stagiar.io/Estação nova/Preparar sala de TI")]
    public static void Preparar()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[Estação nova] Saia do Play antes."); return; }
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var log = new StringBuilder();
        MarcarPerifericosDoPrefab(log);

        Scene cena = EditorSceneManager.OpenScene(Cena, OpenSceneMode.Single);
        GabinetesVazios(cena, log);
        PrepararEstantes(cena, log);

        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/estacao_nova_setup.txt", log.ToString());
        Debug.Log("[Estação nova] Pronto.\n" + log);
    }

    private static void MarcarPerifericosDoPrefab(StringBuilder log)
    {
        GameObject raiz = PrefabUtility.LoadPrefabContents(PrefabPc);
        try
        {
            foreach (var (nome, tipo) in new[] { ("Teclado", Periferico.Tipo.Teclado), ("Mouse", Periferico.Tipo.Mouse) })
            {
                Transform t = raiz.transform.Find(nome);
                if (t == null || t.GetComponent<PickupInteractable>() == null) { Debug.LogError($"[Estação nova] {nome} pegável não encontrado em {PrefabPc}."); continue; }
                Periferico p = t.GetComponent<Periferico>() ?? t.gameObject.AddComponent<Periferico>();
                p.Qual = tipo;
                log.AppendLine($"PC_Escritorio/{nome}: Periferico {tipo}");
            }
            PrefabUtility.SaveAsPrefabAsset(raiz, PrefabPc);
        }
        finally { PrefabUtility.UnloadPrefabContents(raiz); }
    }

    // Todo gabinete da sala de TI sem PcTicketBridge (os de estação têm) começa sem peças.
    private static void GabinetesVazios(Scene cena, StringBuilder log)
    {
        Bounds sala = Limites(Achar(cena, "Sala_TI").transform);
        GameObject complementos = Achar(cena, "Sala_TI_Complementos");
        if (complementos != null) sala.Encapsulate(Limites(complementos.transform));
        sala.Expand(new Vector3(0.5f, 10f, 0.5f));
        foreach (ComputerCase c in cena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ComputerCase>(true)))
        {
            if (c.GetComponent<PcTicketBridge>() != null || !sala.Contains(c.transform.position)) continue;
            var so = new SerializedObject(c);
            so.FindProperty("randomizeComponents").boolValue = false;
            so.FindProperty("fixedComponents").intValue = 0;   // PcComponent.None
            so.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine("gabinete vazio: " + c.name);
        }
    }

    // Mesmo arranjo do tutorial (TutorialMontagemSetup.PrepararEstantes), sem tirar nada das estações.
    private static void PrepararEstantes(Scene cena, StringBuilder log)
    {
        PcTicketBridge pc = cena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PcTicketBridge>(true))
            .FirstOrDefault(b => b.transform.parent != null && b.transform.parent.Find("Teclado") != null && b.transform.parent.Find("Mouse") != null);
        if (pc == null) { Debug.LogError("[Estação nova] Nenhum PC de estação com teclado e mouse na cena."); return; }
        Transform modeloTeclado = pc.transform.parent.Find("Teclado"), modeloMouse = pc.transform.parent.Find("Mouse");
        var todos = cena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        var estoque = todos.Where(t => t.name.StartsWith("Teclado_Estoque")).ToList();
        var caixas = todos.Where(t => t.name.StartsWith("Caixa_Estoque")).ToList();
        int mouses = todos.Count(t => t.name.StartsWith("Mouse_Estoque_"));
        Physics.SyncTransforms();
        var empilhados = new HashSet<Transform>();
        foreach (Transform teclado in estoque)
        {
            foreach (Component c in modeloTeclado.GetComponents<Component>())
            {
                if (c is Transform || c is Collider || c is Renderer || c is MeshFilter || teclado.GetComponent(c.GetType()) != null) continue;
                UnityEditorInternal.ComponentUtility.CopyComponent(c);
                UnityEditorInternal.ComponentUtility.PasteComponentAsNew(teclado.gameObject);
            }
            Bounds b = Limites(teclado);
            bool empilhado = estoque.Any(o => o != teclado && Mathf.Abs(Limites(o).max.y - b.min.y) < 0.02f && Limites(o).Intersects(new Bounds(b.center - Vector3.up * 0.03f, b.size)));
            if (empilhado)
            {
                empilhados.Add(teclado);
                Definir(teclado.GetComponent<PickupInteractable>(), "startsFrozen", p => p.boolValue = false);
                var corpo = teclado.GetComponent<Rigidbody>(); if (corpo != null) corpo.isKinematic = false;
            }
            log.AppendLine($"teclado pegável: {teclado.name} {(empilhado ? "(em cima de outro, com física)" : "")}");
        }
        foreach (Transform teclado in estoque)
        {
            if (empilhados.Contains(teclado)) continue;
            Bounds b = Limites(teclado);
            Transform caixa = caixas.Where(c => c != null && Mathf.Abs(Limites(c).min.y - b.min.y) < 0.05f && Vector3.Distance(Limites(c).center, b.center) < 1.2f)
                                    .OrderBy(c => Vector3.Distance(Limites(c).center, b.center)).FirstOrDefault();
            if (caixa == null) continue;
            Bounds cb = Limites(caixa);
            Vector3 ao = cb.center - b.center; ao.y = 0f; ao = Mathf.Abs(ao.x) > Mathf.Abs(ao.z) ? new Vector3(Mathf.Sign(ao.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(ao.z));
            float prateleira = cb.min.y;
            if (Physics.Raycast(new Vector3(cb.center.x, cb.max.y, cb.center.z), Vector3.down, out RaycastHit h, 1f, ~0, QueryTriggerInteraction.Ignore)) prateleira = h.point.y;
            caixas.Remove(caixa);
            Desempacotar(caixa.gameObject); log.AppendLine("removido da estante: " + caixa.name); Object.DestroyImmediate(caixa.gameObject);
            foreach (float lado in new[] { -0.18f, 0.18f })
            {
                var copia = Object.Instantiate(modeloMouse.gameObject, teclado.parent);
                copia.name = "Mouse_Estoque_" + (++mouses);
                Colocar(copia.transform, teclado.rotation, cb.center + ao * lado, prateleira + 0.002f);
                log.AppendLine($"mouse na estante: {copia.name} {Limites(copia.transform).min:F2}..{Limites(copia.transform).max:F2}");
            }
        }
    }

    private static void Colocar(Transform item, Quaternion rotacao, Vector3 centro, float baseY)
    {
        item.rotation = rotacao;
        Bounds b = Limites(item);
        item.position += new Vector3(centro.x - b.center.x, baseY - b.min.y, centro.z - b.center.z);
    }
    private static Bounds Limites(Transform t)
    {
        Bounds b = new Bounds(t.position, Vector3.zero); bool tem = false;
        foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
        { if (!tem) { b = r.bounds; tem = true; } else b.Encapsulate(r.bounds); }
        return b;
    }
    private static GameObject Achar(Scene cena, string nome)
    {
        foreach (GameObject r in cena.GetRootGameObjects())
            foreach (Transform t in r.GetComponentsInChildren<Transform>(true))
                if (t.name == nome) return t.gameObject;
        return null;
    }
    private static void Definir(Object alvo, string campo, System.Action<SerializedProperty> valor)
    {
        if (alvo == null) return;
        var so = new SerializedObject(alvo); var p = so.FindProperty(campo);
        if (p == null) { Debug.LogWarning($"[Estação nova] {alvo.GetType().Name}.{campo} não existe."); return; }
        valor(p); so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Desempacotar(GameObject go)
    {
        for (int i = 0; i < 8 && go != null && PrefabUtility.IsPartOfPrefabInstance(go); i++)
        {
            GameObject externo = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            if (externo == go) return;
            PrefabUtility.UnpackPrefabInstance(externo, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        }
    }
}
#endif
