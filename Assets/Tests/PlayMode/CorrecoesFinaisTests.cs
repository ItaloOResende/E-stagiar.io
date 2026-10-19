using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// Correções finais na cena oficial <c>Office_Placeholder</c> (menu "Escritório real: correções finais"), pelo caminho
/// REAL do jogador: a câmera do Player mira o objeto, o <c>InteractionDetector</c> acha o alvo como no jogo e um mouse/
/// teclado VIRTUAL do Input System aperta o botão direito / Esc (o detector e as telas leem o mesmo dispositivo que o
/// jogo). Cobre: telas de manutenção do gabinete e do monitor (e notebook/impressora/rack), coleta sem atravessar
/// divisórias, pausa, defeitos de cabo com cabo do gaveteiro, armário de reserva, rack 3D e layout/associações.
/// </summary>
public class CorrecoesFinaisTests
{
    private const string Cena = "Assets/Scenes/Office_Placeholder.unity";
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private static Type T(string n) => Type.GetType(n + ", Assembly-CSharp", true);
    private static Type C(string n) => Type.GetType(n + ", EstagiarIo.Computer", true);
    private static object Get(object o, string p) => o.GetType().GetProperty(p, Any).GetValue(o);
    private static object GetF(object o, string f) => o.GetType().GetField(f, Any).GetValue(o);
    private static void SetF(object o, string f, object v) => o.GetType().GetField(f, Any).SetValue(o, v);
    private static object Call(object o, string m, params object[] a)
    {
        foreach (MethodInfo mi in o.GetType().GetMethods(Any))
        {
            ParameterInfo[] ps = mi.GetParameters();
            if (mi.Name != m || ps.Length != a.Length) continue;
            bool ok = true;
            for (int i = 0; i < ps.Length && ok; i++)
            {
                Type pt = ps[i].ParameterType.IsByRef ? ps[i].ParameterType.GetElementType() : ps[i].ParameterType;
                ok = a[i] == null ? !pt.IsValueType || ps[i].IsOut : pt.IsInstanceOfType(a[i]);
            }
            if (ok) return mi.Invoke(o, a);
        }
        throw new MissingMethodException(o.GetType().Name, m);
    }

    private Mouse mouse;
    private Keyboard keyboard;
    private InputSettings settingsOriginais;

