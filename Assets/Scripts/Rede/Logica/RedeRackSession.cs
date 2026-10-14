using System.Collections.Generic;

namespace EstagiarIo.Rede
{
    /// <summary>
    /// Estado do rack de rede: ligar um cabo crimpado entre a porta do patch panel e a porta do switch.
    /// Regra: os números precisam ser iguais (patch panel 3 &lt;-&gt; switch 3).
    /// Lógica pura, sem MonoBehaviour.
    /// </summary>
    public class RedeRackSession
    {
        public int NumPortas { get; }

        /// <summary>Porta do switch escolhida agora (1..NumPortas), ou null.</summary>
        public int? SwitchEscolhido { get; private set; }

        /// <summary>Porta do patch panel escolhida agora (1..NumPortas), ou null.</summary>
        public int? PatchEscolhido { get; private set; }

        /// <summary>Portas já ligadas corretamente (1..NumPortas).</summary>
        public readonly HashSet<int> ParesLigados = new HashSet<int>();

        public RedeRackSession(int numPortas)
        {
            NumPortas = numPortas < 1 ? 1 : numPortas;
        }

        public void EscolherSwitch(int porta)
        {
            if (porta < 1 || porta > NumPortas) return;
            SwitchEscolhido = SwitchEscolhido == porta ? null : porta;
        }

        public void EscolherPatch(int porta)
        {
            if (porta < 1 || porta > NumPortas) return;
            PatchEscolhido = PatchEscolhido == porta ? null : porta;
        }

        public void LimparEscolhas()
        {
            SwitchEscolhido = null;
            PatchEscolhido = null;
        }

        /// <summary>Já dá para avaliar? (uma porta em cada lado escolhida)</summary>
        public bool PodeAvaliar => SwitchEscolhido.HasValue && PatchEscolhido.HasValue;

        /// <summary>As duas portas escolhidas têm o mesmo número?</summary>
        public bool ParCorreto => SwitchEscolhido.HasValue && PatchEscolhido.HasValue && SwitchEscolhido.Value == PatchEscolhido.Value;

        /// <summary>
        /// Fecha a ligação atual. Retorna true se estava correta (e marca a porta em <see cref="ParesLigados"/>);
        /// false se as portas não batem (a UI mostra o erro). Sempre limpa as escolhas.
        /// </summary>
        public bool Confirmar()
        {
            bool ok = ParCorreto && !ParesLigados.Contains(SwitchEscolhido.Value);
            if (ok) ParesLigados.Add(SwitchEscolhido.Value);
            LimparEscolhas();
            return ok;
        }

        public bool PortaLigada(int porta) => ParesLigados.Contains(porta);

        public int TotalLigado => ParesLigados.Count;

        public bool Completo => ParesLigados.Count >= NumPortas;
    }
}
