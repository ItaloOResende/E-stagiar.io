using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// Tutorial de montagem (cena Tutorial_Montagem salva), do início do atendimento no tablet até a finalização: mesa
/// vazia, gabinete vazio guardado no estoque, o mesmo gabinete que o jogador pega é o do chamado, cada etapa avança pelo
/// estado real e o marcador acompanha o objetivo. Tipos do Assembly-CSharp por reflexão (como nos outros testes).
/// </summary>
public class TutorialMontagemTests
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type T(string n) => Type.GetType(n + ", Assembly-CSharp", true);
    private static object Get(object o, string p) => o.GetType().GetProperty(p, Any).GetValue(o);
    private static object Field(object o, string f) => o.GetType().GetField(f, Any).GetValue(o);
    private static object Call(object o, string m, params object[] a) => o.GetType().GetMethod(m, Any).Invoke(o, a);

    private Component guia, tm, detector, holder, ponte;
    private ComputerCase gabinete;
    private ComputerPower energia;
    private WorkSurface mesa;

    private static IEnumerator Cena()
    {
#if UNITY_EDITOR
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
            "Assets/Scenes/Tutorial_Montagem.unity", new UnityEngine.SceneManagement.LoadSceneParameters());
#endif
        for (int i = 0; i < 10; i++) yield return null;
        yield return new WaitForSeconds(0.5f);
    }

    private string Titulo => (string)Get(guia, "TituloAtual");
    private Transform Alvo => (Transform)Get(guia, "AlvoDoMarcador");
    private Transform AlvoCampo(string f) => (Transform)Field(guia, f);
    private Transform[] Lista(string f) => (Transform[])Field(guia, f);

    // mesma sequência do clique: pegar (Interact) e soltar sobre o tampo da mesa (o que o ItemHolder.Drop faz na mira)
    private void Pegar(Component item)
    {
        Call(item, "Interact", detector);
        Assert.AreEqual(item, Get(holder, "Current"), "na mão: " + item.name);
    }
    private void SoltarNaMesa(float desvio) => SoltarEm(mesa.transform, desvio);
    private void SoltarEm(Transform movel, float desvio)
    {
        var item = (Component)Get(holder, "Current");
        holder.GetType().GetProperty("Current", Any).SetValue(holder, null);
        Bounds b = default; bool tem = false;
        foreach (Collider c in movel.GetComponentsInChildren<Collider>())
        { if (c.isTrigger) continue; if (!tem) { b = c.bounds; tem = true; } else b.Encapsulate(c.bounds); }
        Vector3 ao = b.extents.x >= b.extents.z ? Vector3.right : Vector3.forward;
        // o tampo (não a prateleira de cima): o ponto mais alto do móvel abaixo de 1,2 m, como a mira do jogador acharia
        Vector3 pos = new Vector3(b.center.x, b.min.y, b.center.z) + ao * desvio;
        foreach (RaycastHit h in Physics.RaycastAll(pos + Vector3.up * 3f, Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
            if (h.collider.transform.IsChildOf(movel) && h.point.y < 1.2f && h.point.y > pos.y) pos.y = h.point.y;
        Call(item, "OnDropped", pos, movel.eulerAngles.y + 180f);
    }
    private Component Pegavel(Transform t) => t.GetComponentInParent(T("PickupInteractable"));
    private IEnumerator Esperar(float s = 0.6f) { yield return new WaitForSeconds(s); }

    private static T2 Achar<T2>(Func<T2, bool> filtro) where T2 : Component
    {
        foreach (T2 c in Object.FindObjectsByType<T2>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (filtro(c)) return c;
        return null;
    }

    [UnityTest]
    public IEnumerator FluxoCompleto_DoTabletAoTablet()
    {
        yield return Cena();
        guia = (Component)Object.FindAnyObjectByType(T("TutorialMontagem"));
        Assert.IsNotNull(guia, "TutorialMontagem na cena");
        tm = (Component)Field(guia, "tickets");
        detector = (Component)Object.FindAnyObjectByType(T("InteractionDetector"));
        holder = (Component)Get(detector, "Holder");
        foreach (Object o in Object.FindObjectsByType(T("PcTicketBridge"), FindObjectsSortMode.None)) ponte = (Component)o;
        Assert.IsNotNull(ponte, "estação do tutorial");
        gabinete = ponte.GetComponent<ComputerCase>(); energia = ponte.GetComponent<ComputerPower>();
        mesa = (WorkSurface)Field(ponte, "mesaDaEstacao");
        Assert.IsNotNull(mesa, "mesa da estação configurada");
        Assert.AreEqual(1, ponte.GetComponents<ComputerCase>().Length, "um só ComputerCase no gabinete do chamado");
        Assert.IsNotNull(Pegavel(gabinete.transform), "o chamado está no gabinete que o jogador pega");

        // estado inicial: mesa vazia, gabinete vazio guardado no estoque, sem cabos
        Assert.AreEqual(PcComponent.None, gabinete.Components, "gabinete vazio");
        Assert.IsNotNull(gabinete.SupportingSurface(), "gabinete apoiado num móvel do estoque");
        Assert.AreNotEqual(mesa, gabinete.SupportingSurface(), "gabinete fora da mesa");
        foreach (PcLink l in new[] { PcLink.Power, PcLink.MonitorPower, PcLink.Vga, PcLink.Hdmi, PcLink.Network })
            Assert.IsNull(energia.CableItemOf(l), "sem cabo ligado: " + l);
        Assert.GreaterOrEqual(Lista("teclados").Length, 2, "teclados pegáveis no estoque");
        Assert.GreaterOrEqual(Lista("mouses").Length, 4, "dois mouses por estante");
        var pegaveis = new List<Transform> { gabinete.transform, energia.Monitor };
        pegaveis.AddRange(Lista("teclados")); pegaveis.AddRange(Lista("mouses"));
        foreach (Transform p in pegaveis)
        {
            Assert.IsNotNull(Pegavel(p), "pegável: " + p.name);
            Assert.Greater(Vector3.Distance(p.position, mesa.transform.position), 1.5f, "longe da mesa: " + p.name);
        }
        // apoiados (nada afundado no móvel): a base de cada um fica no topo do collider logo abaixo
        foreach (Transform p in pegaveis)
        {
            Bounds b = ((Bounds)Get(Pegavel(p), "WorldBounds"));
            Assert.IsTrue(Physics.Raycast(new Vector3(b.center.x, b.min.y + 0.03f, b.center.z), Vector3.down, out RaycastHit h, 0.5f), "apoio de " + p.name);
            Assert.Less(h.point.y - b.min.y, 0.012f, "não afundado: " + p.name);
        }
        Assert.IsNull(GameObject.Find("Cadeira"), "a cadeira da estação saiu");
        foreach (ComputerCase c in Object.FindObjectsByType<ComputerCase>(FindObjectsSortMode.None))
            Assert.AreEqual(PcComponent.None, c.Components, "gabinete vazio na sala: " + c.name);

        // 1) atendimento iniciado pelo tablet
        Assert.AreEqual("Inicie o atendimento", Titulo);
        Assert.IsTrue((bool)Get(guia, "MarcandoTablet"), "seta no botão do tablet");
        object ticket = ((IList)Get(tm, "Tickets"))[0];
        int numero = (int)ticket.GetType().GetField("Number").GetValue(ticket);
        Assert.IsTrue((bool)Call(tm, "StartTicket", numero));
        yield return Esperar();
        Assert.AreEqual(ticket, Get(ponte, "Current"), "o chamado reservou o gabinete");
        Assert.AreEqual(PcComponent.None, gabinete.Components, "iniciar não muda o gabinete");

        // 2-3) gabinete: seta nele; na mão, seta na bancada de manutenção; apoiado lá, avança
        Transform manutencao = AlvoCampo("alvoFerramentas");
        Assert.AreEqual("Pegue o gabinete", Titulo);
        Assert.AreEqual(gabinete.transform, Alvo, "seta no gabinete");
        Pegar(Pegavel(gabinete.transform));
        yield return Esperar(0.2f);
        Assert.AreEqual("Leve o gabinete à bancada de manutenção", Titulo, "pegar o gabinete avança");
        Assert.AreEqual(manutencao, Alvo, "com o gabinete na mão, a seta vai para a bancada de manutenção");
        SoltarEm(manutencao, -0.6f);
        yield return Esperar();
        Assert.AreEqual(manutencao.GetComponent<WorkSurface>(), gabinete.SupportingSurface(), "gabinete na bancada de manutenção");

        // 4-5) chave de fenda e abertura (só os parafusos obrigatórios; a tela abre a tampa sozinha)
        Assert.AreEqual("Pegue a chave de fenda", Titulo);
        Transform chave = AlvoCampo("alvoChave");
        Assert.AreEqual(chave, Alvo, "seta na chave de fenda");
        Pegar(Pegavel(chave));
        yield return Esperar(0.2f);
        Assert.AreEqual("Abra o gabinete", Titulo);
        Assert.AreEqual(gabinete.transform, Alvo, "com a chave na mão, seta no gabinete");
        for (int i = 0; i < gabinete.Screws.Count; i++)
            if (gabinete.Screws[i].required) Assert.AreEqual(ScrewRemovalResult.Removed, gabinete.TryRemoveScrew(i, ToolType.Screwdriver));
        Assert.AreEqual(CaseOpenResult.Opened, gabinete.TryOpen());
        yield return Esperar();
        Assert.AreEqual("Instale a placa-mãe", Titulo, "abertura reconhecida antes das peças");

        // 6) peças dos gaveteiros
        foreach (PcComponent peca in new[] { PcComponent.Motherboard, PcComponent.Ram, PcComponent.CpuCooler, PcComponent.Psu, PcComponent.Hdd })
        {
            bool ok = false; string motivos = "";
            foreach (ComputerPartItem item in Object.FindObjectsByType<ComputerPartItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (item.Component != peca || item.IsBroken) continue;
                PartSlotResult r = gabinete.UsePartSlot(peca, item, true, out _);
                if (r == PartSlotResult.Installed) { item.gameObject.SetActive(false); ok = true; break; }
                motivos += r + " ";
            }
            Assert.IsTrue(ok, "peça encontrada e instalada: " + peca + " (" + motivos + ")");
            yield return Esperar(0.4f);
        }
        Assert.AreEqual("Feche o gabinete", Titulo);
        Assert.AreEqual(gabinete.transform, Alvo, "fechar: seta no gabinete (chave na mão)");
        for (int i = 0; i < gabinete.Screws.Count; i++)
            if (gabinete.Screws[i].required) gabinete.TryInstallScrew(i, ToolType.Screwdriver);
        yield return Esperar();

        // 7) gabinete montado vai para a estação de trabalho
        Assert.AreEqual("Leve o gabinete à estação de trabalho", Titulo);
        SoltarEm(manutencao, 0.6f);   // larga a chave na bancada
        yield return null;
        Assert.AreEqual(gabinete.transform, Alvo, "seta no gabinete");
        Pegar(Pegavel(gabinete.transform));
        yield return null; yield return null;
        Assert.AreEqual(AlvoCampo("alvoMesa"), Alvo, "com o gabinete na mão, a seta vai para a estação de trabalho");
        SoltarNaMesa(-0.45f);
        yield return Esperar();
        Assert.AreEqual(mesa, gabinete.SupportingSurface(), "gabinete na estação de trabalho");

        // 8) periféricos, um a um: a seta passa para o próximo que ainda falta
        Assert.AreEqual("Busque os periféricos", Titulo);
        float desvio = 0.05f;
        for (int k = 0; k < 3; k++)
        {
            Transform p = Alvo;
            Assert.IsNotNull(p, "seta num periférico");
            if (k == 0) Assert.AreEqual(energia.Monitor, p, "primeiro o monitor");
            if (k == 1) CollectionAssert.Contains(Lista("teclados"), p, "depois um teclado do estoque");
            if (k == 2) CollectionAssert.Contains(Lista("mouses"), p, "depois um mouse do estoque");
            Pegar(Pegavel(p));
            yield return null; yield return null;
            Assert.AreEqual(AlvoCampo("alvoMesa"), Alvo, "periférico na mão: seta na estação");
            SoltarNaMesa(desvio); desvio += 0.35f;
            yield return Esperar();
        }

        // 9) energia, vídeo, rede, pendrive e sistema
        Assert.AreEqual("Ligue a energia", Titulo, "gabinete, monitor, teclado e mouse na estação");
        Assert.AreEqual(AlvoCampo("alvoCabos"), Alvo, "seta no gaveteiro de cabos");
        var usados = new List<CableItem>();
        Ligar(CableType.Power, PcPort.CasePower, PcPort.Outlet, usados);
        Ligar(CableType.Power, PcPort.MonitorPower, PcPort.MonitorOutlet, usados);
        yield return Esperar();
        Assert.AreEqual("Ligue o cabo de vídeo", Titulo);
        Ligar(CableType.Hdmi, PcPort.CaseHdmi, PcPort.MonitorHdmi, usados);
        yield return Esperar();
        Assert.AreEqual("Ligue o cabo de rede", Titulo, "a rede é pedida");
        Assert.AreEqual(AlvoCampo("alvoCabos"), Alvo, "rede: seta no gaveteiro de cabos");
        Ligar(CableType.Network, PcPort.CaseNetwork, PcPort.NetworkJack, usados);
        yield return Esperar();
        Assert.IsTrue(energia.NetworkConnected, "PC com rede");
        Assert.AreEqual("Encaixe o pendrive", Titulo);
        PendriveItem pendrive = Achar<PendriveItem>(p => true);
        Assert.IsNotNull(pendrive, "pendrive no gaveteiro");
        Assert.AreEqual(PortResult.PluggedFromHand, energia.UseUsbPort(0, pendrive, true, out _));
        pendrive.gameObject.SetActive(false);
        yield return Esperar();
        Assert.AreEqual("Ligue o PC", Titulo);
        Assert.AreEqual(PowerButtonResult.TurnedOn, energia.PressPower());
        energia.AdvanceInstall(999f);
        yield return Esperar(1f);

        // 9) finalizado pelo tablet
        Assert.AreEqual("Finalize o atendimento", Titulo);
        Assert.IsTrue((bool)Get(guia, "MarcandoTablet"), "seta no botão do tablet de novo");
        Assert.IsTrue((bool)Call(tm, "TryFinishTicket", numero), "finalizar pelo tablet");
        yield return Esperar(0.3f);
        Assert.IsTrue((bool)Get(guia, "Concluido"), "tutorial concluído");

        // tela final: cursor livre (nem a trava do 3D o recaptura) e os botões recebem o clique
        for (int i = 0; i < 5; i++) yield return null;
        Assert.IsTrue((bool)T("TutorialMontagem").GetProperty("TelaFinalAberta").GetValue(null));
        Assert.AreEqual(CursorLockMode.None, Cursor.lockState, "cursor livre");
        Assert.IsTrue(Cursor.visible, "cursor visível");
        Assert.IsFalse(((Behaviour)Object.FindAnyObjectByType(T("PlayerLook"))).enabled, "câmera parada");
        foreach (string rotulo in new[] { "COMEÇAR O EXPEDIENTE", "VOLTAR AO MENU" })
        {
            var botao = GameObject.Find("Botao_" + rotulo);
            Assert.IsNotNull(botao, "botão " + rotulo);
            var cantos = new Vector3[4]; ((RectTransform)botao.transform).GetWorldCorners(cantos);
            var ev = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = (cantos[0] + cantos[2]) * .5f };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(ev, hits);
            Assert.IsTrue(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()?.gameObject == botao, "o clique chega no botão " + rotulo);
        }
    }

    // cabo do gaveteiro: ponta no equipamento (da mão) e depois na outra porta
    private void Ligar(CableType tipo, PcPort ponta1, PcPort ponta2, List<CableItem> usados)
    {
        CableItem cabo = Achar<CableItem>(c => c.Type == tipo && !usados.Contains(c));
        Assert.IsNotNull(cabo, "cabo " + tipo + " no gaveteiro");
        usados.Add(cabo);
        Assert.AreEqual(PortResult.PluggedFromHand, energia.UsePort(ponta1, cabo, true, out _), "ponta " + ponta1);
        cabo.gameObject.SetActive(false);
        Assert.AreEqual(PortResult.PluggedOtherEnd, energia.UsePort(ponta2, null, false, out _), "ponta " + ponta2);
    }

    [UnityTest]
    public IEnumerator Controles_PeloPause_VoltaAoJogoSemPrenderOCursor()
    {
        yield return Cena();
        var pause = (Component)Object.FindAnyObjectByType(T("PauseMenu"));
        // sem o desfoque: a captura espera o fim do quadro, que o batchmode (sem renderizar) não entrega
        pause.GetType().GetField("desfocarFundo", Any).SetValue(pause, false);
        Call(pause, "AbrirPause");
        yield return new WaitForSecondsRealtime(0.6f);
        Assert.IsTrue((bool)T("PauseMenu").GetProperty("IsPaused").GetValue(null));
        Call(pause, "MostrarControles");
        yield return new WaitForSecondsRealtime(0.5f);
        Call(pause, "Voltar");
        yield return new WaitForSecondsRealtime(0.5f);
        Assert.AreEqual("Aberto", Field(pause, "estado").ToString(), "pause aberto antes de continuar");
        Call(pause, "Continuar");
        yield return new WaitForSecondsRealtime(0.8f);
        Assert.AreEqual("Fechado", Field(pause, "estado").ToString(), "pause fechado");
        Assert.IsFalse((bool)T("PauseMenu").GetProperty("IsPaused").GetValue(null), "jogo despausado");
        Assert.AreEqual(1f, Time.timeScale);
        Assert.IsTrue(((Behaviour)Object.FindAnyObjectByType(T("PlayerLook"))).enabled, "câmera de volta");
    }

    [UnityTest]
    public IEnumerator Controles_PeloMenuInicial_VoltaAoMenuSemIniciar()
    {
#if UNITY_EDITOR
        yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
            "Assets/Scenes/Office_Placeholder.unity", new UnityEngine.SceneManagement.LoadSceneParameters());
#endif
        yield return new WaitForSecondsRealtime(1.5f);
        Assert.IsTrue((bool)T("MainMenuScreen").GetProperty("Showing").GetValue(null), "menu inicial aberto");
        var menu = (Component)Object.FindAnyObjectByType(T("MainMenuScreen"));
        var pause = (Component)Object.FindAnyObjectByType(T("PauseMenu"));
        Call(menu, "MostrarControles");
        yield return new WaitForSecondsRealtime(0.5f);
        Assert.IsTrue((bool)Get(pause, "PaginaAvulsaAberta"), "página de controles aberta");
        Assert.IsFalse((bool)T("PauseMenu").GetProperty("IsPaused").GetValue(null), "sem pausar");
        Assert.AreEqual(CursorLockMode.None, Cursor.lockState, "cursor livre");
        Call(pause, "Voltar");
        yield return new WaitForSecondsRealtime(0.5f);
        Assert.IsFalse((bool)Get(pause, "PaginaAvulsaAberta"), "controles fechados");
        Assert.IsTrue((bool)T("MainMenuScreen").GetProperty("Showing").GetValue(null), "de volta ao menu");
        var stage = (Component)Object.FindAnyObjectByType(T("StageController"));
        Assert.IsFalse((bool)Get(stage, "IsRunning"), "a partida não começou");
    }

    [UnityTest]
    public IEnumerator Painel_MinimizaERestaura_SemPerderOProgresso()
    {
        yield return Cena();
        guia = (Component)Object.FindAnyObjectByType(T("TutorialMontagem"));
        string antes = (string)Get(guia, "TituloAtual");
        Call(guia, "AlternarMinimizado");
        yield return null;
        Assert.IsTrue((bool)Get(guia, "Minimizado"));
        Assert.AreEqual(antes, Get(guia, "TituloAtual"), "minimizar não muda a etapa");
        Call(guia, "AlternarMinimizado");
        yield return null;
        Assert.IsFalse((bool)Get(guia, "Minimizado"));
    }
}
