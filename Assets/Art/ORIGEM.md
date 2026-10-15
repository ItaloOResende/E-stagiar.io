# Origem dos assets oficiais

## Escritório (Matheus, out/2026)

- **Autor:** Matheus (git `tujikonoriko420`), commits `bf8c704` ("modelos de escritorio") e `9f4929b` ("shaders") enviados por engano à `master`.
- **Integração seletiva na `main`:** cópia arquivo a arquivo (`git show 9f4929b:<caminho>`), com os `.meta` originais (GUIDs preservados). Os arquivos com espaço no nome foram renomeados. O conteúdo é idêntico ao original.
- **Ferramenta:** FBX exportados do Blender 5.0.1 e projeto do autor no Unity 6000.5.10f1.
- **Não integrados:**
  - as cenas `NOVO.unity` e `FUNEC Riacho.unity`, por usarem ProBuilder e o projeto legado;
  - `prefabs/Player.prefab`, que tem scripts legados;
  - `ProjectSettings`, `.vscode` e `.slnx`.
- A cena `NOVO.unity` da `master` serve de referência de layout e de qual material vai em cada modelo.

### Derivado: `Models/character_braco_fp.fbx` (braço em 1ª pessoa)
- Mesma autoria e licença do `character.fbx` (Matheus): é o braço livre do personagem (parte solta do braço + manga), sem alteração de geometria, UV ou material.
- Só a posição, a orientação e a escala mudam (ombro na origem, braço para a frente).
- Gerado por `Tools/blender/character_braco_fp.py` (Blender 5.2, headless) e usado em `Player/Main Camera/Arm_Personagem` com `M_Character_CylinderLayer2`.

### Modelos (`Models/`, origem `Assets/Modelos/`)

São 15 arquivos: `cadeira`, `character`, `divisa`, `gabinete`, `gaveta`, `mesa`, `mesa_2` (`mesa 2`), `mesa_3` (`mesa 3`), `mesa_grande` (`mesa grande`), `monitor`, `mouse`, `teclado`, `tela_projetor` (`tela projetor`), `vaso` e `xicara`.

Todos são malha estática: sem rig, sem animação e sem avatar, incluindo o `character`. Os materiais embutidos nos FBX ("UV", "Lit"…) são do Blender; para usar os do projeto, defina os materiais no renderer.

### `gabinete.fbx` e `monitor.fbx` atualizados (2026-10-12)

- A base continua sendo a malha do Matheus, sem alteração: as peças novas foram acrescentadas e **juntadas na mesma malha** (`Cube.001` no gabinete, `Cube` no monitor). O fileID da malha não mudou, então prefabs e cena seguem apontando para ela.
- **Gabinete:** frente do Matheus + botão Power; traseira, fonte, entradas de energia (IEC), VGA e Ethernet (RJ45) e os 8 parafusos reaproveitados do gabinete v2 (mesmos ids/posições do `ComputerCase` e da tela `pc_back`), e saída HDMI ao lado da VGA.
- **Monitor:** traseira nova (carcaça, VESA, ventilação), entradas de energia (IEC), VGA e HDMI. A frente (tela) não mudou. O cabo de força **não** faz parte do modelo: é um item do jogo (`Pickup_CaboEnergia`) conectado na manutenção (o script ainda sabe modelá-lo, com `CABO_FIXO = True`).
- Gerados por `Tools/blender/pc_modelos_build.py` (Blender 5.2, headless; autoria própria do projeto). Fonte de trabalho em `Art/Source/Blender/` e backup dos originais em `Art/_Backup/`.
- Material 0 = o do Matheus (`M_Escritorio_Escuro`); as peças novas usam `M_PC_Porta`, `M_PC_Painel`, `M_PC_Chapa`, `M_PC_Parafuso`, `M_PC_VGA`, `M_PC_Pino`, `M_PC_LED` e `M_PC_HDMI` (URP/Lit, criados no projeto), remapeados no `.meta` dos FBX.
- Cada conector modelado tem a porta correspondente na lógica (`ComputerPower`): energia, VGA, HDMI e Ethernet do gabinete; energia, VGA e HDMI do monitor.

