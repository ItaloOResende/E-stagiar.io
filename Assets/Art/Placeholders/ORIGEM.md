# Assets PLACEHOLDER — origem e status

**Tudo nesta pasta é temporário.** Veio do projeto legado (branch `master`, Unity 2021.3.7, autoria do Ítalo) apenas para permitir desenvolver e testar a gameplay antes da chegada da arte oficial (Matheus, em `Assets/Art/Official/`).

- **Não referenciar** estes assets a partir de scripts.
- **Licença/autoria: desconhecida** (Regulamento 14.2 — a equipe responde por todo asset). **Não entregar** no build final sem confirmar a origem ou substituir pelo oficial.
- Materiais do legado (Standard/Built-in) **não** foram copiados; os materiais daqui são recriados em URP/Lit.

## Modelos (`Models/`)

| Arquivo aqui | Origem (`master:`) |
|---|---|
| `pc.fbx` | `Assets/blender/PC.fbx` |
| `bancada.fbx` | `Assets/blender/bancada.fbx` |
| `gaveta.fbx` | `Assets/blender/gaveta.fbx` |
| `rack_internet.fbx` | `Assets/blender/rackInternet.fbx` |
| `cabo_energia.fbx` | `Assets/blender/cabo de energia.fbx` |
| `cabo_rede.fbx` | `Assets/blender/cabo de rede.fbx` |
| `cabo_video.fbx` | `Assets/blender/cabo de video.fbx` |
| `janela.fbx` | `Assets/janela.fbx` |
| `porta.fbx` | `Assets/porta.fbx` |

## Texturas (`Textures/`)

| Arquivo aqui | Origem (`master:`) |
|---|---|
| `pc_atlas.png` | `Assets/texturas/PC 3D/PC_atlas.png` |
| `pc_front.png` | `Assets/texturas/PC 3D/pc_front GameObject.png` |
| `pc_back.png` | `Assets/texturas/PC 3D/pc_back GameObject.png` |
| `tela_no_signal.png` | `Assets/texturas/objetos/tela no signal.png` |
| `cabo_rede.png` | `Assets/texturas/objetos/cabo de rede.png` |
| `energia.png` | `Assets/texturas/objetos/energia.png` |
| `tomada_energia.png` | `Assets/texturas/objetos/tomada de energia.png` |
| `tomada_rede.png` | `Assets/texturas/objetos/tomada de rede.png` |
| `vga.png` | `Assets/texturas/objetos/vga.png` |
| `ui_mao.png` | `Assets/texturas/mao.png` (mãozinha de interação, sprite de UI) |
| `bancada.png` | `Assets/texturas/bancada.png` |
| `mesa_branca.png` | `Assets/texturas/mesa branca.png` |
| `mesa_preta.png` | `Assets/texturas/mesa preta.png` |

## Ilustrações 2D (`Sprites/`)

Pixel art 2D do legado, copiadas **byte a byte** (idênticas ao `master`), só renomeadas para ASCII. Autoria/licença **desconhecidas** (como todo o legado). Importadas como Sprite (PPU 100, filtro Point, sem compressão, sem mipmap, Alpha Is Transparency). No legado eram importadas como textura padrão e usadas em cubos finos/UI.

| Arquivo aqui | Origem (`master:`) | Uso atual |
|---|---|---|
| `placa_mae.png` (1280×1280) | `Assets/texturas/objetos/placa mae.png` | `Pickup_PlacaMae` (0,30×0,30 m) |
| `memoria_ram.png` (1160×260) | `Assets/texturas/objetos/memoria ram.png` | `Pickup_MemoriaRAM` (0,14×0,031 m) |
| `hd.png` (738×1120) | `Assets/texturas/objetos/hd.png` | `Pickup_HD` (0,10×0,152 m) |
| `hd_virado.png` (1088×233) | `Assets/texturas/hd virado.png` | importada, **sem uso ainda** (vista lateral/conectores; minigame) |
| `chave_de_fenda.png` (576×576) | `Assets/texturas/objetos/chave de fenda.png` | `Pickup_Ferramenta` (0,22×0,22 m) |

Cabos: `cabo de rede`, `energia`, `vga` e as tomadas **não são ilustrações**; são texturas de UV dos modelos 3D de cabo e já estão em `Textures/` (ver acima). Importada para a tela 2D do gabinete: `gabinete_aberto.png` (930×1000, byte-idêntica a `master:Assets/texturas/PC 3D/gabinete aberto.png`; origem e licença desconhecidas; usada em `Prefabs/UI/UI_Screen_Gabinete`). Também importada: `pc_back.png` (710×1580, byte-idêntica a `master:Assets/texturas/PC 3D/pc_back UI.png`; **mesma arte** da textura `pc_back GameObject.png` do modelo `pc.fbx`; origem e licença desconhecidas) — vista traseira do gabinete com os **8 parafusos reais** (centros medidos por análise de pixels; ver `ComputerCase.CreateDefaultTowerBackScrews`), usada em `Prefabs/UI/UI_Screen_Gabinete`. Encontradas no `master` e **não importadas** (reservadas às próximas telas): `PC 2D/monitor frente.png`, `PC 3D/pc_front UI.png`.

**Sprites de feedback criados para o projeto (sem terceiros):** gerados por script próprio durante o desenvolvimento, livres de licença externa. `ui_parafuso_furo.png` (disco escuro com borda metálica; cobre o parafuso removido), `ui_parafuso_anel.png` (anel branco: destaque ao passar o mouse e clarão ao remover), `ui_branco.png` (quadrado branco 8×8; preenchimento da barra de progresso). Usados em `Prefabs/UI/ScrewView` e `UI_Screen_Gabinete`. Substituíveis pela arte oficial.

Materiais correspondentes (URP/Lit, Alpha Clipping 0,5): `M_Item2D_PlacaMae/MemoriaRAM/HD/ChaveDeFenda`.

## Referência de comportamento (nada copiado)

O legado simulava o braço com um cubo (`braco`, escala ~0.07×0.06×0.5, filho da câmera em ~(0.3,-0.12,0.18)) e o ponto de mão (`mao Fisica`) na ponta. `Arm_Placeholder`/`HoldPoint` na `Office_Placeholder` recriam essa ideia com um cubo primitivo e `M_Braco_Placeholder` (URP/Lit).

## Deliberadamente NÃO migrados

Scripts, prefabs com scripts quebrados (`Gabinete`, `gaveteiro*`), cenas legadas (`FUNEC Riacho`, `Tela Inicial`, `testes`), TextMesh Pro antigo, `ProjectSettings`, Input Manager, tags, build antigo, ~125 materiais `parede bege N`, texturas de cenário escolar e a textura 8K `Fence007A_8K_Displacement.jpg`.
