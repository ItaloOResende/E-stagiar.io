# CLAUDE.md — E-stagiar.io

Instruções para ferramentas de IA (e pessoas) que alteram este projeto. Visão geral do jogo: `README.md`.
Mecânicas: `docs/MECANICAS.md`. Assets: `Assets/Art/README.md` e `Assets/Art/ORIGEM.md`.

## Projeto

Simulação educativa de suporte de TI em primeira pessoa, em português, para o campeonato **Framework Arcade**
(regulamento e GDD ficam fora do repositório). Cenas do build: `Assets/Scenes/Office_Placeholder.unity` (índice 0:
menu inicial e expediente) e `Assets/Scenes/Tutorial_Montagem.unity` (tutorial, aberto pelo menu). As cenas são
carregadas por `GameSession.LoadScene` (pelo índice do build; no Editor também fora dele). `Teste_Integrado`
(testes de Play Mode), `Fase_01` e `Tutorial_Estagiar` (tutorial antigo) ficam fora do build.

## Stack

- **Unity 6000.5.10f1** (`ProjectSettings/ProjectVersion.txt`). Não abrir com outra versão: o batchmode reescreve o
  `Packages/packages-lock.json`.
- URP 17.5, Input System 1.20 (**não usar `UnityEngine.Input`**), AI Navigation 2.0 (NavMesh), uGUI/TextMesh Pro,
  Unity Toon Shader e RealToon (materiais).
- Git com **Git LFS** para binários (`.fbx`, `.png`, `.wav`… — ver `.gitattributes`).

## Regras de Git

- `main` é o projeto oficial. `master` é o protótipo legado (Unity 2021): **não alterar e não mesclar na `main`**;
  dela só se copia arquivo a arquivo, quando necessário.
- Commits pequenos, mensagem `tipo: resumo` (`feat:`, `fix:`, `chore:`, `docs:`, `refactor:`).
- **Nada de push, merge, rebase, reset ou force sem pedido explícito.** Não descartar alterações locais de outras
  pessoas.
- Todo asset vai com o seu `.meta`. Não versionar `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.sln`.
- Ao integrar trabalho de outra pessoa feito sobre uma versão antiga, preservar os dois lados (cópia seletiva,
  mantendo os GUIDs originais).

## Não mexer

- **Shaders e renderização** (pedido do Matheus): em especial
  `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` e os `*_RPAsset`/`*_Renderer`. Se a Unity alterá-los
  sozinha, não commitar (`git checkout HEAD -- <arquivo>`).
- `ProjectSettings/EditorBuildSettings.asset`: só as duas cenas do build (a principal primeiro). Cena nova no build só
  com combinação da equipe.
- O shader de um material compartilhado. Para mudar a aparência de um objeto só, usar `MaterialPropertyBlock` ou uma
  instância de material em runtime.
- GUIDs: mover e renomear assets pelo Unity (ou junto com o `.meta`); nunca recriar um `.meta`.

## Arquitetura

- Cada objeto de gameplay = **raiz lógica** (colliders, `Rigidbody`, scripts) + filho **`Visual`** com o modelo.
  Trocar o modelo = trocar o `Visual`, sem reescrever lógica.
- Interação por **componentes** (`Interactable`, `PickupInteractable`, `OpenableInteractable`,
  `UIScreenInteractable`…), nunca por tag ou nome de GameObject. Nada de `Find`/`FindObjectsByType` por quadro.
- Pastas: `Assets/Scripts/{Core,Data,Interaction,Player,Gameplay,UI,NPC,Notebook,Impressora,Rede,Minigames}`,
  `Assets/Prefabs/{Gameplay,Props,UI}`, `Assets/Art/{Models,Meshes,Textures,Materials,UI,...}`. Assemblies próprios:
  `EstagiarIo.Computer` (`Scripts/Gameplay/Computer`) e `EstagiarIo.Rede` (`Scripts/Rede/Logica`).
- Texto do jogo em português. Chamados são dados (ScriptableObjects em `Assets/TicketsData`).
- A conclusão de um chamado nunca depende de sorte: só do estado real do equipamento.

## Assets

- Prefixo **`PH_`** = placeholder do projeto legado, com origem/licença desconhecida: não referenciar por script e não
  entregar sem substituir. Detalhes em `Assets/Art/README.md`.
- Todo asset novo precisa de origem/licença conhecida, registrada em `Assets/Art/ORIGEM.md` (regulamento 14.2).

## Scripts de setup do Editor (`Assets/Editor/*Setup.cs`)

Montam prefabs e partes da cena por código. Os idempotentes podem ser rodados de novo (ex.: `Integracao1910Setup`,
menu *Tools > E-stagiar.io > Integração 19-10*). **Não rodar de novo** os que recriam grupos inteiros da
`Office_Placeholder` e apagariam ajustes manuais: `MobiliarioEscritorioSetup`, `SalaTIAjustesSetup`,
`OfficeGameplaySetup`, `CorrecoesFinaisSetup` (layout), `EstacaoTrabalhoSetup` e a parte de cena do `Cabos3DSetup`.
A cena do tutorial é gerada por `TutorialMontagemSetup` (*Tools > E-stagiar.io > Tutorial > Montar cena
Tutorial_Montagem*) a partir de uma cópia da `Office_Placeholder`: rodar de novo refaz a cena do zero, então ajustes
dela vão no setup, não na cena.
Antes de rodar um setup na cena, fazer backup dela (`Art/_Backup/`, fora do Git).

## Testes e batchmode

- Fechar o Editor antes de rodar o Unity em batchmode (não roda com o projeto aberto).
- Testes: `Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode|PlayMode -testResults <arquivo.xml>`.
- Problema conhecido: `OfficeIntegracaoTests` pode travar sem terminar. Usar limite de tempo nas execuções.
- O Unity apaga a pasta `Temp/` ao fechar: saída de testes ou capturas que precisem sobreviver vai para `Logs/`.
- Para ver o relatório final sem jogar a fase: menu de contexto do `StageController` (objeto `Chamados`) —
  *TESTE: Concluir chamados e encerrar* ou *TESTE: Encerrar a fase agora*.

## Documentação

Ao criar ou mudar uma mecânica, atualizar `docs/MECANICAS.md` no mesmo trabalho, descrevendo o funcionamento atual
(sem diário de implementação). Não registrar histórico de tarefas neste arquivo.