### Imagens do Canvas de manutenção (`UI/ui_render_*`, 2026-10-12)

- `ui_render_pc_frente`, `ui_render_pc_traseira`, `ui_render_pc_aberto`, `ui_render_monitor_frente` e `ui_render_monitor_traseira` são renders dos próprios `gabinete.fbx`/`monitor.fbx` atualizados, com câmeras ortográficas fixas (sem segunda versão desenhada à mão). Gerados por `Tools/blender/pc_canvas_renders.py` (Blender Workbench).
- O quadro de cada câmera é exatamente a caixa do equipamento, então as coordenadas normalizadas do Canvas (parafusos do `ComputerCase`, portas, botão Power, tela) batem com a imagem.
- O interior (`ui_render_pc_aberto`) é montado só na cena de render, dentro do contorno do gabinete: o modelo do Matheus é maciço. Os encaixes ficam nas posições dos `Encaixe_*` do Canvas.
- Substituem no `UI_Screen_Gabinete` os placeholders `pc_front`, `pc_back`, `monitor_frente`, `ui_monitor_traseira` e `gabinete_aberto` (removidos na reorganização de 2026-10-12).

### Sprites em pixel art da manutenção (`UI/`, 2026-10-12)

- `ui_fonte`, `ui_cooler`, `ui_plugue_energia`, `ui_plugue_vga`, `ui_plugue_hdmi`, `ui_plugue_rj45`, `ui_plugue_tomada`, `ui_tomada_energia` e `ui_ponto_rede`: desenhados por código (`Tools/sprites/gerar_sprites_pc.py`, PIL), autoria própria do projeto, no estilo dos sprites existentes (grade pequena ampliada sem suavização; *Filter Mode* Point).
- `ui_hd_deitado` e `ui_memoria_ram_vertical`: os sprites `hd` e `memoria_ram` `PH_hd`/`PH_memoria_ram` recortados ao desenho e girados 90° (mesma autoria/licença deles).

### Gaveteiros (`Meshes/Gaveteiro_*`, `Textures/Plaquinhas/`, `Materials/Plaquinhas/`, 2026-10-12)

- `Gaveteiro_Puxador.asset`: a peça solta (puxador) do `gaveta.fbx` do **Matheus**, sem mudar a forma: só reorientada (frente → +Z) e escalada para 14 cm. Mesma autoria/licença do `gaveta.fbx`.
- `Gaveteiro_Corpo.asset`, `Gaveteiro_Gaveta.asset` e `Gaveteiro_Plaquinha.asset`: malhas simples (caixas) geradas por `Assets/Editor/GaveteirosSetup.cs`, autoria própria do projeto, com os materiais do Matheus (`M_Escritorio_Claro` no corpo e na frente, `M_Escritorio_Escuro` dentro da gaveta) e `M_PC_Chapa` no puxador.
- O `gaveta.fbx` inteiro (criado-mudo com uma gaveta e nicho) não foi usado como gaveteiro: é uma malha única, a gaveta não desliza separada nem tem caixa interna, e só cabe uma gaveta.
- `Textures/Plaquinhas/plaquinha_*.png`: geradas por `Tools/sprites/gerar_plaquinhas.py` (PIL, pixel art ampliada 4x, fonte DejaVu Sans Mono livre que acompanha o Blender). Materiais `M_Plaquinha_*` (URP/Lit).

### Cabos e conectores 3D (`Models/cabos.fbx`, `Materials/Cabos/`, `UI/ui_render_plugue_*`, 2026-10-12)

- `cabos.fbx`: plugues (tomada NBR 14136, IEC C13, VGA, HDMI, RJ45), rolos dos 4 tipos de cabo e o espelho de parede `PontoEletrico` (2 tomadas + ponto de rede). Autoria própria do projeto, gerados por `Tools/blender/cabos_build.py` (Blender 5.2), na mesma escala "cartoon" (x1,35) dos conectores do gabinete/monitor, para encaixar nas portas modeladas lá; reaproveitam os materiais `M_PC_*` (metal, latão, azul VGA, parafusos).
- Substituem os placeholders `PH_CaboEnergia`/`PH_CaboVideo`/`PH_CaboRede` no `Visual` dos `Pickup_Cabo*` (removidos na reorganização de 2026-10-12).
- Materiais novos `M_Cabo_Preto/Cinza/Azul`, `M_Plug_Preto`, `M_Plug_Translucido` (URP/Lit transparente), `M_Tomada_Branco/Interior/Rebaixo` em `Materials/Cabos/`; `M_Gaveta_Forro` (forro claro das gavetas).
- `UI/ui_render_plugue_*`, `ui_render_tomada`, `ui_render_ponto_rede`: renders fixos desses modelos (Workbench) para o Canvas.

