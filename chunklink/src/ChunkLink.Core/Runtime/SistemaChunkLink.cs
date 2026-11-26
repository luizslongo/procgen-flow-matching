using c2_pcg.flowMatchingDataloader;
using c2_pcg.flowMatchingEval;
using ChunkLink.Agentes;
using ChunkLink.Ambiente;
using ChunkLink.Comunicacao;
using ChunkLink.Estilo;
using ChunkLink.Movimento;

namespace ChunkLink.Runtime;

// Métricas de uma execução (M1, M4, M5, M6, M7, M12, M13 do DSM1).
public sealed record Metricas(
    bool Sucesso, string? Motivo, int Slots, IReadOnlyList<string> Sequencia, int TilesEditados, int Backtracks,
    long Mensagens, long Ticks, IReadOnlyDictionary<string, bool> ConclusaoPorPerfil, double? KlPadroes2x2,
    int CosturasVisiveis, double MenorDistanciaAOriginais, string AssinaturaTrace);

public sealed record ResultadoExecucao(Metricas Metricas, TileMap? Nivel, Tracer Tracer);

// Runtime didático: um processo, escalonamento round-robin determinístico, tempo lógico.
// A cada tick, cada agente (em ordem fixa) percebe sua caixa postal, delibera e age.
public sealed class SistemaChunkLink
{
    private readonly List<Agente> _agentes = new();

    public SistemaChunkLink(
        PoolChunks pool, CorpusReferencia corpus, Parametros p, IReadOnlyList<PerfilJogador> perfisAlvo,
        BiomeTypeEnum bioma, IReadOnlyList<EspecificacaoReparador>? reparadores = null,
        Memoria.MemoriaEpisodica? memoria = null, InjetorFalhas? falhas = null)
    {
        memoria?.IniciarExecucao();
        var tracer = new Tracer();
        var bus = new MessageBus(tracer) { Falhas = falhas };
        var estilo = new ConhecimentoEstilo(corpus);
        Mundo = new Mundo
        {
            Bus = bus, Tracer = tracer, Relogio = new Relogio(),
            Nivel = new Nivel(p.Slots, bioma, bus, new RegrasNivel(p.EMax, p.EMaxChunk, p.TauCopia, estilo.MenorDistanciaAOriginais)),
            Pool = pool, Estilo = estilo, P = p, PerfisAlvo = perfisAlvo, Memoria = memoria,
        };
        Arquiteto = new LevelArchitectAgent(Mundo);
        Curador = new CuratorAgent(Mundo);
        _agentes.Add(Arquiteto);
        _agentes.Add(Curador);
        foreach (var perfil in perfisAlvo) _agentes.Add(new PlaytesterAgent(perfil, Mundo));
        foreach (var spec in reparadores ?? Reparos.Padrao)
        {
            _agentes.Add(new RepairAgent(spec, Mundo));
            Mundo.Reparadores.Add(spec.Id);
        }
    }

    public Mundo Mundo { get; }
    public LevelArchitectAgent Arquiteto { get; }
    public CuratorAgent Curador { get; }
    public IReadOnlyList<Agente> Agentes => _agentes;

    // A execução formata números e porcentagens no trace sempre na cultura invariante: a assinatura
    // do trace (RNF01) não pode depender do idioma da máquina (pt-BR escreve 0,41; en-US, 0.41).
    public ResultadoExecucao Executar()
    {
        var culturaAnterior = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        try
        {
            return ExecutarNaCulturaInvariante();
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = culturaAnterior;
        }
    }

    private ResultadoExecucao ExecutarNaCulturaInvariante()
    {
        long tick = 0;
        while (!Arquiteto.Terminou && tick < Mundo.P.MaxTicks)
        {
            Mundo.Relogio.Tick = ++tick;
            foreach (var a in _agentes) a.Ciclo();
        }
        Arquiteto.Memorizar();
        TileMap? nivel = Arquiteto.Sucesso ? Mundo.Nivel.Completo() : null;
        var metricas = Avaliacao.Medir(nivel, Mundo.Estilo, Mundo.PerfisAlvo, Arquiteto.Sucesso,
            Arquiteto.Sucesso ? null : Arquiteto.MotivoFalha ?? $"limite de {Mundo.P.MaxTicks} ticks",
            Mundo.Nivel.Slots, Enumerable.Range(1, Mundo.Nivel.Slots).Select(k => Mundo.Nivel.ChunkNoSlot(k)?.Id ?? "—").ToList(),
            Mundo.Nivel.TilesEditados, Arquiteto.TotalBacktracks, Mundo.Bus.Enviadas, tick, Mundo.Tracer);
        return new ResultadoExecucao(metricas, nivel, Mundo.Tracer);
    }
}

