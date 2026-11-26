using System.Text.Json;
using c2_pcg.flowMatchingDataloader;
using ChunkLink.Agentes;
using ChunkLink.Ambiente;
using ChunkLink.Comunicacao;
using ChunkLink.Estilo;
using ChunkLink.Memoria;
using ChunkLink.Movimento;
using ChunkLink.Runtime;
using Xunit;

namespace ChunkLink.Tests;

// DSM3: testes adversariais (mensagens forjadas, papéis indevidos, autorizações reutilizadas),
// invariantes do Nível, injeção de falhas de entrega, propriedades sob pools e sementes aleatórios,
// casos-limite do modelo de movimento e memória persistente. Correspondem aos pontos do parecer da DSM2.
public class TestesRobustez
{
    private static IEnumerable<(EventoTrace E, JsonElement D)> Eventos(Tracer t, string tipo, string? agente = null) =>
        t.Eventos.Where(e => e.Tipo == tipo && (agente == null || e.Agente == agente))
            .Select(e => (e, JsonDocument.Parse(e.Dados).RootElement));

    private static string S(JsonElement d, string campo) => d.GetProperty(campo).ToString();

    // ------------------------------------------------------------------ comunicação: autenticação e política

    [Fact]
    public void Seg_CredencialDeOutroBarramentoEhRecusada()
    {
        var tracer = new Tracer();
        var bus = new MessageBus(tracer);
        bus.Registrar("curador", Papel.Curador);
        var arq = bus.Registrar("arquiteto", Papel.Arquiteto);
        // Um agente "impostor" registra o nome do curador num barramento próprio e tenta usar a credencial.
        var falso = new MessageBus(new Tracer()).Registrar("curador", Papel.Curador);

        Assert.Throws<CredencialInvalida>(() => bus.Enviar(falso, "arquiteto", Performativa.Inform,
            new RestricaoCurador(1, "final", "forjado"), "x", 1));
        Assert.Throws<CredencialInvalida>(() => bus.Retirar(falso));
        Assert.Empty(bus.Retirar(arq));
        Assert.Single(tracer.DoTipo("msg.rejeitada"));
    }

    [Fact]
    public void Seg_PoliticaDePapeisBloqueiaMensagensForaDoPapel()
    {
        var bus = new MessageBus(new Tracer());
        var arq = bus.Registrar("arquiteto", Papel.Arquiteto);
        var jogador = bus.Registrar("jogador-P-Peq", Papel.Jogador);
        var reparador = bus.Registrar("reparador-costura", Papel.Reparador);
        var diff = new Diff(Array.Empty<AlteracaoTile>(), "nada");

        // Um jogador não emite aprovações; um reparador não abre Contract Net nem fala em nome de outro.
        Assert.Throws<ViolacaoPolitica>(() => bus.Enviar(jogador, "arquiteto", Performativa.Agree,
            new Aprovacao(1, "jogador-P-Peq", "c", 1, "x", "h", 1), "c", 1));
        Assert.Throws<ViolacaoPolitica>(() => bus.Enviar(reparador, "arquiteto", Performativa.Cfp, "abrir", "c", 1));
        Assert.Throws<ViolacaoPolitica>(() => bus.Enviar(reparador, "arquiteto", Performativa.Propose,
            new PropostaReparo("reparador-suporte", diff, 1), "c", 1));
        // Os mesmos conteúdos, vindos do papel certo, passam.
        bus.Enviar(reparador, "arquiteto", Performativa.Propose, new PropostaReparo("reparador-costura", diff, 1), "c", 1);
        bus.Enviar(jogador, "arquiteto", Performativa.Failure, "pedido não entendido", "c", 1);
        Assert.Equal(2, bus.Retirar(arq).Count);
    }

    [Fact]
    public void Seg_ExecucaoNormalNaoViolaAPoliticaNemOProtocolo()
    {
        var t = CenarioControlado.Criar().Executar().Tracer;
        Assert.Empty(t.DoTipo("msg.rejeitada"));
        Assert.Empty(t.DoTipo("protocolo.violacao"));
        Assert.Empty(t.DoTipo("msg.inesperada"));
    }

