# Mecânicas — E-stagiar.io

Como o jogo funciona, mecânica por mecânica, e o essencial para configurar objetos novos no Editor. Se algo aqui
divergir do código, o código vence — corrija este arquivo.

Cena principal: `Assets/Scenes/Office_Placeholder.unity`.

## Estrutura de um objeto de gameplay

Todo objeto interativo tem uma **raiz lógica** (colliders, `Rigidbody`, scripts) e um filho **`Visual`** com o
modelo 3D. Trocar o visual não afeta a lógica. Prefabs de gameplay ficam em `Assets/Prefabs/Gameplay`, de cenário em
`Assets/Prefabs/Props` e telas 2D em `Assets/Prefabs/UI`.

## Interação

- O jogador mira com o centro da tela (alcance de 2 m). O HUD mostra, ao lado da mira, um ícone para a ação do
  **clique esquerdo** e outro para a do **clique direito**.
- **Esquerdo:** pegar, usar, abrir/fechar. **Direito:** abrir a tela de manutenção do equipamento.
- Paredes e divisórias bloqueiam a mira.
- Componentes: `InteractionDetector` (no `Player`) encontra o alvo subindo pelos pais do collider atingido; qualquer
  objeto com um `Interactable` (ou derivado) vira alvo. Partes de um equipamento (ex.: o painel da impressora) contam
  como o próprio equipamento.

## Pegar, carregar e soltar

- Clique esquerdo pega um item com `PickupInteractable` (um item por vez, na mão).
- Com item na mão, o clique esquerdo solta conforme a mira: **guarda** numa gaveta aberta compatível, **coloca** numa
  superfície ou deixa **cair**.
- Configuração: `PickupInteractable` + `Rigidbody` + collider na raiz. Campos úteis: *Hold Position/Euler Offset*
  (pose na mão), *Hold Scale* (tamanho só na mão), *Use Grip Point* / *Grip Local Point* (segurar pela empunhadura,
  usado em ferramentas).

## Gavetas, armários e estoque

- `OpenableInteractable` abre e fecha deslizando (gavetas) ou girando (portas). *Local Axis* e *Open Amount* definem
  a direção e o curso.
- `DrawerContents` (na mesma gaveta) cria o estoque inicial: lista de itens (prefab, quantidade, posição). Os itens
  ficam ocultos com a gaveta fechada e saem do estoque ao serem pegos; um item pode ser guardado de volta pela área
  `AreaGuardar`.
- Sala de TI: gaveteiros de 3 gavetas `Gaveteiro3_Hardware` (ferramentas; placa-mãe, RAM e cooler; HD e fonte),
  `Gaveteiro3_Cabos` (energia; VGA e HDMI; rede) e `Gaveteiro3_Suprimentos` (memória, SSD e pendrive; toner; papel).
  A gaveta de cabos de rede fica trancada durante o chamado de crimpar cabo (`DrawerTicketLock`).
- Armários de reserva repõem monitores e gabinetes ao fechar. `ReposicaoEstoque` repõe itens esgotados.

## Manutenção do PC

Clique direito no gabinete (ou no monitor) abre a tela de manutenção, com três vistas: **Frente**, **Traseira** e
**Interior**.

- **Parafusos:** com a **chave de fenda** na mão, o cursor vira a chave; clicar retira o parafuso. Só os
  obrigatórios abrem a tampa. Sem a ferramenta aparece o aviso "Falta ferramenta". Recolocar os parafusos fecha o
  gabinete.
- **Onde abrir:** o gabinete só abre apoiado numa mesa ou bancada (`WorkSurface`). Gabinete aberto pode ser carregado,
  mas entregar um chamado com o equipamento aberto é recusado e penalizado.
- **Peças** (placa-mãe, RAM, HD, fonte, cooler): instaladas ou retiradas pelos encaixes do Interior, com a peça na mão.
  Nenhuma peça some: o que sai vai para a mão.
- **Cabos:** energia do PC e do monitor, vídeo (VGA ou HDMI) e rede, ligados pela Traseira com o cabo na mão. O
  painel de tomadas e o monitor só aparecem com o gabinete perto deles.
- **Ligar:** o botão Power vai para a BIOS com energia, placa-mãe, RAM, cooler e fonte; vai para o sistema com um HD
  que tenha sistema operacional. Sem vídeo o monitor mostra "Sem sinal".
- **Sistema operacional:** HD sem sistema + **pendrive** numa USB traseira → ao ligar, a instalação acontece (só avança
  com o jogador olhando o monitor).