public static class Avaliacao
{
    public static Metricas Medir(
        TileMap? nivel, ConhecimentoEstilo estilo, IReadOnlyList<PerfilJogador> perfis, bool sucesso, string? motivo, int slots,
        IReadOnlyList<string> sequencia, int tilesEditados, int backtracks, long mensagens, long ticks, Tracer? tracer)
    {
        var conclusao = new Dictionary<string, bool>();
        double? kl = null;
        int costuras = 0;
        double copia = 1;
        if (nivel != null)
        {
            foreach (var p in perfis) conclusao[p.Id] = BuscaAEstrela.Jogar(nivel, p).Concluiu;
            kl = Math.Round(estilo.KlPadroes2x2(nivel), 4);
            for (int k = 1; k < nivel.Width / Grade.LarguraChunk; k++)
            {
                var esquerda = Grade.Janela(nivel, (k - 1) * Grade.LarguraChunk, Grade.LarguraChunk);
                var direita = Grade.Janela(nivel, k * Grade.LarguraChunk, Grade.LarguraChunk);
                if (ConhecimentoEstilo.Costura(esquerda, direita) > estilo.LimiarCostura) costuras++;
                copia = Math.Min(copia, estilo.MenorDistanciaAOriginais(esquerda));
            }
            copia = Math.Min(copia, estilo.MenorDistanciaAOriginais(Grade.Janela(nivel, nivel.Width - Grade.LarguraChunk, Grade.LarguraChunk)));
        }
        return new Metricas(sucesso, motivo, slots, sequencia, tilesEditados, backtracks, mensagens, ticks, conclusao, kl, costuras,
            Math.Round(copia, 4), tracer?.Assinatura() ?? "—");
    }
}

// Baselines do experimento (DSM1, Seção 7): sem agentes, sem negociação, sem critério de estilo.
public static class Baselines
{
    // E0: concatenação aleatória (semente fixa) de chunks reparados pela pipeline completa do TCC.
    public static (Metricas Metricas, TileMap Nivel) E0(PoolChunks pool, ConhecimentoEstilo estilo, IReadOnlyList<PerfilJogador> perfis, int slots, int semente)
    {
        var rng = new Random(semente);
        var escolhidos = pool.Chunks.OrderBy(_ => rng.Next()).Take(slots).ToList();
        int editados = 0;
        var partes = new List<TileMap>();
        foreach (var c in escolhidos)
        {
            var reparado = ChunkStructureRepair.RepairAll(c.Mapa, new ChunkStructureRepairConfig());
            editados += Grade.Diferencas(c.Mapa, reparado);
            partes.Add(reparado);
        }
        var nivel = Grade.Concatenar(partes);
        bool ok = perfis.All(p => BuscaAEstrela.Jogar(nivel, p).Concluiu);
        return (Avaliacao.Medir(nivel, estilo, perfis, ok, ok ? null : "nível não concluído por todos os perfis", slots,
            escolhidos.Select(c => c.Id).ToList(), editados, 0, 0, 0, null), nivel);
    }

    // E1: centralizado guloso — ranking por playtest, o primeiro candidato (reparado pelo TCC) cujo
    // prefixo todos os perfis concluem; sem curador, sem Contract Net, sem retrocesso.
    public static (Metricas Metricas, TileMap? Nivel) E1(PoolChunks pool, ConhecimentoEstilo estilo, IReadOnlyList<PerfilJogador> perfis, int slots)
    {
        var ranking = pool.Chunks
            .OrderByDescending(c => perfis.Average(p => BuscaAEstrela.Jogar(c.Mapa, p, trechoAberto: true).Progresso))
            .ThenBy(c => c.Id, StringComparer.Ordinal).ToList();
        var partes = new List<TileMap>();
        var usados = new List<string>();
        int editados = 0;
        for (int k = 1; k <= slots; k++)
        {
            bool fechado = false;
            foreach (var c in ranking.Where(c => !usados.Contains(c.Id)))
            {
                var reparado = ChunkStructureRepair.RepairAll(c.Mapa, new ChunkStructureRepairConfig());
                var prefixo = Grade.Concatenar(partes.Append(reparado).ToList());
                if (!perfis.All(p => BuscaAEstrela.Jogar(prefixo, p, trechoAberto: k < slots).Concluiu)) continue;
                partes.Add(reparado);
                usados.Add(c.Id);
                editados += Grade.Diferencas(c.Mapa, reparado);
                fechado = true;
                break;
            }
            if (!fechado)
                return (Avaliacao.Medir(null, estilo, perfis, false, $"nenhum candidato serve no slot {k}", slots, usados, editados, 0, 0, 0, null), null);
        }
        var nivel = Grade.Concatenar(partes);
        return (Avaliacao.Medir(nivel, estilo, perfis, true, null, slots, usados, editados, 0, 0, 0, null), nivel);
    }
}
