#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Cria os ScriptableObjects do catalogo e os associa ao TicketManager da cena aberta.</summary>
public static class TicketCatalogSetup
{
    private const string Folder = "Assets/TicketsData/Gerados";

    private sealed class Spec
    {
        public readonly string id, title, description, requester, department;
        public readonly int minimumStage;
        public readonly bool tutorial;
        public readonly string[] steps;
        public Spec(string id, string title, string description, string requester, string department,
                    int minimumStage, bool tutorial, params string[] steps)
        {
            this.id=id; this.title=title; this.description=description; this.requester=requester;
            this.department=department; this.minimumStage=minimumStage; this.tutorial=tutorial; this.steps=steps;
        }
    }

    // Formato de etapa: id|descricao|peso. IDs sao contratos para conectar os minigames depois.
    private static readonly Spec[] Catalog = {
        new Spec("tutorial-montagem", "Treinamento: prepare um PC completo",
            "Bem-vindo ao suporte! Vamos preparar um computador do zero. Siga as etapas para montar, conectar e iniciar o equipamento.",
            "Augusto", "TI", 0, true,
            "inspecionar-pc|Inspecionar o computador e identificar as pecas necessarias|1",
            "abrir-gabinete|Abrir o gabinete com a ferramenta correta|1",
            "instalar-placa-mae|Instalar a placa-mae|1",
            "instalar-fonte|Instalar a fonte|1",
            "instalar-cooler|Instalar o cooler do processador|1",
            "instalar-ram|Instalar a memoria RAM|1",
            "instalar-hd|Instalar o HD|1",
            "fechar-gabinete|Fechar o gabinete|1",
            "conectar-energia|Conectar os cabos de energia|1",
            "conectar-video|Conectar o monitor com VGA ou HDMI|1",
            "instalar-sistema|Instalar o sistema operacional|1",
            "conectar-rede|Conectar o cabo de rede|1",
            "validar-funcionamento|Validar que o computador inicia e esta pronto|2"),
        new Spec("pc-sem-energia", "Meu computador nao liga",
            "Bom dia! Aperto o botao para ligar o computador e nada acontece. Conseguem verificar, por favor?",
            "Carina", "Financeiro", 1, false,
            "inspecionar-pc|Verificar a alimentacao do computador|1",
            "conectar-energia|Corrigir a conexao de energia|2",
            "ligar-pc|Ligar o computador e confirmar o funcionamento|1"),
        new Spec("monitor-sem-imagem", "Monitor sem imagem",
            "O computador parece ligado, mas a tela nao mostra nada. Preciso voltar ao trabalho.",
            "Rodolfo", "Administrativo", 1, false,
            "inspecionar-video|Verificar cabos e entradas de video|1",
            "conectar-video|Conectar corretamente o cabo de video|2",
            "validar-imagem|Confirmar imagem no monitor|1"),
        new Spec("pc-sem-rede", "Sem acesso a rede",
            "Nao consigo acessar os arquivos compartilhados. Parece que o computador esta sem conexao.",
            "Marcia", "Financeiro", 1, false,
            "verificar-rede|Diagnosticar a conexao de rede|1",
            "conectar-rede|Conectar ou corrigir o cabo de rede|2",
            "validar-rede|Confirmar que a conexao funciona|1"),
        new Spec("pc-sem-ram", "Computador nao inicia apos manutencao",
            "Depois da manutencao, o computador nao inicia. Poderiam conferir se falta alguma peca?",
            "Carmen", "Atendimento", 1, false,
            "abrir-gabinete|Abrir o gabinete|1",
            "instalar-ram|Instalar a memoria RAM corretamente|2",
            "ligar-pc|Ligar e testar o computador|1"),
        new Spec("pc-sem-hd", "Computador nao encontra armazenamento",
            "O computador abre uma tela estranha e nao encontra onde iniciar. Voces conseguem resolver?",
            "Joao", "Administrativo", 2, false,
            "abrir-gabinete|Abrir o gabinete|1",
            "instalar-hd|Instalar a unidade de armazenamento|2",
            "validar-armazenamento|Verificar que o HD foi reconhecido|1"),
        new Spec("pc-sem-sistema", "Sistema operacional nao inicia",
            "Ao ligar o computador aparece uma mensagem de que nao existe sistema para iniciar.",
            "Luiz", "Financeiro", 2, false,
            "verificar-armazenamento|Verificar o armazenamento e o boot|1",
            "instalar-sistema|Instalar o sistema operacional pelo pendrive|3",
            "validar-sistema|Confirmar que o sistema iniciou|1"),
        new Spec("impressora-sem-impressao", "Impressora nao imprime",
            "Estou tentando imprimir documentos e nada sai da impressora. Pode verificar?",
            "Ivan", "Administrativo", 2, false,
            "inspecionar-impressora|Verificar o estado da impressora|1",
            "corrigir-impressora|Corrigir o problema de impressao|2",
            "teste-impressao|Realizar um teste de impressao|1"),
        new Spec("notebook-sem-boot", "Notebook nao inicia",
            "Meu notebook parou de iniciar normalmente. Aparece uma mensagem de erro na tela.",
            "Laura", "Atendimento", 2, false,
            "diagnosticar-notebook|Verificar a falha de inicializacao|1",
            "reparar-notebook|Corrigir o componente ou configuracao necessaria|2",
            "validar-notebook|Confirmar inicializacao do notebook|1"),
        new Spec("crimpar-cabo", "Cabo de rede danificado",
            "O cabo da estacao esta danificado e nao conecta direito. Podem preparar outro cabo?",
            "Francisco", "TI", 2, false,
            "preparar-cabo|Separar os fios na ordem correta|1",
            "crimpar-cabo|Crimpar o conector RJ45|2",
            "testar-cabo|Testar a conexao do cabo|1"),
        new Spec("pc-montagem-completa", "Preparar uma nova estacao de trabalho",
            "Chegou um computador novo para o setor. Precisamos que ele seja montado e fique pronto para uso.",
            "Augusto", "TI", 3, false,
            "abrir-gabinete|Abrir o gabinete|1",
            "instalar-placa-mae|Instalar a placa-mae|2",
            "instalar-fonte|Instalar a fonte de alimentacao|2",
            "instalar-cooler|Instalar o cooler|1",
            "instalar-ram|Instalar a memoria RAM|1",
            "instalar-hd|Instalar o armazenamento|1",
            "conectar-energia|Conectar a alimentacao|1",
            "conectar-video|Conectar o monitor|1",
            "instalar-sistema|Instalar o sistema operacional|2",
            "validar-funcionamento|Verificar que a estacao esta pronta|1"),
        new Spec("pc-sem-video-e-rede", "Estacao sem monitor e internet",
            "Meu computador liga, mas o monitor fica sem imagem e tambem nao consigo usar a rede.",
            "Carina", "Financeiro", 3, false,
            "conectar-video|Restabelecer o sinal de video|2",
            "validar-imagem|Confirmar que o monitor funciona|1",
            "conectar-rede|Restabelecer a ligacao de rede|2",
            "validar-rede|Confirmar que a rede funciona|1"),
        new Spec("pc-pecas-ausentes", "Computador com pecas ausentes",
            "Recebemos um gabinete incompleto. Precisamos identificar as pecas faltantes e coloca-lo para funcionar.",
            "Rodolfo", "Administrativo", 3, false,
            "inspecionar-pc|Identificar os componentes ausentes|1",
            "abrir-gabinete|Abrir o gabinete|1",
            "instalar-ram|Instalar a memoria RAM|1",
            "instalar-hd|Instalar o armazenamento|1",
            "ligar-pc|Ligar o computador|2",
            "validar-funcionamento|Confirmar funcionamento|1")
    };

