using System.Collections.Generic;

/// <summary>A tampa de baixo está fechada ou o notebook já está aberto.</summary>
public enum NotebookEstado { Fechado, Aberto }

/// <summary>O que a tela do notebook mostra (igual à do PC: BIOS sem armazenamento, sem sistema, instalando, sistema).</summary>
public enum NotebookEstadoTela { Desligado, SemArmazenamento, SemSistema, Instalando, Sistema }

public enum NotebookParafusoResultado { Removido, Instalado, FerramentaErrada, JaRemovido, JaNoLugar, Invalido, JaAberto }
public enum NotebookAberturaResultado { Aberto, ParafusosFaltando, JaAberto }

public enum NotebookEncaixeResultado
{
    /// <summary>A peça da mão foi instalada (o chamador tira da mão e guarda o objeto).</summary>
    Instalado,
    /// <summary>A peça foi retirada; o objeto volta para a mão.</summary>
    Removido,
    /// <summary>A tampa de baixo ainda está fechada.</summary>
    Fechado,
    /// <summary>Para retirar uma peça, a mão precisa estar livre.</summary>
    MaoCheia,
    /// <summary>Encaixe vazio e nada (ou não uma peça) na mão.</summary>
    PrecisaPeca,
    /// <summary>Encaixe vazio e a peça da mão é de outro tipo (ex.: SSD no encaixe da RAM).</summary>
    PecaErrada,
    /// <summary>Encaixe inválido.</summary>
    EncaixeVazio,
    /// <summary>Encaixe vazio e a peça da mão está QUEBRADA (caiu): não é instalada.</summary>
    PecaQuebrada
}

public enum NotebookUsbResultado { Conectado, Desconectado, Devolvido, MaoCheia, PrecisaPendrive, ItemErrado }

public enum NotebookBotaoResultado { Ligou, Desligou, FaltamRequisitos }

/// <summary>
/// Estado lógico do notebook: parafusos da tampa de baixo, se está aberto, quais peças (RAM SO-DIMM e SSD)
/// estão instaladas, o pendrive da lateral e o que a tela mostra. Vai no objeto lógico do notebook, então o
/// estado sobrevive a fechar e reabrir a tela.
///
/// Regras (mesma espinha do PC, só com as peças do notebook):
///  - Os parafusos da tampa de baixo podem ser tirados e recolocados QUANTAS VEZES QUISER (com a chave de
///    fenda). O notebook está "aberto" enquanto NENHUM dos 6 estiver no lugar; assim que UM parafuso volta,
///    a tampa está presa de novo (fechado) e a parte de dentro (RAM/SSD) fica inacessível.
///  - Sem memória RAM o botão Power não liga.
///  - Ligado com RAM e sem SSD: BIOS "nenhum armazenamento". Com SSD sem sistema: "sem sistema".
///  - Com SSD sem sistema + pendrive na lateral: ao ligar, dá boot pelo pendrive, instala e vai para o sistema.
///    Tirar o pendrive, o SSD ou a RAM durante a instalação cancela/desliga.
///  - Só se instala/retira peça com o notebook aberto.
/// Sem dependência de UI, modelo, tag ou nome.
/// </summary>
public class NotebookSession
{
    private readonly List<bool> removidos = new List<bool>();
    private float instalacaoRestante = -1f;

    /// <param name="parafusos">Quantidade de parafusos da tampa de baixo.</param>
    public NotebookSession(int parafusos = 6)
    {
        if (parafusos < 1) parafusos = 1;
        for (int i = 0; i < parafusos; i++) removidos.Add(false);
    }

    /// <summary>Tempo (s) da instalação do sistema pelo pendrive. 0 = instantâneo.</summary>
    public float DuracaoInstalacao { get; set; } = 6f;
    /// <summary>Quantas etapas visuais a instalação tem.</summary>
    public int EtapasInstalacao { get; set; } = 5;

