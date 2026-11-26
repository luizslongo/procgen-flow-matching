using c2_pcg.flowMatchingDataloader;
using ChunkLink.Ambiente;
using ChunkLink.Comunicacao;
using ChunkLink.Estilo;
using ChunkLink.Movimento;
using ChunkLink.Runtime;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace ChunkLink.Tests;

// Testes de unidade do ambiente, do modelo de movimento, das normas no artefato Nível e da comunicação.
// O prefixo do nome é o identificador do requisito do DSM1 (modelos/modelo.yaml) que o teste evidencia.
public class TestesAmbienteEMovimento
{
    private static TileMap Chunk(params (int Linha, string Conteudo)[] linhas)
    {
        var rows = Enumerable.Range(0, 14).Select(y => y >= 12 ? new string('X', 28) : new string('-', 28)).ToArray();
        foreach (var (l, c) in linhas) rows[l] = c;
        return Grade.DeAscii(rows, BiomeTypeEnum.Overworld);
    }

    [Fact]
    public void RF01_Pool_RejeitaChunkInvalido()
    {
        var linhas = Enumerable.Repeat(new string('-', 28), 14).ToArray();
        linhas[5] = "-----#----------------------";
        Assert.Throws<FormatException>(() => Grade.DeAscii(linhas, BiomeTypeEnum.Overworld));

        var estreito = Grade.Criar(20);
        Assert.Throws<FormatException>(() => new PoolChunks(new[] { new Chunk("x", estreito, OrigemChunk.Bruto) }));

        var original = new Chunk("orig", Grade.Criar(28), OrigemChunk.VglcOriginal);
        var e = Assert.Throws<FormatException>(() => new PoolChunks(new[] { original }));
        Assert.Contains("N4", e.Message);
    }

