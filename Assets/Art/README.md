# Art — onde fica cada asset

Cada tipo de asset tem **uma única pasta**. Oficiais e placeholders ficam juntos; os placeholders têm o prefixo
**`PH_`**. Autoria e licença de cada asset: [`ORIGEM.md`](ORIGEM.md).

| O quê | Onde |
|---|---|
| Modelos 3D (`.fbx`) | `Assets/Art/Models/` (`npc/` = personagens) |
| Malhas geradas no Unity (`.asset`) | `Assets/Art/Meshes/` |
| Texturas | `Assets/Art/Textures/` (`Plaquinhas/` = etiquetas dos gaveteiros, `Quadros/` = quadros da sala de TI, `Sprites/` = arte 2D do tablet e dos minigames) |
| Materiais | `Assets/Art/Materials/` (subpastas agrupam famílias: `Cabos/`, `Componentes/`, `Mobiliario/`, `Placas/`, `Plaquinhas/`…) |
| UI dos minigames: sprites do Canvas, renders do PC, telas BIOS/sistema, cursores | `Assets/Art/UI/` |
| Fontes | `Assets/Art/Fonts/` |
| Sons | `Assets/Art/Sounds/` e `Assets/Audio/SFX/` |
| Prefabs de cenário (sem lógica) | `Assets/Prefabs/Props/` (`OF_*` oficiais, `PH_*` placeholders) |
| Prefabs de gameplay | `Assets/Prefabs/Gameplay/` |
| Prefabs de tela (Canvas) | `Assets/Prefabs/UI/` |
| Cenas | `Assets/Scenes/` (jogo: `Office_Placeholder`) |

**Shaders:** o projeto não tem shaders próprios. Os materiais usam `Universal Render Pipeline/Lit`/`Unlit` (pacote
URP), o **Unity Toon Shader** (pacote `com.unity.toonshader`) ou o **RealToon** (`Assets/RealToon/`, personagens e
parte do escritório). Não troque o shader de um material existente: muda o visual de todos os objetos que o usam.
Para mudar a cor de um objeto só, use `MaterialPropertyBlock` no script dele. Os assets do pipeline ficam em
`Assets/Settings/` e não devem ser alterados.

## Regras rápidas

- Asset novo vai para a pasta do seu tipo. Não crie uma pasta por objeto nem por minigame.
- Placeholder recebe o nome `PH_<nome>`. Quando chegar o oficial, ele entra sem prefixo e o `PH_` é removido.
- Mova e renomeie **dentro do Unity** (ou com o `.meta` junto): o GUID fica no `.meta` e é ele que mantém as
  referências.
- Os scripts de configuração em `Assets/Editor/*Setup.cs` usam caminhos fixos destas pastas e alguns procuram assets
  pelo nome (ex.: `PH_M_Item2D_HD`, `PH_M_CaboHDMI`, sprites `PH_*` da tela de manutenção). Ao mover ou renomear algo
  que eles usam, atualize o script.
- Placeholders **não podem** ir para a entrega sem confirmar a licença ou substituir (ver `ORIGEM.md`).

## Como trocar um placeholder pelo modelo final

Os prefabs seguem o mesmo padrão:
- **raiz lógica:** `BoxCollider`, scripts, `Rigidbody`, sem escala;
- **filho com o modelo:** `Visual` nos prefabs de gameplay e nos `OF_*`, o próprio FBX nos `PH_*`.

A cena guarda só a instância do prefab: quando o prefab muda, a cena se atualiza sozinha.

1. Importe o FBX final em `Art/Models/`, e os materiais e texturas nas suas pastas.
2. Abra o prefab do placeholder (por exemplo, `PH_Bancada`).
3. Arraste o FBX novo **como filho da raiz**, ajuste posição, rotação e escala desse filho e apague o filho antigo. Não
   mexa na raiz nem nos componentes (no `BoxCollider`, só no tamanho, se necessário).
4. Opcional: renomeie o prefab de `PH_Bancada` para `OF_Bancada` pelo Unity (o GUID continua o mesmo).
5. Quando o placeholder não tiver mais uso, apague o `PH_*.fbx` e o material dele — antes, confira as referências
   (*Find References In Scene* / *Select Dependencies*).

**Casos que pedem atenção:**
- **`PC_Escritorio` e `Estacao_*`:** são composições de prefabs aninhados (gabinete, monitor, periféricos, mesa,
  cadeira). Para trocar um modelo, mude o `Visual` do prefab de origem (`Gabinete`, `OF_*`), não a composição.
- **Gaveteiros:** os da sala de TI (`Gaveteiro3_*`) usam as malhas do `gavetas.fbx` (corpo e cada gaveta separados);
  os itens dentro de cada gaveta vêm do `DrawerContents`. Os `Gaveteiro_*` de 4 gavetas ficam no projeto só como
  referência.
- **Móveis gerados por script do Blender** (`Bancada_Manutencao`, `Armario_TI`, `Estante_Estoque`,
  `Bancada_Impressoras`, peças da sala de TI): para mudar a forma, edite o script em `Tools/blender/` e exporte por
  cima do `.fbx` (mesmo GUID).
- **Piso, paredes e teto da cena:** primitivos com materiais `PH_M_*`; para trocar, basta mudar o material no
  `MeshRenderer`.