    /// <summary>
    /// A tampa de baixo está aberta? Sai direto dos parafusos: com NENHUM dos 6 no lugar a tampa sai (e a
    /// parte de dentro, RAM/SSD, fica acessível); com qualquer parafuso preso, está fechada. Dá para tirar e
    /// pôr os parafusos quantas vezes quiser — o estado acompanha sozinho.
    /// </summary>
    public NotebookEstado Estado =>
        TodosParafusosRemovidos ? NotebookEstado.Aberto : NotebookEstado.Fechado;
    /// <summary>Memória SO-DIMM instalada.</summary>
    public bool TemRam { get; private set; }
    /// <summary>SSD instalado.</summary>
    public bool TemSsd { get; private set; }
    /// <summary>O SSD instalado tem o sistema operacional gravado.</summary>
    public bool SsdTemSistema { get; private set; }
    /// <summary>Há um pendrive na porta USB da lateral.</summary>
    public bool PendriveConectado { get; private set; }
    /// <summary>Notebook ligado.</summary>
    public bool Ligado { get; private set; }

    public int Parafusos => removidos.Count;
    public bool ParafusoRemovido(int indice) => indice >= 0 && indice < removidos.Count && removidos[indice];

    public int Removidos
    {
        get
        {
            int n = 0;
            for (int i = 0; i < removidos.Count; i++) if (removidos[i]) n++;
            return n;
        }
    }

    public bool TodosParafusosRemovidos => Removidos == removidos.Count;
    /// <summary>Todos os parafusos estão de volta na tampa de baixo (é o que fecha o notebook de novo).</summary>
    public bool TodosParafusosInstalados => Removidos == 0;

    /// <summary>Instalando o sistema pelo pendrive agora.</summary>
    public bool Instalando => instalacaoRestante >= 0f;
    public float ProgressoInstalacao => Instalando && DuracaoInstalacao > 0f
        ? 1f - instalacaoRestante / DuracaoInstalacao : 0f;
    public int EtapaInstalacao => Instalando
        ? System.Math.Min(EtapasInstalacao - 1, (int)(ProgressoInstalacao * EtapasInstalacao)) : 0;

    /// <summary>O sistema está pronto para uso (SSD com sistema instalado).</summary>
    public bool TemSistema => TemSsd && SsdTemSistema;

    /// <summary>Tela exata do notebook agora (ao vivo: instalar/retirar o SSD muda a mensagem).</summary>
    public NotebookEstadoTela Tela
    {
        get
        {
            if (!Ligado) return NotebookEstadoTela.Desligado;
            if (Instalando) return NotebookEstadoTela.Instalando;
            if (TemSistema) return NotebookEstadoTela.Sistema;
            return TemSsd ? NotebookEstadoTela.SemSistema : NotebookEstadoTela.SemArmazenamento;
        }
    }

    /// <summary>Remove um parafuso da tampa de baixo (precisa da chave de fenda). Dá para tirar e pôr quantas vezes quiser.</summary>
    public NotebookParafusoResultado RemoverParafuso(int indice, ToolType ferramenta)
    {
        if (indice < 0 || indice >= removidos.Count) return NotebookParafusoResultado.Invalido;
        if (removidos[indice]) return NotebookParafusoResultado.JaRemovido;
        if (ferramenta != ToolType.Screwdriver) return NotebookParafusoResultado.FerramentaErrada;

        removidos[indice] = true;
        return NotebookParafusoResultado.Removido;
    }

    /// <summary>
    /// Recoloca um parafuso no furo vazio (também precisa da chave de fenda). Livre a qualquer momento: com o
    /// último parafuso de volta o notebook fecha de novo (é o que permite entregar montado).
    /// </summary>
    public NotebookParafusoResultado InstalarParafuso(int indice, ToolType ferramenta)
    {
        if (indice < 0 || indice >= removidos.Count) return NotebookParafusoResultado.Invalido;
        if (!removidos[indice]) return NotebookParafusoResultado.JaNoLugar;
        if (ferramenta != ToolType.Screwdriver) return NotebookParafusoResultado.FerramentaErrada;

        removidos[indice] = false;
        return NotebookParafusoResultado.Instalado;
    }