    [Fact]
    public void RNF01_AssinaturaDoTraceIndependeDoIdiomaDaMaquina()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("pt-BR");
            var ptBr = CenarioControlado.Criar().Executar().Metricas.AssinaturaTrace;
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("en-US");
            var enUs = CenarioControlado.Criar().Executar().Metricas.AssinaturaTrace;
            Assert.Equal(ptBr, enUs);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    // ------------------------------------------------------------------ Nível: invariantes impostas pelo ambiente

    [Fact]
    public void Seg_AprovacaoNaoPodeSerReutilizada()
    {
        var pool = CenarioControlado.Pool();
        var (nivel, arq, cur, rep, _) = TestesAmbienteEMovimento.NivelDeTeste(3);
        TestesAmbienteEMovimento.FixarComAceite(nivel, arq, cur, 1, pool.Obter("a-baixo"));
        var chunk = pool.Obter("e-buraco1");
        var diff = new Diff(new[] { new AlteracaoTile(0, 0, TileTypeEnum.Empty, TileTypeEnum.Coin) }, "moeda");
        var edicao = nivel.AbrirEdicao(rep, "reparo:2:1", 2, chunk, diff);
        var aprovacao = nivel.RegistrarAprovacao(cur, "reparo:2:1", 2, chunk.Id, diff.Hash, 1);
        nivel.Commit(arq, edicao, aprovacao.Id);

        // Mesmo slot, mesmo chunk, mesmo diff e mesma conversa: a aprovação já foi consumida.
        nivel.Liberar(arq, 2);
        var repeticao = nivel.AbrirEdicao(rep, "reparo:2:1", 2, chunk, diff);
        var e = Assert.Throws<ViolacaoNorma>(() => nivel.Commit(arq, repeticao, aprovacao.Id));
        Assert.Equal("N5", e.Norma);
        Assert.Null(nivel.ChunkNoSlot(2));
    }

    [Fact]
    public void Seg_FixarExigeAceiteDoCuradorParaAqueleSlotEChunk()
    {
        var pool = CenarioControlado.Pool();
        var (nivel, arq, cur, _, _) = TestesAmbienteEMovimento.NivelDeTeste(3);
        var a = pool.Obter("a-baixo");
        var d = pool.Obter("d-escada");

        Assert.Equal("N5", Assert.Throws<ViolacaoNorma>(() => nivel.Fixar(arq, 1, a, 12345)).Norma);
        var aceiteDeD = nivel.RegistrarAceite(cur, 1, d.Id, 1);
        Assert.Equal("N5", Assert.Throws<ViolacaoNorma>(() => nivel.Fixar(arq, 1, a, aceiteDeD.Id)).Norma);
        var aceiteSlot2 = nivel.RegistrarAceite(cur, 2, a.Id, 1);
        Assert.Equal("N5", Assert.Throws<ViolacaoNorma>(() => nivel.Fixar(arq, 1, a, aceiteSlot2.Id)).Norma);
        Assert.Equal("PAPEL", Assert.Throws<ViolacaoNorma>(() => nivel.RegistrarAceite(arq, 1, a.Id, 1)).Norma);
        Assert.Null(nivel.ChunkNoSlot(1));

        var aceite = nivel.RegistrarAceite(cur, 1, a.Id, 1);
        nivel.Fixar(arq, 1, a, aceite.Id);
        nivel.Liberar(arq, 1);
        // Aceite já usado não vale de novo.
        Assert.Equal("N5", Assert.Throws<ViolacaoNorma>(() => nivel.Fixar(arq, 1, a, aceite.Id)).Norma);
    }

