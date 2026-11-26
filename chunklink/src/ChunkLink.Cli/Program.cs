using System.Text.Json;
using c2_pcg.flowMatchingDataloader;
using c2_pcg.flowMatchingEval;
using ChunkLink.Agentes;
using ChunkLink.Ambiente;
using ChunkLink.Comunicacao;
using ChunkLink.Estilo;
using ChunkLink.Movimento;
using ChunkLink.Runtime;

namespace ChunkLink.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        // Saídas idênticas em qualquer máquina, independentemente do idioma do sistema.
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        if (args.Length == 0) return Ajuda();
        var opcoes = Opcoes.Ler(args.Skip(1));
        try
        {
            return args[0] switch
            {
                "depurar" => Depurar(opcoes),
                "importar-pool" => ImportarPool(opcoes),
                "montar" => Montar(opcoes),
                "comparar" => Comparar(opcoes),
                "validar-movimento" => ValidarMovimento(opcoes),
                "lote" => Lote(opcoes),
                _ => Ajuda(),
            };
        }
        catch (Exception e) when (e is FormatException or FileNotFoundException or DirectoryNotFoundException or ArgumentException)
        {
            Console.Error.WriteLine("erro: " + e.Message);
            return 2;
        }
    }

    private static int Ajuda()
    {
        Console.WriteLine("""
            chunklink — SMA que monta níveis de Super Mario Bros a partir de chunks do TCC

              depurar        [--trace saida/trace.jsonl] [--completo] [--memoria m.db]
                             [--duplicar 0.3] [--perder 0.05] [--semente 1]
                             cenário controlado: imprime a narrativa cognitiva e grava o trace JSONL;
                             opcionalmente com memória persistente e injeção de falhas de entrega
              importar-pool  --bioma overworld --saida dados/pool --log arquivo1.log [arquivo2.log ...]
                             congela a saída da CLI de geração do TCC em pools bruto/ e reparado/
              montar         --pool dados/pool/overworld/reparado --vglc <dir Processed> --bioma overworld
                             [--original mario-1-1] [--slots N] [--sprites <dir>] [--saida saida/montagem]
                             [--memoria memoria.db]  memória episódica persistente entre execuções (CP9)
              comparar       mesmas opções de montar: executa E0, E1 e E2 (ChunkLink) e imprime as métricas
              validar-movimento --vglc <dir Processed>
                             confronta o simulador com os níveis originais: todo original deve ser completável
              lote           --pool ... --vglc ... --bioma ... [--original ...] [--sementes 10] [--tamanho 24] [--saida dir] [--traces]
                             repete E0, E1, E2 e E2 sem cognição em subconjuntos aleatórios do pool e mede o
                             comportamento coletivo de E2 (negociações, concessões, reconsiderações, recuos)
            """);
        return 1;
    }

    // ------------------------------------------------------------------ depurar

    private static int Depurar(Opcoes o)
    {
        // Injeção de falhas de entrega (DSM3): --duplicar 0.3 --perder 0.05 --semente 1
        double duplicar = double.Parse(o.Valor("duplicar") ?? "0", System.Globalization.CultureInfo.InvariantCulture);
        double perder = double.Parse(o.Valor("perder") ?? "0", System.Globalization.CultureInfo.InvariantCulture);
        var falhas = duplicar > 0 || perder > 0 ? new InjetorFalhas(int.Parse(o.Valor("semente") ?? "1"), duplicar, perder) : null;
        using var memoria = o.Valor("memoria") is { } arquivo ? new Memoria.MemoriaEpisodica(arquivo, CenarioControlado.Parametros.TtlMemoria) : null;
        var sistema = CenarioControlado.Criar(memoria: memoria, falhas: falhas);
        var r = sistema.Executar();
        Narrar(r.Tracer, o.Tem("completo"));
        Console.WriteLine();
        Imprimir(r.Metricas);
        if (r.Nivel != null)
        {
            Console.WriteLine("\nNível montado:");
            foreach (var linha in Grade.ParaAscii(r.Nivel)) Console.WriteLine("  " + linha);
        }
        string trace = o.Valor("trace") ?? "saida/depuracao/trace.jsonl";
        r.Tracer.SalvarJsonl(trace);
        Console.WriteLine($"\ntrace: {trace} ({r.Tracer.Eventos.Count} eventos)");
        return r.Metricas.Sucesso ? 0 : 3;
    }

    // Narrativa legível: eventos cognitivos e do ambiente; mensagens de playtest são resumidas.
    private static void Narrar(Tracer tracer, bool completo)
    {
        string[] relevantes =
        {
            "intencao.adotada", "plano.selecionado", "plano.falhou", "plano.ausente", "objetivo.abandonado", "intencao.concluida",
            "intencao.falhou", "intencao.abandonada", "reconsideracao", "concessao", "decisao", "cn.propostas", "cn.descartada",
            "nivel.fixar", "nivel.recuar", "nivel.reserva", "nivel.commit", "nivel.rollback", "protocolo.violacao",
            "msg.rejeitada", "msg.duplicada", "msg.inesperada", "msg.falha", "falha.perda", "falha.duplicacao", "nivel.recusa",
            "memoria.carregada", "memoria.gravada",
        };
        string[] crencasRelevantes =
        {
            "culpa_costura", "exigencia_saida", "falha_costura", "vetado", "restricao", "restricao_emitida", "culpado",
            "impasse", "estilo_relaxado", "limiar", "orcamento", "ranking", "nivel_concluido", "nivel_falhou",
        };
        foreach (var e in tracer.Eventos)
        {
            using var doc = JsonDocument.Parse(e.Dados);
            var d = doc.RootElement;
            string? linha = e.Tipo switch
            {
                "msg" when completo || !EhPlaytest(d) => $"✉ {d.GetProperty("performativa").GetString()} → {d.GetProperty("para").GetString()} [{d.GetProperty("conversa").GetString()}] {Compacto(d.GetProperty("conteudo"))}",
                "crenca+" when completo || crencasRelevantes.Contains(d.GetProperty("predicado").GetString()) =>
                    $"+ crença {d.GetProperty("predicado").GetString()}({d.GetProperty("chave").GetString()}) = {Compacto(d.GetProperty("valor"))}  [fonte: {d.GetProperty("fonte").GetString()}]",
                "crenca-" when completo || crencasRelevantes.Contains(d.GetProperty("predicado").GetString()) =>
                    $"− crença {d.GetProperty("predicado").GetString()}({d.GetProperty("chave").GetString()})",
                _ when relevantes.Contains(e.Tipo) => $"{Icone(e.Tipo)} {e.Tipo}: {Compacto(d)}",
                _ => null,
            };
            if (linha != null) Console.WriteLine($"t={e.Tick,4} {e.Agente,-18} {linha}");
        }
    }

    private static bool EhPlaytest(JsonElement d)
    {
        var c = d.GetProperty("conversa").GetString() ?? "";
        return c == "ranking" || c == "validacao" || (c.StartsWith("slot:") && d.GetProperty("conteudo").ValueKind == JsonValueKind.Object
            && (d.GetProperty("conteudo").TryGetProperty("Rotulo", out _)));
    }

    private static string Icone(string tipo) => tipo switch
    {
        "reconsideracao" or "concessao" => "⟳",
        "plano.falhou" or "intencao.falhou" or "nivel.rollback" or "protocolo.violacao" or "msg.rejeitada" or "nivel.recusa" => "✗",
        "falha.perda" or "falha.duplicacao" or "msg.duplicada" => "⚠",
        "nivel.commit" or "nivel.fixar" or "intencao.concluida" => "✓",
        _ => "•",
    };

    private static string Compacto(JsonElement e)
    {
        string s = e.ValueKind == JsonValueKind.String ? e.GetString()! : e.GetRawText();
        return s.Length > 230 ? s[..227] + "…" : s;
    }

    private static void Imprimir(Metricas m)
    {
        Console.WriteLine($"sucesso: {m.Sucesso}{(m.Motivo != null ? $" ({m.Motivo})" : "")}");
        Console.WriteLine($"sequência: {string.Join(" | ", m.Sequencia)}");
        Console.WriteLine($"conclusão por perfil (M1): {string.Join(", ", m.ConclusaoPorPerfil.Select(kv => $"{kv.Key}={(kv.Value ? "sim" : "não")}"))}");
        Console.WriteLine($"tiles editados (M4): {m.TilesEditados} · backtracks (M5): {m.Backtracks} · mensagens (M6): {m.Mensagens} · ticks: {m.Ticks}");
        Console.WriteLine($"KL padrões 2×2 (M12): {m.KlPadroes2x2?.ToString("F3") ?? "—"} · costuras visíveis (M13): {m.CosturasVisiveis} · menor distância a originais (N4): {m.MenorDistanciaAOriginais:P1}");
        Console.WriteLine($"assinatura do trace (RNF01): {m.AssinaturaTrace}");
    }

    // ------------------------------------------------------------------ pool real

    private static int ImportarPool(Opcoes o)
    {
        var bioma = Enum.Parse<BiomeTypeEnum>(o.Exigir("bioma"), ignoreCase: true);
        string saida = o.Exigir("saida");
        var mapas = o.Lista("log").SelectMany(f => PoolChunks.LerSaidaDoGerador(f, bioma)).ToList();
        if (mapas.Count == 0) throw new FormatException("nenhum chunk encontrado nos logs");
        var brutos = mapas.Select((m, i) => new Chunk(PoolChunks.IdPara(bioma, OrigemChunk.Bruto, i + 1), m, OrigemChunk.Bruto)).ToList();
        var reparados = mapas.Select((m, i) => new Chunk(PoolChunks.IdPara(bioma, OrigemChunk.ReparadoTcc, i + 1),
            ChunkStructureRepair.RepairAll(m, new ChunkStructureRepairConfig()), OrigemChunk.ReparadoTcc)).ToList();
        new PoolChunks(brutos).Salvar(Path.Combine(saida, "bruto"));
        new PoolChunks(reparados).Salvar(Path.Combine(saida, "reparado"));
        Console.WriteLine($"{mapas.Count} chunks de {bioma} congelados em {saida}/bruto e {saida}/reparado (manifesto pool.csv com hash)");
        return 0;
    }

    private sealed record Contexto(PoolChunks Pool, CorpusReferencia Corpus, BiomeTypeEnum Bioma, TileMap Original, string NomeOriginal, Parametros P);

    private static Contexto Carregar(Opcoes o)
    {
        var bioma = Enum.Parse<BiomeTypeEnum>(o.Exigir("bioma"), ignoreCase: true);
        var pool = PoolChunks.Carregar(o.Exigir("pool"));
        var corpus = CorpusReferencia.DoVglc(o.Exigir("vglc"), bioma);
        string nome = o.Valor("original") ?? corpus.Niveis[0].Nome;
        var original = corpus.Niveis.First(n => n.Nome == nome).Mapa;
        int slots = int.Parse(o.Valor("slots") ?? (original.Width / Grade.LarguraChunk).ToString());
        return new Contexto(pool, corpus, bioma, original, nome, new Parametros { Slots = slots });
    }

    private static int Montar(Opcoes o)
    {
        var c = Carregar(o);
        string saida = o.Valor("saida") ?? "saida/montagem";
        Console.WriteLine($"pool: {c.Pool.Chunks.Count} chunks · referência: {c.Corpus.Niveis.Count} níveis de {c.Bioma} · original pareado: {c.NomeOriginal} ({c.Original.Width} colunas) · slots: {c.P.Slots}");
        using var memoria = o.Valor("memoria") is { } arquivo ? new Memoria.MemoriaEpisodica(arquivo, c.P.TtlMemoria) : null;
        var sistema = new SistemaChunkLink(c.Pool, c.Corpus, c.P, new[] { PerfilJogador.Pequeno, PerfilJogador.Grande }, c.Bioma, memoria: memoria);
        var r = sistema.Executar();
        Imprimir(r.Metricas);
        Directory.CreateDirectory(saida);
        r.Tracer.SalvarJsonl(Path.Combine(saida, "trace.jsonl"));
        File.WriteAllText(Path.Combine(saida, "metricas.json"), JsonSerializer.Serialize(r.Metricas, new JsonSerializerOptions { WriteIndented = true }));
        if (r.Nivel != null)
        {
            File.WriteAllLines(Path.Combine(saida, "nivel-montado.txt"), Grade.ParaAscii(r.Nivel));
            if (o.Valor("sprites") is { } sprites)
            {
                Renderizacao.RenderizarNivel(r.Nivel, sprites, Path.Combine(saida, "nivel-montado.png"));
                var par = Renderizacao.RenderizarParCego(r.Nivel, c.Original, sprites, Path.Combine(saida, "teste-cego"),
                    $"{c.Bioma.ToString().ToLowerInvariant()}-{c.NomeOriginal}", semente: int.Parse(o.Valor("semente") ?? "42"));
                Console.WriteLine($"PNG do nível: {Path.Combine(saida, "nivel-montado.png")} · par cego: {par} (gabarito em teste-cego/gabarito.csv)");
            }
        }
        Console.WriteLine($"trace e métricas em {saida}/");
        return r.Metricas.Sucesso ? 0 : 3;
    }

    private static int Comparar(Opcoes o)
    {
        var c = Carregar(o);
        var perfis = new[] { PerfilJogador.Pequeno, PerfilJogador.Grande };
        var estilo = new ConhecimentoEstilo(c.Corpus);
        int semente = int.Parse(o.Valor("semente") ?? "42");
        var e0 = Baselines.E0(c.Pool, estilo, perfis, c.P.Slots, semente).Metricas;
        var e1 = Baselines.E1(c.Pool, estilo, perfis, c.P.Slots).Metricas;
        var e2 = new SistemaChunkLink(c.Pool, c.Corpus, c.P, perfis, c.Bioma).Executar().Metricas;
        var e2a = new SistemaChunkLink(c.Pool, c.Corpus, c.P with { Cognicao = false }, perfis, c.Bioma).Executar().Metricas;
        Console.WriteLine($"pool {c.Pool.Chunks.Count} chunks · {c.P.Slots} slots · original {c.NomeOriginal} · semente E0 = {semente}\n");
        Console.WriteLine($"{"estratégia",-22}{"montou",-8}{"P-Peq",-7}{"P-Gde",-7}{"tiles",-7}{"KL 2×2",-9}{"costuras",-10}{"msgs",-7}");
        var linhas = new[] { ("E0 aleatória+TCC", e0), ("E1 central guloso", e1), ("E2 ChunkLink", e2), ("E2 sem cognição", e2a) };
        foreach (var (nome, m) in linhas)
            Console.WriteLine($"{nome,-22}{(m.Sucesso ? "sim" : "não"),-8}{Sim(m, "P-Peq"),-7}{Sim(m, "P-Gde"),-7}{m.TilesEditados,-7}{m.KlPadroes2x2?.ToString("F3") ?? "—",-9}{m.CosturasVisiveis,-10}{m.Mensagens,-7}");
        foreach (var (nome, m) in linhas.Where(l => l.Item2.Motivo != null))
            Console.WriteLine($"\n{nome}: {m.Motivo}");
        return 0;
    }

    // ------------------------------------------------------------------ validar-movimento

    private static int ValidarMovimento(Opcoes o)
    {
        string vglc = o.Exigir("vglc");
        int alcance = int.Parse(o.Valor("alcance") ?? PerfilJogador.Pequeno.AlcanceSalto.ToString());
        int altura = int.Parse(o.Valor("altura") ?? PerfilJogador.Pequeno.AlturaSalto.ToString());
        var pequeno = PerfilJogador.Pequeno with { AlcanceSalto = alcance, AlturaSalto = altura };
        var grande = PerfilJogador.Grande with { AlcanceSalto = alcance, AlturaSalto = altura };
        var perfis = new[] { pequeno, grande, grande with { Id = "P-Gde sem quebra", QuebraBlocos = false } };
        Console.WriteLine($"alcance do salto d = {alcance} · altura h = {altura}\n");
        Console.WriteLine($"{"nível",-12}{"bioma",-13}{"colunas",-9}" + string.Concat(perfis.Select(p => $"{p.Id,-30}")));
        int total = 0, falhas = 0;
        foreach (var bioma in new[] { BiomeTypeEnum.Overworld, BiomeTypeEnum.Underground, BiomeTypeEnum.Treetop })
        foreach (var (nome, mapa) in CorpusReferencia.DoVglc(vglc, bioma).Niveis)
        {
            var linha = $"{nome,-12}{bioma,-13}{mapa.Width,-9}";
            foreach (var p in perfis)
            {
                var r = BuscaAEstrela.Jogar(mapa, p);
                linha += $"{(r.Concluiu ? "completa" : $"falha {r.Causa} col. {r.ColunaFalha} ({r.Progresso:P0})"),-30}";
                if (p.Id is "P-Peq" or "P-Gde") { total++; if (!r.Concluiu) falhas++; }
            }
            Console.WriteLine(linha);
        }
        Console.WriteLine($"\nperfis-alvo (P-Peq, P-Gde): {total - falhas}/{total} combinações nível × perfil completáveis");
        return falhas == 0 ? 0 : 3;
    }

    // ------------------------------------------------------------------ lote (evidência de comportamento coletivo)

    private static int Lote(Opcoes o)
    {
        var c = Carregar(o);
        int sementes = int.Parse(o.Valor("sementes") ?? "10");
        int tamanho = int.Parse(o.Valor("tamanho") ?? "24");
        string saida = o.Valor("saida") ?? $"saida/lote-{c.Bioma.ToString().ToLowerInvariant()}";
        Directory.CreateDirectory(saida);
        var perfis = new[] { PerfilJogador.Pequeno, PerfilJogador.Grande };
        var estilo = new ConhecimentoEstilo(c.Corpus);
        var linhas = new List<string> { "semente;estrategia;montou;kl;costuras;tiles;mensagens;backtracks" };
        var coletivo = new List<string> { "semente;" + string.Join(';', Coletivo.Campos) };
        var resumo = new Dictionary<string, List<Metricas>>();

        for (int s = 1; s <= sementes; s++)
        {
            var rng = new Random(s);
            var pool = new PoolChunks(c.Pool.Chunks.OrderBy(_ => rng.Next()).Take(Math.Min(tamanho, c.Pool.Chunks.Count)));
            var e2 = new SistemaChunkLink(pool, c.Corpus, c.P, perfis, c.Bioma).Executar();
            if (o.Tem("traces")) e2.Tracer.SalvarJsonl(Path.Combine(saida, "traces", $"e2-semente-{s:D2}.jsonl"));
            var execucoes = new (string Nome, Metricas M)[]
            {
                ("E0", Baselines.E0(pool, estilo, perfis, c.P.Slots, s).Metricas),
                ("E1", Baselines.E1(pool, estilo, perfis, c.P.Slots).Metricas),
                ("E2", e2.Metricas),
                ("E2-a", new SistemaChunkLink(pool, c.Corpus, c.P with { Cognicao = false }, perfis, c.Bioma).Executar().Metricas),
            };
            foreach (var (nome, m) in execucoes)
            {
                (resumo.TryGetValue(nome, out var l) ? l : resumo[nome] = new()).Add(m);
                linhas.Add(string.Join(';', s, nome, m.Sucesso ? 1 : 0, m.KlPadroes2x2?.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) ?? "",
                    m.CosturasVisiveis, m.TilesEditados, m.Mensagens, m.Backtracks));
            }
            coletivo.Add(s + ";" + string.Join(';', Coletivo.Contar(e2.Tracer)));
        }
        File.WriteAllLines(Path.Combine(saida, "metricas.csv"), linhas);
        File.WriteAllLines(Path.Combine(saida, "coletivo-e2.csv"), coletivo);

        Console.WriteLine($"{c.Bioma} · original {c.NomeOriginal} · {c.P.Slots} slots · {sementes} sementes · subconjuntos de {tamanho} de {c.Pool.Chunks.Count} chunks\n");
        Console.WriteLine($"{"estratégia",-12}{"montou",-10}{"KL médio*",-11}{"costuras*",-11}{"tiles*",-9}{"mensagens*",-11}");
        foreach (var (nome, ms) in resumo)
        {
            var ok = ms.Where(m => m.Sucesso).ToList();
            string Media(Func<Metricas, double> f) => ok.Count == 0 ? "—" : ok.Average(f).ToString("F2");
            Console.WriteLine($"{nome,-12}{$"{ok.Count}/{ms.Count}",-10}{Media(m => m.KlPadroes2x2 ?? 0),-11}{Media(m => m.CosturasVisiveis),-11}{Media(m => m.TilesEditados),-9}{Media(m => m.Mensagens),-11}");
        }
        Console.WriteLine("* média só das execuções que montaram o nível");
        var totais = coletivo.Skip(1).Select(l => l.Split(';').Skip(1).Select(int.Parse).ToArray()).ToList();
        Console.WriteLine("\ncomportamento coletivo de E2 (soma nas sementes):");
        for (int i = 0; i < Coletivo.Campos.Length; i++)
            Console.WriteLine($"  {Coletivo.Campos[i],-28}{totais.Sum(t => t[i]),6}");
        Console.WriteLine($"\nCSV em {saida}/metricas.csv e {saida}/coletivo-e2.csv");
        return 0;
    }

    private static string Sim(Metricas m, string perfil) =>
        m.ConclusaoPorPerfil.TryGetValue(perfil, out var v) ? (v ? "sim" : "não") : "—";
}

