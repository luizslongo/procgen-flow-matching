using System.Text.Json;
using c2_pcg.flowMatchingDataloader;
using ChunkLink.Agentes;
using ChunkLink.Ambiente;
using ChunkLink.Bdi;
using ChunkLink.Comunicacao;
using ChunkLink.Estilo;
using ChunkLink.Movimento;
using ChunkLink.Runtime;
using Xunit;

namespace ChunkLink.Tests;

// Testes do interpretador BDI, das decisões cognitivas observáveis do arquiteto e do curador e do
// cenário controlado completo (integração). As asserções leem o trace, a mesma evidência que o
// comando `chunklink depurar` exibe.
public class TestesCognicaoEIntegracao
{
    private static readonly Lazy<ResultadoExecucao> Cenario = new(() => CenarioControlado.Criar().Executar());

    private static IEnumerable<(EventoTrace E, JsonElement D)> Eventos(Tracer t, string tipo, string? agente = null) =>
        t.Eventos.Where(e => e.Tipo == tipo && (agente == null || e.Agente == agente))
            .Select(e => (e, JsonDocument.Parse(e.Dados).RootElement));

    private static string S(JsonElement d, string campo) => d.GetProperty(campo).ToString();

    // ------------------------------------------------------------------ interpretador BDI

    [Fact]
    public void Bdi_FalhaDeSubobjetivoAcionaPlanoTratador()
    {
        var tracer = new Tracer();
        long tick = 0;
        var crencas = new BaseCrencas("agente", tracer, () => tick);
        var motor = new MotorBdi("agente", crencas, tracer, () => tick);
        motor.Desejos.Add(new Desejo("d", 1, "a", c => !c.Tem("feito", "x")));
        motor.Planos.Add(Plano.Para("a", "plano_a", (_, _) => true, _ => CorpoA(crencas)));
        motor.Planos.Add(Plano.Para("b", "plano_b", (_, _) => true, _ => new Passo[] { new Falhar("motivo de teste") }));
        motor.Planos.Add(Plano.SeFalhar("b", "tratador_b", (_, _) => true, ctx => Tratar(crencas, ctx)));

        for (tick = 1; tick <= 3; tick++) motor.Ciclo();

        Assert.True(crencas.Tem("feito", "x"));
        Assert.Equal("motivo de teste", crencas.Valor<string>("tratado", "b"));
        Assert.Contains(Eventos(tracer, "plano.selecionado"), x => S(x.D, "plano") == "tratador_b" && x.D.GetProperty("tratador").GetBoolean());
        Assert.Single(Eventos(tracer, "intencao.concluida"));
    }

    private static IEnumerable<Passo> CorpoA(BaseCrencas c)
    {
        yield return new SubObjetivo("b");
        c.Definir("feito", "x", true, "ação");
    }

    private static IEnumerable<Passo> Tratar(BaseCrencas c, ContextoPlano ctx)
    {
        c.Definir("tratado", "b", ctx.MotivoFalha, "ação");
        yield break;
    }

    [Fact]
    public void Bdi_PoliticaDeReconsideracaoAbandonaObjetivoQuandoCrencaMuda()
    {
        var tracer = new Tracer();
        long tick = 0;
        var crencas = new BaseCrencas("agente", tracer, () => tick);
        var motor = new MotorBdi("agente", crencas, tracer, () => tick);
        motor.Desejos.Add(new Desejo("d", 1, "esperar", c => !c.Tem("desistiu", "x")));
        motor.Planos.Add(Plano.Para("esperar", "aguardar_sempre", (_, _) => true,
            _ => new Passo[] { new Aguardar("algo que nunca acontece", () => false) }));
        motor.Planos.Add(Plano.SeFalhar("esperar", "registrar_desistencia", (_, _) => true, ctx => Desistir(crencas)));
        motor.Politica = (i, c) => c.Tem("evidencia", "x") && i.Contem("esperar")
            ? new AbandonarObjetivo("esperar", "evidência nova torna a espera inútil") : null;

        tick = 1; motor.Ciclo();
        Assert.Empty(Eventos(tracer, "reconsideracao"));
        crencas.Definir("evidencia", "x", true, "percepção");
        tick = 2; motor.Ciclo();

        var r = Assert.Single(Eventos(tracer, "reconsideracao"));
        Assert.Contains("evidência nova", S(r.D, "motivo"));
        Assert.True(crencas.Tem("desistiu", "x"));
    }

    private static IEnumerable<Passo> Desistir(BaseCrencas c)
    {
        c.Definir("desistiu", "x", true, "ação");
        yield break;
    }

    // ------------------------------------------------------------------ arquiteto (cognitivo)