    [Fact]
    public void Seg_NivelImpoeN1N2EN4MesmoComAutorizacao()
    {
        var pool = CenarioControlado.Pool();
        var a = pool.Obter("a-baixo");
        var e = pool.Obter("e-buraco1");

        // N2: o curador "aceitou", mas o chunk já está no nível.
        var (n2, arq, cur, rep, _) = TestesAmbienteEMovimento.NivelDeTeste(3);
        TestesAmbienteEMovimento.FixarComAceite(n2, arq, cur, 1, a);
        Assert.Equal("N2", Assert.Throws<ViolacaoNorma>(() => n2.Fixar(arq, 2, a, n2.RegistrarAceite(cur, 2, a.Id, 1).Id)).Norma);

        // N4: o Nível mede a distância a originais por conta própria.
        var (n4, arq4, cur4, _, _) = TestesAmbienteEMovimento.NivelDeTeste(3, _ => 0.0);
        Assert.Equal("N4", Assert.Throws<ViolacaoNorma>(() => n4.Fixar(arq4, 1, a, n4.RegistrarAceite(cur4, 1, a.Id, 1).Id)).Norma);
        Assert.Null(n4.ChunkNoSlot(1));

        // N1: diff acima de e_max = 10 tiles, mesmo aprovado, não é efetivado.
        var grande = new Diff(Enumerable.Range(0, 11).Select(x => new AlteracaoTile(x, 0, TileTypeEnum.Empty, TileTypeEnum.Coin)).ToList(), "grande");
        var edicao = n2.AbrirEdicao(rep, "reparo:2:1", 2, e, grande);
        var aprovacao = n2.RegistrarAprovacao(cur, "reparo:2:1", 2, e.Id, grande.Hash, 1);
        Assert.Equal("N1", Assert.Throws<ViolacaoNorma>(() => n2.Commit(arq, edicao, aprovacao.Id)).Norma);
        Assert.Null(n2.ChunkNoSlot(2));
        Assert.Equal(0, n2.TilesEditados);
    }

    // ------------------------------------------------------------------ falhas de entrega e de protocolo

