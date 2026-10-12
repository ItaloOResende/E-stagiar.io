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
