# Origem dos assets

Autoria dos recursos de arte, som e fonte do jogo (regulamento, item 14.2). Tudo foi feito pela equipe ou gerado
por scripts do próprio projeto, exceto as fontes e shaders da seção **Terceiros**.

## Matheus ([@tujikonoriko420](https://github.com/tujikonoriko420))

- Modelos do escritório: `cadeira`, `divisa`, `gabinete`, `gaveta`, `gavetas`, `mesa`, `mesa_2`, `mesa_grande`,
  `monitor`, `mouse`, `teclado`, `vaso`, `xicara`, `caixa`, `impressora` (gabinete e monitor com portas, parafusos e
  LED acrescentados pela equipe).
- Personagens (`character`, `boss`, `npc*`; o braço em 1ª pessoa é recorte do `character`) e as texturas e materiais
  do escritório e dos personagens.
- Arte do tablet e da interface (`tablet.png`, `cursor.png`, `chavedefenda.png`, `caixa.png`, `alicate*.png`),
  plaquinhas dos gaveteiros e os sons de `Art/Sounds/`.

## Ana

- Modelo do rack de rede (`PH_rack_internet.fbx` / `PH_RackInternet`), feito no Blender para o projeto legado.

## Gerados pela equipe com o Claude Code

Criados por scripts do projeto (`Tools/`, `Assets/Editor/`), sem material de terceiros:

- Peças e acessórios: componentes do PC e do notebook, versões quebradas, ferramentas, cabos e conectores,
  painel da impressora.
- Mobiliário e sala de TI: bancadas, armários, estante, rack, quadros, placas e decoração das estações (`DEC_*`);
  gaveteiros montados por código.
- Imagens de interface: renders do PC e do monitor, telas do monitor e do notebook (`PH_ui_tela_*`, por
  `Tools/screens/gerar_telas_monitor.py`), ícones, sprites de parafusos e plugues (`PH_ui_parafuso_*`, `PH_ui_branco`),
  rachadura, fotos dos itens no tablet (renders dos prefabs do jogo).
- Materiais URP do projeto (inclusive `PH_M_Braco_Placeholder` e `PH_M_Tela_Monitor`).
- Efeitos sonoros `Audio/SFX/sfx_*.wav` (sintetizados).

## Projeto legado da equipe (branch `master`)

- `PH_chave_de_fenda.png` e `PH_ui_mao.png`: pixel art do protótipo original da equipe (a mão foi retocada pelo
  Matheus).

O prefixo `PH_` só indica que o asset veio da fase de protótipo; não indica origem externa.

## Terceiros

| Recurso | Uso | Licença |
|---|---|---|
| DejaVu Sans Mono | texto das telas do monitor e das plaquinhas em pixel art | livre (Bitstream Vera/DejaVu) |
| Inter | quadros da sala de TI | SIL Open Font License |
| Varela Round (`Fonts/VarelaRound-Regular.ttf`) | tablet, HUD e relatório final | SIL Open Font License |
| Comic Sans Bold (fonte do Windows) | texto renderizado nas plaquinhas de suprimentos e das gavetas | Microsoft |
| RealToon (`Assets/RealToon/`) | shader de personagens e de parte do escritório | Unity Asset Store |
| Unity Toon Shader (`com.unity.toonshader`) | materiais Toon | pacote da Unity |