- Componentes: `ComputerCase` (parafusos, peças), `ComputerPower` (cabos, energia, telas), `MonitorDisplay`,
  `ToolItem`, `CableItem`, `ComputerPartItem`, `PendriveItem`. Prefabs prontos: `PC_Escritorio` e as estações
  `Estacao_*`.

## Notebook

Clique direito no notebook abre a tela com as vistas de baixo, interior, lateral e frente (tela em cima, teclado
embaixo).

- Tampa de baixo presa por parafusos (chave de fenda, como no PC). Aberta, dá acesso à memória **SO-DIMM** e ao **SSD**.
- Pendrive na USB lateral, botão de energia no teclado, telas de BIOS/instalação/sistema iguais às do PC.

## Impressora

Clique direito no corpo ou no painel abre a tela da impressora.

- **Toner** e **papel**: instalados com o item na mão.
- **IP:** o painel aceita só o IP indicado no **cartaz da sala de TI** (`Cartaz_IP_Impressora`). A digitação usa
  máscara IPv4 (os pontos entram sozinhos; `192168061` vira `192.168.0.61`). Cada chamado que apaga o IP pede um IP
  novo, diferente do anterior; uma impressora já configurada continua funcionando.
- **Teste de impressão:** só sai com toner, papel e IP corretos.

## Rede

- **Crimpagem** (caixa de cabo de rede, clique direito): com o **alicate de crimpar** na mão, cortar o cabo, tirar a
  capa, ordenar os 8 fios no padrão **T568B** e crimpar o RJ45. Sem o alicate aparece "Falta ferramenta".
- **Testador de cabo:** encaixar as pontas e testar; os LEDs mostram os pares ligados.
- **Rack:** ligar as portas do patch panel ao switch; só a ligação certa dá rede ao PC da estação.

## Quedas e itens quebrados

Peças e equipamentos podem quebrar ao cair com força (placa-mãe e RAM na primeira queda; HD, fonte, cooler e gabinete
na segunda; monitor na primeira). Item quebrado não é instalado e não liga.

## Chamados

- Os chamados chegam durante a fase (fichas em `Assets/TicketsData`) e aparecem no **tablet** (tecla **T**), com uma
  notificação no canto da tela.
- Ao **iniciar o atendimento**, um equipamento livre do tipo certo é reservado e recebe o defeito do chamado; uma seta
  indica a estação. A estação do próprio solicitante tem prioridade se ainda não atendeu nenhum chamado na fase;
  senão vai a que atendeu menos.
- As etapas se completam pelo estado real do equipamento. O jogador **finaliza** pelo tablet; entrega incompleta ou
  com o equipamento aberto é recusada (com penalidade).
- Cada chamado tem um **prazo** de referência (SLA): passar dele custa pontos, mas não bloqueia nada.
- As mesas têm plaquinha com o nome do funcionário. Mesas sem funcionário não recebem chamados.

## Supervisor (chefe) e infrações

- O supervisor faz ronda pelo escritório (NavMesh) e às vezes olha para o jogador.
- **Infrações:** peça que caiu, peça quebrada e item largado no chão. Só custam pontos quando o supervisor **vê** (visão
  com distância, cone e linha de visão; quedas também pelo som). Ao perceber, ele reage e o HUD mostra o aviso.
- Outros funcionários (`NpcCaminhada`) circulam entre os pontos de `Navegacao_NPCs/Pontos_Circulacao`; os sentados
  ficam nas estações (`NpcSentado`). Ninguém atravessa o jogador nem as paredes.

## Fase, pontuação e relatório

- A fase dura **15 minutos** (cronômetro e faixa de tempo no topo; vermelho no último minuto). Começa pelo menu
  inicial (botões Iniciar jogo, Tutorial, Controles, Configurações e Sair) ou pelo fim do tutorial.
- **Pontos:** cada chamado concluído soma o valor dele; atraso, entrega recusada e infração vista descontam.
- **Estrelas:** percentual da pontuação final sobre a pontuação possível — 1 estrela a partir de 20%, 2 a partir de
  70%, 3 a partir de 90%.
- **Relatório final:** estrelas, pontos, tempo, chamados no prazo/atrasados/não concluídos, extrato completo e o
  motivo das estrelas; botões "Jogar novamente" e "Ir para o menu inicial". Há duas apresentações (`StageReportScreen`,
  campo *Estilo*: Tablet ou Clássico); com o relatório aberto, **F9** alterna entre elas.

## Tutorial

