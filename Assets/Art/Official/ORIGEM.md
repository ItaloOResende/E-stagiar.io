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

### Imagens do Canvas de manutenção (`Sprites/ui_render_*`, 2026-10-12)

- `ui_render_pc_frente`, `ui_render_pc_traseira`, `ui_render_pc_aberto`, `ui_render_monitor_frente` e `ui_render_monitor_traseira` são renders dos próprios `gabinete.fbx`/`monitor.fbx` atualizados, com câmeras ortográficas fixas (sem segunda versão desenhada à mão). Gerados por `Tools/blender/pc_canvas_renders.py` (Blender Workbench).
- O quadro de cada câmera é exatamente a caixa do equipamento, então as coordenadas normalizadas do Canvas (parafusos do `ComputerCase`, portas, botão Power, tela) batem com a imagem.
- O interior (`ui_render_pc_aberto`) é montado só na cena de render, dentro do contorno do gabinete: o modelo do Matheus é maciço. Os encaixes ficam nas posições dos `Encaixe_*` do Canvas.
- Substituem no `UI_Screen_Gabinete` os placeholders `pc_front`, `pc_back`, `monitor_frente`, `ui_monitor_traseira` e `gabinete_aberto` (que continuam em `Placeholders/`).

### Sprites em pixel art da manutenção (`Sprites/`, 2026-10-12)

- `ui_fonte`, `ui_cooler`, `ui_plugue_energia`, `ui_plugue_vga`, `ui_plugue_hdmi`, `ui_plugue_rj45`, `ui_plugue_tomada`, `ui_tomada_energia` e `ui_ponto_rede`: desenhados por código (`Tools/sprites/gerar_sprites_pc.py`, PIL), autoria própria do projeto, no estilo dos sprites existentes (grade pequena ampliada sem suavização; *Filter Mode* Point).
- `ui_hd_deitado` e `ui_memoria_ram_vertical`: os sprites `hd` e `memoria_ram` de `Placeholders/Sprites` recortados ao desenho e girados 90° (mesma autoria/licença deles).

### Gaveteiros (`Meshes/Gaveteiro_*`, `Textures/Plaquinhas/`, `Materials/Plaquinhas/`, 2026-10-12)

- `Gaveteiro_Puxador.asset`: a peça solta (puxador) do `gaveta.fbx` do **Matheus**, sem mudar a forma: só reorientada (frente → +Z) e escalada para 14 cm. Mesma autoria/licença do `gaveta.fbx`.
- `Gaveteiro_Corpo.asset`, `Gaveteiro_Gaveta.asset` e `Gaveteiro_Plaquinha.asset`: malhas simples (caixas) geradas por `Assets/Editor/GaveteirosSetup.cs`, autoria própria do projeto, com os materiais do Matheus (`M_Escritorio_Claro` no corpo e na frente, `M_Escritorio_Escuro` dentro da gaveta) e `M_PC_Chapa` no puxador.
- O `gaveta.fbx` inteiro (criado-mudo com uma gaveta e nicho) não foi usado como gaveteiro: é uma malha única, a gaveta não desliza separada nem tem caixa interna, e só cabe uma gaveta.
- `Textures/Plaquinhas/plaquinha_*.png`: geradas por `Tools/sprites/gerar_plaquinhas.py` (PIL, pixel art ampliada 4x, fonte DejaVu Sans Mono livre que acompanha o Blender). Materiais `M_Plaquinha_*` (URP/Lit).

### Cabos e conectores 3D (`Models/cabos.fbx`, `Materials/Cabos/`, `Sprites/ui_render_plugue_*`, 2026-10-12)

- `cabos.fbx`: plugues (tomada NBR 14136, IEC C13, VGA, HDMI, RJ45), rolos dos 4 tipos de cabo e o espelho de parede `PontoEletrico` (2 tomadas + ponto de rede). Autoria própria do projeto, gerados por `Tools/blender/cabos_build.py` (Blender 5.2), na mesma escala "cartoon" (x1,35) dos conectores do gabinete/monitor, para encaixar nas portas modeladas lá; reaproveitam os materiais `M_PC_*` (metal, latão, azul VGA, parafusos).
- Substituem os placeholders `PH_CaboEnergia`/`PH_CaboVideo`/`PH_CaboRede` no `Visual` dos `Pickup_Cabo*` (os placeholders continuam em `Placeholders/`).
- Materiais novos `M_Cabo_Preto/Cinza/Azul`, `M_Plug_Preto`, `M_Plug_Translucido` (URP/Lit transparente), `M_Tomada_Branco/Interior/Rebaixo` em `Materials/Cabos/`; `M_Gaveta_Forro` (forro claro das gavetas).
- `Sprites/ui_render_plugue_*`, `ui_render_tomada`, `ui_render_ponto_rede`: renders fixos desses modelos (Workbench) para o Canvas.

### Componentes 3D e gabinete aberto (`Models/componentes.fbx`, `Materials/Componentes/`, `Sprites/ui_render_peca_*`, 2026-10-12)

- `componentes.fbx`: placa-mãe (layout do sprite `placa_mae`), memória RAM, HD 3,5", fonte ATX e cooler. São modelos 3D simples de autoria própria do projeto, gerados por `Tools/blender/componentes_build.py`, que reaproveitam os materiais `M_PC_*`. Usados nos itens `Pickup_PlacaMae/MemoriaRAM/HD/Fonte/Cooler` no lugar das ilustrações 2D.
- `ui_render_pc_aberto` e `ui_render_peca_*`: render do gabinete **oficial do Matheus** aberto por boolean (lado da tampa), com estrutura interna e esses componentes (`Tools/blender/pc_interior_renders.py`), recortado em camadas por `pc_interior_camadas.py`.
- `ui_render_pc_frente` agora tem o botão Power apagado; `ui_render_pc_frente_led` é o LED aceso recortado. `ui_hover_anel` foi gerado por `Tools/sprites/gerar_sprites_pc.py`. `M_PC_LED_Off` é o LED apagado no 3D.

### Prefabs visuais (`Prefabs/`, criados na integração)

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
