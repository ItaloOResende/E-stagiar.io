# Art — onde fica cada asset

Cada tipo de asset tem **uma única pasta**. Os oficiais (Matheus e os gerados pelo projeto) e os placeholders ficam juntos, e os placeholders são identificados pelo prefixo **`PH_`**. Origem e licença de cada arquivo: `ORIGEM.md`.

| O quê | Onde |
|---|---|
| Modelos 3D (`.fbx`) | `Assets/Art/Models/` |
| Malhas geradas no Unity (`.asset`) | `Assets/Art/Meshes/` |
| Texturas dos modelos | `Assets/Art/Textures/` (`Plaquinhas/` = etiquetas dos gaveteiros) |
| Materiais (URP/Lit) | `Assets/Art/Materials/` (`Cabos/`, `Componentes/`, `Plaquinhas/` agrupam famílias) |
| UI e minigames: sprites do Canvas, renders do PC, telas BIOS/sistema, cursores, ícones dos itens | `Assets/Art/UI/` |
| Prefabs de cenário (sem lógica) | `Assets/Prefabs/Props/` (`OF_*` oficiais, `PH_*` placeholders) |
| Prefabs de gameplay | `Assets/Prefabs/Gameplay/` |
| Prefabs de tela (Canvas) | `Assets/Prefabs/UI/` |
| Cenas | `Assets/Scenes/` (escritório real: `Office_Placeholder`; referência funcional: `Teste_Integrado`) |

**Shaders:** o projeto não tem shaders próprios. Todos os materiais usam `Universal Render Pipeline/Lit`, que vem do pacote URP instalado pelo Package Manager (`Packages/manifest.json`). Os assets do pipeline ficam em `Assets/Settings/`. Os FBX do Matheus vieram com o Unity Toon Shader, mas os materiais foram convertidos para URP/Lit, então não é preciso instalar nada.

## Regras rápidas

- Asset novo vai para a pasta do seu tipo. Não crie uma pasta por objeto nem por minigame.
- Um placeholder recebe o nome `PH_<nome>`. Quando chegar o oficial, ele entra sem prefixo e o `PH_` é removido.
- Sempre mova e renomeie **dentro do Unity** (ou com o `.meta` junto). O GUID fica no `.meta`, e é ele que mantém as referências.
- Os scripts de configuração em `Assets/Editor/*Setup.cs` usam caminhos fixos destas pastas. Se mover algo que eles usam, atualize a constante no topo do script.

## Como trocar um placeholder pelo modelo final

Os prefabs de cenário (`Prefabs/Props/PH_*`) e de gameplay (`Prefabs/Gameplay/*`) seguem o mesmo padrão:
- **raiz lógica:** `BoxCollider`, scripts, `Rigidbody`, sem escala;
- **filho com o modelo:** `Visual` nos prefabs de gameplay e nos `OF_*`, o próprio FBX nos `PH_*`.

A cena guarda só a instância do prefab. Por isso, quando o prefab é alterado, a cena se atualiza sozinha e nada precisa ser religado.

