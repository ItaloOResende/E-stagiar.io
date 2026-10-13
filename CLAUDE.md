# CLAUDE.md — E-stagiar.io

> Mantenha este arquivo atualizado a cada decisão técnica importante, mudança de estrutura ou avanço do roadmap.

## 1. Objetivo do jogo

Simulação educativa de suporte de TI em **primeira pessoa** (3D cartunizado, referência visual: Overcooked). O jogador é um estagiário de TI em um escritório: NPCs abrem chamados (monitor sem imagem, PC que não liga, rede, periféricos…), o estagiário investiga, consulta um manual, pega ferramentas/peças no armário de suporte, corrige o problema e recebe feedback educativo. Um **chefe bravo** ronda o escritório; condutas inadequadas percebidas por ele custam uma **vida**. Single-player, português.

Projeto para o campeonato **FRAMEWORK ARCADE** (Framework Tecnologia). **Inscrição até 21/10/2026**: GDD + build executável + vídeo ≤ 90 s. Pesos de avaliação: jogabilidade 30%, inovação 25%, viabilidade técnica 25%, apresentação 20%.

## 2. Documentos de referência

- **GDD:** `GameDocCampeonato_V1.docx` (v1.1, 06/10/2026) — fonte principal de requisitos. Fica em `docs/` ao lado do repositório (fora do git, em `E-stagiar.io/docs/`). O **guia prático de mecânicas** da equipe também fica FORA do repositório: `../docs/GUIA_DE_MECANICAS.md`.
- **Regulamento:** `Regulamento-Framework-Arcade.pdf` (mesma pasta). Pontos relevantes: item 6.3 (entrega), 7.2 (build que não abre = desclassificação), 14.2 (somos responsáveis por licenças de todo asset, código, fonte e áudio).

## 3. Stack

- **Unity 6000.3.25f1 (6.3 LTS)** — Editor em `D:\WazPC\Documents\6000.3.25f1\Editor\Unity.exe`
- **URP 17.3.0** (assets em `Assets/Settings`: `PC_RPAsset`, `Mobile_RPAsset`)
- **Input System 1.20.0** (`Assets/InputSystem_Actions.inputactions`). **Não usar** `UnityEngine.Input` (Input Manager antigo).
- **AI Navigation 2.0.14** (NavMesh, para o chefe e NPCs), **uGUI 2.0.0** (inclui TextMeshPro), Timeline 1.8.13.
- C#, git com **Git LFS** (`.fbx/.png/.jpg` etc. via LFS, ver `.gitattributes`).

## 4. Papel das branches

- **`main`** = projeto **oficial**. Todo o desenvolvimento acontece aqui, em commits pequenos e coerentes.
- **`master`** = **backup/referência legada** (Unity 2021.3.7, Built-in RP, Input antigo) do protótipo original do Ítalo. **Não alterar, não mesclar na `main`.** Históricos são independentes (sem ancestral comum).
- Do `master` só se tira, seletivamente e por cópia de arquivos (`git show master:<caminho>`), (a) modelos/texturas como **placeholders** e (b) *comportamento* como referência. Scripts, `ProjectSettings`, Input Manager, builds, TextMesh Pro antigo e configs da Unity 2021 **não** são migrados.

## 5. Regras de Git

- Trabalhar direto na `main`; commits pequenos, mensagem `tipo: resumo` (`feat:`, `fix:`, `chore:`, `docs:`, `refactor:`).
- **Sem push automático** e sem merge/rebase/reset destrutivo sem revisão do usuário. Nunca mexer na `master`.
- Todo asset deve ser commitado com seu `.meta`. Não versionar `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.sln`.
- Fechar o Editor antes de rodar Unity em batchmode (não roda com o projeto aberto).

## 6. Arquitetura (alvo)

Separar claramente:

| Camada | Pasta | Responsabilidade |
|---|---|---|
| Interação | `Assets/Scripts/Interaction` | `InteractionDetector` (raycast no centro da câmera + mãozinha + clique esquerdo e direito), `ItemHolder` (ponto de mão, pegar/soltar), `Interactable` (base) → `PickupInteractable`, `OpenableInteractable` + `DrawerContents` (conteúdo por gaveta via Inspector), `UIScreenInteractable` (clique direito abre tela 2D); futuros `Socket` |
| Player | `Assets/Scripts/Player` | `PlayerMovement` (CharacterController), `PlayerLook` (câmera FPS) |
| Lógica de gameplay | `Assets/Scripts/Gameplay` | chamados, estados, vidas, infrações — lógica sem dependência de modelos. `Gameplay/Computer` (assembly `EstagiarIo.Computer`): `ComputerCase` (parafusos, estado e peças do gabinete), `ComputerPower` (cabos, Power, BIOS/sistema), `MonitorDisplay` (tela do monitor 3D), `ToolItem`/`ToolType`, `CableItem`, `ComputerPartItem` (identificam ferramenta/cabo/peça na mão) |
| Dados | `Assets/Scripts/Data`, `Assets/Data` | ScriptableObjects: chamados (`Data/Tickets`), itens/peças (`Data/Items`), textos educativos |
| NPCs / Chefe | `Assets/Scripts/NPC` | solicitantes e chefe (NavMesh, percepção) |
| UI | `Assets/Scripts/UI` | `UIScreenManager` (abre/fecha telas 2D e trava/libera o jogador) e `UIScreen` (raiz do prefab da tela, em `Assets/Prefabs/UI`; recebe `Source`/`Interactor` ao abrir); `UI/Computer`: `ComputerScreenController`, `ScrewView`, `ComputerPortView`, `ComputerSlotView`, `CableLineView` (tela do gabinete, só apresentação); `InteractionPrompt` (HUD: ações dos cliques esquerdo/direito); futuros HUD, manual/tablet/livro, feedback educativo |
| Minigames | `Assets/Scripts/Minigames` | estrutura para minigames |
| Testes | `Assets/Tests/EditMode`, `Assets/Tests/PlayMode` | testes automáticos (NUnit/Test Framework): lógica do `ComputerCase` e fluxo da tela com os prefabs reais. Rodar em batchmode com o Editor fechado: `-runTests -testPlatform EditMode\|PlayMode` |
| Núcleo | `Assets/Scripts/Core` | bootstrap, eventos, serviços compartilhados |
| Documentação | `../docs/GUIA_DE_MECANICAS.md` | guia prático (Inspector/prefabs) das mecânicas; guia prático externo ao repositório, atualizado junto com as mudanças de código |
| Arte | `Assets/Art` | por tipo: `Models/`, `Meshes/`, `Textures/`, `Materials/`, `UI/`; placeholders com prefixo `PH_` (ver `Assets/Art/README.md`) |
| Prefabs lógicos | `Assets/Prefabs/Gameplay` | prefabs de gameplay estáveis |
| Prefabs de cenário | `Assets/Prefabs/Props` | `OF_*` (oficiais) e `PH_*` (placeholders), raiz com collider + modelo filho |

**Regra de desacoplamento lógica × arte:** componentes e responsabilidades de gameplay (colliders, `Interactable`, `Pickable`, `Socket`, dados) ficam num **root lógico**; o mesh/material fica num filho **`Visual`** substituível. Substituir PC/monitor/mesa/gaveta placeholder pelo oficial do Matheus = trocar o `Visual` (ou usar Prefab Variant), sem reescrever sistemas. Código de gameplay **nunca** deve depender de nome, tag ou hierarquia do modelo 3D.

## 7. Estrutura de pastas

```
Assets/
  Scripts/{Core,Gameplay,Data,NPC,UI,Minigames,Player,Interaction}
  Data/{Tickets,Items}
  Art/{Models,Meshes,Textures,Materials,UI}              <- um lugar por tipo; PH_* = placeholder
  Prefabs/{Gameplay,Props,UI}
  Scenes/{Office_Placeholder,Prototype,SampleScene}
  Settings/                                              <- URP
```

## 8. Política de assets: placeholder × oficial

- Todo asset com prefixo `PH_` (em `Assets/Art/*` e `Assets/Prefabs/Props`) é **PLACEHOLDER temporário** vindo do projeto legado, para desenvolver e testar a gameplay. **Origem e licença desconhecidas** → rastreado em `Assets/Art/ORIGEM.md`. Não deve ir para a entrega final sem confirmação/substituição.
- Os assets **oficiais** (estilo 3D cartunizado: escritório, personagem, gavetas, paredes, mesas, PCs…) são produzidos por **Matheus** e serão subidos para o repositório e atualizados por ele. Vão nas pastas por tipo de `Assets/Art/`, sem prefixo.
- Não preservar visual/layout do legado. Não referenciar placeholders de dentro de scripts.
- Materiais do legado (Built-in/Standard) **não** são copiados; são recriados em URP/Lit.
- Lixo/duplicatas do legado (ex.: ~125 materiais `parede bege N`) não entram.

