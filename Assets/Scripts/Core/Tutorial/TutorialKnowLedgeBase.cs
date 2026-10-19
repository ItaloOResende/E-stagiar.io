using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Biblioteca de consulta exclusiva do tutorial. Nao altera o tablet nem seus prefabs.
/// Exibe um botao auxiliar enquanto o tablet estiver aberto e uma janela propria de leitura.
/// A biblioteca nao conclui chamados nem altera minigames.
/// </summary>
[DefaultExecutionOrder(9000)]
public sealed class TutorialKnowledgeBase : MonoBehaviour
{
    [Serializable]
    private sealed class Entry
    {
        public readonly string Name, ImageId, What, Where, How, Tip;
        public Entry(string name, string imageId, string what, string where, string how, string tip)
        { Name = name; ImageId = imageId; What = what; Where = where; How = how; Tip = tip; }
    }
    private sealed class Category
    {
        public readonly string Name;
        public readonly Entry[] Entries;
        public Category(string name, params Entry[] entries) { Name = name; Entries = entries; }
    }

    private static readonly Category[] Categories = {
        new Category("Pecas do computador",
            new Entry("Gabinete", "gabinete", "Estrutura que recebe os componentes do PC.", "Estoque/ponto de retirada de equipamentos.", "Transporte o gabinete ate a bancada do chamado; use a interacao do gabinete para soltar os parafusos e abrir a tampa.", "Nao tente instalar pecas com a tampa fechada."),
            new Entry("Placa-mae", "placa-mae", "Conecta os componentes do computador.", "Gaveta de componentes.", "Com o gabinete aberto, posicione a placa-mae sobre os pontos de fixacao do chassi. No minigame, escolha o encaixe da placa-mae (a base grande onde serao ligados CPU, RAM, armazenamento e cabos); confirme a fixacao antes das outras pecas.", "Ela e a base para os demais componentes."),
            new Entry("Fonte de alimentacao", "fonte", "Converte e fornece energia ao PC.", "Gaveta de componentes.", "Coloque a fonte no compartimento de alimentacao do gabinete, normalmente na parte traseira inferior ou superior conforme o modelo exibido. Depois ligue os conectores de energia da fonte aos pontos indicados na placa-mae; isso e diferente do cabo externo da tomada.", "Instalar a fonte nao substitui ligar os cabos de energia."),
            new Entry("Memoria RAM", "ram", "Memoria temporaria de trabalho.", "Gaveta de memoria e armazenamento.", "Localize os slots longos de memoria ao lado do processador na placa-mae. Alinhe o recorte do modulo RAM com a guia do slot e encaixe-o na posicao sinalizada pelo minigame. Nao e um conector de cabo nem uma porta externa.", "Confira a orientacao da peca antes de instalar."),
            new Entry("Armazenamento HD / SSD", "ssd", "Guarda arquivos e o sistema operacional.", "Gaveta de memoria e armazenamento.", "Confira qual disco o chamado exige. Um SSD/HD SATA fica na baia de armazenamento do gabinete e precisa de dados SATA ate a placa-mae e energia da fonte, se o jogo oferecer essas conexoes. Um SSD M.2 entra direto no slot M.2 da placa-mae, sem cabo SATA. Siga o modelo realmente exibido no minigame.", "Sem armazenamento adequado, a instalacao do sistema nao fica completa."),
            new Entry("Cooler", "cooler", "Ajuda a resfriar o processador.", "Gaveta de componentes.", "O cooler do processador e fixado SOBRE o processador, na placa-mae, usando o suporte indicado. Quando existir o conector CPU_FAN no minigame, ligue ali o cabo da ventoinha; nao confunda com o cooler do gabinete.", "Verifique se a montagem permite fechar a tampa."),
            new Entry("Monitor", "monitor", "Mostra a imagem do computador.", "Ponto de retirada de equipamentos.", "Posicione o monitor na mesa correta. Conecte o cabo de video entre a ENTRADA HDMI/VGA do monitor e a SAIDA de video do PC; conecte separadamente a energia do monitor. A tela nao recebe video apenas por estar ligada na tomada.", "Sem cabo de video, o monitor nao recebe imagem."),
            new Entry("Sistema operacional", "sistema", "Permite usar o computador apos a montagem.", "Confira os dispositivos de instalacao no estoque.", "Depois de montar e conectar o PC, use a interacao de instalacao de sistema oferecida pelo equipamento.", "Teste a inicializacao ate o sistema, nao somente ate a BIOS.")),
        new Category("Cabos e conexoes",
            new Entry("Cabo de energia do PC", "energia", "Leva energia para o gabinete.", "Suprimentos e cabos.", "Uma ponta entra na tomada de alimentacao da FONTE, atras do gabinete; a outra vai a tomada/regua de energia da mesa. Esse cabo nao entra na placa-mae nem na porta de rede. Se houver etapa de ligacao no minigame, conclua as duas extremidades.", "Confira as duas extremidades."),
            new Entry("Cabo de energia do monitor", "energia-monitor", "Alimenta a tela.", "Suprimentos e cabos.", "Conecte uma ponta na entrada de energia atras do monitor e a outra na tomada/regua. Nao confunda com HDMI/VGA: a energia faz o monitor ligar, mas nao transmite imagem.", "Monitor desligado nao significa necessariamente PC com defeito."),
            new Entry("Cabo HDMI", "hdmi", "Transporta imagem digital.", "Gaveteiro de cabos.", "Ligue uma ponta na porta HDMI de SAIDA do computador (atras do gabinete) e a outra na HDMI de ENTRADA do monitor. No minigame, selecione as duas portas compativeis; HDMI transmite video, nao substitui a alimentacao eletrica.", "Video e energia sao conexoes distintas."),
            new Entry("Cabo VGA", "vga", "Transporta imagem para monitores compativeis.", "Gaveteiro de cabos.", "Ligue o conector VGA (15 pinos) a saida VGA do computador e o outro a entrada VGA do monitor. Use somente quando ambos mostrarem portas VGA. Caso o jogo exija apertar travas/parafusos do conector, conclua essa etapa.", "Use HDMI ou VGA de acordo com as portas da estacao."),
            new Entry("Cabo de rede RJ45", "rede", "Conecta a estacao a rede.", "Gaveteiro de cabos.", "A primeira ponta RJ45 entra na porta Ethernet/LAN do PC, normalmente atras do gabinete. A outra deve ir a tomada de rede da mesa ou a porta do switch/rack indicada no chamado. Se o cabo foi criado no minigame de crimpagem, finalize a crimpagem e o teste antes de conecta-lo. Nao use uma porta USB nem HDMI.", "Uma rede sem conexao pode ser um problema de cabo ou de rack.")),
        new Category("Ferramentas",
            new Entry("Chave de fenda", "chave", "Permite remover e prender parafusos.", "Area de ferramentas.", "Pegue a ferramenta, interaja com os parafusos do gabinete e abra/feche a tampa conforme a mecanica apresentada.", "Verifique todos os parafusos antes de forcar a tampa."),
            new Entry("Alicate de crimpar", "alicate", "Prende conectores de rede aos cabos.", "Gaveteiro de ferramentas.", "1) Pegue o cabo UTP e os conectores RJ45. 2) Abra o minigame de crimpagem. 3) Organize os oito fios na ordem solicitada (T568A ou T568B, conforme o jogo), insira no conector e crimpe. 4) Se houver testador, valide. 5) LEVE o cabo pronto e conecte-o entre a LAN do PC e o ponto de rede/switch solicitado: crimpar sozinho nao instala a rede.", "Confira a ordem dos fios antes da crimpagem."),
            new Entry("Pendrive", "pendrive", "Dispositivo removivel para tarefas de manutencao.", "Gaveteiro de suprimentos.", "Pegue o pendrive e use a interacao disponivel no computador ou notebook para a tarefa solicitada.", "Para instalar sistema, observe as opcoes do equipamento."),
            new Entry("Testador de cabo", "testador", "Verifica continuidade de um cabo de rede.", "Estacao ou area de rede.", "Depois de crimpar, conecte as duas pontas RJ45 do cabo nas portas do testador (principal e remoto, se existirem). Rode o teste do minigame e confira o resultado. Se falhar, volte a crimpagem; so depois conecte o cabo no PC e no ponto de rede.", "Um cabo aparentemente inteiro pode falhar no teste.")),
        new Category("Impressora",
            new Entry("Impressora", "impressora", "Imprime documentos do escritorio.", "Estacao de impressao.", "Interaja com o equipamento para abrir a tela do minigame de impressora e identificar o problema do chamado.", "Leia o painel antes de substituir qualquer item."),
            new Entry("Toner", "toner", "Suprimento de impressao.", "Gaveteiro de suprimentos.", "Abra o compartimento de consumiveis da impressora pelo minigame. Retire o cartucho antigo se a tarefa pedir e encaixe o toner no trilho/suporte proprio, nunca na bandeja de papel. Feche a tampa e teste a impressao.", "Nem toda falha de impressao e falta de toner."),
            new Entry("Resma de papel", "resma", "Papel para impressao.", "Gaveteiro de suprimentos.", "Abra a BANDEJA DE PAPEL da impressora, alinhe as folhas e reabasteca a bandeja na quantidade pedida. Nao coloque papel no compartimento do toner. Depois, use a opcao de teste se o minigame apresentar.", "Confira primeiro se ha papel no equipamento."),
            new Entry("Configuracao de rede", "impressora-rede", "Permite que a impressora seja acessada pela rede.", "Painel da impressora.", "Use a interface de diagnostico/configuracao do minigame e siga os indicadores disponiveis.", "Uma impressora ligada pode estar sem comunicacao de rede.")),
        new Category("Notebook",
            new Entry("Notebook", "notebook", "Computador portatil com componentes internos proprios.", "Estacao do chamado de notebook.", "Interaja com o notebook para abrir o minigame especifico; suas pecas nao sao instaladas como no gabinete de mesa.", "Observe o tipo de falha antes de desmontar."),
            new Entry("RAM do notebook", "ram-notebook", "Memoria de trabalho do notebook.", "Suprimentos para notebook.", "Abra o minigame de manutencao do notebook; o modulo SO-DIMM encaixa no slot de memoria da placa do notebook, geralmente acessivel por tampa inferior. Alinhe a ranhura e trave no suporte indicado; ele nao vai na porta USB.", "Nao confunda o encaixe do notebook com o do PC."),
            new Entry("SSD do notebook", "ssd-notebook", "Armazenamento interno do notebook.", "Suprimentos para notebook.", "No minigame do notebook, procure a baia SATA de 2,5 polegadas OU o slot M.2, conforme a peca mostrada. Encaixe no alojamento correto e prenda pela fixacao indicada antes de fechar a tampa e iniciar a instalacao do sistema.", "Pode ser necessario instalar o sistema depois."),
            new Entry("Sistema do notebook", "sistema-notebook", "Software necessario para inicializacao.", "Ferramentas e dispositivos de instalacao.", "Use a opcao de instalacao do proprio minigame do notebook apos preparar o armazenamento.", "Confira a inicializacao completa no final.")),
        new Category("Rede e rack",
            new Entry("Rack de rede", "rack", "Centraliza conexoes da rede do escritorio.", "Area de infraestrutura de rede.", "No rack, identifique o SWITCH (portas Ethernet ativas) e, se presente, o PATCH PANEL (portas ligadas as tomadas das mesas). Conecte o RJ45 a porta indicada pelo chamado. Um cabo de rede do PC pode terminar em uma tomada da mesa; a ligacao desta tomada ao switch pode exigir um patch cord dentro do rack.", "Identifique o ponto solicitado antes de trocar cabos."),
            new Entry("Crimpagem RJ45", "crimpagem", "Preparacao de um cabo de rede com conector.", "Estacao de crimpagem.", "Prepare o cabo de rede no minigame: desencape a ponta, ordene os oito condutores no padrao mostrado, encaixe no plugue RJ45 e acione o alicate. Confira no testador, se disponivel. Em seguida, use o cabo montado na porta LAN do computador e no ponto de rede/switch do chamado.", "Use o testador depois de montar o cabo."),
            new Entry("Teste de conectividade", "conectividade", "Confirma se o caminho da rede esta funcional.", "Ponto de rede, rack ou testador.", "Confira as DUAS pontas do cabo (PC na porta LAN e outra ponta no ponto de rede/switch). Se o jogo oferecer testador ou minigame de conectividade, execute-o depois da instalacao e observe o resultado. Apenas crimpar ou apenas conectar uma ponta nao basta.", "Concluir a etapa exige verificar o resultado, nao so encaixar um cabo."))
    };

