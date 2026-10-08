# CLAUDE.md — E-stagiar.io

> Mantenha este arquivo atualizado a cada decisão técnica importante, mudança de estrutura ou avanço do roadmap.

## 1. Objetivo do jogo

Simulação educativa de suporte de TI em **primeira pessoa** (3D cartunizado, referência visual: Overcooked). O jogador é um estagiário de TI em um escritório: NPCs abrem chamados (monitor sem imagem, PC que não liga, rede, periféricos…), o estagiário investiga, consulta um manual, pega ferramentas/peças no armário de suporte, corrige o problema e recebe feedback educativo. Um **chefe bravo** ronda o escritório; condutas inadequadas percebidas por ele custam uma **vida**. Single-player, português.

Projeto para o campeonato **FRAMEWORK ARCADE** (Framework Tecnologia). **Inscrição até 21/10/2026**: GDD + build executável + vídeo ≤ 90 s. Pesos de avaliação: jogabilidade 30%, inovação 25%, viabilidade técnica 25%, apresentação 20%.

## 2. Documentos de referência

- **GDD:** `GameDocCampeonato_V1.docx` (v1.1, 06/10/2026) — fonte principal de requisitos. Fica em `docs/` ao lado do repositório (fora do git, em `E-stagiar.io/docs/`).
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
| Interação | `Assets/Scripts/Interaction` | `Interaction` (raycast da câmera), `Interactable` (contrato), futuros destaque, `Pickable`, `Socket` |
| Player | `Assets/Scripts/Player` | `PlayerMovement` (CharacterController), `PlayerLook` (câmera FPS) |
| Lógica de gameplay | `Assets/Scripts/Gameplay` | chamados, estados, vidas, infrações — lógica sem dependência de modelos |
| Dados | `Assets/Scripts/Data`, `Assets/Data` | ScriptableObjects: chamados (`Data/Tickets`), itens/peças (`Data/Items`), textos educativos |
| NPCs / Chefe | `Assets/Scripts/NPC` | solicitantes e chefe (NavMesh, percepção) |
| UI | `Assets/Scripts/UI` | HUD, manual/tablet/livro, feedback educativo |
| Minigames | `Assets/Scripts/Minigames` | estrutura para minigames |
| Núcleo | `Assets/Scripts/Core` | bootstrap, eventos, serviços compartilhados |
| Arte | `Assets/Art` | `Placeholders/` (temporário) e `Official/` (Matheus) |
| Prefabs lógicos | `Assets/Prefabs/Gameplay` | prefabs de gameplay estáveis |

**Regra de desacoplamento lógica × arte:** componentes e responsabilidades de gameplay (colliders, `Interactable`, `Pickable`, `Socket`, dados) ficam num **root lógico**; o mesh/material fica num filho **`Visual`** substituível. Substituir PC/monitor/mesa/gaveta placeholder pelo oficial do Matheus = trocar o `Visual` (ou usar Prefab Variant), sem reescrever sistemas. Código de gameplay **nunca** deve depender de nome, tag ou hierarquia do modelo 3D.

## 7. Estrutura de pastas

```
Assets/
  Scripts/{Core,Gameplay,Data,NPC,UI,Minigames,Player,Interaction}
  Data/{Tickets,Items}
  Art/Placeholders/{Models,Textures,Materials,Prefabs}   <- temporário, vem do master
  Art/Official/                                          <- assets do Matheus
  Prefabs/Gameplay/
  Scenes/{Prototype,SampleScene,Office_Placeholder}
  Settings/                                              <- URP
```

## 8. Política de assets: placeholder × oficial

- Tudo em `Assets/Art/Placeholders/` é **PLACEHOLDER temporário** vindo do projeto legado, para desenvolver e testar a gameplay. **Origem e licença desconhecidas** → rastreado em `Assets/Art/Placeholders/ORIGEM.md`. Não deve ir para a entrega final sem confirmação/substituição.
- Os assets **oficiais** (estilo 3D cartunizado: escritório, personagem, gavetas, paredes, mesas, PCs…) são produzidos por **Matheus** e serão subidos para o repositório e atualizados por ele. Vão em `Assets/Art/Official/`.
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
- Cena `Office_Placeholder` pronta (escritório 8x6 m, 2 mesas com PC, gaveta, rack, bancada, porta, janela; Player copiado da `Prototype`). Escalas ajustadas no importador: cabos 0.1, gaveta 0.25 (valores estimados, revisar visualmente no Editor).
- Projeto importa e compila no Unity 6000.3.25f1 em batchmode, sem erros.
- **Não existem ainda:** tickets, minigames, NPCs, chefe, vidas, manual, UI de jogo.

## 11. Roadmap até a entrega (21/10/2026)

Fundação (agora) → interação/pegar/socket → cena de escritório → tickets + CH-01 → manual → chefe + vidas → CH-02 → polimento, substituição por arte oficial → **build testado em máquina limpa até ~19/10**, vídeo ≤ 90 s, GDD final, pasta no Drive (item 6.4 do regulamento).

Se o prazo apertar: manter fundação, CH-01, armário, chefe simplificado + build; cortar manual avançado, segundo chamado e acessibilidade extra.

## 12. Decisões técnicas importantes

- `main` oficial; `master` só referência; sem merge (históricos independentes).
- Scripts legados **não** são migrados; servem como especificação de comportamento (ex.: máquina de estados do PC: sem vídeo → BIOS → sistema; a gaveta que abre/fecha).
- Interação por **componentes** (`Interactable` etc.), não por tags nem por nome de GameObject.
- **Sem aleatoriedade** para a conclusão de chamados: só sucesso técnico (GDD §10). Evitar `Random` decidindo peças/estados (o legado fazia isso).
- Manipulação **3D** em primeira pessoa; o minigame 2D de arrastar ícones do legado não é o modelo-alvo.
- Chamados definidos como dados (ScriptableObjects), com ficha conforme GDD §6.2.

## 13. Regras para agentes/IA

1. Ler este arquivo e o GDD antes de alterar o projeto; manter este arquivo atualizado.
2. Nunca alterar a `master`, nunca mesclar `master` → `main`, nunca fazer push/merge/force sem pedido explícito.
3. Fazer mudanças pequenas e commits coerentes; validar que o projeto abre no Unity 6000.3.25f1 sem erros no Console (batchmode com o Editor fechado).
4. Não usar `UnityEngine.Input`, nem tags/nomes como mecanismo de gameplay, nem `FindObjectsOfType`/`FindGameObjectsWithTag` por frame.
5. Não colocar lógica de gameplay dentro de prefabs de arte; manter o desacoplamento Visual × lógica (seção 6).
6. Qualquer asset novo precisa de origem/licença conhecida (Regulamento 14.2). Placeholders ficam em `Art/Placeholders` e são documentados em `ORIGEM.md`.
7. Não implementar sistemas fora do escopo pedido na etapa atual.
8. Texto de jogo e conteúdo educativo em português; conteúdo técnico de TI deve ser revisado antes da entrega.