## 9. Sistemas planejados (ordem de prioridade)

1. Estrutura de pastas/arquitetura ✔ (feito)
2. Player + câmera + interação (base existente na `main`)
3. `Interactable` robusto (hoje só um marcador com mensagem)
4. Destaque/seleção de objetos sob a mira
5. Pegar/carregar/soltar objetos
6. Objetos colocáveis/conectáveis (`Socket` ou equivalente: cabos, peças)
7. Cena de escritório provisória com placeholders
8. Sistema genérico de chamados/tickets (dados em ScriptableObject)
9. **CH-01 (monitor sem imagem) como vertical slice completo:** receber chamado → ler descrição → investigar → identificar equipamento → interagir → pegar/conectar cabo quando necessário → validar solução → confirmar funcionamento → concluir → feedback educativo
10. Manual/tablet/livro
11. Minigames
12. NPCs e chefe (ronda, percepção, repreensão)
13. Vidas/infrações (com proteção temporal pós-repreensão)
14. Feedback educativo
15. CH-02 e demais funcionalidades do MVP

**MVP (GDD 8.1):** escritório navegável, movimentação FPS, 1 NPC solicitante, 1–2 chamados completos, armário com objetos interativos, manual básico, chefe com ronda e ≥1 infração detectável, indicador de vidas, feedback educativo.

## 10. Estado atual (atualizar!)

