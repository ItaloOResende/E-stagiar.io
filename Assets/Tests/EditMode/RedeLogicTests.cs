using System.Collections.Generic;
using EstagiarIo.Rede;
using NUnit.Framework;

/// <summary>Testes da lógica pura do minigame de cabo de rede (padrão T568B, crimpagem e rack).</summary>
public class RedeLogicTests
{
    // ------------------------------------------------------------------ cores / T568B

    [Test]
    public void OrdemT568B_tem_os_oito_fios_na_sequencia_certa()
    {
        RedeFio[] esperada =
        {
            RedeFio.BrancoLaranja, RedeFio.Laranja, RedeFio.BrancoVerde, RedeFio.Azul,
            RedeFio.BrancoAzul, RedeFio.Verde, RedeFio.BrancoMarrom, RedeFio.Marrom,
        };
        CollectionAssert.AreEqual(esperada, RedeCores.OrdemT568B);
    }

    [Test]
    public void PosicaoCorreta_mapeia_cada_fio()
    {
        Assert.AreEqual(0, RedeCores.PosicaoCorreta(RedeFio.BrancoLaranja));
        Assert.AreEqual(3, RedeCores.PosicaoCorreta(RedeFio.Azul));
        Assert.AreEqual(7, RedeCores.PosicaoCorreta(RedeFio.Marrom));
    }

    [Test]
    public void OrdemCorreta_aceita_a_sequencia_certa_e_recusa_o_resto()
    {
        Assert.IsTrue(RedeCores.OrdemCorreta(RedeCores.OrdemT568B));

        var trocada = (RedeFio[])RedeCores.OrdemT568B.Clone();
        (trocada[0], trocada[1]) = (trocada[1], trocada[0]);
        Assert.IsFalse(RedeCores.OrdemCorreta(trocada));
        Assert.IsFalse(RedeCores.OrdemCorreta(null));
        Assert.IsFalse(RedeCores.OrdemCorreta(new[] { RedeFio.Azul }));
    }

    // ------------------------------------------------------------------ crimpagem

    private static RedeCrimpSession AteOrdenar()
    {
        var s = new RedeCrimpSession();
        Assert.IsTrue(s.Cortar());
        for (int i = 0; i < RedeCrimpSession.ToquesParaDescascar; i++) Assert.IsTrue(s.Descascar());
        Assert.AreEqual(RedeCrimpEtapa.Ordenar, s.Etapa);
        return s;
    }

    [Test]
    public void Descascar_precisa_de_um_toque()
    {
        var s = new RedeCrimpSession();
        s.Cortar();
        Assert.IsTrue(s.Descascar());
        Assert.AreEqual(1, s.DescascarToques);
        Assert.AreEqual(RedeCrimpEtapa.Ordenar, s.Etapa);
    }

    [Test]
    public void Embaralhar_deixa_os_oito_fios_soltos_saindo_do_cabo()
    {
        var s = new RedeCrimpSession();
        s.Embaralhar(new System.Random(123));

        Assert.AreEqual(8, s.Soltos.Count);
        Assert.AreEqual(8, new HashSet<RedeFio>(s.Soltos).Count);
        Assert.IsNull(s.NaMao);
        foreach (RedeFio? f in s.Fila) Assert.IsNull(f);   // as posições começam VAZIAS
    }

    [Test]
    public void Embaralhar_funciona_tambem_em_descascar()
    {
        // Depois de errar a crimpagem o estado volta para "Descascar" e os fios são soltos de novo.
        var s = new RedeCrimpSession();
        s.Embaralhar(new System.Random(1));
        s.Cortar();
        s.Embaralhar(new System.Random(2));

        Assert.AreEqual(8, s.Soltos.Count);
        Assert.AreEqual(8, new HashSet<RedeFio>(s.Soltos).Count);
        foreach (RedeFio? f in s.Fila) Assert.IsNull(f);
    }

    [Test]
    public void Trocar_permuta_dois_fios()
    {
        var s = AteOrdenar();
        for (int i = 0; i < 8; i++) s.Fila[i] = RedeCores.OrdemT568B[i];

        Assert.IsTrue(s.Trocar(0, 7));
        Assert.AreEqual(RedeCores.OrdemT568B[7], s.Fila[0]);
        Assert.AreEqual(RedeCores.OrdemT568B[0], s.Fila[7]);
        Assert.IsFalse(s.OrdemCorreta());
        Assert.AreEqual(2, s.PosicoesErradas().Count);
    }