    private enum ScreenMode { Categories, Items, Detail }
    private ScreenMode screen = ScreenMode.Categories;
    private TicketTabletPrototype tablet;
    private bool opened;
    private int categoryIndex, entryIndex;
    private Vector2 scroll;
    private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
    private GUIStyle titleStyle, subTitleStyle, bodyStyle, captionStyle, buttonStyle, selectedButtonStyle, sectionStyle;
    private Texture2D backgroundTexture, cardTexture, accentTexture;

    private void Awake() { tablet = GetComponent<TicketTabletPrototype>(); }
    private bool TabletOpen() { return tablet != null && tablet.IsOpen; }

    private void Update()
    {
        if (!TabletOpen()) { opened = false; screen = ScreenMode.Categories; return; }
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
        { opened = !opened; if (opened) screen = ScreenMode.Categories; }
    }

    private Rect TabletRect()
    {
        if (tablet == null) return new Rect(0, 0, 0, 0);
        var f = typeof(TicketTabletPrototype).GetField("painel", BindingFlags.Instance | BindingFlags.NonPublic);
        RectTransform panel = f == null ? null : f.GetValue(tablet) as RectTransform;
        if (panel != null)
        {
            Vector3[] p = new Vector3[4]; panel.GetWorldCorners(p);
            Canvas canvas = panel.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, p[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, p[2]);
            Rect r = new Rect(a.x, Screen.height - b.y, b.x - a.x, b.y - a.y);
            if (r.width > 150 && r.height > 150) return r;
        }
        var m = typeof(TicketTabletPrototype).GetMethod("RetanguloAberto", BindingFlags.Instance | BindingFlags.NonPublic);
        if (m != null)
        {
            object r = m.Invoke(tablet, null);
            if (r is Rect) return (Rect)r;
        }
        return new Rect(Screen.width - 430, Screen.height - 670, 410, 640);
    }