- Unity 6.3 / URP / Input System configurados.
- Player (`PlayerMovement`, `PlayerLook`) e `Interaction`/`Interactable` básicos funcionando na cena `Prototype` (`Interaction` ainda só faz `Debug.Log`).
- Estrutura de pastas criada (vazias, com `.gitkeep`).
- Placeholders migrados do `master` (9 FBX, 12 texturas) em `Art/Placeholders`, materiais recriados em URP/Lit, prefabs `PH_*` com BoxCollider (ainda sem `Interactable`/lógica).
- Cena `Office_Placeholder` (sala 8x6x3 m: piso, 4 paredes, teto, 2 mesas com PC, bancada+gaveta na parede oeste, rack, porta na leste, janela na oeste; Player copiado da `Prototype`), revisada visualmente no Unity. FBX importados com `useFileScale` ligado (gaveta 0.25, cabos 0.1, estimados); prefabs `PH_*` = root lógico (origem na base, sem rotação/escala, BoxCollider) + filho `Visual` com o FBX. FBX de mesh único trazem rotação 270° X no root: não sobrescrever.
- Projeto importa e compila no Unity 6000.3.25f1 em batchmode, sem erros.
- Interação (aguardando teste visual do usuário): mira estável no `InteractionDetector` (ajuda de mira por esfera de 3,5 cm, tolerância de 0,15 s ao alvo anterior, roda depois do `PlayerLook`). Clique esquerdo (Input System), mãozinha 2D (`ui_mao.png`) quando há `Interactable` ao alcance (3 m), `PickupInteractable` (pega/carrega/solta), `OpenableInteractable` (gaveta desliza; eixo/distância configuráveis). Prefabs lógicos em `Assets/Prefabs/Gameplay` (root lógico + `Visual` aninhado). Braço placeholder = cubo na câmera (ref. legado). Gaveta e cabos da `Office_Placeholder` configurados para teste. `PickupInteractable` preserva escala/rotação mundiais e usa bounds dos renderers, então funciona até em nós de FBX; ainda assim preferir root lógico de escala 1 + `Visual` (prefabs `Pickup_*`).
- Gaveteiros: `DrawerContents` (ao lado do `OpenableInteractable`) lista entradas {prefab `PickupInteractable`, quantidade, `PhysicalInDrawer` ou `DirectToHand`, posição/step locais}. Físicos são criados 1x no Awake, ocultos com a gaveta fechada, e saem do conteúdo ao serem pegos (sem cópias ao reabrir); entrega direta consome estoque (1 por abertura, mão livre). Na `Office_Placeholder`: `Gaveteiro_Hardware` (RAM+cooler, placa-mãe, HD+fonte, ferramentas) e `Gaveteiro_Cabos` (2 energia, VGA, HDMI, 2 rede), com gavetas ocas, puxador do `gaveta.fbx` do Matheus e plaquinhas; gerados por `Assets/Editor/GaveteirosSetup.cs` (recria prefabs e instâncias). Os itens soltos da cena foram guardados neles. Cabos com `Visual` reduzido (~11 cm) para caber. `Gaveteiro_Teste` saiu da cena. Teste: `GaveteirosTests` (Play Mode).
- Ilustrações 2D (pixel art do legado) integradas em `Art/UI` (`PH_*`) e usadas como itens no 3D (`Pickup_PlacaMae/MemoriaRAM/HD/Ferramenta`): dois quads (frente/verso) com material URP/Lit Alpha Clip, deitados; segurados inclinados (`Hold Euler Offset` -60,0,0). Aguardando teste visual.
- Telas 2D (Canvas): clique direito num objeto com `UIScreenInteractable` abre um prefab de tela (`Assets/Prefabs/UI`) sobre a primeira pessoa; o `UIScreenManager` (no Player) libera o cursor, desliga `PlayerLook`/`PlayerMovement` e bloqueia o `InteractionDetector` até fechar (Esc ou botão). Primeira tela: `UI_Screen_Gabinete`. Cena tem `ScreenCanvas` e `EventSystem` (ações UI do `InputSystem_Actions`). Texto em `Text` legado (TMP sem Essential Resources). Com item na mão o clique direito continua abrindo telas (o esquerdo só solta).
- **Manutenção do PC (tela do gabinete, implementada):**
  - **Gabinete:** `Gabinete.prefab` = root lógico escala 1 (`ComputerCase`, `UIScreenInteractable`, `PickupInteractable`, Rigidbody, BoxCollider) + filho `Visual`.
  - **Tela:** `UI_Screen_Gabinete` ocupa a tela inteira, com 3 vistas: Frente (`monitor_frente` + `pc_front`), Traseira (`ui_monitor_traseira` + `pc_back` com 8 parafusos) e Interior (`gabinete_aberto` + componentes).
    - Navegação linear Voltar/Avançar (sem dar a volta) e Fechar, no canto inferior direito.
    - Sem barra/contador de progresso.
    - Cursor = chave de fenda só na Traseira (`Cursor.SetCursor`, hotspot na ponta; restaurado ao trocar de vista ou fechar).
  - **Parafusos:** `ScrewData.required`. Só os 3 da direita são obrigatórios; topo e esquerda são opcionais.
    - `ComputerCase.RequiredScrewsRemoved`/`TryOpen` verificam o conjunto de obrigatórios, não a contagem.
    - Avançar da Traseira abre a tampa e mostra o Interior; sem os obrigatórios, fica na Traseira.
  - **Componentes internos** (`PcComponent`: placa-mãe, RAM, HD, fonte, cooler; RAM e cooler montados na placa-mãe):
    - Sorteados de forma independente (RAM/cooler sem placa-mãe são descartados), uma vez por instância, no `ComputerCase` (`Randomize Components`, `Component Chance`, `Component Seed`, ou `Fixed Components`/`SetComponents`).
    - Estáveis ao fechar/reabrir; a tela só mostra ou esconde os sprites.
  - Estado sempre no objeto lógico. Chave de fenda = item real (`Pickup_Ferramenta` + `ToolItem`).
  - Validado: 21 testes Edit Mode + 8 Play Mode + capturas renderizadas; **aguardando teste manual** (clique real, cursor). Guia: `../docs/GUIA_DE_MECANICAS.md` §4.3.