    [Fact]
    public void RF01_Pool_CongeladoDetectaAlteracaoPorHash()
    {
        string dir = Path.Combine(Path.GetTempPath(), "chunklink-pool-" + Guid.NewGuid().ToString("N"));
        try
        {
            CenarioControlado.Pool().Salvar(dir);
            Assert.Equal(7, PoolChunks.Carregar(dir).Chunks.Count);

            var arquivo = Path.Combine(dir, "a-baixo.txt");
            var linhas = File.ReadAllLines(arquivo);
            linhas[2] = "o" + linhas[2][1..];
            File.WriteAllLines(arquivo, linhas);
            var e = Assert.Throws<FormatException>(() => PoolChunks.Carregar(dir));
            Assert.Contains("hash", e.Message);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void RF02_Jogador_ReportaBuracoIntransponivel()
    {
        var buraco = Chunk((12, "XXXXXXXXXX-----XXXXXXXXXXXXX"), (13, "XXXXXXXXXX-----XXXXXXXXXXXXX"));
        foreach (var perfil in new[] { PerfilJogador.Pequeno, PerfilJogador.Grande })
        {
            var r = BuscaAEstrela.Jogar(buraco, perfil);
            Assert.False(r.Concluiu);
            Assert.Equal(CausaFalha.Buraco, r.Causa);
            Assert.Equal(10, r.ColunaFalha);
        }
    }

    [Fact]
    public void RF02_BuracoDentroDoAlcanceEhTransponivel()
    {
        var buraco = Chunk((12, "XXXXXXXXXX---XXXXXXXXXXXXXXX"), (13, "XXXXXXXXXX---XXXXXXXXXXXXXXX"));
        Assert.True(BuscaAEstrela.Jogar(buraco, PerfilJogador.Pequeno).Concluiu);
    }

    [Fact]
    public void RF02_PerfisHeterogeneos_SoOMarioGrandeFalhaNoTeto()
    {
        var teto = Chunk(Enumerable.Range(3, 8).Select(y => (y, "--------XXXXXXXXX-----------")).ToArray());
        Assert.True(BuscaAEstrela.Jogar(teto, PerfilJogador.Pequeno).Concluiu);
        var grande = BuscaAEstrela.Jogar(teto, PerfilJogador.Grande);
        Assert.False(grande.Concluiu);
        Assert.Equal(CausaFalha.Teto, grande.Causa);
    }

    [Fact]
    public void RF02_JogadorSaltaSobreInimigoMasNaoAtravessaParede()
    {
        var inimigo = Chunk((11, "--------------------E-------"));
        Assert.True(BuscaAEstrela.Jogar(inimigo, PerfilJogador.Pequeno).Concluiu);

        var parede = Chunk(Enumerable.Range(4, 8).Select(y => (y, "--------------XX------------")).ToArray());
        var r = BuscaAEstrela.Jogar(parede, PerfilJogador.Pequeno);
        Assert.False(r.Concluiu);
        Assert.Equal(CausaFalha.Parede, r.Causa);
    }

    [Fact]
    public void RF02_TrechoAbertoAceitaSaidaPorSaltoNaBordaDireita()
    {
        var bordaComBuraco = Chunk((12, "XXXXXXXXXXXXXXXXXXXXXXXXXX--"), (13, "XXXXXXXXXXXXXXXXXXXXXXXXXX--"));
        Assert.False(BuscaAEstrela.Jogar(bordaComBuraco, PerfilJogador.Pequeno, trechoAberto: false).Concluiu);
        Assert.True(BuscaAEstrela.Jogar(bordaComBuraco, PerfilJogador.Pequeno, trechoAberto: true).Concluiu);
    }

    [Fact]
    public void RNF04_OrcamentoDaBuscaEsgotadoViraInconclusivo()
    {
        var longo = Grade.Concatenar(Enumerable.Repeat(Chunk(), 6).ToList());
        var r = BuscaAEstrela.Jogar(longo, PerfilJogador.Pequeno with { OrcamentoNos = 5 });
        Assert.False(r.Concluiu);
        Assert.Equal(CausaFalha.Inconclusivo, r.Causa);
    }

    [Fact]
    public void RS01_ReservaSobrepostaRecusada_AdjacenteAceita()
    {
        var t = new TabelaReservas();
        t.Reservar(new Reserva("reparador-a", 10, 20));
        Assert.Throws<ConflitoReserva>(() => t.Reservar(new Reserva("reparador-b", 15, 25)));
        t.Reservar(new Reserva("reparador-b", 20, 30)); // intervalo meio-aberto: [10,20) e [20,30) não se sobrepõem
        Assert.Equal(2, t.Ativas.Count);
        t.Liberar("reparador-a");
        t.Reservar(new Reserva("reparador-c", 12, 18));
    }

    // Nível de teste com credenciais emitidas pelo barramento para cada papel.
    internal static (Nivel Nivel, Credencial Arq, Credencial Cur, Credencial Rep, MessageBus Bus) NivelDeTeste(
        int slots = 2, Func<TileMap, double>? distanciaOriginais = null)
    {
        var bus = new MessageBus(new Tracer());
        var arq = bus.Registrar("arquiteto", Papel.Arquiteto);
        var cur = bus.Registrar("curador", Papel.Curador);
        var rep = bus.Registrar("reparador-costura", Papel.Reparador);
        return (new Nivel(slots, BiomeTypeEnum.Overworld, bus, new RegrasNivel(40, 10, 0.10, distanciaOriginais)), arq, cur, rep, bus);
    }

    internal static void FixarComAceite(Nivel n, Credencial arq, Credencial cur, int slot, Chunk c) =>
        n.Fixar(arq, slot, c, n.RegistrarAceite(cur, slot, c.Id, 1).Id);

    [Fact]
    public void RS03_CommitSemAprovacaoValidaEhNegadoENaoAlteraNivel()
    {
        var pool = CenarioControlado.Pool();
        var (nivel, arq, cur, rep, _) = NivelDeTeste();
        FixarComAceite(nivel, arq, cur, 1, pool.Obter("a-baixo"));
        var chunk = pool.Obter("e-buraco1");
        var copia = Grade.Clonar(chunk.Mapa);
        SetChao(copia);
        var diff = Diff.Entre(chunk.Mapa, copia, "teste");
        var edicao = nivel.AbrirEdicao(rep, "reparo:2:1", 2, chunk, diff);

        Assert.Equal("N5", Assert.Throws<ViolacaoNorma>(() => nivel.Commit(arq, edicao, 999)).Norma);
        var outroDiff = nivel.RegistrarAprovacao(cur, "reparo:2:1", 2, chunk.Id, "hash-de-outro-diff", 1);
        Assert.Equal("N5", Assert.Throws<ViolacaoNorma>(() => nivel.Commit(arq, edicao, outroDiff.Id)).Norma);
        var outraConversa = nivel.RegistrarAprovacao(cur, "reparo:2:9", 2, chunk.Id, diff.Hash, 1);
        Assert.Equal("N5", Assert.Throws<ViolacaoNorma>(() => nivel.Commit(arq, edicao, outraConversa.Id)).Norma);
        // Só o curador registra aprovações; só o arquiteto efetiva.
        Assert.Equal("PAPEL", Assert.Throws<ViolacaoNorma>(() => nivel.RegistrarAprovacao(rep, "reparo:2:1", 2, chunk.Id, diff.Hash, 1)).Norma);
        var valida = nivel.RegistrarAprovacao(cur, "reparo:2:1", 2, chunk.Id, diff.Hash, 1);
        Assert.Equal("PAPEL", Assert.Throws<ViolacaoNorma>(() => nivel.Commit(rep, edicao, valida.Id)).Norma);
        Assert.Null(nivel.ChunkNoSlot(2));

        nivel.Rollback(arq, edicao);
        Assert.Null(nivel.ChunkNoSlot(2));
        Assert.Empty(nivel.Reservas.Ativas);
        Assert.Equal(0, nivel.TilesEditados);
    }

    private static void SetChao(TileMap m)
    {
        for (int x = 10; x < 15; x++) Grade.Definir(m, x, 13, TileTypeEnum.Solid);
    }

    [Fact]
    public void RF06_CommitComAprovacaoAplicaDiffEContaTiles()
    {
        var pool = CenarioControlado.Pool();
        var (nivel, arq, cur, rep, _) = NivelDeTeste();
        FixarComAceite(nivel, arq, cur, 1, pool.Obter("a-baixo"));
        var chunk = pool.Obter("e-buraco1");
        var copia = Grade.Clonar(chunk.Mapa);
        SetChao(copia);
        var diff = Diff.Entre(chunk.Mapa, copia, "ponte");
        var edicao = nivel.AbrirEdicao(rep, "reparo:2:1", 2, chunk, diff);
        Assert.Single(nivel.Reservas.Ativas);

        nivel.Commit(arq, edicao, nivel.RegistrarAprovacao(cur, "reparo:2:1", 2, chunk.Id, diff.Hash, 3).Id);
        Assert.Equal("e-buraco1", nivel.ChunkNoSlot(2)!.Id);
        Assert.Equal(5, nivel.TilesEditados);
        Assert.Empty(nivel.Reservas.Ativas);
        Assert.True(BuscaAEstrela.Jogar(nivel.Completo(), PerfilJogador.Pequeno).Concluiu);
    }

    [Fact]
    public void RNF03_BarramentoRejeitaDestinatarioDesconhecidoERegistraNoTrace()
    {
        var tracer = new Tracer();
        var bus = new MessageBus(tracer);
        var arq = bus.Registrar("arquiteto", Papel.Arquiteto);
        Assert.Throws<DestinatarioDesconhecido>(() => bus.Enviar(arq, "inexistente", Performativa.Inform, new SlotFechado(1, "x", 0), "c1", 1));
        bus.Enviar(arq, "arquiteto", Performativa.Inform, new SlotFechado(1, "x", 0), "c2", 1);
        var m = Assert.Single(bus.Retirar(arq));
        Assert.Equal("c2", m.Conversa);
        Assert.Equal("arquiteto", m.Remetente);
        Assert.Single(tracer.DoTipo("msg"));
    }

    [Fact]
    public void P3_ContractNetRejeitaTransicaoInvalida()
    {
        var c = new ConversaContractNet();
        Assert.Throws<ViolacaoProtocolo>(() => c.Avancar(Performativa.AcceptProposal));
        c.Avancar(Performativa.Cfp);
        c.Avancar(Performativa.Propose);
        Assert.Throws<ViolacaoProtocolo>(() => c.Avancar(Performativa.Inform));
        c.Avancar(Performativa.AcceptProposal);
        Assert.Equal(ConversaContractNet.Estado.Concluido, c.Avancar(Performativa.Inform));
    }

    [Fact]
    public void RS04_CopiaDeOriginalTemDistanciaEstruturalAbaixoDoLimiar()
    {
        var estilo = new ConhecimentoEstilo(CenarioControlado.Referencia());
        var pool = CenarioControlado.Pool();
        Assert.True(estilo.MenorDistanciaAOriginais(pool.Obter("f-copia").Mapa) < 0.10);
        Assert.True(estilo.MenorDistanciaAOriginais(pool.Obter("a-baixo").Mapa) > 0.10);
        Assert.True(estilo.MenorDistanciaAOriginais(pool.Obter("d-escada").Mapa) > 0.10);
    }

    [Fact]
    public void RF11_EstiloDosOriginaisFicaDentroDaFaixaDeReferencia()
    {
        var corpus = CenarioControlado.Referencia();
        var estilo = new ConhecimentoEstilo(corpus);
        foreach (var (_, mapa) in corpus.Niveis)
        {
            for (int i = 0; i < 3; i++)
            {
                var pos = i == 0 ? PosicaoNoNivel.Inicio : i == 2 ? PosicaoNoNivel.Fim : PosicaoNoNivel.Meio;
                Assert.True(estilo.Avaliar(Grade.Janela(mapa, i * 28, 28), pos, null).Distancia <= 1.5);
            }
            Assert.True(estilo.KlPadroes2x2(mapa) < 0.5);
        }
        var ruido = Grade.Criar(84);
        var rng = new Random(1);
        for (int i = 0; i < ruido.Tiles.Length; i++) ruido.Tiles[i] = (TileTypeEnum)rng.Next(1, 14);
        Assert.True(estilo.KlPadroes2x2(ruido) > 2.0);
    }

    [Fact]
    public void RF09_ParCegoTemMesmaEscalaEGabaritoSeparado()
    {
        string dir = Path.Combine(Path.GetTempPath(), "chunklink-render-" + Guid.NewGuid().ToString("N"));
        string sprites = Path.Combine(dir, "sprites");
        Directory.CreateDirectory(sprites);
        try
        {
            foreach (var t in Enum.GetValues<TileTypeEnum>().Where(t => t != TileTypeEnum.Error))
            {
                using var img = new Image<Rgba32>(16, 16, new Rgba32((byte)(t.GetHashCode() * 17), 80, 160));
                img.Save(Path.Combine(sprites, t + ".png"));
            }
            var corpus = CenarioControlado.Referencia();
            var montado = Grade.Concatenar(CenarioControlado.Pool().Chunks.Take(3).Select(c => c.Mapa).ToList());
            string par = Renderizacao.RenderizarParCego(montado, corpus.Niveis[0].Mapa, sprites, Path.Combine(dir, "teste-cego"), "01", 7);

            Assert.Equal("par-01.png", Path.GetFileName(par));
            using var imagem = Image.Load<Rgba32>(par);
            Assert.Equal(84 * 16, imagem.Width);
            Assert.Equal(14 * 16 * 2 + 16, imagem.Height);
            var gabarito = File.ReadAllLines(Path.Combine(dir, "teste-cego", "gabarito.csv"));
            Assert.Matches("^01;(cima|baixo)$", Assert.Single(gabarito));
            Assert.DoesNotContain("original", Path.GetFileName(par));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