    private Texture2D Picture(string id)
    {
        Texture2D cached;
        if (textures.TryGetValue(id, out cached)) return cached;
        cached = Resources.Load<Texture2D>("TutorialIcons/" + id);
        textures[id] = cached;
        return cached;
    }

    private static Texture2D Solid(Color color)
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private void EnsureStyle()
    {
        if (bodyStyle != null) return;
        backgroundTexture = Solid(new Color(.075f, .105f, .16f));
        cardTexture = Solid(new Color(.13f, .175f, .24f));
        accentTexture = Solid(new Color(.22f, .42f, .64f));
        bodyStyle = new GUIStyle(GUI.skin.label) {
            fontSize = 13, wordWrap = true, richText = false,
            normal = { textColor = new Color(.93f, .95f, .99f) },
            padding = new RectOffset(2, 2, 2, 2)
        };
        titleStyle = new GUIStyle(bodyStyle) { fontSize = 19, fontStyle = FontStyle.Bold };
        subTitleStyle = new GUIStyle(bodyStyle) { fontSize = 15, fontStyle = FontStyle.Bold };
        captionStyle = new GUIStyle(bodyStyle) { fontSize = 11, normal = { textColor = new Color(.72f, .80f, .89f) } };
        sectionStyle = new GUIStyle(subTitleStyle) { fontSize = 13, normal = { textColor = new Color(.54f, .79f, 1f) } };
        buttonStyle = new GUIStyle(GUI.skin.button) {
            fontSize = 13, wordWrap = true, alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(12, 8, 8, 8),
            margin = new RectOffset(1, 1, 3, 3),
            normal = { background = cardTexture, textColor = Color.white },
            hover = { background = accentTexture, textColor = Color.white },
            active = { background = accentTexture, textColor = Color.white }
        };
        selectedButtonStyle = new GUIStyle(buttonStyle) { fontStyle = FontStyle.Bold,
            normal = { background = accentTexture, textColor = Color.white } };
    }