    /// <summary>
    /// Confirma que a tampa de baixo saiu. Como o estado sai dos próprios parafusos (ver
    /// <see cref="Estado"/>), isto só responde: com todos os parafusos fora, está aberto.
    /// </summary>
    public NotebookAberturaResultado Abrir()
    {
        return TodosParafusosRemovidos ? NotebookAberturaResultado.Aberto : NotebookAberturaResultado.ParafusosFaltando;
    }

    /// <summary>
    /// Clique num encaixe. Encaixe vazio + peça certa na mão → instala. Encaixe ocupado + mão livre → retira
    /// (o objeto volta para a mão pelo chamador). Só com o notebook aberto.
    /// </summary>
    public NotebookEncaixeResultado UsarEncaixe(NotebookParte encaixe, bool temItemNaMao, NotebookParte pecaNaMao)
    {
        if (encaixe == NotebookParte.Nenhuma) return NotebookEncaixeResultado.EncaixeVazio;
        if (Estado != NotebookEstado.Aberto) return NotebookEncaixeResultado.Fechado;

        bool ocupado = encaixe == NotebookParte.Ram ? TemRam : TemSsd;
        if (ocupado)
        {
            if (temItemNaMao) return NotebookEncaixeResultado.MaoCheia;
            if (encaixe == NotebookParte.Ram) TemRam = false;
            else { TemSsd = false; SsdTemSistema = false; }
            Revalidar();
            return NotebookEncaixeResultado.Removido;
        }

        if (!temItemNaMao) return NotebookEncaixeResultado.PrecisaPeca;
        if (pecaNaMao != encaixe) return NotebookEncaixeResultado.PecaErrada;

        if (encaixe == NotebookParte.Ram) TemRam = true;
        else TemSsd = true;
        return NotebookEncaixeResultado.Instalado;
    }

    /// <summary>Marca se o SSD instalado tem o sistema (o chamador lê/grava isso no próprio item do SSD).</summary>
    public void DefinirSistemaNoSsd(bool tem) => SsdTemSistema = tem && TemSsd;

    public void ConectarPendrive()
    {
        PendriveConectado = true;
    }

    public void DesconectarPendrive()
    {
        PendriveConectado = false;
        Revalidar();
    }

    /// <summary>Botão Power: liga (se os requisitos forem atendidos) ou desliga.</summary>
    public NotebookBotaoResultado PressPower()
    {
        if (Ligado)
        {
            instalacaoRestante = -1f;
            Ligado = false;
            return NotebookBotaoResultado.Desligou;
        }

        // Sem memória não dá nem para chegar na BIOS.
        if (!TemRam) return NotebookBotaoResultado.FaltamRequisitos;

        if (TemSistema)
        {
            Ligado = true;
            return NotebookBotaoResultado.Ligou;
        }

        // Boot pelo pendrive: só instala se houver SSD onde gravar.
        bool instalar = PendriveConectado && TemSsd;
        if (instalar) instalacaoRestante = DuracaoInstalacao;
        Ligado = true;
        if (instalar && DuracaoInstalacao <= 0f) ConcluirInstalacao();
        return NotebookBotaoResultado.Ligou;
    }

    /// <summary>Avança a instalação em andamento (chamado pelo Update).</summary>
    public void Avancar(float deltaTime)
    {
        if (!Instalando) return;

        instalacaoRestante -= deltaTime;
        if (instalacaoRestante > 0f) return;
        ConcluirInstalacao();
    }

    private void ConcluirInstalacao()
    {
        instalacaoRestante = -1f;
        // Só grava no SSD se tudo ainda estiver de pé.
        if (Ligado && PendriveConectado && TemSsd) SsdTemSistema = true;
    }

    /// <summary>Desliga o notebook se a configuração atual não sustenta mais o estado em que ele está.</summary>
    public void Revalidar()
    {
        if (!Ligado) return;

        if (!TemRam || (SsdTemSistema && !TemSsd))
        {
            instalacaoRestante = -1f;
            Ligado = false;
            return;
        }

        // Instalação interrompida (pendrive ou SSD retirado): volta para a BIOS, sem sistema.
        if (Instalando && (!PendriveConectado || !TemSsd)) instalacaoRestante = -1f;
    }
}
