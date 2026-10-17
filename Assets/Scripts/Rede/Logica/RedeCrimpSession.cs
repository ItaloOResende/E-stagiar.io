using System;
using System.Collections.Generic;

namespace EstagiarIo.Rede
{
    /// <summary>Etapas da crimpagem de um cabo de rede.</summary>
    public enum RedeCrimpEtapa
    {
        Cortar = 0,
        Descascar = 1,
        Ordenar = 2,
        EncaixarRj45 = 3,
        Crimpar = 4,
        Concluido = 5,
    }

    /// <summary>Resultado de uma tentativa de crimpar.</summary>
    public enum RedeCrimpResultado
    {
        /// <summary>Crimpou certo: o cabo está pronto.</summary>
        Ok = 0,
        /// <summary>A ordem estava errada: o cabo foi descartado e é preciso descascar de novo.</summary>
        OrdemErrada = 1,
        /// <summary>Não estava na etapa de crimpar.</summary>
        EtapaInvalida = 2,
    }

    /// <summary>
    /// Estado de UMA crimpagem: cortar o cabo, tirar a capa azul, ordenar os 8 fios no padrão T568B,
    /// encaixar o RJ45 e crimpar. A SEQUÊNCIA é obrigatória: cada etapa só vale na etapa anterior concluída (cortar →
    /// descascar → ordenar → RJ45 → crimpar); mexer nos fios só durante a ordenação; um cabo só existe (é registrado
    /// pelo <c>RedeGame</c>) com a sessão em <see cref="RedeCrimpEtapa.Concluido"/>.
    /// <para>
    /// A ordenação funciona como o cabo de energia: os 8 fios saem SOLTOS da capa azul (<see cref="Soltos"/>),
    /// o jogador PEGA um fio (<see cref="PegarSolto"/>/<see cref="PegarDaPosicao"/> — ele fica em <see cref="NaMao"/>)
    /// e o COLOCA numa posição da fileira (<see cref="ColocarNaPosicao"/>). O jogo só percebe a ordem no fim,
    /// ao crimpar (ou em <see cref="OrdemCorreta"/>).
    /// </para>
    /// É lógica pura (sem MonoBehaviour) para ser testável e reaproveitável.
    /// </summary>
    public class RedeCrimpSession
    {
        /// <summary>Quantos "puxões" de capa são precisos para descascar (1 = um clique só).</summary>
        public const int ToquesParaDescascar = 1;

        public RedeCrimpEtapa Etapa { get; private set; }
        public int DescascarToques { get; private set; }

        /// <summary>Quantos fios estavam fora de lugar na última tentativa de crimpar (para a mensagem de erro).</summary>
        public int FiosErradosNaUltimaCrimpagem { get; private set; }

        /// <summary>Posições (0..7) com fio errado na última crimpagem que falhou (vazio = a última deu certo).</summary>
        public List<int> PosicoesErradasNaUltimaCrimpagem { get; private set; } = new List<int>();

        /// <summary>Fio em cada posição (0..7) da fileira. null = posição vazia.</summary>
        public readonly RedeFio?[] Fila = new RedeFio?[RedeCores.OrdemT568B.Length];

        /// <summary>Fios que ainda estão soltos, saindo do cabo (na ordem em que aparecem na tela).</summary>
        public readonly List<RedeFio> Soltos = new List<RedeFio>();

        /// <summary>Fio que o jogador está segurando agora (null = mão vazia). Só se segura UM por vez.</summary>
        public RedeFio? NaMao { get; private set; }

        public RedeCrimpSession() => Reset();

        public void Reset()
        {
            Etapa = RedeCrimpEtapa.Cortar;
            DescascarToques = 0;
            FiosErradosNaUltimaCrimpagem = 0;
            Limpar();
        }

        /// <summary>
        /// Descarta o cabo e volta a descascar (usado quando a ordem estava errada ao crimpar):
        /// a fileira é limpa e é preciso tirar a capa de novo.
        /// </summary>
        public void VoltarParaDescascar()
        {
            Etapa = RedeCrimpEtapa.Descascar;
            DescascarToques = 0;
            Limpar();
        }

        private void Limpar()
        {
            for (int i = 0; i < Fila.Length; i++) Fila[i] = null;
            Soltos.Clear();
            NaMao = null;
        }

        public int NumFios => Fila.Length;

        /// <summary>Todas as 8 posições já têm fio?</summary>
        public bool TodasPreenchidas
        {
            get
            {
                for (int i = 0; i < Fila.Length; i++)
                    if (Fila[i] == null) return false;
                return true;
            }
        }

        // ------------------------------------------------------------------ etapas
        public bool Cortar()
        {
            if (Etapa != RedeCrimpEtapa.Cortar) return false;
            Etapa = RedeCrimpEtapa.Descascar;
            return true;
        }

        /// <summary>Um toque de "puxar a capa". Ao completar <see cref="ToquesParaDescascar"/>, passa para a ordenação.</summary>
        public bool Descascar()
        {
            if (Etapa != RedeCrimpEtapa.Descascar) return false;
            if (DescascarToques < ToquesParaDescascar) DescascarToques++;
            if (DescascarToques >= ToquesParaDescascar) Etapa = RedeCrimpEtapa.Ordenar;
            return true;
        }

