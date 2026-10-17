using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Rodada 3 (2026-10-17): estação 2 com o gabinete do lado do corredor (direita), plaquinhas apoiadas no tampo de
/// todas as mesas e trava da gaveta de cabos de rede prontos durante o chamado de crimpagem. Idempotente.
/// </summary>
public static class CorrecoesRodada3Setup
{
    private const string Cena = "Assets/Scenes/Teste_Integrado.unity";
    private const string Gameplay = "Assets/Prefabs/Gameplay/";
    private const BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Static;

    [MenuItem("Tools/E-stagiar.io/Correções 2026-10-17 (3): estação 2, plaquinhas, gaveta de rede")]
    public static void Tudo()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var log = new List<string>();
        TravaGavetaRede(log);
        var scene = EditorSceneManager.OpenScene(Cena);
        Estacao2(scene, log);
        foreach (StationEmployee e in Object.FindObjectsByType<StationEmployee>(FindObjectsSortMode.None)) Placa(e, log);
        Chamar(typeof(CorrecoesGameplaySetup), "AssarNavMesh", log);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Rodada3] " + string.Join(" · ", log));
    }

    // ------------------------------------------------------------------ gaveta de cabos de rede

    private static void TravaGavetaRede(List<string> log)
    {
        string path = Gameplay + "Gaveteiro_Cabos.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (DrawerContents d in root.GetComponentsInChildren<DrawerContents>(true))
            {
                var so = new SerializedObject(d);
                SerializedProperty entries = so.FindProperty("entries");
                bool rede = false;
                for (int i = 0; i < entries.arraySize; i++)
                {
                    var p = entries.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue;
                    if (p != null && p.name == "Pickup_CaboRede") rede = true;
                }
                if (!rede) continue;
                if (d.GetComponent<DrawerTicketLock>() == null) d.gameObject.AddComponent<DrawerTicketLock>();
                log.Add($"trava de crimpagem na gaveta {d.name} do Gaveteiro_Cabos");
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ------------------------------------------------------------------ estação 2

    // A Estacao_2 (variante "Direita", girada 180°) ficava com o gabinete do lado da divisória/parede e o NPC no lado do
    // corredor. Troca pela variante "Esquerda" no MESMO lugar (gabinete do lado direito, aberto para o corredor, como a
    // Estacao_3); a antiga fica desativada (não é apagada). Funcionário, placa, NPC e chamados passam para a nova.
    private static void Estacao2(UnityEngine.SceneManagement.Scene scene, List<string> log)
    {
        Transform antiga = Estacao("Estacao_2");
        if (antiga == null) { log.Add("FALTA Estacao_2"); return; }
        if (PrefabUtility.GetCorrespondingObjectFromSource(antiga.gameObject) != null &&
            PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(antiga.gameObject).Contains("Esquerda"))
        { log.Add("Estacao_2 já é a variante Esquerda"); return; }

        TicketManager tickets = Object.FindAnyObjectByType<TicketManager>(FindObjectsInactive.Include);
        ScoreKeeper score = tickets != null ? tickets.GetComponent<ScoreKeeper>() : null;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + "Estacao_Esquerda_Funcional.prefab");
        var nova = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).transform;
        nova.SetParent(antiga.parent, false);
        nova.SetPositionAndRotation(antiga.position, antiga.rotation);
        antiga.name = "Estacao_2_Antiga (desativada)";
        nova.name = "Estacao_2";
        nova.SetSiblingIndex(antiga.GetSiblingIndex());

        // funcionário
        StationEmployee velho = antiga.GetComponent<StationEmployee>();
        var emp = nova.gameObject.AddComponent<StationEmployee>();
        emp.employeeName = velho != null ? velho.employeeName : "Marcia";
        emp.department = velho != null ? velho.department : "Financeiro";
        // placa vai junto (reposicionada abaixo)
        Transform placa = antiga.Find("Placa_Mesa");
        if (placa != null) placa.SetParent(nova, true);
        // NPC: sai da estação antiga e fica ao lado da nova, do lado oposto ao gabinete (como o EnsureNpc)
        Transform npc = null;
        foreach (Transform t in antiga.GetComponentsInChildren<Transform>(true)) if (t.name == "NPC_Estacao_2") npc = t;
        if (npc != null)
        {
            npc.SetParent(nova, true);
            ComputerCase c = nova.GetComponentInChildren<ComputerCase>(true);
            float lado = Mathf.Sign(nova.InverseTransformPoint(c.transform.position).x);
            npc.position = nova.TransformPoint(new Vector3(-lado * 1.45f, 0f, 0.55f));
            Vector3 olhar = nova.position - npc.position; olhar.y = 0f;
            npc.rotation = Quaternion.LookRotation(olhar.normalized, Vector3.up);
        }
        antiga.gameObject.SetActive(false);

        // chamados (mesmos ids e etiqueta da antiga), porta do rack e obstáculo de NavMesh
        PcTicketBridge velhaPonte = antiga.GetComponentInChildren<PcTicketBridge>(true);
        string[] ids = velhaPonte != null ? velhaPonte.AcceptedTicketIds.ToArray() : new string[0];
        ComputerCase pcCase = nova.GetComponentInChildren<ComputerCase>(true);
        System.Action<SerializedObject> extra = so => so.FindProperty("scoreKeeper").objectReferenceValue = score;
        typeof(TesteIntegradoSetup).GetMethod("MakeFunctional", Priv).Invoke(null, new object[] { pcCase });
        typeof(TesteIntegradoSetup).GetMethod("Bridge", Priv).MakeGenericMethod(typeof(PcTicketBridge))
            .Invoke(null, new object[] { pcCase.gameObject, tickets, "PC da Estação 2", ids, npc, extra });
        typeof(TesteIntegradoSetup).GetMethod("LinkRackPort", Priv).Invoke(null, new object[] { nova, 2, log });
        typeof(TesteIntegradoSetup).GetMethod("AddObstacle", Priv).Invoke(null, new object[] { nova.gameObject });
        log.Add("Estacao_2 trocada pela variante Esquerda (gabinete do lado do corredor); a antiga ficou desativada");
    }

    // ------------------------------------------------------------------ plaquinha

    // Sobre o TAMPO (raio de cima no colisor da mesa), do lado da cadeira, na metade oposta ao computador e longe da beira onde fica o NPC, alinhada aos
    // eixos da mesa (nada de direção diagonal até a cadeira) e virada para a cadeira.
    private static void Placa(StationEmployee e, List<string> log)
    {
        Transform placa = e.transform.Find("Placa_Mesa");
        WorkSurface mesa = e.GetComponentInChildren<WorkSurface>(true);
        if (placa == null || mesa == null) { log.Add($"{e.name}: {(placa == null ? "sem placa" : "sem mesa")}"); return; }

        Transform m = mesa.transform;
        Bounds lb = LocalBounds(m);
        // profundidade = o eixo horizontal local mais curto do tampo
        bool profundidadeZ = lb.extents.z <= lb.extents.x;
        Vector3 eixoP = profundidadeZ ? Vector3.forward : Vector3.right, eixoL = profundidadeZ ? Vector3.right : Vector3.forward;
        float meiaP = profundidadeZ ? lb.extents.z : lb.extents.x, meiaL = profundidadeZ ? lb.extents.x : lb.extents.z;

        Transform cadeira = e.transform.Find("Cadeira");
        Vector3 local = cadeira != null ? m.InverseTransformPoint(cadeira.position) - lb.center : -eixoP;
        float sinalP = Vector3.Dot(local, eixoP) >= 0f ? 1f : -1f;
        Transform pc = null;
        foreach (Transform t in e.GetComponentsInChildren<Transform>())   // só os ATIVOS (gabinete ou notebook em uso)
            if (t.GetComponent<ComputerCase>() != null || t.GetComponent<NotebookEstacao>() != null) pc = t;
        float sinalL = pc != null && Vector3.Dot(m.InverseTransformPoint(pc.position) - lb.center, eixoL) > 0f ? -1f : 1f;

        Vector3 alvoLocal = lb.center + eixoP * sinalP * (meiaP - 0.14f) + eixoL * sinalL * (meiaL - 0.6f);
        Vector3 alvo = m.TransformPoint(alvoLocal);
        float y = Topo(mesa, alvo, out bool achou);
        if (!achou) { log.Add($"{e.name}: tampo não encontrado sob a placa"); return; }
        Vector3 frente = m.TransformDirection(eixoP * sinalP); frente.y = 0f;
        placa.SetPositionAndRotation(new Vector3(alvo.x, y, alvo.z), Quaternion.LookRotation(frente.normalized, Vector3.up));
        placa.GetComponent<StationNamePlate>()?.Refresh();
        log.Add($"placa {e.employeeName} em {e.name} (tampo y={y:0.000})");
    }

    // Bounds dos renderers do tampo no espaço local da mesa.
    private static Bounds LocalBounds(Transform m)
    {
        bool tem = false; Bounds b = default;
        foreach (MeshFilter mf in m.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Bounds mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1));
                Vector3 l = m.InverseTransformPoint(mf.transform.TransformPoint(c));
                if (!tem) { b = new Bounds(l, Vector3.zero); tem = true; } else b.Encapsulate(l);
            }
        }
        return b;
    }

    private static float Topo(WorkSurface mesa, Vector3 p, out bool achou)
    {
        Physics.SyncTransforms();
        float best = float.NegativeInfinity;
        foreach (RaycastHit h in Physics.RaycastAll(new Vector3(p.x, p.y + 2f, p.z), Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
            if (h.collider.transform.IsChildOf(mesa.transform) && h.point.y > best) best = h.point.y;
        achou = !float.IsNegativeInfinity(best);
        return achou ? best : p.y;
    }

    // ------------------------------------------------------------------ util

    private static void Chamar(System.Type t, string metodo, params object[] args) =>
        t.GetMethod(metodo, Priv).Invoke(null, args);

    private static Transform Estacao(string nome)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == nome && t.GetComponentInChildren<ComputerCase>(true) != null) return t;
        return null;
    }
}