    private void OnDestroy()
    {
        if (backgroundTexture != null) Destroy(backgroundTexture);
        if (cardTexture != null) Destroy(cardTexture);
        if (accentTexture != null) Destroy(accentTexture);
    }

    private void OnGUI()
    {
        if (!TabletOpen()) return;
        Rect body = TabletRect();
        if (body.width <= 150 || body.height <= 150) return;
        GUI.depth = -9000;
        float inset = Mathf.Max(10f, body.width * .035f);
        Rect inner = new Rect(body.x + inset, body.y + inset * 1.5f,
            body.width - 2f * inset, body.height - 2.6f * inset);
        Rect launcher = new Rect(inner.x + 8f, inner.y + 6f, Mathf.Min(124f, inner.width * .35f), 30f);
        if (!opened)
        {
            if (GUI.Button(launcher, "APPS / GUIA [F1]")) { opened = true; screen = ScreenMode.Categories; }
            return;
        }
        EnsureStyle();
        GUI.DrawTexture(inner, backgroundTexture);
        Rect area = new Rect(inner.x + 13f, inner.y + 11f, inner.width - 26f, inner.height - 22f);
        GUILayout.BeginArea(area);
        try
        {
            GUILayout.BeginHorizontal(GUILayout.Height(32));
            GUILayout.Label("GUIA DO ESTAGIARIO", subTitleStyle, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("CHAMADOS", buttonStyle, GUILayout.Width(94), GUILayout.Height(30)))
                opened = false;
            GUILayout.EndHorizontal();
            DrawDivider();
            if (screen == ScreenMode.Categories) DrawCategories();
            else if (screen == ScreenMode.Items) DrawItems();
            else DrawDetail();
        }
        finally { GUILayout.EndArea(); }
    }