### Componentes 3D e gabinete aberto (`Models/componentes.fbx`, `Materials/Componentes/`, `UI/ui_render_peca_*`, 2026-10-12)

- `componentes.fbx`: placa-mãe (layout do sprite `placa_mae`), memória RAM, HD 3,5", fonte ATX e cooler. São modelos 3D simples de autoria própria do projeto, gerados por `Tools/blender/componentes_build.py`, que reaproveitam os materiais `M_PC_*`. Usados nos itens `Pickup_PlacaMae/MemoriaRAM/HD/Fonte/Cooler` no lugar das ilustrações 2D.
- `ui_render_pc_aberto` e `ui_render_peca_*`: render do gabinete **oficial do Matheus** aberto por boolean (lado da tampa), com estrutura interna e esses componentes (`Tools/blender/pc_interior_renders.py`), recortado em camadas por `pc_interior_camadas.py`.
- `ui_render_pc_frente` agora tem o botão Power apagado; `ui_render_pc_frente_led` é o LED aceso recortado. `ui_hover_anel` foi gerado por `Tools/sprites/gerar_sprites_pc.py`. `M_PC_LED_Off` é o LED apagado no 3D.

### Pendrive e USB elevadas (`Models/componentes.fbx` `Item_Pendrive`, `UI/ui_render_pendrive`, 2026-10-13)

- `Item_Pendrive`: pendrive simples (corpo azul, etiqueta, conector USB-A) de autoria própria do projeto, gerado por `Tools/blender/componentes_build.py` (mesmos materiais `M_Comp_*`). O conector tem a medida da boca das USB do gabinete (9 x 16 mm) para encaixar sem flutuar. `ui_render_pendrive`: render dele encaixado, visto de trás (mesmo render Workbench dos plugues, `cabos_build.render`).
- `gabinete.fbx`: só as 3 USB do I/O traseiro subiram (`gabinete_v2_build.USB_YN` 0,615 → 0,47, acima das PS/2) para os cabos das placas de expansão não esconderem o pendrive. Regerado pelo fluxo de sempre (`pc_modelos_build.py` sobre o original do Matheus em `Art/_Backup/2026-10-12_gabinete/`); a malha, os materiais e o resto do modelo não mudaram (render da frente idêntico). `ui_render_pc_traseira` regerado por `pc_canvas_renders.py`. Backup do estado anterior em `Art/_Backup/2026-10-13_usb_pendrive/`.

### Chave de fenda 3D, cursor e telas de boot/instalação (2026-10-13)

- `Item_ChaveDeFenda` (`componentes.fbx`): chave de fenda Phillips simples (cabo vermelho com frisos, haste de aço) de autoria própria do projeto, gerada por `Tools/blender/componentes_build.py` com os materiais `M_Comp_*`. Usada no `Pickup_Ferramenta` no lugar das ilustrações 2D (`PH_chave_de_fenda` continua como ícone do HUD).
- `UI/ui_cursor_chave_de_fenda.png`: render dessa chave a 45° (ponta embaixo à esquerda), pelo mesmo script; cursor da Traseira.
- `UI/PH_ui_tela_sem_sinal.png` (2026-10-13): tela preta do monitor sem entrada de vídeo, só com "Sem sinal" e "Nenhuma entrada de vídeo detectada", 640 × 464 (proporção da área do display), gerada pela função `sem_sinal` do mesmo script (autoria própria, fonte DejaVu Sans Mono livre).
- `UI/PH_ui_tela_bios_sem_sistema.png` e `UI/PH_ui_tela_instalando_0..4.png`: telas geradas por `Tools/screens/gerar_telas_monitor.py` (autoria própria, fonte DejaVu Sans Mono livre); `PH_ui_tela_bios.png` regerada com a mensagem "Nenhum dispositivo de armazenamento detectado".