        /// <summary>
        /// Encaixa o RJ45 na ponta (ainda sem crimpar). A ordem dos fios só é conferida ao crimpar, mas é preciso
        /// ter os 8 fios na fileira antes.
        /// </summary>
        public bool EncaixarRj45()
        {
            if (Etapa != RedeCrimpEtapa.Ordenar) return false;
            if (!TodasPreenchidas) return false;
            Etapa = RedeCrimpEtapa.EncaixarRj45;
            return true;
        }

        /// <summary>
        /// Crimpa com o alicate. Se a ordem dos fios estiver errada, o cabo é DESCARTADO e volta-se a descascar
        /// (<see cref="RedeCrimpResultado.OrdemErrada"/>); se estiver certa, conclui (<see cref="RedeCrimpResultado.Ok"/>).
        /// </summary>
        public RedeCrimpResultado Crimpar()
        {
            if (Etapa != RedeCrimpEtapa.EncaixarRj45) return RedeCrimpResultado.EtapaInvalida;
            if (!TodasPreenchidas || NaMao != null) return RedeCrimpResultado.EtapaInvalida;   // RJ45 sem os 8 fios

            if (!OrdemCorreta())
            {
                PosicoesErradasNaUltimaCrimpagem = PosicoesErradas();
                FiosErradosNaUltimaCrimpagem = PosicoesErradasNaUltimaCrimpagem.Count;
                VoltarParaDescascar();
                return RedeCrimpResultado.OrdemErrada;
            }

            FiosErradosNaUltimaCrimpagem = 0;
            PosicoesErradasNaUltimaCrimpagem = new List<int>();
            Etapa = RedeCrimpEtapa.Concluido;
            return RedeCrimpResultado.Ok;
        }

        // ------------------------------------------------------------------ ordenação

        /// <summary>
        /// Deixa os 8 fios SOLTOS (saindo do cabo) numa ordem qualquer, com as posições vazias:
        /// é assim que o jogador começa a montar a fileira T568B.
        /// </summary>
        public void Embaralhar(Random rng)
        {
            if (Etapa == RedeCrimpEtapa.EncaixarRj45 || Etapa == RedeCrimpEtapa.Concluido) return;

            for (int i = 0; i < Fila.Length; i++) Fila[i] = null;
            NaMao = null;

            Soltos.Clear();
            Soltos.AddRange(RedeCores.Todos());
            for (int i = Soltos.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (Soltos[i], Soltos[j]) = (Soltos[j], Soltos[i]);
            }
        }

        /// <summary>Troca dois fios de posição (útil para embaralhar uma fileira já montada).</summary>
        public bool Trocar(int a, int b)
        {
            if (Etapa != RedeCrimpEtapa.Ordenar) return false;
            if (a == b) return false;
            if (a < 0 || b < 0 || a >= Fila.Length || b >= Fila.Length) return false;
            (Fila[a], Fila[b]) = (Fila[b], Fila[a]);
            return true;
        }

        /// <summary>Pega um fio que estava solto (índice em <see cref="Soltos"/>). Só dá para pegar se a mão estiver vazia.</summary>
        public bool PegarSolto(int indice)
        {
            if (Etapa != RedeCrimpEtapa.Ordenar) return false;   // só com a capa tirada e antes do RJ45
            if (NaMao != null) return false;
            if (indice < 0 || indice >= Soltos.Count) return false;
            NaMao = Soltos[indice];
            Soltos.RemoveAt(indice);
            return true;
        }

        /// <summary>Tira um fio de uma posição da fileira (o fio vai para a mão). Só se a mão estiver vazia.</summary>
        public bool PegarDaPosicao(int posicao)
        {
            if (Etapa != RedeCrimpEtapa.Ordenar) return false;   // com o RJ45 encaixado a fileira não mexe mais
            if (NaMao != null) return false;
            if (posicao < 0 || posicao >= Fila.Length) return false;
            if (Fila[posicao] == null) return false;
            NaMao = Fila[posicao];
            Fila[posicao] = null;
            return true;
        }

        /// <summary>
        /// Coloca o fio da mão numa posição da fileira. Se a posição já tiver fio, os dois TROCAM de lugar
        /// (o que estava lá vai para a mão).
        /// </summary>
        public bool ColocarNaPosicao(int posicao)
        {
            if (Etapa != RedeCrimpEtapa.Ordenar) return false;
            if (NaMao == null) return false;
            if (posicao < 0 || posicao >= Fila.Length) return false;

            RedeFio? tinha = Fila[posicao];
            Fila[posicao] = NaMao;
            NaMao = tinha;
            return true;
        }

        /// <summary>Devolve o fio da mão para fora do cabo (volta a ficar solto).</summary>
        public bool DevolverAoSoltos()
        {
            if (NaMao == null) return false;
            Soltos.Add(NaMao.Value);
            NaMao = null;
            return true;
        }

        public bool OrdemCorreta()
        {
            var ordem = new RedeFio[Fila.Length];
            for (int i = 0; i < Fila.Length; i++)
            {
                if (Fila[i] == null) return false;
                ordem[i] = Fila[i].Value;
            }
            return RedeCores.OrdemCorreta(ordem);
        }

        /// <summary>Índices das posições que ainda estão vazias ou com o fio errado (vazio = tudo certo).</summary>
        public List<int> PosicoesErradas()
        {
            var erradas = new List<int>();
            for (int i = 0; i < Fila.Length; i++)
            {
                if (Fila[i] == null || Fila[i].Value != RedeCores.OrdemT568B[i]) erradas.Add(i);
            }
            return erradas;
        }
    }
}