1. Importe o FBX final em `Art/Models/`, e os materiais e texturas nas suas pastas.
2. Abra o prefab do placeholder (por exemplo, `PH_Porta`).
3. Arraste o FBX novo **como filho da raiz**, ajuste a posição, a rotação e a escala desse filho, e apague o filho antigo. Não mexa na raiz, nos componentes nem no `BoxCollider` (só no tamanho, se necessário).
4. Opcional: renomeie o prefab de `PH_Porta` para `OF_Porta` pelo Unity. O GUID continua o mesmo, e a cena segue apontando para ele.
5. Quando o placeholder não tiver mais uso, apague o `PH_*.fbx` e o material dele. Antes, confira em *Find References In Scene*/*Select Dependencies*.

**Exceções (pedem atenção manual):**
- **`PC_Escritorio` e `Estacao_Trabalho` (`Prefabs/Gameplay/`):** são composições de prefabs aninhados (gabinete, monitor, periféricos, ponto elétrico; mesa, cadeira, PC, xícara). Para trocar um modelo, mude o `Visual` do prefab de origem (`Gabinete`, `OF_*`), não a composição. O `PC_Escritorio` é o antigo `PH_Pc` (mesmo GUID). Ver `../docs/GUIA_DE_MECANICAS.md` §4.7.
- **`Openable_Gaveta`:** tem o `PH_Gaveta` aninhado e o `OpenableInteractable` move a raiz (`movingPart` vazio). Por isso, trocar o modelo filho funciona como nos outros prefabs. Mas o `gaveta.fbx` oficial é uma malha única (sem gaveta separada), então os gaveteiros oficiais (`Gaveteiro_Hardware`/`Cabos`) foram montados à parte por `GaveteirosSetup.cs`.
- **Piso, paredes, teto e tela do monitor da cena:** são primitivos da cena com materiais `PH_M_*`. Para trocar, basta mudar o material no `MeshRenderer`.
- **Materiais `PH_M_Item2D_HD`, `PH_M_CaboHDMI` e sprites `PH_*` de UI:** scripts de Editor os procuram pelo nome (`PcManutencaoSetup.cs`, `MinigamesEstacoesSetup.cs`). Se forem renomeados, atualize o script. Os outros `PH_M_Item2D_*` saíram em 2026-10-17 (os itens usam modelos 3D).
- **Móveis da sala de TI e das impressoras (`Props/Bancada_Manutencao`, `Armario_TI`, `Estante_Estoque`, `Bancada_Impressoras`):** raiz com os colisores (e `WorkSurface` na bancada de manutenção) + filho `Visual` com o `.fbx` gerado por `Tools/blender/mobiliario_escritorio_build.py`. Para mudar a forma, edite o script do Blender e exporte de novo por cima do `.fbx` (mesmo GUID); as medidas dos colisores estão repetidas em `Assets/Editor/MobiliarioEscritorioSetup.cs`. `Armario_TI`, `Estante_Estoque` e as peças da sala de TI (`Bancada_Armarios`, `Quadro_Seguranca`, `Quadro_Rede`, `Calendario_TI`, `Aviso_Manutencao`, `Placa_SalaTI`) usam os materiais URP/Lit de `Materials/Mobiliario/` e `Materials/Quadros/`, não o Toon (ver `SalaTIAjustesSetup.cs`). As imagens dos quadros ficam em `Textures/Quadros/` (`Tools/textures/gerar_quadros_ti.py`).
- **Puff e carrinho do rack (`Props/Puff`, `Props/Carrinho_Rack`):** raiz com `BoxCollider` (não coletáveis) + `Visual` com o `.fbx` (`Tools/blender/puff_build.py`, `carrinho_rack_build.py`); o rack fica apoiado no tampo do carrinho (0,80 m). Prefabs e posições por `Assets/Editor/OfficeGameplaySetup.cs`.
- **`Props/Armario_Reserva`:** armário de TI funcional (portas `Porta_Esquerda`/`Porta_Direita` com `OpenableInteractable` em giro, ligadas; `DrawerContents` com reserva que se repõe ao fechar). `Gameplay/Gabinete_Reserva`: variante do `Gabinete` que começa funcional.
- **`Gameplay/Monitor_Estoque`:** variante do `OF_Monitor` com os mesmos componentes do monitor do `PC_Escritorio` (tela 3D, portas, quebrável, pegável) + `MonitorAvulso`; usado na estante da sala de TI.

## Placeholders ainda em uso (2026-10-13)

- **Cena `Office_Placeholder`:**
  - os prefabs `PH_Bancada`, `PH_Janela`, `PH_Porta` e `PH_RackInternet`;
  - os materiais `PH_M_Piso/Parede_Placeholder` e `PH_M_Braco_Placeholder`;
  - o material `PH_M_Tela_Monitor` (na `Tela` do monitor do `PC_Escritorio`).
- **`Openable_Gaveta`:** o `PH_Gaveta`.
- **Tela de manutenção (`UI_Screen_Gabinete`, `ScrewView`):**
  - os sprites `PH_ui_tela_bios/sistema/sistema_rede/sem_sinal`, `PH_ui_mao`, `PH_ui_branco`, `PH_ui_parafuso_anel/furo` e `PH_chave_de_fenda`.
- `PH_M_Metal_Cinza`: remapeado no `PH_rack_internet.fbx`.

**Removidos em 2026-10-13** (cópias com `.meta` em `Art/_Removidos_2026-10-13/`, fora de `Assets`): `PH_pc.fbx`, `PH_M_PC_Atlas/TowerBack/TowerFront`, `Textures/PH_pc_atlas/back/front`, `PH_M_Mesa_Placeholder` e `Pickup_CaixaTeste`.

**Removidos em 2026-10-17** (cópias com `.meta` em `Art/_Removidos_2026-10-17/`): cena `Prototype` (primeiro protótipo: piso, cubo, Player; substituída pela `Office_Placeholder`, sem referência em código, cenas ou build), materiais `PH_M_Item2D_ChaveDeFenda/Cooler/Fonte/MemoriaRAM/PlacaMae` (itens 2D antigos; os `Pickup_*` usam modelos 3D) e `UI/PH_ui_cursor_chave_de_fenda` (trocado pelo `ui_cursor_chave_de_fenda`). Todos com zero referências (GUID e nome) em cenas, prefabs, materiais, scripts e `Tools/`.

**Sem uso, mas mantidos de propósito:**
- `PH_hd_virado`: previsto para um minigame.
- `PH_pc_back`: o `.meta` serve de modelo de importação para os scripts de Editor.
- `PH_hd`, `PH_memoria_ram` e `PH_placa_mae`: fonte do `gerar_sprites_pc.py`.
- Texturas legadas `PH_tela_no_signal`, `PH_tomada_*`, `PH_vga`, `PH_cabo_rede`, `PH_energia` e `PH_mesa_*`: possíveis telas e ícones de minigame.
- Sprites pixel art `ui_plugue_*`, `ui_tomada_energia`, `ui_ponto_rede`, `ui_hd_deitado` e `ui_memoria_ram_vertical`: o `PcManutencaoSetup.cs` usa.
- Modelos do Matheus ainda não integrados: `mesa_3`, `tela_projetor`, `bebedouro`, `cafe`, `objetos`, `gaveta_separada` e `gaveta` (o `divisa`, o `mesa_2` e o `mesa_grande` já estão na `Office_Placeholder`).
- Materiais e texturas do Matheus sem uso atual: `M_Of`, `M_Ceiling`, `*_PENDENTE`, `Decoração`, `Decoração 2`, `xadrez 7`, `New Material`, `M_Character_Tex1_1 1`, `ceiling.png`, `checkered.jpg`, `of.png`; sons `alerta`, `chefe`, `chefe detecta player`, `encaixe cabo`, `fim da fase`, `sucesso` (ainda não ligados). Ficam até o Matheus decidir.
- `PH_Gaveta` + `PH_gaveta.fbx` + `PH_M_Gaveta_*`: o único uso era o `Openable_Gaveta`, apagado na cópia de trabalho do Matheus (ainda não commitado). As cenas `Escritorio_Teste`, `Fase_01` e `Teste_Integrado` ainda têm um objeto `Gaveta` apontando para esse prefab (aparece como *Missing Prefab*). Revisar junto com ele.
- Malhas geradas `Meshes/Notebook_*`/`Testador_*` sem uso: saem do `EquipamentosToonGerador.cs` (nomes montados no código) e voltam se ele rodar de novo.
- `M_Pickup_RamSodimm`/`M_Pickup_Ssd`: o `NotebookImpressoraSetup.cs` os recria pelo nome.
- `Prefabs/Gameplay/Estacao_Esquerda_Problema`: faz parte da família de estações (esquerda/direita × funcional/problema).