### Assets do Matheus recebidos pelo Discord e Toon Shader (2026-10-13)

- Pasta recebida `assets matheus/` (fora do repositório, não alterada). Integrados com os GUIDs dele: modelos `caixa.fbx`, `impressora.fbx`, `objetos.fbx`, `character2.fbx`, `gaveta_separada.fbx` (era `gaveta separada.fbx`); texturas `character_1DARK.png`, `character_2.png`, `officepapers.png`; materiais `M_Objetos_Parede.mat` (era `Objetos parede.mat`) e `M_Character_Tex1_1.mat` (era `M_Character_Tex1 1.mat`). Os modelos novos ainda não têm materiais ligados (no projeto dele isso fica na cena) nem prefabs.
- Substituídos pelas versões dele (Unity Toon Shader, mesmos GUIDs): `M_Character_CylinderLayer2`, `M_Character_Tex1` (agora usa `character_1DARK`), `M_Escritorio_Claro`, `M_Escritorio_Escuro`, `M_Of`, `M_Plant`. Pacote `com.unity.toonshader` 0.15.1-preview reinstalado. Backup dos anteriores (URP/Lit) em `Art/_Backup/2026-10-13_matheus_toon/`.
- Não usados: 69 arquivos idênticos; `character_1.png`/`plant_texture.png` (mesmos pixels); `gabinete.fbx` e `componentes.fbx` (cópias antigas das nossas, sem USB elevadas/pendrive/chave); `monitor.fbx` (corrompido: ponteiro Git LFS com conflito de merge); `character.fbx` (mantido o atual, que tem 6 slots de material; o dele tem 1).
- Sombras dos materiais Toon corrigidas (`ToonShaderSetup.FixShading`): sombra com a textura/cor da própria peça (`_Use_BaseAs1st`), cor sem multiplicar pela intensidade da luz (`_Is_LightColor_* = 0`) e sem luz de borda branca (`_RimLight = 0`). Sem isso, peças pretas ficavam lilás, a planta perdia a textura e havia um halo claro nas bordas (perto do monitor).
- `monitor.fbx` regerado (2026-10-13, `pc_modelos_build.py`, mesma geometria): UV por projeção nas peças acrescentadas e nas faces do original com UV degenerada (laterais do pé/pescoço). Nelas o Unity calculava tangente paralela à normal; o Toon Shader gerava NaN e o bloom do pós-processamento o espalhava como um brilho forte nas laterais do monitor, maior de perto. Backup em `Art/_Backup/2026-10-13_monitor_uv/`.
- `M_PC_*` (detalhes do gabinete e do monitor, criados pelo projeto) passaram para o Toon Shader com a configuração do `M_Escritorio_Escuro` dele e as nossas cores (`Assets/Editor/ToonShaderSetup.cs`); os modelos não mudaram.

### Peças quebradas (`componentes.fbx`: `Item_PlacaMae_Quebrada`, `Item_MemoriaRAM_Quebrada`, 2026-10-13)

- Também `Item_HD_Quebrado`, `Item_Fonte_Quebrada` e `Item_Cooler_Quebrado` (carcaça partida), mesmo processo.
- Geradas por `Tools/blender/componentes_build.py` a partir das próprias malhas da placa-mãe e da RAM (corte em diagonal, metade afastada e dobrada, lascas), mesmos materiais. Autoria própria do projeto.

### Prefabs visuais (`Assets/Prefabs/Props/`, criados na integração)

Seguem o padrão dos `PH_*`:
- **Raiz:** origem na base, sem rotação nem escala, com `BoxCollider` ajustado à malha.
- **Filho `Visual`:** instância do FBX.

Os prefabs são `OF_Mesa`, `OF_Monitor`, `OF_Teclado`, `OF_Mouse`, `OF_Cadeira`, `OF_Vaso` e `OF_Xicara`. Não têm lógica de gameplay.

