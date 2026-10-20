# E-stagiar.io

Simulação educativa de **suporte de TI em primeira pessoa**. Você é o estagiário de TI de um escritório: chegam
chamados no tablet (PC que não liga, monitor sem imagem, impressora parada, cabo de rede com defeito…), você vai até o
equipamento, descobre o problema, busca peças e ferramentas na sala de TI e conserta — tudo com o chefe fazendo ronda
e de olho no seu cuidado com o material.

Projeto desenvolvido para o campeonato **Framework Arcade**. Texto do jogo em português.

## Objetivo

Resolver o máximo de chamados dentro do expediente (15 minutos), no prazo e sem infrações. No fim da fase, o
relatório mostra pontos, chamados atendidos e de 0 a 3 estrelas.

Primeira vez? No menu inicial, **Tutorial** ensina a montar um PC do zero na sala de TI, etapa por etapa (opcional;
dá para pular a qualquer momento ou começar o expediente direto).

## Principais mecânicas

- **Chamados** recebidos no tablet, cada um ligado a um equipamento real da cena, com prazo de referência.
- **Manutenção do PC:** parafusos com a chave de fenda, troca de peças (placa-mãe, RAM, HD, fonte, cooler), cabos de
  energia, vídeo e rede, BIOS e instalação do sistema por pendrive.
- **Notebook:** tampa de baixo, memória SO-DIMM e SSD.
- **Impressora:** toner, papel e configuração do IP indicado no cartaz da sala de TI.
- **Rede:** crimpagem de cabo no padrão T568B, testador de cabo e rack.
- **Física e cuidado:** peças podem cair e quebrar; o supervisor percebe infrações e elas custam pontos.

Detalhes de cada mecânica: [`docs/MECANICAS.md`](docs/MECANICAS.md).

## Controles

| Tecla | Ação |
|---|---|
| W A S D | andar |
| Mouse | olhar / mirar |
| Clique esquerdo | pegar, usar, abrir e fechar; com item na mão, soltar ou guardar |
| Clique direito | abrir a tela de manutenção do equipamento |
| T | abrir e fechar o tablet de chamados |
| Alt esquerdo (segurar) | soltar o cursor para clicar no tablet |
| Esc | fechar a tela aberta; no jogo, pausar |
| F8 (duas vezes) | no tutorial, pular e voltar ao menu |

## Tecnologias

- **Unity 6000.5.10f1** (versão exata; outras versões podem alterar o projeto)
- Universal Render Pipeline (URP) 17.5, Input System 1.20, AI Navigation 2.0 (NavMesh), uGUI / TextMesh Pro
- Unity Toon Shader e RealToon (materiais)
- Git LFS para modelos, imagens e sons

## Como obter e abrir

1. Instale o [Git LFS](https://git-lfs.com) e rode `git lfs install` (uma vez).
2. Clone o repositório: `git clone https://github.com/ItaloOResende/E-stagiar.io.git`
   (se os arquivos grandes não vierem, rode `git lfs pull` dentro da pasta).
3. No **Unity Hub**, adicione a pasta do projeto e abra com o **Unity 6000.5.10f1**.
4. Abra a cena `Assets/Scenes/Office_Placeholder.unity` e aperte **Play**. O jogo começa pelo menu inicial (Iniciar
   jogo, Tutorial, Configurações e Sair).

## Estrutura

| Pasta | Conteúdo |
|---|---|
| `Assets/Scripts` | código do jogo (interação, jogador, equipamentos, chamados, NPCs, UI) |
| `Assets/Prefabs` | objetos de gameplay, cenário e telas 2D |
| `Assets/Art` | modelos, texturas, materiais, UI, fontes e sons ([organização](Assets/Art/README.md)) |
| `Assets/TicketsData` | fichas dos chamados |
| `Assets/Tests` | testes automáticos (Edit Mode e Play Mode) |
| `Tools` | scripts que geram parte da arte (Blender, Python) |

## Equipe

- Ana — [@AnaSouzaPinheiro](https://github.com/AnaSouzaPinheiro)
- Ítalo — [@ItaloOResende](https://github.com/ItaloOResende)
- Matheus — [@tujikonoriko420](https://github.com/tujikonoriko420) 
- Fernanda — [@Fefevsc](https://github.com/Fefevsc)

## Créditos e licenças

Autoria e licença de cada asset (modelos, texturas, fontes e sons), incluindo os itens cuja licença ainda precisa ser
confirmada: [`Assets/Art/ORIGEM.md`](Assets/Art/ORIGEM.md).

## Desenvolvimento

Regras do projeto (Git, Unity, arquitetura, assets, testes): [`CLAUDE.md`](CLAUDE.md).