    [Fact]
    public void Rob_MensagensDuplicadasNaoMudamODesfecho()
    {
        var normal = CenarioControlado.Criar().Executar();
        var comDuplicatas = CenarioControlado.Criar(falhas: new InjetorFalhas(7, probabilidadeDuplicar: 0.3, probabilidadePerder: 0)).Executar();

        Assert.NotEmpty(comDuplicatas.Tracer.DoTipo("falha.duplicacao"));
        Assert.NotEmpty(comDuplicatas.Tracer.DoTipo("msg.duplicada"));
        Assert.Equal(normal.Metricas.Sequencia, comDuplicatas.Metricas.Sequencia);
        Assert.Equal(normal.Metricas.TilesEditados, comDuplicatas.Metricas.TilesEditados);
        Assert.Empty(comDuplicatas.Tracer.DoTipo("protocolo.violacao"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Rob_PerdaDeMensagensEhRecuperadaPorReenvio(int semente)
    {
        var sistema = CenarioControlado.Criar(falhas: new InjetorFalhas(semente, probabilidadeDuplicar: 0, probabilidadePerder: 0.05));
        var r = sistema.Executar();

        Assert.NotEmpty(r.Tracer.DoTipo("falha.perda"));
        // Com 5% de perda, o reenvio de pedidos sem resposta recupera a execução: o nível é montado.
        Assert.NotEmpty(r.Tracer.DoTipo("reenvio"));
        Assert.True(r.Metricas.Sucesso, r.Metricas.Motivo);
        VerificarInvariantes(sistema, r, CenarioControlado.Parametros);
    }

    [Fact]
    public void Rob_ViolacaoDeProtocoloEhTratadaERespondidaComFalha()
    {
        var tracer = new Tracer();
        var bus = new MessageBus(tracer);
        var mundo = new Mundo
        {
            Bus = bus, Tracer = tracer, Relogio = new Relogio { Tick = 1 },
            Nivel = new Nivel(4, BiomeTypeEnum.Overworld, bus, new RegrasNivel(40, 10, 0.10, null)), Pool = CenarioControlado.Pool(),
            Estilo = new ConhecimentoEstilo(CenarioControlado.Referencia()), P = new Parametros(), PerfisAlvo = CenarioControlado.Perfis,
        };
        var arq = bus.Registrar(Mundo.IdArquiteto, Papel.Arquiteto);
        var reparador = new RepairAgent(Reparos.Costura, mundo);

        // accept-proposal sem cfp nem proposta: transição inválida na máquina de estados do P3.
        bus.Enviar(arq, reparador.Id, Performativa.AcceptProposal, "e-buraco1", "reparo:9:1", 1);
        reparador.Ciclo();

        Assert.Single(tracer.DoTipo("protocolo.violacao"));
        var resposta = Assert.Single(bus.Retirar(arq));
        Assert.Equal(Performativa.Failure, resposta.Performativa);
        Assert.Empty(mundo.Nivel.Reservas.Ativas);
    }

    // ------------------------------------------------------------------ propriedades sob pools e sementes aleatórios

    public static IEnumerable<object[]> CasosAleatorios()
    {
        foreach (var bioma in new[] { "overworld", "underground", "treetop" })
            for (int semente = 1; semente <= 4; semente++)
                yield return new object[] { bioma, semente };
    }

    [Theory]
    [MemberData(nameof(CasosAleatorios))]
    public void Prop_InvariantesValemEmPoolsEConfiguracoesAleatorios(string bioma, int semente)
    {
        var rng = new Random(semente * 31 + bioma.Length);
        var completo = PoolChunks.Carregar(Path.Combine(RaizDoRepositorio(), "dados", "pool", bioma, "reparado"));
        var pool = new PoolChunks(completo.Chunks.OrderBy(_ => rng.Next()).Take(rng.Next(10, 21)));
        var p = new Parametros
        {
            Slots = rng.Next(2, 5), MCandidatos = rng.Next(2, 6), EMax = rng.Next(10, 41), ReplyBy = rng.Next(1, 4),
            Cognicao = rng.Next(4) != 0,
        };
        var sistema = new SistemaChunkLink(pool, CenarioControlado.Referencia(), p, CenarioControlado.Perfis, Enum.Parse<BiomeTypeEnum>(bioma, true));
        var r = sistema.Executar();

        Assert.True(r.Metricas.Ticks < p.MaxTicks, "o sistema não terminou dentro do limite");
        Assert.True(r.Metricas.Sucesso || !string.IsNullOrWhiteSpace(r.Metricas.Motivo));
        Assert.Empty(r.Tracer.DoTipo("msg.rejeitada"));
        Assert.Empty(r.Tracer.DoTipo("protocolo.violacao"));
        VerificarInvariantes(sistema, r, p);
    }

    private static void VerificarInvariantes(SistemaChunkLink sistema, ResultadoExecucao r, Parametros p)
    {
        var nivel = sistema.Mundo.Nivel;
        // Ao terminar, nenhuma reserva fica presa.
        Assert.Empty(nivel.Reservas.Ativas);
        // N1: o orçamento nunca é excedido.
        Assert.True(nivel.TilesEditados <= p.EMax);
        // N5: todo commit no histórico do Nível cita uma aprovação registrada.
        Assert.All(nivel.Historico.Where(h => h.StartsWith("commit")), h => Assert.Contains("aprovação #", h));
        Assert.Equal(nivel.Historico.Count(h => h.StartsWith("commit")), r.Tracer.DoTipo("nivel.commit").Count());
        if (!r.Metricas.Sucesso) return;
        // Sucesso implica: todos os perfis concluem o nível fechado e nenhum chunk se repete (N2).
        Assert.All(r.Metricas.ConclusaoPorPerfil.Values, Assert.True);
        Assert.Equal(r.Metricas.Sequencia.Count, r.Metricas.Sequencia.Distinct().Count());
        Assert.All(sistema.Mundo.PerfisAlvo, perfil => Assert.True(BuscaAEstrela.Jogar(r.Nivel!, perfil).Concluiu));
    }

    private static string RaizDoRepositorio()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ChunkLink.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("raiz do repositório (ChunkLink.sln) não encontrada");
    }

    // ------------------------------------------------------------------ casos-limite do movimento

    private static TileMap Plano(Action<TileMap>? desenhar = null)
    {
        var m = Grade.Criar(28);
        for (int x = 0; x < 28; x++) { Grade.Definir(m, x, 12, TileTypeEnum.Solid); Grade.Definir(m, x, 13, TileTypeEnum.Solid); }
        desenhar?.Invoke(m);
        return m;
    }

    private static void Buraco(TileMap m, int inicio, int largura)
    {
        for (int x = inicio; x < inicio + largura; x++) { Grade.Definir(m, x, 12, TileTypeEnum.Empty); Grade.Definir(m, x, 13, TileTypeEnum.Empty); }
    }

    [Theory]
    [InlineData(4, true)]   // alcance d = 5: buraco de até d − 1 colunas
    [InlineData(5, false)]
    public void Mov_BuracoPulavelAteAlcanceMenosUm(int largura, bool passa)
    {
        var m = Plano(x => Buraco(x, 10, largura));
        foreach (var perfil in CenarioControlado.Perfis)
        {
            var r = BuscaAEstrela.Jogar(m, perfil);
            Assert.Equal(passa, r.Concluiu);
            if (!passa) Assert.Equal(CausaFalha.Buraco, r.Causa);
        }
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(5, false)]
    public void Mov_ParedeEscalavelAteAAlturaDoSalto(int altura, bool passa)
    {
        var m = Plano(x =>
        {
            for (int c = 14; c < 28; c++) for (int y = 12 - altura; y < 12; y++) Grade.Definir(x, c, y, TileTypeEnum.Solid);
        });
        var r = BuscaAEstrela.Jogar(m, PerfilJogador.Pequeno);
        Assert.Equal(passa, r.Concluiu);
        if (!passa) Assert.Equal(CausaFalha.Parede, r.Causa);
    }

    [Fact]
    public void Mov_InimigosSaoPisadosOuPuladosMasBloqueiamNumCorredorBaixo()
    {
        var um = Plano(x => Grade.Definir(x, 11, 11, TileTypeEnum.Enemy));
        Assert.True(BuscaAEstrela.Jogar(um, PerfilJogador.Pequeno).Concluiu);

        // Uma fila de Goombas maior que o alcance é atravessada quicando (pisão), como no jogo.
        var fila = Plano(x => { for (int c = 10; c < 17; c++) Grade.Definir(x, c, 11, TileTypeEnum.Enemy); });
        Assert.True(BuscaAEstrela.Jogar(fila, PerfilJogador.Pequeno).Concluiu);

        // Num corredor de uma linha não há como saltar sobre o inimigo: a falha é INIMIGO.
        var corredor = Plano(x =>
        {
            for (int c = 0; c < 28; c++) Grade.Definir(x, c, 10, TileTypeEnum.Solid);
            for (int y = 0; y < 10; y++) Grade.Definir(x, 0, y, TileTypeEnum.Solid); // sem entrada por cima
            Grade.Definir(x, 14, 11, TileTypeEnum.Enemy);
        });
        var r = BuscaAEstrela.Jogar(corredor, PerfilJogador.Pequeno);
        Assert.False(r.Concluiu);
        Assert.Equal(CausaFalha.Inimigo, r.Causa);
    }

    [Fact]
    public void Mov_MarioGrandeQuebraTijoloPorBaixoPequenoNao()
    {
        // Uma plataforma de 3 de altura no fim; um teto contínuo sobre toda a aproximação impede o
        // salto até ela (e impede contorná-lo por cima). Só quem quebra o teto por baixo sobe.
        TileMap Montar(TileTypeEnum teto) => Plano(x =>
        {
            for (int c = 16; c < 28; c++) for (int y = 9; y < 12; y++) Grade.Definir(x, c, y, TileTypeEnum.Solid);
            for (int c = 0; c < 16; c++) Grade.Definir(x, c, 8, teto);
            // A busca parte de qualquer posição em pé da coluna 0: fecha a entrada por cima do teto.
            for (int y = 0; y < 8; y++) Grade.Definir(x, 0, y, TileTypeEnum.Solid);
        });

        Assert.True(BuscaAEstrela.Jogar(Montar(TileTypeEnum.Breakable), PerfilJogador.Grande).Concluiu);
        Assert.False(BuscaAEstrela.Jogar(Montar(TileTypeEnum.Breakable), PerfilJogador.Pequeno).Concluiu);
        Assert.False(BuscaAEstrela.Jogar(Montar(TileTypeEnum.Solid), PerfilJogador.Grande).Concluiu);
    }

    [Fact]
    public void Mov_NiveisDeReferenciaSaoCompletaveisPelosPerfisAlvo()
    {
        foreach (var (nome, mapa) in CenarioControlado.Referencia().Niveis)
            foreach (var perfil in CenarioControlado.Perfis)
                Assert.True(BuscaAEstrela.Jogar(mapa, perfil).Concluiu, $"{nome} não é completável por {perfil.Id}");
    }

    [Fact]
    public void Rep_CosturaEstreitaOBuracoEmVezDeTapar()
    {
        var r = CenarioControlado.Criar().Executar();
        var commit = Eventos(r.Tracer, "nivel.commit").Single();
        Assert.Equal("reparador-costura", S(commit.D, "reparador"));
        Assert.Equal(1, commit.D.GetProperty("tiles").GetInt32()); // buraco de 5 estreitado para 4 (alcance − 1)
        // O desafio continua: o nível final ainda tem um buraco, agora pulável.
        Assert.Contains(Enumerable.Range(0, r.Nivel!.Width), x => Grade.AlturaChao(r.Nivel, x) == 0);
        Assert.All(r.Metricas.ConclusaoPorPerfil.Values, Assert.True);
    }

    // ------------------------------------------------------------------ memória persistente (CP9)

    [Fact]
    public void Mem_SegundaExecucaoReusaIncompatibilidadesEVetos()
    {
        string arquivo = Path.Combine(Path.GetTempPath(), $"chunklink-mem-{Guid.NewGuid():N}.db");
        try
        {
            ResultadoExecucao primeira, segunda;
            using (var mem = new MemoriaEpisodica(arquivo, ttlExecucoes: 5)) primeira = CenarioControlado.Criar(memoria: mem).Executar();
            using (var mem = new MemoriaEpisodica(arquivo, ttlExecucoes: 5)) segunda = CenarioControlado.Criar(memoria: mem).Executar();

            Assert.Empty(Eventos(primeira.Tracer, "crenca+", "arquiteto").Where(x => S(x.D, "fonte") == "memória"));
            var lembradas = Eventos(segunda.Tracer, "crenca+", "arquiteto").Where(x => S(x.D, "fonte") == "memória").ToList();
            Assert.Contains(lembradas, x => S(x.D, "predicado") == "incompativel");
            Assert.Contains(lembradas, x => S(x.D, "predicado") == "vetado" && S(x.D, "chave") == "f-copia");
            Assert.True(segunda.Metricas.Sucesso);
            Assert.True(segunda.Metricas.Mensagens < primeira.Metricas.Mensagens,
                $"a memória deveria poupar mensagens ({segunda.Metricas.Mensagens} vs {primeira.Metricas.Mensagens})");
        }
        finally
        {
            File.Delete(arquivo);
        }
    }

    [Fact]
    public void Mem_EpisodioExpiraDepoisDoTtlSemReforco()
    {
        string arquivo = Path.Combine(Path.GetTempPath(), $"chunklink-ttl-{Guid.NewGuid():N}.db");
        try
        {
            using var mem = new MemoriaEpisodica(arquivo, ttlExecucoes: 2);
            mem.IniciarExecucao();
            mem.Lembrar("vetado", "abc", "N4", "Overworld");
            mem.IniciarExecucao();
            mem.IniciarExecucao();
            Assert.Single(mem.Carregar("Overworld"));   // execução 3: ainda dentro do TTL
            mem.IniciarExecucao();
            Assert.Empty(mem.Carregar("Overworld"));    // execução 4: expirou
        }
        finally
        {
            File.Delete(arquivo);
        }
    }
}