**Escala:** um fator único **k = 0,364** sobre a escala do FBX, para a mesa ficar com 0,75 m de altura (a das mesas da cena). Nessa escala o personagem fica com cerca de 1,65 m, o que confirma a proporção usada na cena do Matheus. Algumas peças levam um fator extra:
- cadeira ×0,85 e vaso ×0,55, os mesmos da cena `NOVO`;
- xícara ×0,5, porque com k ela ficava com 24 cm.

Tamanhos finais (m):

| Prefab | Largura × altura × profundidade |
|---|---|
| `OF_Mesa` | 2,19 × 0,75 × 1,07 |
| `OF_Monitor` | 0,73 × 0,73 × 0,15 |
| `OF_Teclado` | 0,68 × 0,06 × 0,41 |
| `OF_Mouse` | 0,33 × 0,04 × 0,26 |
| `OF_Cadeira` | 0,81 × 1,22 × 0,81 |
| `OF_Vaso` | 0,91 × 1,24 × 0,93 |
| `OF_Xicara` | 0,12 × 0,09 × 0,09 |
| Gabinete (gameplay) | 0,30 × 0,52 × 0,62 |

A frente dos modelos aponta para +Z do FBX. Na `Office_Placeholder` as instâncias usam 180° Y, para ficarem viradas para a sala.

### Texturas (`Textures/`, origem `Assets/Materials/`)

| Arquivo | Original | Usada por |
|---|---|---|
| `character_1.png` | `1.png` | `M_Character_Tex1` |
| `character_plane_layer.png` | `Plane Layer.png` | `M_Character_PlaneLayer` |
| `character_cylinder_layer_2.png` | `Cylinder.001 Layer 2.png` | `M_Character_CylinderLayer2` |
| `plant_texture.png` | `PlantTexture.png` | `M_Plant` |
| `ceiling.png` | `ceiling.png` | `M_Ceiling` |
| `of.png` | `of.png` | `M_Of` |

**Licença a confirmar com o Matheus** (Regulamento 14.2): saber se as texturas foram pintadas por ele ou baixadas. O nome `demountable-ceiling-tile-stack-60ecba3934ac5-1200` sugere imagem da internet.

### Materiais (`Materials/`, origem `Assets/Materials/`)

Originalmente usavam o **Unity Toon Shader** (modo Built-in), que não está no projeto. Foram convertidos para **URP/Lit**, mantendo a textura, o tiling/offset e a cor base.

| Material | Original | Uso na cena `NOVO` |
|---|---|---|
| `M_Escritorio_Claro` | `New Material` | divisa, gaveta, mesa, mesa 2, xícara |
| `M_Escritorio_Escuro` | `New Material 2` | gabinete, monitor, mouse, teclado, cadeira, gaveta, character (slot 5) |
| `M_Character_CylinderLayer2` | `New Material 4` | character (slot 0) |
| `M_Character_PlaneLayer` | `New Material 3` | character (slot 1) |
| `M_Character_Tex1` | `New Material 1` | character (slot 2) |
| `M_Plant` | `New Material 8` | vaso |
| `M_Ceiling` | `New Material 7` | teto (cubos ProBuilder) |
| `M_Of` | `New Material 5` | cubo ProBuilder (placa/parede) |
| `M_NewMaterial6_PENDENTE` | `New Material 6` | não usado na cena |
| `M_CeilingTileStack_PENDENTE` | `Materials/demountable-ceiling-tile-stack-60ecba3934ac5-1200` | não usado na cena |

**Perdido na conversão (todos os materiais):**
- o contorno preto do toon (`Outline_Width` 40; o `M_CeilingTileStack` tinha 0);
- o sombreamento em faixas, com a cor de sombra arroxeada `1st_ShadeColor`.

A emissão estava ligada, mas com cor preta, então não fez diferença. Recuperar esse visual exige instalar o pacote Unity Toon Shader (URP) ou criar um shader/Renderer Feature próprio.