internal sealed class Opcoes
{
    private readonly Dictionary<string, List<string>> _valores = new();

    public static Opcoes Ler(IEnumerable<string> args)
    {
        var o = new Opcoes();
        string? atual = null;
        foreach (var a in args)
        {
            if (a.StartsWith("--")) o._valores[atual = a[2..]] = new List<string>();
            else if (atual != null) o._valores[atual].Add(a);
        }
        return o;
    }

    public bool Tem(string nome) => _valores.ContainsKey(nome);
    public string? Valor(string nome) => _valores.TryGetValue(nome, out var v) && v.Count > 0 ? v[0] : null;
    public IReadOnlyList<string> Lista(string nome) => _valores.TryGetValue(nome, out var v) ? v : new List<string>();
    public string Exigir(string nome) => Valor(nome) ?? throw new ArgumentException($"opção --{nome} é obrigatória");
}

// Contagem de eventos de interação no trace: evidência de comportamento coletivo (DSM3).
internal static class Coletivo
{
    public static readonly string[] Campos =
    {
        "playtests_pedidos", "pareceres_de_estilo", "restricoes_proativas", "vetos", "concessoes", "suspensoes_de_estilo",
        "reconsideracoes_arquiteto", "recuos", "contract_nets", "propostas", "recusas_de_reparadores",
        "aprovacoes", "recusas_do_curador", "commits", "rollbacks", "mensagens",
    };

