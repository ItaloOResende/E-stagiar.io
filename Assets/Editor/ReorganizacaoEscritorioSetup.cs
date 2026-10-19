using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using static OfficeGameplaySetup;

/// <summary>
/// Reorganização das estações da <c>Office_Placeholder</c> em conjuntos "+": Tools &gt; E-stagiar.io &gt; Escritório real:
/// reorganizar estações. O MODELO é o conjunto montado à mão pelo usuário (4 estações em volta de 2 divisórias em "+",
/// antes em Estacoes_Trabalho/Setor_1, depois em Conjunto_01): as posições/rotações das estações e das divisórias em
/// relação ao centro do "+" são copiadas para os outros conjuntos — 4 no espaço principal (grade 2×2) e 3 no secundário
/// (um abaixo do outro). Reaproveita as estações existentes (só troca pela variante espelhada o desktop cujo PC ficaria
/// fora das divisórias); cria as que faltam. Hierarquia: Estacoes_Trabalho/Setor_Principal|Setor_Secundario/
/// Conjunto_NN/{Divisorias, Estacoes}, estações Estacao_01…28. Funcionários = os solicitantes das fichas (12 nomes):
/// só as estações deles têm plaquinha, ponte de chamado e (Carmen e Paula) NPC; as outras são mesas livres.
/// Também: correções de posição localizadas, NavMesh, ronda do supervisor e pontos de circulação para NPCs.
/// Rodar de novo reaplica (lê os conjuntos já criados). Relatório: Logs/ReorganizacaoEscritorio.txt.
/// </summary>
public static class ReorganizacaoEscritorioSetup
{
    private const string OfficePath = "Assets/Scenes/Office_Placeholder.unity";
    private const string Gameplay = "Assets/Prefabs/Gameplay/";
    private const string LogPath = "Logs/ReorganizacaoEscritorio.txt";
    private static List<string> L => OfficeGameplaySetup.log;

    // centros dos "+" (x, z): principal = grade 2×2; secundário = coluna (um abaixo do outro, longe da bancada de impressora)
    private static readonly Vector2[] Centros =
    {
        new Vector2(22.5f, 6.0f), new Vector2(32.5f, 6.0f), new Vector2(22.5f, -2.5f), new Vector2(32.5f, -2.5f),
        new Vector2(46.9f, 22.0f), new Vector2(46.9f, 14.5f), new Vector2(46.9f, 7.0f),
    };
    private const int ConjuntosPrincipais = 4;
    // tipo por vaga (NO, NE, SO, SE) nos conjuntos novos: 'D' desktop, 'N' notebook
    private static readonly string[] TiposNovos = { "NDDD", "DDDN", "DNDD", "DDND" };

    // funcionários = solicitantes das fichas (TicketsData/Gerados); por conjunto, em ordem de vaga (SE, NE, NO, SO)
    private static readonly (string nome, char tipo)[][] Funcionarios =
    {
        new[] { ("Francisco", 'D'), ("Paula", 'N'), ("Laura", 'N') },
        new[] { ("Carmen", 'D'), ("Marcia", 'D') },
        new[] { ("Rodolfo", 'D'), ("Carina", 'D') },
        new[] { ("Joao", 'D'), ("Luiz", 'D') },
        new[] { ("Augusto", 'D') },
        new[] { ("Ivan", 'D') },
        new[] { ("Joana", 'D') },
    };
    private static readonly Dictionary<string, string> NpcDe = new Dictionary<string, string>
    {
        { "Carmen", "Assets/Art/Models/npc/character2.fbx" }, { "Paula", "Assets/Art/Models/npc/npc2.fbx" },
    };
    private static readonly (int sx, int sz)[] OrdemVagas = { (-1, 1), (1, 1), (-1, -1), (1, -1) };   // NO, NE, SO, SE

    private class Vaga { public Vector3 off; public Quaternion rot; public char tipo; public (int, int) q; }
    private class Div { public Vector3 off; public Quaternion rot; public Vector3 escala; public GameObject prefab; }

    [MenuItem("Tools/E-stagiar.io/Escritório real: reorganizar estações")]
    public static void Run()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        OfficeGameplaySetup.log = new List<string>();
        try
        {
            Scene scene = EditorSceneManager.OpenScene(OfficePath, OpenSceneMode.Single);
            List<Transform> estacoes = Conjuntos(scene);
            Impressoras(scene);
            Posicoes(scene);
            CorrecoesFinaisSetup.MesasNoPiso(scene);
            Bake(scene);
            Associar(scene, estacoes);
            CorrecoesFinaisSetup.Plaquinhas(scene);
            CorrecoesFinaisSetup.Ronda(scene);
            PontosCirculacao(scene);
            Validar(scene, estacoes);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        catch (System.Exception e) { L.Add("ERRO: " + e); Debug.LogException(e); }
        File.WriteAllLines(LogPath, L);
        Debug.Log("[Reorganizacao]\n" + string.Join("\n", L));
    }