**PENDENTE:** `M_NewMaterial6_PENDENTE` e `M_CeilingTileStack_PENDENTE` referenciam a textura de GUID `1c8ec3f9c33df2646af67198e495e1e3`, que não foi enviada ao repositório (não existe em nenhum commit). Por isso aparecem brancos. A referência foi mantida no `.mat` para religar automaticamente quando a textura chegar com o `.meta` original.

---

## Assets PLACEHOLDER — origem e status

**Todo arquivo com prefixo `PH_` é temporário** (antes ficavam em `Art/Placeholders/`; na reorganização de 2026-10-12 foram movidos para as pastas por tipo e ganharam o prefixo `PH_`, mantendo os GUIDs). Veio do projeto legado (branch `master`, Unity 2021.3.7, autoria do Ítalo) apenas para permitir desenvolver e testar a gameplay antes da chegada da arte oficial (Matheus, sem prefixo). Nas tabelas abaixo, o arquivo atual é `PH_` + o nome listado (materiais: `PH_M_*`), na pasta do seu tipo (`Models/`, `Textures/`, `Materials/`, `UI/`; prefabs em `Assets/Prefabs/Props/`).

- **Não referenciar** estes assets a partir de scripts.
- **Licença/autoria: desconhecida** (Regulamento 14.2 — a equipe responde por todo asset). **Não entregar** no build final sem confirmar a origem ou substituir pelo oficial.
- Materiais do legado (Standard/Built-in) **não** foram copiados; os materiais daqui são recriados em URP/Lit.

## Modelos (`Models/`)

| Arquivo aqui | Origem (`master:`) |
|---|---|
| ~~`pc.fbx`~~ | `Assets/blender/PC.fbx` — **removido em 2026-10-13** (substituído pelo `PC_Escritorio` com o gabinete/monitor oficiais; cópia em `Art/_Removidos_2026-10-13/`) |
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
| ~~`pc_atlas.png`~~ | `Assets/texturas/PC 3D/PC_atlas.png` — **removido em 2026-10-13** (só o `PH_pc.fbx` usava; cópia em `Art/_Removidos_2026-10-13/`) |
| ~~`pc_front.png`~~ | `Assets/texturas/PC 3D/pc_front GameObject.png` — **removido em 2026-10-13** (idem) |
| ~~`pc_back.png`~~ | `Assets/texturas/PC 3D/pc_back GameObject.png` — **removido em 2026-10-13** da pasta `Textures/` (idem; o `UI/PH_pc_back.png` continua) |
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

## Ilustrações 2D (`UI/`)

Pixel art 2D do legado, copiadas **byte a byte** (idênticas ao `master`), só renomeadas para ASCII. Autoria/licença **desconhecidas** (como todo o legado). Importadas como Sprite (PPU 100, filtro Point, sem compressão, sem mipmap, Alpha Is Transparency). No legado eram importadas como textura padrão e usadas em cubos finos/UI.

| Arquivo aqui | Origem (`master:`) | Uso atual |
|---|---|---|
| `placa_mae.png` (1280×1280) | `Assets/texturas/objetos/placa mae.png` | `Pickup_PlacaMae` (0,30×0,30 m) |
| `memoria_ram.png` (1160×260) | `Assets/texturas/objetos/memoria ram.png` | `Pickup_MemoriaRAM` (0,14×0,031 m) |
| `hd.png` (738×1120) | `Assets/texturas/objetos/hd.png` | `Pickup_HD` (0,10×0,152 m) |
| `hd_virado.png` (1088×233) | `Assets/texturas/hd virado.png` | importada, **sem uso ainda** (vista lateral/conectores; minigame) |
| `chave_de_fenda.png` (576×576) | `Assets/texturas/objetos/chave de fenda.png` | `Pickup_Ferramenta` (0,22×0,22 m) |