    [MenuItem("Tools/E-stagiar/Chamados/Gerar catalogo e vincular na cena")]
    public static void GenerateAndAssign()
    {
        EnsureFolder("Assets/TicketsData");
        EnsureFolder(Folder);
        var generated = new List<TicketDefinition>();
        TicketDefinition tutorial = null;
        foreach (var spec in Catalog)
        {
            string path = Folder + "/" + spec.id + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<TicketDefinition>(path);
            // Nao sobrescreve edicoes de tickets ja criados: e seguro repetir o comando.
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<TicketDefinition>();
                asset.ticketId = spec.id;
                asset.title = spec.title;
                asset.description = spec.description;
                asset.requester = spec.requester;
                asset.department = spec.department;
                asset.minimumStage = spec.minimumStage;
                asset.tutorialOnly = spec.tutorial;
                foreach (var entry in spec.steps)
                {
                    var p = entry.Split('|');
                    asset.steps.Add(new TicketStepDefinition { id = p[0], description = p[1], weight = int.Parse(p[2]) });
                }
                AssetDatabase.CreateAsset(asset, path);
            }
            if (spec.tutorial) tutorial = asset;
            else generated.Add(asset);
        }
        AssetDatabase.SaveAssets();
        var manager = UnityEngine.Object.FindFirstObjectByType<TicketManager>();
        if (manager == null)
        {
            EditorUtility.DisplayDialog("Catalogo criado", "Tickets criados em Assets/TicketsData/Gerados. Abra a cena com o objeto Chamados e execute o menu novamente para vincular automaticamente.", "OK");
            return;
        }
        Undo.RecordObject(manager, "Vincular catalogo de chamados");
        var so = new SerializedObject(manager);
        var catalog = so.FindProperty("catalog");
        var tutorialField = so.FindProperty("tutorialTicket");
        if (catalog == null || tutorialField == null)
        {
            Debug.LogError("TicketCatalogSetup: nao encontrou os campos catalog/tutorialTicket no TicketManager.");
            return;
        }
        // Mantem os tickets que a equipe ja cadastrou, sem duplicar os IDs gerados.
        var existingIds = new HashSet<string>();
        for (int i = 0; i < catalog.arraySize; i++)
        {
            var current = catalog.GetArrayElementAtIndex(i).objectReferenceValue as TicketDefinition;
            if (current != null) existingIds.Add(current.ticketId);
        }
        foreach (var asset in generated)
        {
            if (!existingIds.Add(asset.ticketId)) continue;
            int index = catalog.arraySize;
            catalog.InsertArrayElementAtIndex(index);
            catalog.GetArrayElementAtIndex(index).objectReferenceValue = asset;
        }
        tutorialField.objectReferenceValue = tutorial;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(manager);
        if (manager.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        Debug.Log($"Catalogo gerado: {generated.Count} tickets normais + 1 tutorial. Vinculado ao objeto '{manager.name}'. Salve a cena quando quiser manter a vinculacao.", manager);
        EditorUtility.DisplayDialog("Catalogo configurado", $"Criados/identificados {generated.Count} tickets normais e 1 tutorial. Vinculados ao objeto '{manager.name}'. Salve a cena (Ctrl+S) quando estiver pronta.", "OK");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        for (int i=1;i<parts.Length;i++)
        {
            string parent = string.Join("/", parts, 0, i);
            string current = parent + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(current)) AssetDatabase.CreateFolder(parent, parts[i]);
        }
    }
}
#endif