- **Personagem oficial:**
  - `Player/Visual` = `character.fbx` (malha estática, sem rig), ajustado à cápsula do `CharacterController` e com o espelhamento do FBX preservado.
  - **Visível** (*Cast Shadows* On) na camada **`PlayerBody`** (layer 8), excluída só da `Main Camera` (que fica dentro da cabeça). O antigo *Shadows Only* era a causa do modelo "transparente" em todas as vistas. A sala fechada não recebe sol, então o corpo não projeta sombra em nenhum modo.
  - Braço em 1ª pessoa = braço livre do próprio personagem: `character_braco_fp.fbx`, extraído por `Tools/blender/character_braco_fp.py`, em `Main Camera/Arm_Personagem` (`ItemHolder.armVisual`). O `Arm_Placeholder` (cubo) ficou desativado. `Main Camera` com near clip 0,05 (era 0,3 e cortava braço e item na mão).
  - A cápsula placeholder da raiz ficou com o `MeshRenderer` desligado. Na cena, o `Player` está com escala **0,85** (era 0,514: cápsula de 2 m → 1,0 m de personagem e olhos a ~0,93 m, mais baixo que a cadeira de 1,22 m). Agora: cápsula 1,70 m, modelo ~1,65 m, olhos ~1,53 m; `CharacterController.radius` 0,35 (0,30 m no mundo); começa apoiado no chão (y 0,93). Câmera e controles não mudaram.
- **HUD de interação:** `HUD/Prompt` (`InteractionPrompt`) mostra a mão + ação do clique esquerdo e, à direita, o ícone + ação do clique direito (`Interactable.promptIcon`/`PromptText`). No gabinete: "Esq.: Pegar" e chave de fenda "Dir.: Manutenção". O `InteractionDetector.handCursor` ficou vazio (legado).
- **Energia, cabos e peças do PC (implementado, aguardando teste manual):**
  - `ComputerPower` no root do `Gabinete`. Um cabo por ligação (`PcLink`): energia do PC (tomada ↔ gabinete), energia do monitor (tomada própria ↔ monitor), VGA e HDMI (gabinete ↔ monitor; basta um) e rede (ponto de rede ↔ gabinete, opcional), conectados pela Traseira da tela 2D com o cabo na mão (`CableItem`). Valores novos de `PcPort`/`PcLink`/`CableType`/`PcComponent` sempre no fim (prefabs guardam o número).
  - Peças (`ComputerPartItem`) instaladas/retiradas pelos encaixes do Interior (`ComputerCase.UsePartSlot`). Itens conectados/instalados saem da mão (`ItemHolder.Stow`) e voltam por `Retrieve`: nunca são destruídos. Peça que veio no gabinete vira uma instância do prefab configurado ao ser retirada.
  - Power (Frente) → BIOS com energia do PC e do monitor + vídeo + fonte + placa-mãe + RAM + cooler; → sistema com + HD (tela "conectado" com a rede); sem requisitos não liga; perder requisito com o PC ligado desliga; HD novo só vale após reiniciar.
  - Monitor: tela 2D (`Frente/Monitor/Area/Tela`) e 3D (`OF_Monitor/.../Tela` com `MonitorDisplay`). Telas `ui_tela_bios`/`ui_tela_sistema` geradas por `Tools/screens/gerar_telas_monitor.py`.
  - Tomadas (PC e monitor) e ponto de rede à direita do gabinete na Traseira. Vistas = renders fixos dos modelos 3D (`Tools/blender/pc_canvas_renders.py`); interior com fonte, placa-mãe (RAM e cooler sobre ela) e HD deitado. Prefabs/cena configurados por `Assets/Editor/PcManutencaoSetup.cs` (idempotente).
  - Cabos 3D: `PcCableView3D` no `Gabinete` desenha cada cabo conectado (tubo curvo + plugues; uma ponta = pendente) entre âncoras `Porta3D_<PcPort>` (Z para fora) do gabinete, do monitor da cena e do `PontoEletrico` (parede). Itens `Pickup_Cabo*` = rolos de `cabos.fbx` (`Tools/blender/cabos_build.py`). Na tela 2D: cabos curvos (`UICableCurve`), plugues/tomadas renderizados, ponta solta segue o mouse. Configurado por `Assets/Editor/Cabos3DSetup.cs`.
  - Visual (etapa 3): fundo `#DDE5E8`, proporções naturais (mesma escala px/m), LED do Power só aceso com o PC ligado (Canvas `powerLed` e 3D `PcPowerLed3D`), hover em anel branco, gabinete aberto = render 3D do gabinete oficial com camadas das peças 3D (`componentes.fbx`, também nos itens do mundo). Configurado por `Assets/Editor/ManutencaoVisualSetup.cs`. Reverter: `Art/_Backup/2026-10-12_etapa3/RESTAURAR.md`.
  - Validado: 38 testes Edit Mode + 15 Play Mode (inclui `GaveteirosTests`, `MiraECabos3DTests`) + capturas. Guia: `../docs/GUIA_DE_MECANICAS.md` §4.4.
