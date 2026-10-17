using System;
using System.Collections.Generic;

namespace EstagiarIo.Rede
{
    /// <summary>Uma porta do rack: do patch panel (P1..Pn) ou do switch (S1..Sn).</summary>
    public readonly struct PortaRack : IEquatable<PortaRack>
    {
        public readonly bool Patch;
        public readonly int Numero;

        public PortaRack(bool patch, int numero)
        {
            Patch = patch;
            Numero = numero;
        }

        public static PortaRack P(int numero) => new PortaRack(true, numero);
        public static PortaRack S(int numero) => new PortaRack(false, numero);

        /// <summary>Etiqueta da porta (P3, S5).</summary>
        public string Rotulo => (Patch ? "P" : "S") + Numero;

        public bool Equals(PortaRack o) => Patch == o.Patch && Numero == o.Numero;
        public override bool Equals(object obj) => obj is PortaRack o && Equals(o);
        public override int GetHashCode() => (Patch ? 1000 : 0) + Numero;
        public override string ToString() => Rotulo;
    }

    /// <summary>Um cabo ligado entre duas portas do rack (qualquer combinação).</summary>
    public class LigacaoRack
    {
        public PortaRack A { get; internal set; }
        public PortaRack B { get; internal set; }
        /// <summary>O cabo tem defeito (crimpagem errada ou estragado por um chamado): liga, mas não passa sinal.</summary>
        public bool Defeituoso { get; internal set; }
        /// <summary>O cabo foi feito na crimpagem (não é um cabo pronto da gaveta).</summary>
        public bool Crimpado { get; internal set; }
        /// <summary>Ligação do estado inicial (sem cabo físico: retirá-la descarta o cabo).</summary>
        public bool PreLigada { get; internal set; }
        /// <summary>Momento (relógio do jogo) em que foi ligada; -1 = estado inicial.</summary>
        public float LigadaEm { get; internal set; } = -1f;
        /// <summary>O objeto do cabo físico (no Unity, o PickupInteractable), null se não há.</summary>
        public object Cabo { get; internal set; }

        /// <summary>Ligação CERTA da topologia: patch panel N ↔ switch N.</summary>
        public bool Correta => A.Patch != B.Patch && A.Numero == B.Numero;
        /// <summary>Patch ↔ switch, mas com números diferentes (a estação N vai parar na porta M do switch).</summary>
        public bool NumeroTrocado => A.Patch != B.Patch && A.Numero != B.Numero;
        /// <summary>Switch ↔ switch: laço no switch (derruba a rede inteira).</summary>
        public bool Laco => !A.Patch && !B.Patch;
        /// <summary>Patch ↔ patch: liga dois pontos de parede entre si, sem passar pelo switch.</summary>
        public bool PatchComPatch => A.Patch && B.Patch;

        public bool Envolve(PortaRack p) => A.Equals(p) || B.Equals(p);
        public PortaRack Outra(PortaRack p) => A.Equals(p) ? B : A;
        public override string ToString() => A.Rotulo + "↔" + B.Rotulo;
    }

    /// <summary>
    /// Topologia REAL do rack: os cabos ligados entre portas do patch panel (P) e do switch (S), em qualquer combinação
    /// (P↔S de mesmo número, P↔S trocado, P↔P, S↔S). Nada é corrigido automaticamente: o jogo só LÊ o estado.
    /// Regras de rede (o que os chamados e os PCs consultam):
    ///  - o ponto de parede da porta N só tem rede com um cabo BOM ligando P N ↔ S N (<see cref="PortaFunciona"/>);
    ///  - P N ligado a S M (M ≠ N) não serve: a estação cai na porta errada do switch;
    ///  - P ↔ P liga duas paredes entre si, sem switch: nenhuma das duas tem rede por ali;
    ///  - S ↔ S é um laço no switch: a rede INTEIRA cai enquanto ele existir (<see cref="LacoNoSwitch"/>).
    /// Cada porta aceita um cabo só. Lógica pura (sem Unity), para testes.
    /// </summary>
    public class RedeRackTopologia
    {
        private readonly List<LigacaoRack> ligacoes = new List<LigacaoRack>();