    [Fact]
    public void Arquiteto_InfereCulpaDaCosturaReconsideraERecua()
    {
        var t = Cenario.Value.Tracer;
        var culpa = Eventos(t, "crenca+", "arquiteto").First(x => S(x.D, "predicado") == "culpa_costura");
        Assert.Equal("inferência", S(culpa.D, "fonte"));
        Assert.Equal("2", S(culpa.D, "chave"));

        var reconsideracao = Eventos(t, "reconsideracao", "arquiteto").First();
        Assert.True(reconsideracao.E.Seq > culpa.E.Seq);
        Assert.Contains("abandonar !preencher", S(reconsideracao.D, "decisao"));

        var recuo = Eventos(t, "nivel.recuar").First();
        Assert.True(recuo.E.Seq > reconsideracao.E.Seq);
        Assert.Equal("a-baixo", S(recuo.D, "chunk_removido"));
        // A Contract Net não chega a ser aberta para o slot 2: a intenção de reparo foi abandonada.
        Assert.DoesNotContain(Eventos(t, "msg", "arquiteto"), x => S(x.D, "conversa").StartsWith("reparo:2"));
    }

    [Fact]
    public void Arquiteto_UsaConhecimentoAprendidoParaReplanejar()
    {
        var t = Cenario.Value.Tracer;
        Assert.Contains(Eventos(t, "crenca+", "arquiteto"), x => S(x.D, "predicado") == "exigencia_saida" && S(x.D, "valor") == "alta");
        // Depois do recuo, o slot 1 recebe a escada (saída alta) que resolve os muros do slot 2.
        var fixacoes = Eventos(t, "nivel.fixar").Select(x => (Slot: x.D.GetProperty("slot").GetInt32(), Chunk: S(x.D, "chunk"))).ToList();
        Assert.Equal(("a-baixo"), fixacoes.First(f => f.Slot == 1).Chunk);
        Assert.Equal(("d-escada"), fixacoes.Last(f => f.Slot == 1).Chunk);
        Assert.StartsWith("b-muro", fixacoes.First(f => f.Slot == 2).Chunk);
    }

    // ------------------------------------------------------------------ curador (cognitivo)

    [Fact]
    public void Curador_EmiteRestricaoProativaAntesDeSerConsultado()
    {
        var t = Cenario.Value.Tracer;
        var restricao = Eventos(t, "msg", "curador").First(x => S(x.D, "conversa") == "restricao:4");
        var primeiroPedidoSlot4 = Eventos(t, "msg", "arquiteto").First(x => S(x.D, "conversa").StartsWith("estilo:s4"));
        Assert.True(restricao.E.Seq < primeiroPedidoSlot4.E.Seq);
        Assert.Contains(Eventos(t, "crenca+", "arquiteto"), x => S(x.D, "predicado") == "restricao" && S(x.D, "chave") == "4");
    }

    [Fact]
    public void Curador_ConcedeESuspendeDesejoDeEstiloAposImpasses()
    {
        var tracer = new Tracer();
        var bus = new MessageBus(tracer);
        var mundo = new Mundo
        {
            Bus = bus, Tracer = tracer, Relogio = new Relogio(),
            Nivel = new Nivel(4, BiomeTypeEnum.Overworld, bus, new RegrasNivel(40, 10, 0.10, null)), Pool = CenarioControlado.Pool(),
            Estilo = new ConhecimentoEstilo(CenarioControlado.Referencia()), P = new Parametros(),
            PerfisAlvo = CenarioControlado.Perfis,
        };
        var arq = mundo.Bus.Registrar(Mundo.IdArquiteto, Papel.Arquiteto);
        var curador = new CuratorAgent(mundo);
        var denso = Grade.Criar(28);
        for (int y = 3; y < 12; y++) for (int x = 0; x < 28; x += 2) Grade.Definir(denso, x, y, TileTypeEnum.Breakable);
        for (int x = 0; x < 28; x++) { Grade.Definir(denso, x, 12, TileTypeEnum.Solid); Grade.Definir(denso, x, 13, TileTypeEnum.Solid); }

        var respostas = new List<RespostaEstilo>();
        for (int pedido = 1; pedido <= 3; pedido++)
        {
            mundo.Relogio.Tick++;
            mundo.Bus.Enviar(arq, Mundo.IdCurador, Performativa.Request,
                new PedidoEstilo(1, 4, new[] { new CandidatoEstilo("denso", denso) }, null), $"estilo:{pedido}", mundo.Relogio.Tick);
            curador.Ciclo();
            mundo.Relogio.Tick++;
            curador.Ciclo();
            respostas.AddRange(mundo.Bus.Retirar(arq).Select(m => (RespostaEstilo)m.Conteudo));
        }

        Assert.False(respostas[0].Pareceres[0].Aceitavel);
        Assert.False(respostas[1].Pareceres[0].Aceitavel);
        Assert.True(respostas[1].LimiarUsado > respostas[0].LimiarUsado); // concessão gradual
        Assert.True(respostas[2].EstiloRelaxado);
        Assert.True(respostas[2].Pareceres[0].Aceitavel);
        Assert.Contains(Eventos(tracer, "reconsideracao", "curador"), x => S(x.D, "decisao").Contains("parecer_original"));
    }