- **Pendrive e sistema operacional (implementado, aguardando teste manual):** o sistema é do **HD** (`ComputerPartItem.HasOperatingSystem`); o HD que vem no gabinete tem sistema conforme `ComputerCase` *Starting Hdd Has Operating System* (padrão ligado; `SetComponents(c, hddHasOs)` para cenários) e o leva ao ser retirado; HD da gaveta vem vazio. Sistema exige HD com sistema; HD vazio + `Pickup_Pendrive` numa das 3 USB da traseira (`ComputerPower.UseUsbPort`, `UsbPortView`, `PcPendriveView3D`) → ao ligar, BIOS por 3 s instalando e depois sistema (telas de rede inalteradas). Configurado por `Assets/Editor/PendriveSetup.cs`. Guia §4.5.
- **Ajustes da manutenção (implementado, aguardando teste manual):** telas de boot distintas (`ComputerPower.Screen`: sem HD / HD sem sistema "No bootable device" / instalando em 5 etapas / sistema), instalação de 6 s que só anda com o jogador na Frente da tela 2D (`InstallPaused`); mãozinha padrão em todas as telas (`UIScreenManager` + `UICursor`) e chave de fenda 3D como cursor só na Traseira com a chave na mão, proporcional aos parafusos; `Pickup_Ferramenta` com modelo 3D; cabo da tomada do monitor na frente do plugue; soltar pela mira (`ItemHolder.Drop`: guarda na gaveta compatível com `DrawerContents.TryStore`/`AreaGuardar`, coloca em superfície, ou cai pela física) e `PickupInteractable.Placement` para o supervisor futuro. Configurado por `Assets/Editor/ManutencaoAjustesSetup.cs`. Guia §2, §4, §4.3 e §4.5.
- **Câmera, estoque e dano (implementado, aguardando teste manual):** câmera nos olhos do modelo, HoldPoint mais baixo e braço refeito (`JogadorBracoSetup.cs`); item solto não colide com o jogador até se afastar; estoque finito nas gavetas (`GaveteirosSetup.HardwareDrawers/CableDrawers`); quedas reais (impacto ≥ 2,2 m/s) registradas em `ComputerPartItem.Condition` por `PartFallDamage`: placa-mãe e RAM quebram na 1ª queda, HD/fonte/cooler na 2ª (modelo quebrado, não instala nem volta à gaveta). Mira ignora os colliders do próprio jogador; item solto sem destino cai à frente. Guia §2, §4, §4.6.
- **Toon Shader de volta:** `com.unity.toonshader` reinstalado para os materiais do Matheus (2ª leva, ver `Assets/Art/ORIGEM.md`); `M_PC_*` convertidos e sombras/luz de borda corrigidas em todos os materiais Toon (`ToonShaderSetup.cs`).
- **Assets oficiais do Matheus (1ª leva, aguardando conferência no Editor):** 15 FBX de escritório em `Art/Models`, 6 texturas e 10 materiais convertidos de Unity Toon Shader para URP/Lit (o contorno/toon foi perdido; 2 materiais pendentes por textura ausente). Vieram por engano da `master` e foram integrados por cópia seletiva com GUIDs preservados; cenas/Player/ProjectSettings do commit dele não entraram. Detalhes: `Art/ORIGEM.md`.
- **Uso dos oficiais na `Office_Placeholder` (aguardando conferência visual):**
  - Prefabs visuais `Prefabs/Props/OF_*` (root lógico + `Visual`, escala única k=0,364).
  - As 2 mesas usam `OF_Mesa`. Os primitivos `Top`/`Side_*` foram desativados, não apagados.
  - `PC_2` (PH_Pc) foi desativado e substituído por `OF_Monitor`/`OF_Teclado`/`OF_Mouse`. Também entraram 2 `OF_Cadeira`, `OF_Vaso` e `OF_Xicara`.
  - `Gabinete.prefab` trocou só o `Visual` (malha oficial) e o tamanho do `BoxCollider`.
  - Continuam placeholders: `PC_1` (tem pickups de teste nos nós do FBX), gaveteiro/gavetas (o `gaveta.fbx` oficial é malha única, sem gavetas móveis), bancada, porta, janela, rack, cabos, paredes/piso/teto.