- Menu inicial → **TUTORIAL** carrega a cena `Tutorial_Montagem` (opcional: "Iniciar jogo" vai direto ao expediente).
- A cena é só a **sala de TI**, isolada do escritório (sem o cenário de fora e com a porta fechada por parede). O PC é
  montado na **bancada de manutenção** (onde ficam a chave de fenda e o alicate) e depois levado para a **estação de
  trabalho** (prefab `Estacao_Trabalho`, encostada no fundo da sala, sem cadeira), que começa com a **mesa vazia**. O
  gabinete da estação (sem nenhuma peça e sem cabos) e o monitor ficam no móvel do estoque (`Bancada_Armarios (1)`), no
  lugar das reservas, que não existem nesta cena. Teclados e mouses ficam nas duas estantes do estoque (dois teclados e
  dois mouses em cada, no lugar da caixa que ficava ao lado dos teclados); qualquer um serve. Os cabos, as peças e o
  pendrive saem dos gaveteiros.
- Um único chamado, *Monte um PC completo* (`tutorial-montagem`, fase 0: sem cronômetro, pontuação nem
  NPCs), ligado ao gabinete da estação. As etapas aparecem num card no alto à esquerda e **avançam sozinhas** quando a
  ação é reconhecida pelo estado real do jogo: iniciar o atendimento no tablet → pegar o gabinete → levá-lo à bancada
  de manutenção → pegar a chave de fenda → abrir o gabinete (retirar os 3 parafusos obrigatórios da traseira; a tampa
  abre sozinha) → placa-mãe → RAM → cooler → fonte → HD → fechar o gabinete → levá-lo à estação de trabalho → trazer
  monitor, teclado e mouse → energia do PC e do monitor → vídeo → rede (cabo do gabinete ao ponto de rede da parede; vale
  quando o PC tem rede) → pendrive → ligar o PC e instalar o sistema → finalizar o atendimento no tablet. O chamado só é
  entregue com o gabinete e o monitor na mesa da estação.
- Um marcador (nome + seta) aponta o objetivo da etapa: o botão do tablet nas etapas do tablet; o item a buscar e, com
  ele na mão, o lugar onde levá-lo. Objeto na mão não é marcado; alvo fora da tela fica preso à borda, com a seta virada
  para ele. **TAB** (ou o botão "-" no canto do card) minimiza o card, deixando só a etapa atual.
- **F8** duas vezes pula o tutorial (volta ao menu inicial). No fim: **Começar o expediente** (carrega a fase e começa
  sem passar pelo menu) ou **Voltar ao menu**; nessa tela o jogador fica parado e o cursor, livre. A pausa também volta ao menu.
- A cena é gerada por *Tools > E-stagiar.io > Tutorial > Montar cena Tutorial_Montagem* (`TutorialMontagemSetup`), a
  partir de uma cópia da `Office_Placeholder`; refazê-la apaga ajustes manuais na cena do tutorial (faça-os no setup).
  Componente: `TutorialMontagem` (no objeto `Chamados` da cena).

## Interface

- **Tablet** (tecla **T** ou botão no canto inferior direito): chamados pendentes, em atendimento e histórico.
- **Alt esquerdo** (segurando): solta o cursor para clicar no botão do tablet sem abrir uma tela.
- **Esc:** fecha a tela 2D ou o tablet abertos; no jogo, abre a **pausa** (continuar, controles, configurações de
  volume e sensibilidade, sair).
- **Controles:** lista de teclas, aberta pela pausa ou pelo menu inicial (a mesma página do `PauseMenu`; no menu
  inicial, sem pausar nem iniciar a partida, e Voltar/Esc voltam ao menu).
- HUD: pontuação (alto à esquerda), cronômetro (alto à direita), ícones de ação ao lado da mira, avisos dos minigames
  no rodapé.

## Configurar no Editor (resumo)

- **Novo item pegável:** prefab com raiz (`PickupInteractable`, `Rigidbody`, `BoxCollider`) + filho `Visual`.
  Ferramenta: acrescentar `ToolItem` (tipo); cabo: `CableItem`; peça: `ComputerPartItem`.
- **Nova estação:** usar um prefab `Estacao_*`. O `StationEmployee` na raiz define o funcionário (nome, setor,
  identificação); a plaquinha `Placa_Mesa` mostra o nome e a cor própria da mesa.
- **Nova superfície de manutenção:** `WorkSurface` no objeto da mesa/bancada.
- **Rota de NPC:** filhos de `Pontos_Circulacao` (caminhantes) ou `Ronda_Supervisor` (chefe), sobre o NavMesh.
- **Novo chamado:** criar uma ficha (`TicketDefinition`) em `Assets/TicketsData` com o *Ticket Id* tratado pela ponte
  do equipamento (`PcTicketBridge`, `NotebookTicketBridge`, `ImpressoraTicketBridge`, `RedeTicketBridge`).