    [Fact]
    public void RS04_CuradorVetaChunkCopiadoDeOriginal()
    {
        var r = Cenario.Value;
        Assert.Contains(Eventos(r.Tracer, "crenca+", "curador"), x => S(x.D, "predicado") == "vetado" && S(x.D, "chave") == "f-copia" && S(x.D, "valor") == "N4");
        Assert.DoesNotContain("f-copia", r.Metricas.Sequencia);
    }

    // ------------------------------------------------------------------ integração

    [Fact]
    public void RF04_RF08_CenarioControladoMontaNivelJogavelPorTodosOsPerfis()
    {
        var m = Cenario.Value.Metricas;
        Assert.True(m.Sucesso, m.Motivo);
        Assert.All(m.ConclusaoPorPerfil.Values, Assert.True);
        Assert.Equal(4, m.Sequencia.Count);
        Assert.Equal(m.Sequencia.Count, m.Sequencia.Distinct().Count()); // N2: sem repetição
        Assert.True(m.TilesEditados <= CenarioControlado.Parametros.EMax);
    }

    [Fact]
    public void RF05_RF06_ContractNetComReservaEdicaoProvisoriaRevalidacaoECommit()
    {
        var t = Cenario.Value.Tracer;
        var propostas = Eventos(t, "cn.propostas").First();
        Assert.Contains("reparador-costura", propostas.D.GetProperty("propostas").ToString());
        Assert.Contains(Eventos(t, "msg", "reparador-folga"), x => S(x.D, "performativa") == "Refuse");
        var reserva = Eventos(t, "nivel.reserva").First();
        var commit = Eventos(t, "nivel.commit").First();
        Assert.True(reserva.E.Seq < commit.E.Seq);
        Assert.Equal("reparador-costura", S(commit.D, "reparador"));
        // A aprovação do curador precede o aceite da proposta (N5).
        var agree = Eventos(t, "msg", "curador").First(x => S(x.D, "performativa") == "Agree");
        var accept = Eventos(t, "msg", "arquiteto").First(x => S(x.D, "performativa") == "AcceptProposal");
        Assert.True(agree.E.Seq < accept.E.Seq);
    }

    [Fact]
    public void RF05_PropostaTardiaNaoEhConsideradaERS02_CuradorRecusaAcimaDoLimite()
    {
        var lenta = Reparos.Costura with { AtrasoResposta = 5 };
        var r = CenarioControlado.Criar(new[] { lenta, Reparos.Folga, Reparos.Suporte }).Executar();

        Assert.Contains(Eventos(r.Tracer, "cn.descartada"), x => S(x.D, "reparador") == "reparador-costura");
        Assert.DoesNotContain(Eventos(r.Tracer, "nivel.commit"), x => S(x.D, "reparador") == "reparador-costura");
        // A pipeline do TCC propõe 11 tiles para o buraco; o curador recusa por N1 (e_max = 10).
        Assert.Contains(Eventos(r.Tracer, "msg", "curador"), x => S(x.D, "performativa") == "Refuse" && x.D.GetProperty("conteudo").GetProperty("Norma").GetString() == "N1");
    }

    [Fact]
    public void RF06_RevalidacaoFalhaFazRollbackETentaAProximaProposta()
    {
        // Reparador "cosmético": a proposta mais barata (1 tile) é aprovada pelo curador, mas não fecha o buraco.
        var cosmetico = new EspecificacaoReparador("reparador-cosmetico", new HashSet<CausaFalha> { CausaFalha.Buraco },
            (_, c) => new Diff(new[] { new AlteracaoTile(0, 0, Grade.Obter(c, 0, 0), TileTypeEnum.Coin) }, "moeda decorativa"));
        var r = CenarioControlado.Criar(new[] { cosmetico, Reparos.Costura, Reparos.Folga, Reparos.Suporte }).Executar();

        Assert.True(r.Metricas.Sucesso);
        var rollback = Assert.Single(Eventos(r.Tracer, "nivel.rollback"));
        Assert.Equal("reparador-cosmetico", S(rollback.D, "reparador"));
        var commit = Assert.Single(Eventos(r.Tracer, "nivel.commit"));
        Assert.Equal("reparador-costura", S(commit.D, "reparador"));
        Assert.True(rollback.E.Seq < commit.E.Seq);
        // A edição revertida não conta no nível: só o tile que estreita o buraco foi efetivado.
        Assert.Equal(1, r.Metricas.TilesEditados);
    }