    [SetUp]
    public void SetUp()
    {
        // batchmode = janela sem foco: sem isto o Input System desliga os dispositivos e nenhum clique chega ao jogo.
        // Uma CÓPIA das configurações (o asset do projeto não é alterado), devolvida no TearDown.
        settingsOriginais = InputSystem.settings;
        InputSettings s = Object.Instantiate(settingsOriginais);
        s.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        s.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings = s;
        mouse = InputSystem.AddDevice<Mouse>();
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse.MakeCurrent();
        keyboard.MakeCurrent();
    }

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (mouse != null) InputSystem.RemoveDevice(mouse);
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        if (settingsOriginais != null) InputSystem.settings = settingsOriginais;
    }


    // Sai da cena carregada: os testes seguintes montam cenas próprias e não podem herdar os sistemas do escritório.
    [UnityTearDown]
    public IEnumerator SairDaCena()
    {
        Time.timeScale = 1f;
        var vazia = UnityEngine.SceneManagement.SceneManager.CreateScene("Vazia_" + Guid.NewGuid().ToString("N").Substring(0, 6));
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(vazia);
        for (int i = UnityEngine.SceneManagement.SceneManager.sceneCount - 1; i >= 0; i--)
        {
            var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (s != vazia && s.isLoaded) yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(s);
        }
    }

    // ------------------------------------------------------------------ helpers

    private Component det, player, screens;
    private Camera cam;

    private IEnumerator Partida(string primeiroChamado = null)
    {
#if UNITY_EDITOR
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(Cena, new UnityEngine.SceneManagement.LoadSceneParameters());
#endif
        for (int i = 0; i < 4; i++) yield return null;
        Component stage = (Component)Object.FindAnyObjectByType(T("StageController"));
        if (primeiroChamado != null) Fixar(stage.GetComponent(T("TicketManager")), primeiroChamado);
        Component menu = stage.GetComponent(T("MainMenuScreen"));
        Assert.IsNotNull(menu, "a cena abre no menu inicial");
        Call(menu, "Choose", Enum.Parse(T("GameMode"), "Normal"));
        yield return null;
        det = (Component)Object.FindAnyObjectByType(T("InteractionDetector"));
        player = det;
        screens = det.GetComponent(T("UIScreenManager"));
        cam = (Camera)Get(det, "PlayerCamera");
        alturaRaiz = player.transform.position.y;   // de pé no piso, no começo da partida
    }

    private float alturaRaiz = 0.98f;

    private IEnumerator Clique(bool direito)
    {
        var s = new MouseState();
        s = s.WithButton(direito ? MouseButton.Right : MouseButton.Left, true);
        InputSystem.QueueStateEvent(mouse, s);
        yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState());
        yield return null;
    }

    private IEnumerator Tecla(Key k)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(k));
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
    }

    private static Bounds Limites(Component c)
    {
        var rs = c.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    /// <summary>
    /// Leva o jogador (no chão, no NavMesh, sem encostar em nada) para um ponto de onde o OLHO vê o objeto sem nada na
    /// frente e vira câmera e corpo para ele. Devolve true se achou o ponto.
    /// </summary>
    private bool PosicionarVendo(Component alvo, out Vector3 olho)
    {
        olho = default;
        Transform p = player.transform;
        float alturaOlho = cam.transform.position.y - p.position.y;
        Bounds b = Limites(alvo);
        var cc = p.GetComponent<CharacterController>();
        foreach (float dist in new[] { 0.9f, 1.15f, 1.4f })
            for (int k = 0; k < 16; k++)
            {
                float a = k * 22.5f * Mathf.Deg2Rad;
                Vector3 chao = new Vector3(b.center.x + Mathf.Cos(a) * dist, 0f, b.center.z + Mathf.Sin(a) * dist);
                if (!NavMesh.SamplePosition(chao, out NavMeshHit nh, 0.25f, NavMesh.AllAreas) || nh.position.y > 0.2f) continue;
                Vector3 raiz = new Vector3(chao.x, alturaRaiz, chao.z);
                Vector3 e = raiz + Vector3.up * alturaOlho;
                if (Physics.OverlapCapsule(raiz - Vector3.up * 0.4f, raiz + Vector3.up * 0.5f, 0.3f, ~0, QueryTriggerInteraction.Ignore)
                    .Any(c => !c.transform.IsChildOf(p) && c.bounds.max.y > 0.05f)) continue;
                Vector3 dir = b.center - e;
                if (dir.magnitude > 1.9f) continue;
                var hits = Physics.RaycastAll(e, dir.normalized, dir.magnitude + 0.2f, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => !h.collider.transform.IsChildOf(p)).OrderBy(h => h.distance).ToArray();
                if (hits.Length == 0 || !hits[0].collider.transform.IsChildOf(alvo.transform)) continue;
                cc.enabled = false;
                p.position = raiz;
                Mirar(b.center);
                cc.enabled = true;
                olho = e;
                return true;
            }
        return false;
    }

    private void Mirar(Vector3 ponto)
    {
        Transform p = player.transform;
        Vector3 d = ponto - cam.transform.position;
        Vector3 plano = new Vector3(d.x, 0f, d.z);
        p.rotation = Quaternion.LookRotation(plano.normalized);
        float pitch = Mathf.Atan2(-d.y, plano.magnitude) * Mathf.Rad2Deg;
        Component look = p.GetComponent(T("PlayerLook"));
        SetF(look, "xRotation", pitch);
        cam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private IEnumerator MirarEm(Component alvo)
    {
        Assert.IsTrue(PosicionarVendo(alvo, out _), $"há um ponto livre de onde se vê {alvo.name}");
        yield return null; yield return null;
    }

    private Component Secundario() => Get(det, "CurrentSecondaryTarget") as Component;
    private Component Primario() => Get(det, "CurrentTarget") as Component;
    private bool TelaAberta => (bool)Get(screens, "IsOpen");

    private Component TelaAtual() => Get(screens, "Current") as Component;

    // ------------------------------------------------------------------ 1. telas de manutenção

    [UnityTest]
    public IEnumerator Gabinete_E_Monitor_AbremAManutencao_PeloCliqueDireito_EFechamComEsc()
    {
        yield return Partida();
        Component pc = Object.FindObjectsByType(C("ComputerCase"), FindObjectsSortMode.None).Cast<Component>()
            .First(c => c.GetComponentInParent(T("StationEmployee")) != null);
        foreach (string alvoNome in new[] { "gabinete", "monitor" })
        {
            Component alvo = alvoNome == "gabinete" ? pc
                : ((Transform)Get(pc.GetComponent(C("ComputerPower")), "Monitor")).GetComponentInParent(T("PickupInteractable"));
            yield return MirarEm(alvo);
            Component sec = Secundario();
            Assert.IsNotNull(sec, $"{alvoNome}: a mira oferece o clique direito (ícone da ferramenta)");
            yield return Clique(true);
            Assert.IsTrue(TelaAberta, $"{alvoNome}: clique direito abre a tela");
            Component tela = TelaAtual();
            Assert.IsNotNull(tela.GetComponent(T("ComputerScreenController")), $"{alvoNome}: é a tela de manutenção do PC");
            Canvas canvas = tela.GetComponentInParent<Canvas>();
            Assert.IsNotNull(canvas, $"{alvoNome}: a tela está dentro de um Canvas (antes ficava fora e não aparecia)");
            Assert.IsTrue(tela.gameObject.activeInHierarchy && canvas.isActiveAndEnabled, $"{alvoNome}: a tela está visível");
            Assert.AreEqual(pc.gameObject, Get(tela.GetComponent(T("UIScreen")), "Source"),
                $"{alvoNome}: a tela é deste PC");
            Assert.IsFalse(((Behaviour)player.GetComponent(T("PlayerLook"))).enabled, "câmera parada com a tela aberta");
            Assert.AreEqual(CursorLockMode.None, Cursor.lockState, "cursor livre na tela");
            yield return Tecla(Key.Escape);
            yield return null;
            Assert.IsFalse(TelaAberta, $"{alvoNome}: Esc fecha");
            Assert.IsFalse((bool)T("PauseMenu").GetProperty("IsPaused").GetValue(null), "o Esc que fechou a tela não pausa");
            Assert.IsTrue(((Behaviour)player.GetComponent(T("PlayerLook"))).enabled, "câmera volta");
            Assert.IsTrue(((Behaviour)player.GetComponent(T("PlayerMovement"))).enabled, "movimento volta");
            // em batchmode (sem janela em foco) a Unity não trava o cursor: aí só dá para conferir no Editor
            if (!Application.isBatchMode) Assert.AreEqual(CursorLockMode.Locked, Cursor.lockState, "cursor travado de novo no 3D");
        }
    }

    [UnityTest]
    public IEnumerator Notebook_Impressora_ERack_ContinuamAbrindoPeloCliqueDireito()
    {
        yield return Partida();
        var alvos = new List<(Component c, string tipo)>
        {
            ((Component)Object.FindObjectsByType(T("NotebookEstacao"), FindObjectsSortMode.None)[0], "NotebookTela"),
            ((Component)Object.FindObjectsByType(T("ImpressoraEstacao"), FindObjectsSortMode.None)[0], "ImpressoraTela"),
            (Object.FindObjectsByType(T("RedeEstacaoInteractable"), FindObjectsSortMode.None).Cast<Component>()
                .First(r => r.GetComponent(T("ImpressoraEstacao")) == null && r.GetComponent(T("NotebookEstacao")) == null && r.GetComponent<Rigidbody>() == null), "RedeRackScreen"),
        };
        foreach (var (c, tipo) in alvos)
        {
            yield return MirarEm(c);
            Assert.IsNotNull(Secundario(), $"{c.name}: clique direito disponível");
            yield return Clique(true);
            Assert.IsTrue(TelaAberta, $"{c.name}: abriu");
            Assert.IsNotNull(TelaAtual().GetComponent(T(tipo)), $"{c.name}: tela {tipo}");
            Assert.IsNotNull(TelaAtual().GetComponentInParent<Canvas>(), "dentro de um Canvas");
            yield return Tecla(Key.Escape);
            yield return null;
            Assert.IsFalse(TelaAberta);
        }
    }

    // ------------------------------------------------------------------ 2. coleta

    [UnityTest]
    public IEnumerator Coleta_PeloLadoVisivel_ENuncaAtravesDeDivisoria()
    {
        yield return Partida();
        int testados = 0;
        foreach (Component item in Object.FindObjectsByType(T("PickupInteractable"), FindObjectsSortMode.None).Cast<Component>()
                     .Where(i => i.GetComponentInParent(T("StationEmployee")) != null && i.GetComponent(C("ComputerCase")) == null).Take(12))
        {
            if (!PosicionarVendo(item, out _)) continue;
            yield return null; yield return null;
            Assert.AreSame(item, Primario() ?? (object)"nada", $"{item.transform.parent?.parent?.name}/{item.name}: visível e ao alcance = alvo");
            testados++;
            // agora de trás de uma divisória/parede: com algo na frente, não pode ser alvo (nem pela tolerância)
            Bounds b = Limites(item);
            Transform p = player.transform;
            var cc = p.GetComponent<CharacterController>();
            for (int k = 0; k < 16; k++)
            {
                float a = k * 22.5f * Mathf.Deg2Rad;
                Vector3 raiz = new Vector3(b.center.x + Mathf.Cos(a) * 1.2f, alturaRaiz, b.center.z + Mathf.Sin(a) * 1.2f);
                Vector3 e = raiz + Vector3.up * (cam.transform.position.y - p.position.y);
                if (!NavMesh.SamplePosition(new Vector3(raiz.x, 0f, raiz.z), out NavMeshHit nh2, 0.25f, NavMesh.AllAreas) || nh2.position.y > 0.2f) continue;
                Vector3 dir = b.center - e;
                var hits = Physics.RaycastAll(e, dir.normalized, dir.magnitude, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => !h.collider.transform.IsChildOf(p)).OrderBy(h => h.distance).ToArray();
                if (hits.Length == 0 || hits[0].collider.transform.IsChildOf(item.transform) || hits[0].collider.GetComponentInParent(T("Interactable")) != null) continue;
                // "atrás" de verdade: nenhum ponto do item (cantos e centro) à vista desse olho
                bool algumVisivel = false;
                for (int q = 0; q < 9 && !algumVisivel; q++)
                {
                    Vector3 alvo = q == 8 ? b.center : b.center + Vector3.Scale(b.extents * 0.95f, new Vector3((q & 1) * 2 - 1, (q & 2) - 1, (q & 4) / 2 - 1));
                    Vector3 dq = alvo - e;
                    var hq = Physics.RaycastAll(e, dq.normalized, dq.magnitude + 0.05f, ~0, QueryTriggerInteraction.Ignore)
                        .Where(h => !h.collider.transform.IsChildOf(p)).OrderBy(h => h.distance).FirstOrDefault();
                    algumVisivel = hq.collider != null && hq.collider.transform.IsChildOf(item.transform);
                }
                if (algumVisivel) continue;
                cc.enabled = false; p.position = raiz; Mirar(b.center); cc.enabled = true;
                yield return null;
                var camHits = Physics.RaycastAll(cam.transform.position, cam.transform.forward, 2.5f, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => !h.collider.transform.IsChildOf(p)).OrderBy(h => h.distance).Take(3).Select(h => $"{h.collider.name}@{h.distance:0.000}");
                Assert.AreNotSame(item, Primario(), $"{item.name}: com {hits[0].collider.name} na frente não pode ser pego " +
                    $"(olho esperado {e:F3}, câmera {cam.transform.position:F3}, raio da câmera: {string.Join(" ", camHits)}; item {Limites(item).center:F3} ext {Limites(item).extents:F3})");
                break;
            }
        }
        Assert.GreaterOrEqual(testados, 6, "itens das estações testados");
    }

    // ------------------------------------------------------------------ 3. pausa

    [UnityTest]
    public IEnumerator Esc_PausaORelogio_ESupervisor_ERetomaComTudoIgual()
    {
        yield return Partida();
        Component stage = (Component)Object.FindAnyObjectByType(T("StageController"));
        Component tm = stage.GetComponent(T("TicketManager"));
        Component score = stage.GetComponent(T("ScoreKeeper"));
        // um item na mão antes de pausar
        Component item = Object.FindObjectsByType(T("PickupInteractable"), FindObjectsSortMode.None).Cast<Component>()
            .First(i => i.GetComponent(C("ToolItem")) != null && i.gameObject.activeInHierarchy);
        Component holder = det.GetComponent(T("ItemHolder"));
        Call(holder, "Pick", item);
        yield return new WaitForSeconds(0.3f);
        int chamados = ((IList)Get(tm, "Tickets")).Count;
        int pontos = (int)Get(score, "Score");

        yield return Tecla(Key.Escape);
        float antes = (float)Get(stage, "SecondsRemaining");   // já parado
        Assert.IsTrue((bool)T("PauseMenu").GetProperty("IsPaused").GetValue(null), "Esc no 3D pausa");
        Assert.AreEqual(0f, Time.timeScale, "jogo parado");
        Assert.AreEqual(CursorLockMode.None, Cursor.lockState, "cursor livre no menu");
        Assert.IsFalse(((Behaviour)Object.FindAnyObjectByType(T("SupervisorPerception"))).enabled, "supervisor não percebe nada na pausa");
        Assert.IsFalse(((Behaviour)Object.FindAnyObjectByType(T("ConductMonitor"))).enabled, "sem registro de conduta na pausa");
        yield return new WaitForSecondsRealtime(1.5f);
        Assert.AreEqual(antes, (float)Get(stage, "SecondsRemaining"), 0.001f, "o relógio da fase não anda na pausa");

        yield return Tecla(Key.Escape);
        yield return new WaitForSecondsRealtime(0.4f);   // o PauseMenu fecha com animação (tempo real)
        Assert.IsFalse((bool)T("PauseMenu").GetProperty("IsPaused").GetValue(null), "Esc retoma");
        Assert.AreEqual(1f, Time.timeScale);
        Assert.IsTrue(((Behaviour)player.GetComponent(T("PlayerLook"))).enabled && ((Behaviour)player.GetComponent(T("PlayerMovement"))).enabled);
        Assert.IsTrue(((Behaviour)Object.FindAnyObjectByType(T("SupervisorPerception"))).enabled);
        if (!Application.isBatchMode) Assert.AreEqual(CursorLockMode.Locked, Cursor.lockState, "controle em 1ª pessoa de volta");
        Assert.AreSame(item, Get(holder, "Current"), "o item continua na mão");
        Assert.AreEqual(chamados, ((IList)Get(tm, "Tickets")).Count);
        Assert.AreEqual(pontos, (int)Get(score, "Score"));
        yield return new WaitForSeconds(0.5f);
        Assert.Less((float)Get(stage, "SecondsRemaining"), antes, "o relógio volta a andar");
    }

    // ------------------------------------------------------------------ 4. defeitos de cabo

    private static string escolheId;
    private static D Escolher<D>(List<D> lista, D anterior) where D : ScriptableObject
    {
        foreach (D d in lista) if ((string)d.GetType().GetField("ticketId").GetValue(d) == escolheId) return d;
        return null;
    }

    // O 1º chamado da partida (criado ao escolher o modo no menu) é este.
    private static void Fixar(Component tm, string id)
    {
        Type td = T("TicketDefinition");
        Type funcT = typeof(Func<,,>).MakeGenericType(typeof(List<>).MakeGenericType(td), td, td);
        MethodInfo escolher = typeof(CorrecoesFinaisTests).GetMethod(nameof(Escolher), BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(td);
        escolheId = id;
        tm.GetType().GetProperty("SelectionOverride").SetValue(tm, Delegate.CreateDelegate(funcT, escolher));
    }

    private static IEnumerator Atender(Action<Component, object> pronto)
    {
        Component tm = (Component)Object.FindAnyObjectByType(T("TicketManager"));
        IList tickets = (IList)Get(tm, "Tickets");
        Assert.Greater(tickets.Count, 0, "chegou o 1º chamado");
        object ticket = tickets[0];
        Assert.AreEqual(escolheId, T("TicketDefinition").GetField("ticketId").GetValue(GetF(ticket, "Definition")));
        Assert.IsTrue((bool)Call(tm, "StartTicket", (int)GetF(ticket, "Number")));
        for (int i = 0; i < 4; i++) yield return null;
        yield return new WaitForSeconds(0.3f);
        pronto(tm, ticket);
    }

    [Test]
    public void DefeitosDeCabo_RodizioPelosPesos_SemSorteio()
    {
        var go = new GameObject("pc");
        go.SetActive(false);
        go.AddComponent(C("ComputerCase"));
        go.AddComponent(C("ComputerPower"));
        Component b = go.AddComponent(T("PcTicketBridge"));
        T("PcTicketBridge").GetField("cableDefectTurn", Any).SetValue(null, 0);
        var seq = Enumerable.Range(0, 6).Select(_ => Call(b, "NextCableDefect").ToString()).ToList();
        Assert.AreEqual(new[] { "LooseAtWall", "LooseAtDevice", "Missing", "LooseAtWall", "LooseAtDevice", "Missing" }, seq);
        SetF(b, "pesoAusente", 0); SetF(b, "pesoSoltoNaParede", 2);
        T("PcTicketBridge").GetField("cableDefectTurn", Any).SetValue(null, 0);
        seq = Enumerable.Range(0, 3).Select(_ => Call(b, "NextCableDefect").ToString()).ToList();
        Assert.AreEqual(new[] { "LooseAtWall", "LooseAtDevice", "LooseAtWall" }, seq, "pesos 2/1/0");
        Object.DestroyImmediate(go);
    }

    [UnityTest]
    public IEnumerator SemEnergia_CaboAusente_SoConcluiComOCaboDoGaveteiro()
    {
#if UNITY_EDITOR
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(Cena, new UnityEngine.SceneManagement.LoadSceneParameters());
#endif
        for (int i = 0; i < 4; i++) yield return null;
        // rodízio 1/1/1 na 3ª posição = "cabo ausente" (as outras duas variações: teste de rodízio acima)
        T("PcTicketBridge").GetField("cableDefectTurn", Any).SetValue(null, 2);
        Component stage = (Component)Object.FindAnyObjectByType(T("StageController"));
        Fixar(stage.GetComponent(T("TicketManager")), "pc-sem-energia");
        Call(stage.GetComponent(T("MainMenuScreen")), "Choose", Enum.Parse(T("GameMode"), "Normal"));
        yield return null;
        det = (Component)Object.FindAnyObjectByType(T("InteractionDetector")); player = det; cam = (Camera)Get(det, "PlayerCamera");
        Component tm = null; object t = null;
        yield return Atender((m, x) => { tm = m; t = x; });
        Component pc = Object.FindObjectsByType(T("PcTicketBridge"), FindObjectsSortMode.None).Cast<Component>().First(b => Get(b, "Current") == t);
        Assert.AreEqual("Missing", Get(pc, "LastCableDefect").ToString());
        Component powerC = pc.GetComponent(C("ComputerPower"));
        Assert.IsNull(Call(powerC, "CableItemOf", Enum.Parse(C("PcLink"), "Power")), "o cabo sumiu da estação");
        Assert.IsFalse((bool)Get(powerC, "PowerConnected"), "sem energia de verdade");
        Assert.IsFalse((bool)Call(tm, "TryFinishTicket", (int)GetF(t, "Number")), "não conclui sem a correção");
        // o jogador pega um cabo de energia no gaveteiro de cabos e liga as duas pontas
        bool Energia(Component x) => x != null && x.GetComponent(C("CableItem")) != null && Get(x.GetComponent(C("CableItem")), "Type").ToString() == "Power";
        Component gaveta = Object.FindObjectsByType(T("DrawerContents"), FindObjectsSortMode.None).Cast<Component>()
            .First(d => Enumerable.Range(0, (int)Get(d, "EntryCount")).Any(e => Energia(Call(d, "EntryPrefab", e) as Component)));
        Call(gaveta.GetComponent(T("OpenableInteractable")), "SetOpen", true);
        yield return new WaitForSeconds(0.8f);
        Component cabo = ((IList)Get(gaveta, "Inside")).Cast<Component>().FirstOrDefault(x => x != null && x.GetComponent(C("CableItem")) != null
                         && Get(x.GetComponent(C("CableItem")), "Type").ToString() == "Power");
        Assert.IsNotNull(cabo, "há cabo de energia no gaveteiro");
        Component holder = det.GetComponent(T("ItemHolder"));
        Call(holder, "Pick", cabo);
        Component pw = pc.GetComponent(C("ComputerPower"));
        MethodInfo usePort = C("ComputerPower").GetMethod("UsePort");
        Type port = C("PcPort");
        // perto do gabinete (o alcance das portas é entre equipamentos; o jogador ao lado)
        var args = new object[] { Enum.Parse(port, "Outlet"), cabo.GetComponent(C("CableItem")), true, null };
        object r1 = usePort.Invoke(pw, args);
        Call(holder, "Stow", pc.transform.position);
        object r2 = usePort.Invoke(pw, new object[] { Enum.Parse(port, "CasePower"), null, false, null });
        Assert.IsTrue((bool)Get(pw, "PowerConnected"), $"cabo do gaveteiro ligado ({r1}, {r2})");
        Call(pw, "PressPower");
        yield return new WaitForSeconds(4f);
        int n = (int)GetF(t, "Number");
        // investigar = abrir a manutenção deste gabinete pelo clique direito (caminho real) e fechar com Esc
        screens = det.GetComponent(T("UIScreenManager"));
        yield return MirarEm(pc);
        yield return Clique(true);
        Assert.IsTrue(TelaAberta, "manutenção do gabinete aberta");
        yield return Tecla(Key.Escape);
        yield return new WaitForSeconds(0.5f);
        bool entregou = (bool)Call(tm, "TryFinishTicket", n);
        string etapas = string.Join(", ", ((IList)T("TicketDefinition").GetField("steps").GetValue(GetF(t, "Definition"))).Cast<object>()
            .Select(st => (string)st.GetType().GetField("id").GetValue(st)).Select(id => id + "=" + Call(t, "HasCompleted", id)));
        Assert.IsTrue(entregou, $"com o cabo novo ligado e o PC no sistema, entrega (estado {Get(pw, "State")}; {etapas}; motivo: {Get(t, "LastRejectReason")})");
    }

    // ------------------------------------------------------------------ 5. armário de reserva

    [UnityTest]
    public IEnumerator Armario_AbreRetiraFechaEReabre_ComAReservaCompleta_SemDuplicar()
    {
        yield return Partida();
        Component dc = Object.FindObjectsByType(T("DrawerContents"), FindObjectsSortMode.None).Cast<Component>()
            .First(d => (bool)GetF(d, "refillOnClose"));
        Component porta = dc.GetComponent(T("OpenableInteractable"));
        Type pickup = T("PickupInteractable");
        int Mundo(string comp) => Object.FindObjectsByType(pickup, FindObjectsInactive.Include, FindObjectsSortMode.None).Cast<Component>()
            .Count(p => (comp == "monitor" ? p.GetComponentInChildren(C("MonitorDisplay"), true) != null : p.GetComponent(C("ComputerCase")) != null));
        IList Dentro() => (IList)Get(dc, "Inside");

        // abrir pela mira (clique esquerdo na porta)
        yield return MirarEm(porta);
        Assert.AreSame(porta, Primario(), "a porta é alvo do clique esquerdo");
        yield return Clique(false);
        yield return new WaitForSeconds(0.8f);
        Assert.IsTrue((bool)Get(porta, "IsOpen"), "abriu");
        Assert.AreEqual(3, Dentro().Count, "2 monitores + 1 gabinete de reserva");
        foreach (Component it in Dentro()) Assert.IsTrue(it.gameObject.activeInHierarchy, "visíveis com a porta aberta");
        int monitores = Mundo("monitor"), gabinetes = Mundo("gabinete");
        // monitores e gabinete de reserva começam funcionais e soltos de qualquer PC
        foreach (Component it in Dentro())
            if (it.GetComponent(C("ComputerCase")) is Component cc)
                Assert.AreEqual(-1 & (int)C("ComputerCase").GetField("AllComponents").GetValue(null), (int)Get(cc, "Components"), "gabinete de reserva com todas as peças");

        // retira um monitor (pega e leva para fora)
        Component monitor = Dentro().Cast<Component>().First(c => c.GetComponentInChildren(C("MonitorDisplay"), true) != null);
        Component holder = det.GetComponent(T("ItemHolder"));
        Call(holder, "Pick", monitor);
        yield return null;
        Assert.AreEqual(2, Dentro().Count);
        Call(porta, "SetOpen", false);
        yield return new WaitForSeconds(0.9f);
        Call(porta, "SetOpen", true);
        yield return new WaitForSeconds(0.8f);
        Assert.AreEqual(3, Dentro().Count, "a reserva voltou a 3 ao fechar");
        Assert.AreEqual(monitores + 1, Mundo("monitor"), "só 1 monitor novo (o retirado continua na mão)");
        Assert.AreSame(monitor, Get(holder, "Current"), "o monitor retirado não foi mexido");

        // abrir/fechar várias vezes sem retirar nada não cria nada
        for (int i = 0; i < 3; i++)
        {
            Call(porta, "SetOpen", false); yield return new WaitForSeconds(0.9f);
            Call(porta, "SetOpen", true); yield return new WaitForSeconds(0.8f);
        }
        Assert.AreEqual(monitores + 1, Mundo("monitor"), "sem duplicatas");
        Assert.AreEqual(gabinetes, Mundo("gabinete"));
        // itens da reserva estão dentro do armário (não atravessam paredes/porta: centro dentro do corpo)
        Bounds arm = Limites(dc.transform.parent);
        foreach (Component it in Dentro()) Assert.IsTrue(arm.Contains(Limites(it).center), $"{it.name} dentro do armário");
    }

    [UnityTest]
    public IEnumerator ItensExpostos_NaoReaparecem_EReservaVoltaNaPartidaNova()
    {
        yield return Partida();
        Component exposto = Object.FindObjectsByType(T("MonitorAvulso"), FindObjectsSortMode.None).Cast<Component>()
            .First(m => m.transform.parent != null && m.transform.parent.name == "Estoque").GetComponent(T("PickupInteractable"));
        string nome = exposto.name;
        Vector3 lugar = exposto.transform.position;
        Component holder = det.GetComponent(T("ItemHolder"));
        Call(holder, "Pick", exposto);
        yield return new WaitForSeconds(2.5f);   // a reposição global verifica a cada 1 s
        Assert.IsFalse(Physics.OverlapSphere(lugar + Vector3.up * 0.3f, 0.1f).Any(c => c.GetComponentInParent(T("MonitorAvulso")) != null),
            "o monitor exposto retirado não reaparece na estante");
        // partida nova (menu inicial): tudo volta ao estado inicial da cena
        T("GameSession").GetMethod("GoToMainMenu").Invoke(null, null);
        for (int i = 0; i < 8; i++) yield return null;
        Assert.IsTrue(Object.FindObjectsByType(T("MonitorAvulso"), FindObjectsSortMode.None).Cast<Component>().Any(m => m.name == nome),
            "na partida nova a estante está como no começo");
    }

    // ------------------------------------------------------------------ 6. rack 3D, monitores, layout

    [UnityTest]
    public IEnumerator Rack3D_CabosELeds_SeguemARede()
    {
        yield return Partida();
        Component eq = (Component)Object.FindAnyObjectByType(T("RackEquipamento3D"));
        Assert.IsNotNull(eq, "equipamentos 3D no rack");
        Assert.IsTrue(eq.GetComponentsInChildren<Collider>().Length == 0, "sem colisores (não atrapalham a mira no rack)");
        object g = T("RedeGame").GetMethod("Garantir").Invoke(null, null);
        GameObject cabo1 = ((GameObject[])GetF(eq, "cabos"))[0];
        bool antes = cabo1.activeSelf;
        Call(g, "Desligar", 1, null);
        yield return null;
        Assert.IsFalse(cabo1.activeSelf, "porta 1 sem cabo no patch panel = patch cord some");
        Assert.IsFalse((bool)Call(g, "PortaFunciona", 1));
        Type pr = Type.GetType("EstagiarIo.Rede.PortaRack, EstagiarIo.Rede", true);
        Call(g, "Ligar", Activator.CreateInstance(pr, true, 1), Activator.CreateInstance(pr, false, 1), null);
        yield return null;
        Assert.IsTrue(cabo1.activeSelf, "religado = patch cord aparece");
        Assert.IsTrue(antes, "começa ligado");
    }

    [UnityTest]
    public IEnumerator Layout_SeteConjuntos_FuncionariosDasFichas_PlaquinhasNoTampo_NavMeshNosDoisSetoresENaTI()
    {
        yield return Partida();
        var conj = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name.StartsWith("Conjunto_")).ToList();
        Assert.AreEqual(4, conj.Count(c => c.parent.name == "Setor_Principal"), "4 conjuntos no espaço principal");
        Assert.AreEqual(3, conj.Count(c => c.parent.name == "Setor_Secundario"), "3 conjuntos no secundário");
        var mesas = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name.StartsWith("Estacao_") && t.parent.name == "Estacoes").OrderBy(t => t.name).ToList();
        CollectionAssert.AreEqual(Enumerable.Range(1, 28).Select(i => $"Estacao_{i:00}"), mesas.Select(t => t.name), "nomes únicos e sequenciais");
        foreach (Transform c in conj) Assert.AreEqual(4, c.Find("Estacoes").childCount, $"{c.name}: 4 estações em volta do +");
        var est = Object.FindObjectsByType(T("StationEmployee"), FindObjectsSortMode.None).Cast<Component>().ToList();
        Assert.AreEqual(12, est.Count, "um funcionário por solicitante das fichas");
        Assert.AreEqual(12, est.Select(e => (string)GetF(e, "employeeName")).Distinct().Count(), "nomes diferentes");
        // os nomes são os solicitantes cadastrados nas fichas do catálogo
        Component tmc = (Component)Object.FindAnyObjectByType(T("TicketManager"));
        var solicitantes = ((IList)GetF(tmc, "catalog")).Cast<object>().Select(d => (string)d.GetType().GetField("requester").GetValue(d)).ToList();
        foreach (Component e in est) CollectionAssert.Contains(solicitantes, (string)GetF(e, "employeeName"), "funcionário com chamado cadastrado");
        Assert.AreEqual(2, Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.name == "NPC_Estacao"), "sem NPC duplicado");
        Assert.AreEqual(1, Object.FindObjectsByType(T("SupervisorPatrol"), FindObjectsSortMode.None).Length, "um supervisor só");
        Assert.GreaterOrEqual(Object.FindObjectsByType(T("ImpressoraEstacao"), FindObjectsSortMode.None).Length, 3, "mais de duas impressoras");
        foreach (Component e in est)
        {
            Assert.IsNotNull(e.GetComponentInChildren(T("TicketDeviceBridge")), $"{e.name}: ligada aos chamados");
            Component placa = e.GetComponentInChildren(T("StationNamePlate"));
            Assert.AreEqual(GetF(e, "employeeName"), placa.GetComponentInChildren<TextMesh>().text, $"{e.name}: plaquinha = funcionário");
            Component mesa = e.GetComponentInChildren(C("WorkSurface"));
            float topo = mesa.GetComponent<Collider>().bounds.max.y;
            Assert.AreEqual(topo, placa.transform.position.y, 0.02f, $"{e.name}: plaquinha apoiada no tampo");
        }
        foreach (Transform m in mesas)
        {
            Component mesa = m.GetComponentInChildren(C("WorkSurface"));
            foreach (Renderer r in mesa.GetComponentsInChildren<Renderer>()) Assert.GreaterOrEqual(r.bounds.min.y, -0.01f, $"{m.name}: mesa não atravessa o piso");
            if (m.GetComponent(T("StationEmployee")) == null)
                Assert.IsNull(m.GetComponentInChildren(T("TicketDeviceBridge")), $"{m.name}: mesa livre sem chamados");
        }
        // NavMesh: do jogador às estações, à sala de TI e aos pontos da ronda (que cobre os dois setores e a TI)
        NavMesh.SamplePosition(player.transform.position, out NavMeshHit ini, 2f, NavMesh.AllAreas);
        Transform ronda = GameObject.Find("Ronda_Supervisor").transform;
        bool s1 = false, s2 = false, ti = false;
        foreach (Transform p in ronda)
        {
            var path = new NavMeshPath();
            Assert.IsTrue(NavMesh.CalculatePath(ini.position, p.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, $"ronda {p.name} alcançável");
            if (p.position.x > 42.7f) s2 = true;
            else if (p.position.z > 12.3f && p.position.z < 16.9f && p.position.x > 30.3f && p.position.x < 40.4f) ti = true;
            else if (p.position.z < 11.6f) s1 = true;
        }
        Assert.IsTrue(s1 && s2 && ti, "a ronda passa pelos dois setores e pela sala de TI");
        // o supervisor anda de verdade
        Component boss = (Component)Object.FindAnyObjectByType(T("SupervisorPatrol"));
        Vector3 b0 = boss.transform.position;
        float andou = 0f;
        for (float tempo = 0f; tempo < 15f && andou < 0.5f; tempo += 0.5f)
        {
            yield return new WaitForSeconds(0.5f);
            andou = (boss.transform.position - b0).magnitude;
        }
        Assert.IsTrue(boss.GetComponent<NavMeshAgent>().isOnNavMesh, "supervisor no NavMesh");
        Assert.Greater(andou, 0.5f, "o supervisor patrulha");
    }

    [UnityTest]
    public IEnumerator NpcCaminhante_AndaPelosPontos_SemRegrasDeSupervisor_EOsDasEstacoesFicam()
    {
        yield return Partida();
        var andantes = Object.FindObjectsByType(T("NpcCaminhada"), FindObjectsSortMode.None).Cast<Component>().ToList();
        Assert.AreEqual(1, andantes.Count, "só o NPC escolhido anda");
        Component npc = andantes[0];
        Assert.IsNull(npc.GetComponent(T("SupervisorPatrol")), "sem ronda de supervisor");
        Assert.IsNull(npc.GetComponent(T("SupervisorPerception")), "não percebe nem penaliza o jogador");
        foreach (Transform st in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name == "NPC_Estacao"))
            Assert.IsNull(st.GetComponent(T("NpcCaminhada")), "funcionário da estação não sai andando");
        NavMeshAgent ag = npc.GetComponent<NavMeshAgent>();
        Assert.IsTrue(ag.isOnNavMesh, "no NavMesh");
        Vector3 p0 = npc.transform.position;
        float maior = 0f;
        for (float t = 0f; t < 15f && maior < 2f; t += 0.5f)
        {
            yield return new WaitForSeconds(0.5f);
            maior = Mathf.Max(maior, (npc.transform.position - p0).magnitude);
        }
        Assert.Greater(maior, 2f, "andou até um destino");
        object dest = Get(npc, "Destino");
        if (dest != null) Assert.IsTrue(NavMesh.SamplePosition((Vector3)dest, out _, 0.3f, NavMesh.AllAreas), "destino válido no NavMesh");
    }

    [UnityTest]
    public IEnumerator SalaTI_SoMonitoresReais()
    {
        yield return Partida();
        foreach (Object o in Object.FindObjectsByType(C("MonitorDisplay"), FindObjectsSortMode.None))
        {
            var md = (Component)o;
            Vector3 p = md.transform.position;
            if (p.x < 30.3f || p.x > 40.4f || p.z < 12.3f || p.z > 16.9f) continue;
            Assert.IsNotNull(md.GetComponentInParent(T("PickupInteractable")), $"monitor da sala de TI é o real (pegável): {md.transform.parent.name}");
            Assert.IsNotNull(md.GetComponentInParent(T("MonitorScreenInteractable")));
        }
    }
}