Cabos: `cabo de rede`, `energia`, `vga` e as tomadas **não são ilustrações**; são texturas de UV dos modelos 3D de cabo e já estão em `Textures/` (ver acima). Importada para a tela 2D do gabinete: `gabinete_aberto.png` (930×1000, byte-idêntica a `master:Assets/texturas/PC 3D/gabinete aberto.png`; origem e licença desconhecidas; usada em `Prefabs/UI/UI_Screen_Gabinete`). Também importada: `pc_back.png` (710×1580, byte-idêntica a `master:Assets/texturas/PC 3D/pc_back UI.png`; **mesma arte** da textura `pc_back GameObject.png` do modelo `pc.fbx`; origem e licença desconhecidas) — vista traseira do gabinete com os **8 parafusos reais** (centros medidos por análise de pixels; ver `ComputerCase.CreateDefaultTowerBackScrews`), usada em `Prefabs/UI/UI_Screen_Gabinete`. Também importadas para a tela de manutenção, byte-idênticas ao legado, com origem e licença desconhecidas:
- `pc_front.png` (710×1600), de `master:Assets/texturas/PC 3D/pc_front UI.png`: frente do gabinete (vista Frente);
- `monitor_frente.png` (678×577), de `master:Assets/texturas/PC 2D/monitor frente.png`: frente do monitor (vista Frente).

Usadas também no interior da tela (vista Interior): `placa_mae`, `memoria_ram` e `hd`.

**Criados para o projeto (sem terceiros), por script próprio:**
- `ui_monitor_traseira.png` (678×577): traseira simples do monitor, com entrada de energia e de vídeo (VGA).
- `ui_cursor_chave_de_fenda.png` (64×64): cursor da vista Traseira. Importado como **Texture Type = Cursor**, com hotspot (0, 62) na ponta. É **derivado** de `chave_de_fenda.png` (legado, licença desconhecida), por recorte e redução sem filtro; segue a licença dessa origem.

**Telas do monitor criadas para o projeto (sem terceiros):** `ui_tela_bios.png` e `ui_tela_sistema.png` (640×320, pixel art, mesmo formato da `tela_no_signal`), geradas por `Tools/screens/gerar_telas_monitor.py`. Texto rasterizado com DejaVu Sans Mono (licença livre Bitstream Vera/DejaVu; cópia que acompanha o Blender). Marcas fictícias ("E-STAGIAR.IO BIOS", "EstagiOS"). Usadas na tela 2D do gabinete e no monitor 3D (`M_Tela_Monitor`, URP/Unlit, criado para o projeto).

**`Textures/tomada_energia.png`** passou a ser importada como **Sprite** (mesmo arquivo e GUID; não era usada por nenhum material) para a tomada da Traseira da tela do gabinete.

**Sprites de feedback criados para o projeto (sem terceiros):** gerados por script próprio durante o desenvolvimento, livres de licença externa. `ui_parafuso_furo.png` (disco escuro com borda metálica; cobre o parafuso removido), `ui_parafuso_anel.png` (anel branco: destaque ao passar o mouse e clarão ao remover), `ui_branco.png` (quadrado branco 8×8; preenchimento da barra de progresso). Usados em `Prefabs/UI/ScrewView` e `UI_Screen_Gabinete`. Substituíveis pela arte oficial.

Materiais correspondentes (URP/Lit, Alpha Clipping 0,5): `M_Item2D_PlacaMae/MemoriaRAM/HD/ChaveDeFenda`.

## Referência de comportamento (nada copiado)

O legado simulava o braço com um cubo (`braco`, escala ~0.07×0.06×0.5, filho da câmera em ~(0.3,-0.12,0.18)) e o ponto de mão (`mao Fisica`) na ponta. `Arm_Placeholder`/`HoldPoint` na `Office_Placeholder` recriam essa ideia com um cubo primitivo e `M_Braco_Placeholder` (URP/Lit).

## Deliberadamente NÃO migrados

Scripts, prefabs com scripts quebrados (`Gabinete`, `gaveteiro*`), cenas legadas (`FUNEC Riacho`, `Tela Inicial`, `testes`), TextMesh Pro antigo, `ProjectSettings`, Input Manager, tags, build antigo, ~125 materiais `parede bege N`, texturas de cenário escolar e a textura 8K `Fence007A_8K_Displacement.jpg`.


### Manutenção do PC: telas e materiais (2026-10-12)