    [Fact]
    public void RF07_PoolInsuficienteTerminaComFalhaExplicita()
    {
        var pool = new PoolChunks(CenarioControlado.Pool().Chunks.Where(c => c.Id is "b-muro1" or "b-muro2" or "e-buraco1"));
        var sistema = new SistemaChunkLink(pool, CenarioControlado.Referencia(), CenarioControlado.Parametros with { Slots = 3 },
            CenarioControlado.Perfis, BiomeTypeEnum.Overworld, Array.Empty<EspecificacaoReparador>());
        var r = sistema.Executar();
        Assert.False(r.Metricas.Sucesso);
        Assert.False(string.IsNullOrWhiteSpace(r.Metricas.Motivo));
        Assert.True(r.Metricas.Ticks < CenarioControlado.Parametros.MaxTicks);
        Assert.Contains(Eventos(r.Tracer, "crenca+", "arquiteto"), x => S(x.D, "predicado") == "nivel_falhou");
    }

    [Fact]
    public void RNF01_ExecucaoReproduzivel()
    {
        var a = CenarioControlado.Criar().Executar();
        var b = CenarioControlado.Criar().Executar();
        Assert.Equal(a.Metricas.AssinaturaTrace, b.Metricas.AssinaturaTrace);
        Assert.Equal(a.Metricas.Sequencia, b.Metricas.Sequencia);
        Assert.Equal(Grade.ParaAscii(a.Nivel!), Grade.ParaAscii(b.Nivel!));
    }

    [Fact]
    public void RNF03_TraceJsonlTemUmaLinhaPorEventoComAgenteETipo()
    {
        var r = Cenario.Value;
        string arquivo = Path.Combine(Path.GetTempPath(), "chunklink-trace-" + Guid.NewGuid().ToString("N") + ".jsonl");
        try
        {
            r.Tracer.SalvarJsonl(arquivo);
            var linhas = File.ReadAllLines(arquivo);
            Assert.Equal(r.Tracer.Eventos.Count, linhas.Length);
            foreach (var l in linhas)
            {
                var d = JsonDocument.Parse(l).RootElement;
                Assert.False(string.IsNullOrEmpty(d.GetProperty("agente").GetString()));
                Assert.False(string.IsNullOrEmpty(d.GetProperty("tipo").GetString()));
            }
            // Toda mensagem da conversa de reparo pode ser reconstruída pelo identificador de conversa.
            Assert.True(r.Tracer.DoTipo("msg").Count(e => e.Dados.Contains("\"conversa\":\"reparo:")) >= 4);
        }
        finally
        {
            File.Delete(arquivo);
        }
    }

    [Fact]
    public void Baselines_ExecutamSobreOMesmoPool()
    {
        var estilo = new ConhecimentoEstilo(CenarioControlado.Referencia());
        var (e0, nivel0) = Baselines.E0(CenarioControlado.Pool(), estilo, CenarioControlado.Perfis, 4, 42);
        Assert.Equal(4 * 28, nivel0.Width);
        Assert.Equal(e0.Sequencia, Baselines.E0(CenarioControlado.Pool(), estilo, CenarioControlado.Perfis, 4, 42).Metricas.Sequencia);
        var (e1, _) = Baselines.E1(CenarioControlado.Pool(), estilo, CenarioControlado.Perfis, 4);
        Assert.Equal(0, e1.Mensagens); // sem agentes, sem mensagens
    }
}

public class TestesAblacao
{
    [Fact]
    public void Ablacao_SemCognicaoNaoHaInferenciaNemReconsideracao()
    {
        var sistema = new SistemaChunkLink(CenarioControlado.Pool(), CenarioControlado.Referencia(),
            CenarioControlado.Parametros with { Cognicao = false }, CenarioControlado.Perfis, BiomeTypeEnum.Overworld);
        var r = sistema.Executar();
        Assert.Empty(r.Tracer.DoTipo("reconsideracao"));
        Assert.DoesNotContain(r.Tracer.DoTipo("crenca+"), e => e.Dados.Contains("\"culpa_costura\""));
        // Sem atribuir culpa à costura, o arquiteto tenta reparar os muros do slot 2 em vez de recuar.
        Assert.Contains(r.Tracer.DoTipo("msg"), e => e.Agente == "arquiteto" && e.Dados.Contains("\"conversa\":\"reparo:2"));
    }
}