    // ================================================================== conjuntos

    private static bool EhEstacao(Transform t) =>
        t.name.StartsWith("Estacao") && (t.GetComponentInChildren<ComputerCase>(true) != null || t.GetComponentInChildren<NotebookEstacao>(true) != null);
    private static bool EhDivisa(Transform t) => t.name.ToLower().StartsWith("divisa") || t.name.StartsWith("Divisoria_");
    private static char Tipo(Transform st) => st.GetComponentInChildren<NotebookEstacao>(true) != null && st.GetComponentInChildren<ComputerCase>(true) == null ? 'N' : 'D';
    private static Vector3 V(Vector2 c) => new Vector3(c.x, 0f, c.y);

    private static List<Transform> Conjuntos(Scene scene)
    {
        Transform raiz = Find(scene, "Estacoes_Trabalho");
        Transform principal = raiz.Find("Setor_Principal") ?? Filho(raiz, "Setor_Principal");
        Transform secundario = raiz.Find("Setor_Secundario") ?? Filho(raiz, "Setor_Secundario");

        // ---- grupos atuais: (estações, divisórias) de cada conjunto
        var grupos = new List<(List<Transform> est, List<Transform> div)>();
        Transform c1 = Find(scene, "Conjunto_01");
        if (c1 != null)
        {
            foreach (Transform setor in new[] { principal, secundario })
                foreach (Transform c in setor.Cast<Transform>().OrderBy(t => t.name))
                    grupos.Add((c.Find("Estacoes").Cast<Transform>().ToList(), c.Find("Divisorias").Cast<Transform>().ToList()));
        }
        else
        {
            Transform s1 = raiz.Find("Setor_1"), s2 = raiz.Find("Setor_2");
            grupos.Add((s1.Cast<Transform>().Where(EhEstacao).ToList(), s1.Cast<Transform>().Where(EhDivisa).ToList()));   // o modelo do usuário
            grupos.Add((new[] { "Estacao_03", "Estacao_04", "Estacao_05", "Estacao_06" }.Select(n => raiz.Find(n) ?? Find(scene, n)).Where(t => t != null).ToList(),
                        new[] { "divisa (1)", "divisa (3)" }.Select(n => Find(scene, n)).Where(t => t != null).ToList()));
            grupos.Add((new List<Transform>(), new List<Transform>()));
            grupos.Add((new List<Transform>(), new List<Transform>()));
            grupos.Add((s2.Cast<Transform>().Where(EhEstacao).ToList(), s2.Find("Divisorias").Cast<Transform>().ToList()));
            grupos.Add((new List<Transform>(), new List<Transform>()));
            grupos.Add((new List<Transform>(), new List<Transform>()));
        }

        // ---- modelo: vagas e divisórias em relação ao centro do "+" (meio das duas divisórias)
        var (mEst, mDiv) = grupos[0];
        if (mEst.Count != 4 || mDiv.Count != 2) throw new System.Exception($"modelo inesperado: {mEst.Count} estações, {mDiv.Count} divisórias");
        Vector3 mc = (mDiv[0].position + mDiv[1].position) * 0.5f; mc.y = 0f;
        var vagas = mEst.Select(s =>
        {
            Vector3 o = s.position - mc; o.y = 0f;
            return new Vaga { off = o, rot = s.rotation, tipo = Tipo(s), q = (o.x < 0 ? -1 : 1, o.z < 0 ? -1 : 1) };
        }).ToList();
        var divs = mDiv.Select(d => new Div
        {
            off = new Vector3(d.position.x - mc.x, d.position.y, d.position.z - mc.z), rot = d.rotation, escala = d.lossyScale,
            prefab = PrefabUtility.GetCorrespondingObjectFromSource(d.gameObject),
        }).ToList();
        L.Add($"modelo (conjunto do usuário): centro {mc:F2}; vagas {string.Join(" ", vagas.Select(v => $"{v.q}:{v.tipo}@{v.off.x:0.00},{v.off.z:0.00} r{v.rot.eulerAngles.y:0}"))}; " +
              $"divisórias escala {divs[0].escala:F2}");

        // ---- monta os 7 conjuntos
        var todas = new List<Transform>();
        int novos = 0, espelhados = 0;
        for (int k = 0; k < Centros.Length; k++)
        {
            Transform setor = k < ConjuntosPrincipais ? principal : secundario;
            string nome = $"Conjunto_{k + 1:00}";
            Transform conj = setor.Find(nome) ?? Filho(setor, nome);
            conj.SetPositionAndRotation(V(Centros[k]), Quaternion.identity);
            Transform gDiv = conj.Find("Divisorias") ?? Filho(conj, "Divisorias");
            Transform gEst = conj.Find("Estacoes") ?? Filho(conj, "Estacoes");
            var (est, dv) = grupos[k];

            // divisórias: as existentes (pela rotação mais parecida) e as que faltam, do mesmo prefab e escala do modelo
            var livres = new List<Transform>(dv);
            for (int i = 0; i < divs.Count; i++)
            {
                Transform d = livres.OrderBy(x => Quaternion.Angle(x.rotation, divs[i].rot)).FirstOrDefault();
                if (d != null) livres.Remove(d);
                else
                {
                    d = ((GameObject)PrefabUtility.InstantiatePrefab(divs[i].prefab, scene)).transform;
                }
                d.SetParent(gDiv, true);
                d.name = $"Divisoria_{(char)('A' + i)}";
                d.SetPositionAndRotation(new Vector3(Centros[k].x + divs[i].off.x, divs[i].off.y, Centros[k].y + divs[i].off.z), divs[i].rot);
                d.localScale = divs[i].escala;
            }
            foreach (Transform sobra in livres) { L.Add($"{nome}: divisória a mais {sobra.name} desligada"); sobra.gameObject.SetActive(false); }

            // estações: cada existente vai para a vaga do mesmo lado (rotação) e quadrante; as vagas vazias recebem novas
            var porVaga = new Dictionary<(int, int), Transform>();
            Vector3 centroide = est.Count > 0 ? est.Aggregate(Vector3.zero, (a, s) => a + s.position) / est.Count : Vector3.zero;
            foreach (Transform s in est.OrderBy(s => s.position.x))
            {
                int sz = Vector3.Dot(s.forward, Vector3.forward) >= 0f ? 1 : -1;
                Vaga v = vagas.Where(x => x.q.Item2 == sz && !porVaga.ContainsKey(x.q)).OrderBy(x => x.q.Item1 * (s.position.x < centroide.x ? 1 : -1)).FirstOrDefault()
                         ?? vagas.FirstOrDefault(x => !porVaga.ContainsKey(x.q));
                if (v == null) { L.Add($"{nome}: estação a mais {s.name} (sem vaga) mantida fora do conjunto"); continue; }
                porVaga[v.q] = s;
            }
            for (int i = 0; i < OrdemVagas.Length; i++)
            {
                (int sx, int sz) q = OrdemVagas[i];
                Vaga v = vagas.First(x => x.q == q);
                if (!porVaga.TryGetValue(q, out Transform s))
                {
                    char tipo = k == 0 ? v.tipo : TiposNovos[Mathf.Min(k - 2 < 0 ? 0 : (k >= 4 ? k - 3 : k - 2), TiposNovos.Length - 1)][i];
                    s = Nova(scene, tipo);
                    novos++;
                }
                s.SetParent(gEst, true);
                s.SetPositionAndRotation(new Vector3(Centros[k].x + v.off.x, 0f, Centros[k].y + v.off.z), v.rot);
                // desktop: o PC fica do lado das divisórias (dentro do "+"); senão troca pela variante espelhada
                if (k != 0 && Tipo(s) == 'D' && PcParaFora(s, q.sx))
                {
                    Transform m = Espelhar(scene, s);
                    if (m != null) { s = m; s.SetParent(gEst, true); s.SetPositionAndRotation(new Vector3(Centros[k].x + v.off.x, 0f, Centros[k].y + v.off.z), v.rot); espelhados++; }
                }
                porVaga[q] = s;
            }
            foreach (var q in OrdemVagas) todas.Add(porVaga[q]);
        }

        // nomes sequenciais e únicos
        for (int i = 0; i < todas.Count; i++)
        {
            todas[i].name = $"Estacao_{i + 1:00}";
            todas[i].SetSiblingIndex(i % 4);
        }
        // grupos antigos que ficaram vazios saem
        foreach (string n in new[] { "Setor_1", "Setor_2", "Divisas_Novas" })
        {
            Transform t = raiz.Find(n);
            if (t != null && t.GetComponentsInChildren<Transform>(true).Length == (t.Find("Divisorias") != null ? 2 : 1)) Object.DestroyImmediate(t.gameObject);
            else if (t != null) L.Add($"{n} ainda tem objetos: mantido");
        }
        L.Add($"conjuntos: {Centros.Length} ({ConjuntosPrincipais} no principal, {Centros.Length - ConjuntosPrincipais} no secundário), " +
              $"{todas.Count} estações ({novos} novas, {espelhados} desktop(s) espelhado(s) para o PC ficar dentro das divisórias)");
        return todas;
    }