    public static IEnumerable<int> Contar(Tracer t)
    {
        var msgs = t.DoTipo("msg").Select(e => (e.Agente, Doc: JsonDocument.Parse(e.Dados).RootElement)).ToList();
        int Msg(string agente, string performativa, Func<string, bool>? conversa = null) => msgs.Count(m =>
            (agente == "*" || m.Agente == agente || (agente == "reparador" && m.Agente.StartsWith("reparador")))
            && m.Doc.GetProperty("performativa").GetString() == performativa
            && (conversa == null || conversa(m.Doc.GetProperty("conversa").GetString()!)));
        yield return Msg("arquiteto", "Request", c => c.StartsWith("slot:") || c == "ranking" || c == "validacao");
        yield return Msg("curador", "Inform", c => c.StartsWith("estilo:"));
        yield return Msg("curador", "Inform", c => c.StartsWith("restricao:"));
        yield return t.DoTipo("crenca+").Count(e => e.Agente == "curador" && e.Dados.Contains("\"predicado\":\"vetado\""));
        yield return t.DoTipo("concessao").Count();
        yield return t.DoTipo("reconsideracao").Count(e => e.Agente == "curador");
        yield return t.DoTipo("reconsideracao").Count(e => e.Agente == "arquiteto");
        yield return t.DoTipo("nivel.recuar").Count();
        yield return t.DoTipo("cn.propostas").Count();
        yield return Msg("reparador", "Propose");
        yield return Msg("reparador", "Refuse");
        yield return Msg("curador", "Agree");
        yield return Msg("curador", "Refuse");
        yield return t.DoTipo("nivel.commit").Count();
        yield return t.DoTipo("nivel.rollback").Count();
        yield return msgs.Count;
    }
}