    [Test]
    public void Pegar_fio_solto_e_colocar_numa_posicao()
    {
        var s = AteOrdenar();
        s.Embaralhar(new System.Random(7));

        RedeFio alvo = s.Soltos[0];
        Assert.IsTrue(s.PegarSolto(0));
        Assert.AreEqual(alvo, s.NaMao);
        Assert.AreEqual(7, s.Soltos.Count);

        Assert.IsTrue(s.ColocarNaPosicao(2));
        Assert.IsNull(s.NaMao);
        Assert.AreEqual(alvo, s.Fila[2]);
    }

    [Test]
    public void Colocar_em_posicao_ocupada_troca_com_o_fio_da_mao()
    {
        var s = AteOrdenar();
        s.Embaralhar(new System.Random(3));

        RedeFio a = s.Soltos[0];
        RedeFio b = s.Soltos[1];

        s.PegarSolto(0);
        s.ColocarNaPosicao(5);                    // "a" vai para a posição 5
        Assert.AreEqual(a, s.Fila[5]);

        s.PegarSolto(s.Soltos.IndexOf(b));
        s.ColocarNaPosicao(5);                    // posição ocupada: os dois trocam
        Assert.AreEqual(b, s.Fila[5]);
        Assert.AreEqual(a, s.NaMao);
    }

    [Test]
    public void Pegar_da_posicao_e_devolver_deixa_o_fio_solto_de_novo()
    {
        var s = AteOrdenar();
        s.Embaralhar(new System.Random(4));

        s.PegarSolto(0);
        s.ColocarNaPosicao(1);
        Assert.IsNotNull(s.Fila[1]);

        Assert.IsTrue(s.PegarDaPosicao(1));
        Assert.IsNull(s.Fila[1]);
        Assert.IsTrue(s.NaMao.HasValue);

        Assert.IsTrue(s.DevolverAoSoltos());
        Assert.IsNull(s.NaMao);
        Assert.AreEqual(8, s.Soltos.Count);
    }

    [Test]
    public void Nao_se_segura_mais_de_um_fio_por_vez()
    {
        var s = AteOrdenar();
        s.Embaralhar(new System.Random(2));

        Assert.IsTrue(s.PegarSolto(0));
        Assert.IsFalse(s.PegarSolto(0));          // a mão já está cheia
        Assert.IsFalse(s.PegarDaPosicao(0));      // e da posição também não dá

        Assert.IsTrue(s.ColocarNaPosicao(0));
        Assert.IsNull(s.NaMao);
    }

    [Test]
    public void Encaixar_nao_avanca_com_posicoes_vazias()
    {
        var s = AteOrdenar();
        s.Embaralhar(new System.Random(1));

        Assert.AreEqual(8, s.Soltos.Count);
        Assert.IsFalse(s.EncaixarRj45());
        Assert.AreEqual(RedeCrimpEtapa.Ordenar, s.Etapa);
    }

    [Test]
    public void Montar_a_fileira_com_os_fios_soltos_e_crimpar_certo()
    {
        var s = AteOrdenar();
        s.Embaralhar(new System.Random(99));

        // O jogador pega cada fio solto e o coloca na posição certa do padrão T568B.
        for (int pos = 0; pos < 8; pos++)
        {
            RedeFio alvo = RedeCores.OrdemT568B[pos];
            int indice = s.Soltos.IndexOf(alvo);
            Assert.GreaterOrEqual(indice, 0, "o fio " + alvo + " não estava solto");

            Assert.IsTrue(s.PegarSolto(indice));
            Assert.IsTrue(s.ColocarNaPosicao(pos));
        }

        Assert.IsTrue(s.TodasPreenchidas);
        Assert.AreEqual(0, s.Soltos.Count);
        Assert.IsTrue(s.OrdemCorreta());

        Assert.IsTrue(s.EncaixarRj45());
        Assert.AreEqual(RedeCrimpResultado.Ok, s.Crimpar());
        Assert.AreEqual(RedeCrimpEtapa.Concluido, s.Etapa);
    }

    [Test]
    public void Encaixar_avanca_sem_conferir_a_ordem()
    {
        var s = AteOrdenar();
        for (int i = 0; i < 8; i++) s.Fila[i] = RedeCores.OrdemT568B[i];
        s.Fila[0] = RedeFio.Marrom;   // ordem errada, mas encaixar não confere
        Assert.IsTrue(s.EncaixarRj45());
        Assert.AreEqual(RedeCrimpEtapa.EncaixarRj45, s.Etapa);
    }