    private static Transform Filho(Transform pai, string nome)
    {
        var t = new GameObject(nome).transform;
        t.SetParent(pai, false);
        return t;
    }

    private static Transform Nova(Scene scene, char tipo)
    {
        string p = tipo == 'N' ? "Estacao_Notebook" : "Estacao_Esquerda_Problema";
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + p + ".prefab"), scene);
        return go.transform;
    }

    // O gabinete está do lado de fora do "+" (mesmo lado da vaga em X).
    private static bool PcParaFora(Transform st, int sx)
    {
        ComputerCase c = st.GetComponentInChildren<ComputerCase>(true);
        return c != null && Mathf.Sign(c.transform.position.x - st.position.x) == sx;
    }

    private static readonly Dictionary<string, string> Espelho = new Dictionary<string, string>
    {
        { "Estacao_Direita_Problema", "Estacao_Esquerda_Problema" }, { "Estacao_Esquerda_Problema", "Estacao_Direita_Problema" },
        { "Estacao_Direita_Funcional", "Estacao_Esquerda_Funcional" }, { "Estacao_Esquerda_Funcional", "Estacao_Direita_Funcional" },
    };

    private static Transform Espelhar(Scene scene, Transform st)
    {
        string src = PrefabUtility.GetCorrespondingObjectFromSource(st.gameObject)?.name;
        if (src == null || !Espelho.TryGetValue(src, out string outro)) { L.Add($"{st.name}: PC para fora e sem variante espelhada ({src})"); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Gameplay + outro + ".prefab"), scene);
        go.name = st.name;
        L.Add($"{st.name}: {src} → {outro} (PC para dentro do conjunto)");
        Object.DestroyImmediate(st.gameObject);
        return go.transform;
    }

    // ================================================================== impressoras

    // Cada impressora num grupo próprio (bancada + impressora + toner + resmas). A impressora 3 estava longe da bancada
    // que o usuário moveu: o conjunto dela vai para cima dessa bancada, como as outras.
    private static void Impressoras(Scene scene)
    {
        Transform area = Find(scene, "Area_Impressoras");
        var bancadas = area.GetComponentsInChildren<Transform>(true).Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)
            && PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)?.name == "Bancada_Impressoras").OrderBy(t => t.name).ToList();
        var soltos = area.GetComponentsInChildren<Transform>(true).Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)
            && (t.GetComponent<ImpressoraEstacao>() != null || t.GetComponent<ImpressoraPartItem>() != null)).ToList();
        Transform modelo = bancadas.FirstOrDefault(b => b.name == "Bancada_Impressoras");
        for (int i = 0; i < bancadas.Count; i++)
        {
            Transform b = bancadas[i];
            string suf = b.name.Substring("Bancada_Impressoras".Length);   // "", " (1)", " (2)"
            var meus = soltos.Where(s => s.name.EndsWith(suf == "" ? "" : suf) && (suf != "" || !s.name.EndsWith(")"))).ToList();
            // longe da bancada (>1,5 m): recoloca pelas posições relativas do conjunto modelo
            if (modelo != null && b != modelo)
                foreach (Transform s in meus)
                {
                    if (Flat(s.position - b.position).magnitude < 1.5f) continue;
                    string baseNome = suf == "" ? s.name : s.name.Substring(0, s.name.Length - suf.Length);
                    Transform par = soltos.FirstOrDefault(x => x.name == baseNome);
                    if (par == null) continue;
                    Vector3 rel = Quaternion.Inverse(modelo.rotation) * (par.position - modelo.position);
                    Quaternion relR = Quaternion.Inverse(modelo.rotation) * par.rotation;
                    Vector3 antes = s.position;
                    s.SetPositionAndRotation(b.position + b.rotation * rel, b.rotation * relR);
                    L.Add($"{s.name}: {antes:F2} → {s.position:F2} (sobre {b.name})");
                }
            Transform g = area.Find($"Impressora_{i + 1:00}") ?? Filho(area, $"Impressora_{i + 1:00}");
            b.SetParent(g, true);
            foreach (Transform s in meus) s.SetParent(g, true);
        }
        foreach (Transform t in area.Cast<Transform>().Where(t => t.childCount == 0 && !t.name.StartsWith("Impressora_")).ToList()) Object.DestroyImmediate(t.gameObject);
        L.Add($"impressoras: {bancadas.Count} grupos Impressora_NN (bancada, impressora, toner e resmas)");
    }

    // ================================================================== funcionários, plaquinhas, NPCs, chamados

    private static void Associar(Scene scene, List<Transform> estacoes)
    {
        TicketManager tickets = Object.FindAnyObjectByType<TicketManager>(FindObjectsInactive.Include);
        ScoreKeeper score = tickets.GetComponent<ScoreKeeper>();
        string[] pcIds = Field<string[]>("PcTickets").Concat(Field<string[]>("RedePcTickets")).ToArray();
        string[] nbIds = Field<string[]>("NotebookTickets");
        var placa = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/Placa_Mesa.prefab");
        var setores = new Dictionary<string, string>();
        foreach (string g in AssetDatabase.FindAssets("t:TicketDefinition", new[] { "Assets/TicketsData/Gerados" }))
        {
            var def = AssetDatabase.LoadAssetAtPath<TicketDefinition>(AssetDatabase.GUIDToAssetPath(g));
            if (def != null && !string.IsNullOrEmpty(def.requester)) setores[def.requester] = def.department;
        }

        // quem senta onde: por conjunto, na ordem SE, NE, NO, SO, a 1ª vaga livre do tipo pedido
        var dono = new Dictionary<Transform, string>();
        int[] ordem = { 3, 1, 0, 2 };
        for (int k = 0; k < Funcionarios.Length; k++)
            foreach (var (nome, tipo) in Funcionarios[k])
            {
                if (!setores.ContainsKey(nome)) { L.Add($"FUNCIONÁRIO SEM FICHA (ignorado): {nome}"); continue; }
                Transform st = ordem.Select(i => estacoes[k * 4 + i]).FirstOrDefault(s => !dono.ContainsKey(s) && Tipo(s) == tipo);
                if (st == null) { L.Add($"sem vaga {tipo} no conjunto {k + 1} para {nome}"); continue; }
                dono[st] = nome;
            }

        int porta = 0;
        foreach (Transform st in estacoes)
        {
            foreach (Transform npc in st.GetComponentsInChildren<Transform>(true).Where(c => c.name == "NPC_Estacao").ToList()) Object.DestroyImmediate(npc.gameObject);
            ComputerCase pc = st.GetComponentInChildren<ComputerCase>(true);
            NotebookEstacao nb = st.GetComponentInChildren<NotebookEstacao>(true);
            if (pc != null) MakeFunctional(pc);
            var wall = st.GetComponentInChildren<WallConnectionPoint>(true);
            if (!dono.TryGetValue(st, out string nome))
            {
                // mesa livre: sem funcionário, sem plaquinha e sem chamados (equipamento continua no lugar, funcionando)
                foreach (TicketDeviceBridge b in st.GetComponentsInChildren<TicketDeviceBridge>(true)) Object.DestroyImmediate(b);
                foreach (ComputerCaseTicketBridge b in st.GetComponentsInChildren<ComputerCaseTicketBridge>(true)) Object.DestroyImmediate(b);
                foreach (StationNamePlate p in st.GetComponentsInChildren<StationNamePlate>(true)) Object.DestroyImmediate(p.gameObject);
                var e0 = st.GetComponent<StationEmployee>(); if (e0 != null) Object.DestroyImmediate(e0);
                var pr0 = wall != null ? wall.GetComponent<PontoDeRedeRack>() : null; if (pr0 != null) Object.DestroyImmediate(pr0);
                continue;
            }
            string label = "Estação " + st.name.Substring(st.name.Length - 2);
            var emp = st.GetComponent<StationEmployee>() ?? st.gameObject.AddComponent<StationEmployee>();
            emp.employeeName = nome;
            emp.department = setores[nome];
            emp.stationLabel = label;
            EditorUtility.SetDirty(emp);
            if (st.GetComponentInChildren<StationNamePlate>(true) == null && placa != null)
            {
                var p = (GameObject)PrefabUtility.InstantiatePrefab(placa, scene);
                p.name = "Placa_Mesa";
                p.transform.SetParent(st, false);
            }
            Transform npcT = NpcDe.TryGetValue(nome, out string modelo) ? CorrecoesFinaisSetup.CriarNpc(st, modelo, scene) : null;
            if (pc != null)
            {
                bool temPorta = wall != null && porta < 6;
                string[] ids = temPorta ? pcIds : pcIds.Where(i => i != TestadorCabo.ChamadoRack).ToArray();
                Bridge<PcTicketBridge>(pc.gameObject, tickets, $"PC da {label}", ids, npcT, so => so.FindProperty("scoreKeeper").objectReferenceValue = score);
                if (wall != null)
                {
                    var link = wall.GetComponent<PontoDeRedeRack>() ?? wall.gameObject.AddComponent<PontoDeRedeRack>();
                    var so = new SerializedObject(link);
                    so.FindProperty("portaRack").intValue = temPorta ? ++porta : 0;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                L.Add($"{st.name}: {nome} ({setores[nome]}) · desktop{(temPorta ? $" · porta {porta} do rack" : " · sem porta no rack")}{(npcT != null ? " · NPC" : "")}");
            }
            else
            {
                Bridge<NotebookTicketBridge>(nb.gameObject, tickets, $"Notebook da {label}", nbIds, npcT, Extra("NotebookExtra"));
                L.Add($"{st.name}: {nome} ({setores[nome]}) · notebook{(npcT != null ? " · NPC" : "")}");
            }
        }
        foreach (ImpressoraEstacao pr in Object.FindObjectsByType<ImpressoraEstacao>(FindObjectsSortMode.None))
            Bridge<ImpressoraTicketBridge>(pr.gameObject, tickets, $"Impressora {pr.transform.parent.name.Substring(pr.transform.parent.name.Length - 2)}",
                Field<string[]>("PrinterTickets"), null, Extra("PrinterExtra"));
        typeof(TesteIntegradoSetup).GetMethod("ConfigureRestock", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Invoke(null, new object[] { tickets.gameObject, L });
        L.Add($"funcionários: {dono.Count} (solicitantes das fichas); mesas livres: {estacoes.Count - dono.Count}");
    }

    // ================================================================== correções de posição (localizadas)

    private static void Posicoes(Scene scene)
    {
        Physics.SyncTransforms();
        Transform mapa = Find(scene, "MAPA");
        // estações no piso
        foreach (Transform st in Find(scene, "Estacoes_Trabalho").GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Estacao_") && t.parent.name == "Estacoes"))
            if (Mathf.Abs(st.position.y) > 0.005f) { L.Add($"{st.name}: y {st.position.y:0.000} → 0"); st.position = new Vector3(st.position.x, 0f, st.position.z); }

        // poça do conjunto 3 (cairia no meio das mesas) vai para o corredor entre as colunas
        Transform poca = Find(scene, "obstaculo - poça");
        if (poca != null && Rect.MinMaxRect(19.0f, -5.0f, 26.0f, 0f).Contains(new Vector2(poca.position.x, poca.position.z)))
        {
            L.Add($"obstáculo poça: {poca.position:F2} → corredor (27.5, -2.5)");
            poca.position = new Vector3(27.5f, poca.position.y, -2.5f);
        }

        // móveis de chão (sala de TI, impressoras, puffs): base no piso
        var moveis = new List<Transform>();
        foreach (string g in new[] { "Sala_TI", "Sala_TI_Complementos", "Area_Impressoras" })
            moveis.AddRange(Find(scene, g).GetComponentsInChildren<Transform>().Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)
                && t.GetComponentInParent<Rigidbody>() == null && t.GetComponent<Collider>() != null));
        foreach (Transform m in moveis)
        {
            Bounds b = WorldBounds(m.gameObject);
            if (b.min.y > 0.3f || Mathf.Abs(b.min.y) < 0.015f) continue;
            L.Add($"{PathOf(m)}: base y {b.min.y:0.000} → 0");
            m.position -= Vector3.up * b.min.y;
        }

        // puffs: não podem se sobrepor (o 2º desliza ao longo da parede até ficar livre)
        var puffs = Find(scene, "Sala_TI_Complementos").GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Puff")).OrderBy(t => t.name).ToList();
        for (int i = 1; i < puffs.Count; i++)
        {
            Bounds a = WorldBounds(puffs[i].gameObject);
            for (int n = 0; n < 20 && puffs.Take(i).Any(p => WorldBounds(p.gameObject).Intersects(a)); n++)
            {
                puffs[i].position += Vector3.back * 0.05f;
                a = WorldBounds(puffs[i].gameObject);
            }
            L.Add($"{puffs[i].name}: em {puffs[i].position:F2} (sem sobrepor o outro puff)");
        }

        // itens soltos sobre mesas/bancadas (com Rigidbody): base encostada na superfície de baixo
        Physics.SyncTransforms();
        foreach (Rigidbody rb in Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
        {
            if (rb.GetComponent<PickupInteractable>() == null || rb.GetComponent<NavMeshAgent>() != null) continue;
            Bounds b = WorldBounds(rb.gameObject);
            if (b.size.y < 0.005f) continue;
            float melhor = float.NegativeInfinity;
            foreach (RaycastHit h in Physics.RaycastAll(new Vector3(b.center.x, b.min.y + 0.03f, b.center.z), Vector3.down, 1.5f, ~0, QueryTriggerInteraction.Ignore))
                if (!h.collider.transform.IsChildOf(rb.transform) && h.collider.attachedRigidbody != rb && h.point.y > melhor) melhor = h.point.y;
            if (float.IsNegativeInfinity(melhor)) continue;
            float gap = b.min.y - melhor;
            if (gap > 0.02f) { rb.transform.position -= Vector3.up * gap; L.Add($"{PathOf(rb.transform)}: flutuava {gap * 100:0} cm → apoiado"); }
        }

        // peças de parede: encostadas na parede de trás
        foreach (string g in new[] { "Parede", "Externo" })
            foreach (Transform w in Find(scene, "Sala_TI_Complementos").Find(g))
            {
                Bounds b = WorldBounds(w.gameObject);
                Vector3 tras = -Flat(w.forward).normalized;
                foreach (RaycastHit h in Physics.RaycastAll(b.center, tras, 0.5f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                {
                    if (!h.collider.transform.IsChildOf(mapa)) continue;
                    float fundo = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(tras.x), 0, Mathf.Abs(tras.z))));
                    float folga = h.distance - fundo;
                    if (Mathf.Abs(folga) > 0.01f) { w.position += tras * folga; L.Add($"{w.name}: {folga * 100:0} cm da parede → encostado"); }
                    break;
                }
            }
    }

    // ================================================================== pontos de circulação (NPCs que andam)

    private static void PontosCirculacao(Scene scene)
    {
        Transform raiz = Find(scene, "Navegacao_NPCs");
        if (raiz == null) { raiz = new GameObject("Navegacao_NPCs").transform; SceneManager.MoveGameObjectToScene(raiz.gameObject, scene); }
        Transform pts = raiz.Find("Pontos_Circulacao") ?? Filho(raiz, "Pontos_Circulacao");
        for (int i = pts.childCount - 1; i >= 0; i--) Object.DestroyImmediate(pts.GetChild(i).gameObject);
        Transform player = Find(scene, "Player");
        NavMesh.SamplePosition(player.position, out NavMeshHit ini, 2f, NavMesh.AllAreas);
        var estacoes = Find(scene, "Estacoes_Trabalho").GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Estacao_") && t.parent.name == "Estacoes").ToList();
        Rect[] areas = { Rect.MinMaxRect(13.2f, -7.6f, 40.7f, 11.6f), Rect.MinMaxRect(42.9f, 3.9f, 58.9f, 32.9f), Rect.MinMaxRect(25.1f, 11.6f, 30.0f, 32.9f), Rect.MinMaxRect(25.1f, 26.0f, 42.8f, 32.9f) };
        var cand = new List<Vector3>();
        foreach (Rect a in areas)
            for (float x = a.xMin; x <= a.xMax; x += 1f)
                for (float z = a.yMin; z <= a.yMax; z += 1f)
                {
                    if (!NavMesh.SamplePosition(new Vector3(x, 0f, z), out NavMeshHit h, 0.4f, NavMesh.AllAreas) || h.position.y > 0.3f) continue;
                    if (!NavMesh.FindClosestEdge(h.position, out NavMeshHit e, NavMesh.AllAreas) || e.distance < 0.9f) continue;
                    if (estacoes.Any(s => Flat(s.position - h.position).magnitude < 2.2f)) continue;   // longe das mesas
                    var path = new NavMeshPath();
                    if (!NavMesh.CalculatePath(ini.position, h.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                    cand.Add(h.position);
                }
        var esc = new List<Vector3>();
        if (cand.Count > 0) esc.Add(cand[0]);
        while (esc.Count < 18 && esc.Count < cand.Count) esc.Add(cand.OrderByDescending(p => esc.Min(q => (p - q).sqrMagnitude)).First());
        for (int i = 0; i < esc.Count; i++)
        {
            var t = new GameObject($"Ponto_{i + 1:00}").transform;
            t.SetParent(pts, false);
            t.position = esc[i] + Vector3.up * 0.05f;
        }
        L.Add($"pontos de circulação para NPCs: {esc.Count} (Navegacao_NPCs/Pontos_Circulacao; nenhum NPC ligado — menu 'NPC: ativar caminhada nos selecionados')");
    }

    // ================================================================== validação

    private static void Validar(Scene scene, List<Transform> estacoes)
    {
        var prob = new List<string>();
        Physics.SyncTransforms();
        Transform player = Find(scene, "Player");
        NavMesh.SamplePosition(player.position, out NavMeshHit ini, 2f, NavMesh.AllAreas);
        bool Alcanca(Vector3 alvo)
        {
            if (!NavMesh.SamplePosition(alvo, out NavMeshHit h, 0.8f, NavMesh.AllAreas)) return false;
            var p = new NavMeshPath();
            return NavMesh.CalculatePath(ini.position, h.position, NavMesh.AllAreas, p) && p.status == NavMeshPathStatus.PathComplete;
        }
        L.Add("VALIDAÇÃO");
        var nomes = estacoes.Select(e => e.name).ToList();
        if (nomes.Distinct().Count() != nomes.Count) prob.Add("nomes de estação repetidos");
        var dup = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name.StartsWith("Estacao_")).GroupBy(t => t.name).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (dup.Count > 0) prob.Add("nomes duplicados na cena: " + string.Join(", ", dup));
        foreach (Transform st in estacoes)
        {
            WorkSurface mesa = st.GetComponentInChildren<WorkSurface>();
            Bounds mb = mesa.GetComponent<Collider>().bounds;
            Transform cad = st.Find("Cadeira");
            if (cad != null && !Alcanca(cad.position + Flat(cad.position - mb.center).normalized * 0.45f)) prob.Add($"{st.name}: lugar da cadeira inalcançável");
            foreach (Collider c in Physics.OverlapBox(mb.center + Vector3.up * 0.1f, new Vector3(mb.extents.x, 0.3f, mb.extents.z) * 0.95f, mesa.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c.transform.IsChildOf(st) || c.bounds.max.y < 0.05f || EhDivisa(c.transform) || c.GetComponentInParent<Rigidbody>() != null) continue;
                prob.Add($"{st.name} encosta em {PathOf(c.transform)}");
            }
            var emp = st.GetComponent<StationEmployee>();
            var placa = st.GetComponentInChildren<StationNamePlate>();
            if (emp != null)
            {
                if (st.GetComponentInChildren<TicketDeviceBridge>() == null) prob.Add($"{st.name}: funcionário sem ponte de chamado");
                if (placa == null || placa.GetComponentInChildren<TextMesh>().text != emp.employeeName) prob.Add($"{st.name}: plaquinha ≠ {emp.employeeName}");
            }
            else if (placa != null || st.GetComponentInChildren<TicketDeviceBridge>() != null) prob.Add($"{st.name}: mesa livre com plaquinha/chamados");
            if (Tipo(st) == 'D')
            {
                Vector3 centro = st.parent.parent.position;
                ComputerCase c = st.GetComponentInChildren<ComputerCase>();
                if (Mathf.Abs(c.transform.position.x - centro.x) > Mathf.Abs(st.position.x - centro.x) + 0.05f) prob.Add($"{st.name}: PC fora das divisórias");
            }
        }
        foreach (Transform p in Find(scene, "Ronda_Supervisor"))
            if (!Alcanca(p.position)) prob.Add("ronda " + p.name + " inalcançável");
        foreach (Transform p in Find(scene, "Pontos_Circulacao"))
            if (!Alcanca(p.position)) prob.Add("circulação " + p.name + " inalcançável");
        var funcs = Object.FindObjectsByType<StationEmployee>(FindObjectsSortMode.None).Select(e => e.employeeName).ToList();
        if (funcs.Distinct().Count() != funcs.Count) prob.Add("funcionário repetido");
        L.Add($"  estações {estacoes.Count}, funcionários {funcs.Count} ({string.Join(", ", funcs.OrderBy(x => x))}), NPCs de estação " +
              $"{Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name == "NPC_Estacao")}, impressoras {Object.FindObjectsByType<ImpressoraEstacao>(FindObjectsSortMode.None).Length}");
        L.Add($"VALIDAÇÃO: {prob.Count} problema(s)");
        foreach (string p in prob) L.Add("  PROBLEMA: " + p);
    }
}