    private void DrawDivider()
    {
        GUILayout.Space(6);
        Rect rect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
        GUI.DrawTexture(rect, accentTexture);
        GUILayout.Space(8);
    }

    private void DrawCategories()
    {
        GUILayout.Label("Escolha um aplicativo", titleStyle);
        GUILayout.Label("Consulte as pecas, conexoes e tarefas antes de executar o chamado.", captionStyle);
        GUILayout.Space(10);
        scroll = GUILayout.BeginScrollView(scroll);
        for (int i = 0; i < Categories.Length; i++)
        {
            string text = Categories[i].Name + "     (" + Categories[i].Entries.Length + " topicos)   >";
            if (GUILayout.Button(text, buttonStyle, GUILayout.Height(48)))
            { categoryIndex = i; screen = ScreenMode.Items; scroll = Vector2.zero; }
        }
        GUILayout.EndScrollView();
    }

    private void DrawItems()
    {
        if (GUILayout.Button("<  Todos os aplicativos", buttonStyle, GUILayout.Height(30)))
        { screen = ScreenMode.Categories; scroll = Vector2.zero; return; }
        GUILayout.Space(8);
        GUILayout.Label(Categories[categoryIndex].Name, titleStyle);
        GUILayout.Label("Selecione um item para ver onde encaixar e como usar no jogo.", captionStyle);
        GUILayout.Space(8);
        scroll = GUILayout.BeginScrollView(scroll);
        Entry[] entries = Categories[categoryIndex].Entries;
        for (int i = 0; i < entries.Length; i++)
        {
            if (GUILayout.Button(entries[i].Name + "    >", buttonStyle, GUILayout.Height(44)))
            { entryIndex = i; screen = ScreenMode.Detail; scroll = Vector2.zero; }
        }
        GUILayout.EndScrollView();
    }

    private void DrawDetail()
    {
        if (GUILayout.Button("<  " + Categories[categoryIndex].Name, buttonStyle, GUILayout.Height(30)))
        { screen = ScreenMode.Items; scroll = Vector2.zero; return; }
        Entry entry = Categories[categoryIndex].Entries[entryIndex];
        GUILayout.Space(8);
        GUILayout.Label(entry.Name, titleStyle);
        scroll = GUILayout.BeginScrollView(scroll);
        Texture2D picture = Picture(entry.ImageId);
        if (picture != null)
        {
            GUILayout.Space(7);
            Rect r = GUILayoutUtility.GetRect(1f, 122f, GUILayout.ExpandWidth(true));
            float width = Mathf.Min(210, r.width);
            GUI.DrawTexture(new Rect(r.x + (r.width - width) * .5f, r.y, width, r.height), picture, ScaleMode.ScaleToFit);
        }
        else GUILayout.Label("[Imagem do item ainda nao encontrada nos assets]", captionStyle);
        DrawSection("O QUE E", entry.What);
        DrawSection("ONDE ENCONTRAR", entry.Where);
        DrawSection("ONDE ENCAIXAR / CONECTAR E COMO FAZER", entry.How);
        DrawSection("ATENCAO", entry.Tip);
        GUILayout.Space(10);
        GUILayout.Label("As interacoes disponiveis dependem do minigame e do chamado aberto.", captionStyle);
        GUILayout.EndScrollView();
    }

    private void DrawSection(string heading, string content)
    {
        GUILayout.Space(13);
        GUILayout.Label(heading, sectionStyle);
        GUILayout.Space(3);
        GUILayout.Label(content, bodyStyle);
    }
}