    [Test]
    public void Crimpar_com_ordem_errada_descarta_o_cabo_e_volta_a_descascar()
    {
        var s = AteOrdenar();
        for (int i = 0; i < 8; i++) s.Fila[i] = RedeCores.OrdemT568B[i];
        s.Fila[0] = RedeFio.Marrom;   // 1 fio fora de lugar (o Marrom já está na posição 7)
        s.EncaixarRj45();

        Assert.AreEqual(RedeCrimpResultado.OrdemErrada, s.Crimpar());
        Assert.AreEqual(RedeCrimpEtapa.Descascar, s.Etapa);
        Assert.AreEqual(0, s.DescascarToques);
        Assert.AreEqual(1, s.FiosErradosNaUltimaCrimpagem);
        foreach (RedeFio? f in s.Fila) Assert.IsNull(f);   // fileira limpa

        // Depois de descascar de novo, dá para remontar e crimpar certo.
        Assert.IsTrue(s.Descascar());
        Assert.AreEqual(RedeCrimpEtapa.Ordenar, s.Etapa);
        for (int i = 0; i < 8; i++) s.Fila[i] = RedeCores.OrdemT568B[i];
        Assert.IsTrue(s.EncaixarRj45());
        Assert.AreEqual(RedeCrimpResultado.Ok, s.Crimpar());
        Assert.AreEqual(RedeCrimpEtapa.Concluido, s.Etapa);
        Assert.AreEqual(0, s.FiosErradosNaUltimaCrimpagem);
    }

    [Test]
    public void Nao_da_para_crimpar_antes_de_encaixar()
    {
        var s = AteOrdenar();
        for (int i = 0; i < 8; i++) s.Fila[i] = RedeCores.OrdemT568B[i];
        Assert.AreEqual(RedeCrimpResultado.EtapaInvalida, s.Crimpar());
        Assert.AreEqual(RedeCrimpEtapa.Ordenar, s.Etapa);
    }

    // ------------------------------------------------------------------ rack

    [Test]
    public void Rack_liga_par_de_mesmo_numero()
    {
        var r = new RedeRackSession(6);
        Assert.IsFalse(r.PodeAvaliar);

        r.EscolherSwitch(3);
        r.EscolherPatch(3);
        Assert.IsTrue(r.PodeAvaliar);
        Assert.IsTrue(r.ParCorreto);
        Assert.IsTrue(r.Confirmar());
        Assert.IsTrue(r.PortaLigada(3));
        Assert.AreEqual(1, r.TotalLigado);
        Assert.IsFalse(r.PodeAvaliar);   // limpa as escolhas
    }

    [Test]
    public void Rack_rejeita_numeros_diferentes()
    {
        var r = new RedeRackSession(6);
        r.EscolherSwitch(2);
        r.EscolherPatch(5);
        Assert.IsFalse(r.ParCorreto);
        Assert.IsFalse(r.Confirmar());
        Assert.AreEqual(0, r.TotalLigado);
    }

    [Test]
    public void Rack_nao_repete_porta_ja_ligada()
    {
        var r = new RedeRackSession(6);
        r.EscolherSwitch(1); r.EscolherPatch(1); Assert.IsTrue(r.Confirmar());
        r.EscolherSwitch(1); r.EscolherPatch(1); Assert.IsFalse(r.Confirmar());
        Assert.AreEqual(1, r.TotalLigado);
    }

    [Test]
    public void Rack_troca_a_escolha_ao_clicar_na_mesma_porta()
    {
        var r = new RedeRackSession(6);
        r.EscolherSwitch(4);
        Assert.AreEqual(4, r.SwitchEscolhido);
        r.EscolherSwitch(4);
        Assert.IsFalse(r.SwitchEscolhido.HasValue);
    }

    [Test]
    public void Rack_completa_quando_todas_as_portas_estao_ligadas()
    {
        var r = new RedeRackSession(3);
        for (int p = 1; p <= 3; p++) { r.EscolherSwitch(p); r.EscolherPatch(p); Assert.IsTrue(r.Confirmar()); }
        Assert.IsTrue(r.Completo);
    }

    [Test]
    public void Rack_desliga_a_porta_e_deixa_ligar_de_novo()
    {
        var r = new RedeRackSession(6);
        r.EscolherSwitch(3); r.EscolherPatch(3); Assert.IsTrue(r.Confirmar());

        Assert.IsTrue(r.Desligar(3));
        Assert.IsFalse(r.PortaLigada(3));
        Assert.AreEqual(0, r.TotalLigado);

        // Ligada de novo: dá pra colocar e tirar quantas vezes quiser.
        r.EscolherSwitch(3); r.EscolherPatch(3); Assert.IsTrue(r.Confirmar());
        Assert.IsTrue(r.PortaLigada(3));
        Assert.IsTrue(r.Desligar(3));
        Assert.IsFalse(r.Desligar(3));   // já estava sem cabo
    }
}