        public int NumPortas { get; }
        public IReadOnlyList<LigacaoRack> Ligacoes => ligacoes;

        public RedeRackTopologia(int numPortas)
        {
            NumPortas = numPortas < 1 ? 1 : numPortas;
        }

        public bool Valida(PortaRack p) => p.Numero >= 1 && p.Numero <= NumPortas;

        public LigacaoRack LigacaoEm(PortaRack p)
        {
            foreach (LigacaoRack l in ligacoes) if (l.Envolve(p)) return l;
            return null;
        }

        public bool Ocupada(PortaRack p) => LigacaoEm(p) != null;

        /// <summary>
        /// Liga um cabo entre <paramref name="a"/> e <paramref name="b"/> (qualquer combinação de lados e números).
        /// Recusa (null) só o que é fisicamente impossível: porta inexistente, a mesma porta nas duas pontas ou porta
        /// já ocupada.
        /// </summary>
        public LigacaoRack Ligar(PortaRack a, PortaRack b, bool crimpado, bool defeituoso, object cabo, float agora, bool preLigada = false)
        {
            if (!Valida(a) || !Valida(b) || a.Equals(b) || Ocupada(a) || Ocupada(b)) return null;
            // P sempre na ponta A quando houver um (só para leitura; a ligação não tem direção)
            if (!a.Patch && b.Patch) (a, b) = (b, a);
            var l = new LigacaoRack
            {
                A = a, B = b, Crimpado = crimpado, Defeituoso = defeituoso, Cabo = cabo, PreLigada = preLigada,
                LigadaEm = preLigada ? -1f : agora
            };
            ligacoes.Add(l);
            return l;
        }

        /// <summary>Tira o cabo que está na porta (as duas pontas saem). Devolve a ligação retirada (null = porta livre).</summary>
        public LigacaoRack Desligar(PortaRack p)
        {
            LigacaoRack l = LigacaoEm(p);
            if (l != null) ligacoes.Remove(l);
            return l;
        }

        public void Limpar() => ligacoes.Clear();

        /// <summary>Há um cabo ligando duas portas do switch (laço): a rede inteira para.</summary>
        public bool LacoNoSwitch
        {
            get
            {
                foreach (LigacaoRack l in ligacoes) if (l.Laco) return true;
                return false;
            }
        }

        /// <summary>O ponto de parede da porta N tem rede: P N ↔ S N com cabo bom e nenhum laço no switch.</summary>
        public bool PortaFunciona(int numero)
        {
            if (LacoNoSwitch) return false;
            LigacaoRack l = LigacaoEm(PortaRack.P(numero));
            return l != null && l.Correta && !l.Defeituoso;
        }

        /// <summary>Quantas ligações estão fora da topologia certa (número trocado, P↔P ou S↔S).</summary>
        public int Incorretas
        {
            get
            {
                int n = 0;
                foreach (LigacaoRack l in ligacoes) if (!l.Correta) n++;
                return n;
            }
        }

        /// <summary>Diagnóstico da porta N (para o Console e testes): "ok", "sem cabo", "trocada com S3", "laço", ...</summary>
        public string Diagnostico(int numero)
        {
            LigacaoRack l = LigacaoEm(PortaRack.P(numero));
            if (LacoNoSwitch) return "laço no switch";
            if (l == null) return "sem cabo no patch panel";
            if (l.PatchComPatch) return "ligada a outro ponto do patch panel (" + l.Outra(PortaRack.P(numero)).Rotulo + ")";
            if (l.NumeroTrocado) return "ligada na porta errada do switch (" + l.Outra(PortaRack.P(numero)).Rotulo + ")";
            if (l.Defeituoso) return "cabo com defeito";
            return "ok";
        }
    }
}