- `UI/ui_tela_sistema_rede.png`: variante da `ui_tela_sistema` com "Rede: conectado", gerada pelo mesmo `Tools/screens/gerar_telas_monitor.py` (que também atualizou a BIOS com a linha do cooler e "VGA/HDMI", e a tela do sistema com o aviso "sem conexão").
- `Materials/M_Item2D_Fonte` e `M_Item2D_Cooler`: cópias do `M_Item2D_HD` com os sprites `ui_fonte`/`ui_cooler` (itens `Pickup_Fonte`/`Pickup_Cooler`).
- `Materials/M_CaboHDMI`: cópia do material do cabo de vídeo, cinza-escuro, para o `Pickup_CaboHDMI`.
- Criados por `Assets/Editor/PcManutencaoSetup.cs` (menu *Tools > E-stagiar.io > Configurar manutenção do PC*).

## 2026-10-14 — efeitos sonoros (autoria própria)
- `Assets/Audio/SFX/sfx_*.wav` (11 arquivos: parafuso retirar/recolocar, gabinete abrir/fechar, pegar, colocar, soltar, cabo conectar/desconectar, gaveta abrir/fechar): **sintetizados** por `Tools/audio/gerar_sfx.py` (ruído filtrado e senoides com envelope, semente fixa). Sem amostras de terceiros; autoria da equipe, sem restrição de licença. Placeholders sonoros: podem ser trocados por áudios finais na tabela do `Sons_Interacao`.
- `Models/character2.fbx` (Matheus) passou a ser usado como supervisor provisório (`Prefabs/Gameplay/NPC_Supervisor`, material `M_Character_Tex1_1`), sem alteração no FBX.
- `Audio/SFX/sfx_supervisor_alerta.wav` (2026-10-14): alerta do supervisor, dois tons sintetizados por `Tools/audio/gerar_sfx.py` (autoria própria).

## 2026-10-14 — itens e painel dos minigames, rachaduras (autoria própria) e impressora do Matheus
- `Models/componentes.fbx`: entraram `Item_Ssd` (SSD M.2 2280 do notebook), `Item_AlicateCrimpar` (alicate de crimpar RJ45) e `Item_PainelImpressora` (painel com visor, botões e LED). São modelos simples de **autoria própria**, gerados por `Tools/blender/componentes_build.py` com os mesmos materiais `M_Comp_*`/`M_PC_*`. Os itens que já existiam foram regerados **idênticos** (mesma geometria e materiais, conferido vértice a vértice).
- `Textures/T_Rachadura.png` e `Materials/M_Rachadura.mat`: rachadura **gerada por código** (`Tools/textures/gerar_rachadura.py`, semente fixa; autoria própria). O material copia a configuração URP/Lit Alpha Clip do `PH_M_Item2D_HD` (só os parâmetros: não usa a textura legada). É a camada "quebrado" do monitor e do gabinete.
- `Models/impressora.fbx` (Matheus) passou a ser usado **sem alteração** no `Prefabs/Gameplay/Impressora_Estacao` (`Visual/Modelo`). O painel é um objeto à parte, preso à face da frente do modelo.
- Correção (2026-10-14): a impressora usa o **material original** dele. O "Material.001" do FBX aponta para a textura `officepapers.png` (dele), que o importador não achava; o FBX agora é remapeado para o `M_Objetos_Parede` (material dele com essa textura). O `M_Escritorio_Claro` saiu da impressora.
- `Models/componentes.fbx` (2026-10-14): entraram `Item_MemoriaSodimm` (SO-DIMM do notebook, a mesma construção da RAM do PC em 6,8 × 3,0 cm) e as versões quebradas `Item_MemoriaSodimm_Quebrada` e `Item_Ssd_Quebrado`, pelo mesmo `Tools/blender/componentes_build.py` (autoria própria). Os demais itens continuam idênticos.
- `UI/PH_ui_tela_notebook_*.png` (8 telas, 2026-10-14): as telas do monitor (BIOS, sem sistema, instalando 0..4, sistema) com a terminologia do notebook (SSD, SO-DIMM, tela integrada, ícone de Wi-Fi), geradas pelo mesmo `Tools/screens/gerar_telas_monitor.py` (perfil `notebook`; autoria própria, fonte DejaVu Sans Mono livre). As telas do PC continuam idênticas (conferido pixel a pixel).