- **Não existem ainda:** tickets, minigames, NPCs, chefe, vidas, manual, HUD de jogo (além do prompt), bancada, encaixe de cabos/peças no mundo 3D (`Socket`), energia do monitor, rede e periféricos, tela "sem sinal". Princípio para a bancada: peças vêm dos objetos físicos próximos; instalar/remover deve manter UI e mundo em correspondência, sem duplicar nem destruir definitivamente os itens.

## 11. Roadmap até a entrega (21/10/2026)

Fundação (agora) → interação/pegar/socket → cena de escritório → tickets + CH-01 → manual → chefe + vidas → CH-02 → polimento, substituição por arte oficial → **build testado em máquina limpa até ~19/10**, vídeo ≤ 90 s, GDD final, pasta no Drive (item 6.4 do regulamento).

Se o prazo apertar: manter fundação, CH-01, armário, chefe simplificado + build; cortar manual avançado, segundo chamado e acessibilidade extra.

## 12. Decisões técnicas importantes

- `main` oficial; `master` só referência; sem merge (históricos independentes).
- Scripts legados **não** são migrados; servem como especificação de comportamento (ex.: máquina de estados do PC: sem vídeo → BIOS → sistema; a gaveta que abre/fecha).
- Interação por **componentes** (`Interactable` etc.), não por tags nem por nome de GameObject.
- **Sem aleatoriedade** para a **conclusão** de chamados: só sucesso técnico (GDD §10). Exceção decidida na revisão do projeto (10/2026): o **conteúdo interno** de cada PC (placa-mãe/RAM/HD) é sorteado uma vez por instância, de forma estável. Um chamado que dependa de um componente deve **fixá-lo** (`Fixed Components`/`SetComponents` ou `Component Seed`), nunca contar com o sorteio.
- Manipulação **3D** em primeira pessoa; o minigame 2D de arrastar ícones do legado não é o modelo-alvo.
- Chamados definidos como dados (ScriptableObjects), com ficha conforme GDD §6.2.

## 13. Regras para agentes/IA

1. Ler este arquivo e o GDD antes de alterar o projeto; manter este arquivo atualizado.
2. Nunca alterar a `master`, nunca mesclar `master` → `main`, nunca fazer push/merge/force sem pedido explícito.
3. Fazer mudanças pequenas e commits coerentes; validar que o projeto abre no Unity 6000.3.25f1 sem erros no Console (batchmode com o Editor fechado).
4. Não usar `UnityEngine.Input`, nem tags/nomes como mecanismo de gameplay, nem `FindObjectsOfType`/`FindGameObjectsWithTag` por frame.
5. Não colocar lógica de gameplay dentro de prefabs de arte; manter o desacoplamento Visual × lógica (seção 6).
6. Qualquer asset novo precisa de origem/licença conhecida (Regulamento 14.2). Placeholders levam o prefixo `PH_` e são documentados em `Assets/Art/ORIGEM.md`.
7. Não implementar sistemas fora do escopo pedido na etapa atual.
8. **Documentação obrigatória de mecânicas:** toda mecânica reutilizável nova ou alterada deve ser acompanhada, no mesmo trabalho, de atualização de `../docs/GUIA_DE_MECANICAS.md` (uma seção por mecânica, não por objeto): componentes e em qual GameObject ficam (raiz lógica × `Visual` × FBX), componentes nativos (Collider/Rigidbody), campos do Inspector com exemplos, criação de prefab e de novas instâncias, como configurar um objeto novo sem código, como testar, erros comuns, limitações e dependências. Baseado em código/prefabs reais; marcar o status (implementado / testado manualmente / planejado) e nunca documentar o que não existe. O objetivo é que a equipe configure novos objetos sozinha; se um objeto exigir comportamento realmente novo, explicar por que os componentes existentes não bastam. Não basta compilar nem funcionar só nos objetos configurados pelo agente.
9. Texto de jogo e conteúdo educativo em português; conteúdo técnico de TI deve ser revisado antes da entrega.
