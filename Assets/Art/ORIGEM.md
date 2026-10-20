# Origem dos assets

Autoria e licença dos recursos de arte, som e fonte do projeto. O regulamento do campeonato (item 14.2) torna a
equipe responsável por todo asset entregue: **itens marcados como "a confirmar" ou "desconhecida" precisam ser
confirmados ou substituídos antes da entrega pública.**

## Matheus ([@tujikonoriko420](https://github.com/tujikonoriko420))

| Assets | Observação |
|---|---|
| Modelos do escritório: `cadeira`, `divisa`, `gabinete`, `gaveta`, `gavetas`, `mesa`, `mesa_2`, `mesa_grande`, `monitor`, `mouse`, `teclado`, `vaso`, `xicara`, `caixa`, `impressora` | Os `.fbx` do gabinete e do monitor receberam peças extras geradas pela equipe (portas, parafusos, LED) sobre a malha original. |
| Personagens: `character`, `character2`, `boss`, `npc`, `npc2`…`npc4`, `npc 5`…`npc 9` | `character_braco_fp.fbx` (braço em 1ª pessoa) é recorte do `character`, mesma autoria. |
| Texturas e materiais do escritório e dos personagens (`M_Escritorio_*`, `M_Character_*`, `M_Objetos_Parede`, `M_Plant`, `boss baby`, `officepapers.png`, `character_*.png`, `plant_texture.png`) | Origem das texturas **a confirmar** (pintadas por ele ou baixadas?). |
| Arte do tablet e da interface: `Textures/Sprites/tablet.png`, `cursor.png`, `chavedefenda.png`, `caixa.png`, `alicate1.png`, `alicate2.png` | **Origem a confirmar.** |
| Sons do menu e do jogo: `Sounds/click menu.mp3`, `notification.mp3`, `woosh.mp3`, `corte.mp3` e demais em `Art/Sounds/` | **Origem desconhecida — confirmar.** |
| Plaquinhas dos gaveteiros com fonte de pincel (`plaquinha_energia`, `plaquinha_rede`, `plaquinha_titulo_hardware`…) | Recebidas já como imagem; fonte usada **não identificada**. |

## Gerados pela equipe com o Claude Code

Modelos, imagens e sons criados por scripts do próprio projeto (`Tools/`, `Assets/Editor/`), sem material de terceiros:

- Peças e acessórios do PC: componentes internos (placa-mãe, RAM, HD, fonte, cooler, SSD, SO-DIMM, pendrive), versões quebradas, chave de fenda, alicate de crimpar, painel da impressora, cabos e conectores (`cabos.fbx`, `componentes.fbx`).
- Mobiliário e sala de TI: bancadas, armários, estante, puff, carrinho e interior do rack, quadros, calendário, placas; decoração das estações (`DEC_*`).
- Gaveteiros montados por código (`Gaveteiro_*`) e os de 3 gavetas que reaproveitam o `gavetas.fbx` do Matheus.
- Imagens de interface: renders do PC e do monitor, telas do monitor/notebook (BIOS, instalação, sistema, sem sinal), ícones de interação, sprites de parafusos e plugues, cursor da chave de fenda, rachadura, plaquinhas em pixel art e as plaquinhas das gavetas de 3 gavetas.
- Efeitos sonoros `Audio/SFX/sfx_*.wav` (sintetizados).
- Fotos dos itens nos aplicativos do tablet (`Resources/TutorialIcons/*.png`): renders dos próprios prefabs do jogo
  (`TabletFotosGerador`), herdando a autoria de cada modelo.
- Cena do tutorial `Tutorial_Montagem` (`TutorialMontagemSetup`): cópia reduzida da `Office_Placeholder`, sem asset novo.

## Terceiros

| Recurso | Uso | Licença |
|---|---|---|
| DejaVu Sans Mono (distribuída com o Blender) | texto das telas do monitor e das plaquinhas em pixel art | livre (Bitstream Vera/DejaVu) |
| Inter (distribuída com o Blender) | quadros da sala de TI | SIL Open Font License |
| Varela Round (`Fonts/VarelaRound-Regular.ttf`) | tablet, HUD e relatório final | Google Fonts, SIL OFL — **conferir** |
| Comic Sans Bold (`comicbd.ttf`, fonte do Windows) | texto das plaquinhas de suprimentos e das gavetas de 3 gavetas | fonte da Microsoft — **confirmar se o uso no jogo é permitido** |
| RealToon (`Assets/RealToon/`) | shader dos materiais dos personagens e de parte do escritório | pacote comercial — **confirmar a licença de uso** |
| Unity Toon Shader (`com.unity.toonshader`) | materiais Toon | pacote da Unity |

## Placeholders (`PH_*`) — temporários

Vieram do projeto legado (branch `master`, Ítalo — [@ItaloOResende](https://github.com/ItaloOResende)): modelos
(`bancada`, `gaveta`, `rack_internet`, `cabo_*`), texturas e ilustrações 2D em pixel art
(placa-mãe, RAM, HD, chave de fenda, telas e tomadas). **Autoria e licença desconhecidas.** Não devem ir para a
entrega sem confirmar a origem ou substituir pelo oficial. Imagens derivadas deles (ex.: `ui_hd_deitado`,
`ui_memoria_ram_vertical`) seguem a mesma situação.
