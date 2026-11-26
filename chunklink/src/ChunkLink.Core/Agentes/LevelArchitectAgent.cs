using ChunkLink.Ambiente;
using ChunkLink.Bdi;
using ChunkLink.Comunicacao;
using ChunkLink.Estilo;
using ChunkLink.Movimento;

namespace ChunkLink.Agentes;

// Arquiteto do nível — agente cognitivo (BDI).
//   Crenças: resultados de playtest por (rodada, chunk, perfil); slots fechados; pareceres e decisões do
//            curador; propostas de reparo; orçamento; restrições do curador; e crenças inferidas:
//            falha_costura, culpa_costura(k), exigencia_saida(k), incompativel(a|b), culpado(k|chunk).
//   Desejo: montar_nivel_jogavel (até nivel_concluido ou nivel_falhou).
//   Intenções/planos: montar_nivel → ranquear_pool, preencher(k), reparar_slot(k), recuar(k), validar_nivel;
//            tratadores -!preencher escolhem entre recuar (culpa), nova rodada ou recuar (esgotamento).
//   Reconsideração: abandona preencher(k) quando a crença culpa_costura(k) surge antes do Contract Net
//            (a causa está no slot k−1), e abandona reparar_slot quando o orçamento acaba.
public sealed class LevelArchitectAgent : Agente
{
    private readonly Dictionary<int, HashSet<string>> _tentados = new();
    private readonly Dictionary<int, HashSet<string>> _falhasCostura = new();
    private readonly Dictionary<int, int> _backtracks = new();
    private readonly HashSet<string> _cnAbertas = new();
    // Episódios observados nesta execução (não os carregados da memória): só eles renovam o TTL.
    private readonly HashSet<(string Tipo, string Chave)> _observados = new();
    private List<string> _ranking = new();
    private int _rodadas, _reparos, _validacoes, _iteracoes;

    public LevelArchitectAgent(Mundo mundo) : base(Mundo.IdArquiteto, Papel.Arquiteto, mundo)
    {
        Crencas = new BaseCrencas(Id, M.Tracer, () => Tick);
        Motor = new MotorBdi(Id, Crencas, M.Tracer, () => Tick);
        Crencas.Definir("orcamento", "nivel", M.P.EMax, "configuração");

        Motor.Desejos.Add(new Desejo("montar_nivel_jogavel", 10, "montar_nivel",
            c => !c.Tem("nivel_concluido", "nivel") && !c.Tem("nivel_falhou", "nivel")));

        Motor.Planos.Add(Plano.Para("montar_nivel", "montar_slot_a_slot", (_, _) => true, MontarNivel));
        Motor.Planos.Add(Plano.SeFalhar("montar_nivel", "registrar_falha_do_nivel", (_, _) => true, RegistrarFalha));
        Motor.Planos.Add(Plano.Para("ranquear_pool", "playtest_individual", (_, _) => true, RanquearPool));
        Motor.Planos.Add(Plano.Para("preencher", "testar_candidatos", (_, a) => Shortlist((int)a[0]).Count > 0, Preencher));
        Motor.Planos.Add(Plano.SeFalhar("preencher", "recuar_por_culpa_da_costura",
            (c, a) => (int)a[0] > 1 && c.Tem("culpa_costura", a[0]) && Backtracks((int)a[0]) < M.P.BBacktracks,
            ctx => Recuo(ctx.Arg<int>(0), "falhas repetidas na costura apontam o slot anterior")));
        Motor.Planos.Add(Plano.SeFalhar("preencher", "tentar_outros_candidatos",
            (_, a) => Shortlist((int)a[0]).Count > 0, NovaRodada));
        Motor.Planos.Add(Plano.SeFalhar("preencher", "recuar_por_esgotamento",
            (_, a) => (int)a[0] > 1 && Backtracks((int)a[0]) < M.P.BBacktracks,
            ctx => Recuo(ctx.Arg<int>(0), "candidatos esgotados para o slot")));
        Motor.Planos.Add(Plano.Para("reparar_slot", "contract_net_de_reparo",
            (c, _) => c.Valor<int>("orcamento", "nivel") > 0 && M.Reparadores.Count > 0, RepararSlot));
        Motor.Planos.Add(Plano.Para("recuar", "liberar_slot_anterior", (_, a) => (int)a[0] > 1, Recuar));
        Motor.Planos.Add(Plano.Para("validar_nivel", "playtest_do_nivel_completo", (_, _) => true, ValidarNivel));

        if (M.P.Cognicao) Motor.Politica = Reconsiderar;
        CarregarMemoria();
    }

    // ------------------------------------------------------------------ memória entre execuções (CP9)

    // Incompatibilidades e vetos de execuções anteriores viram crenças com fonte "memória".
    // Os episódios são indexados pelo hash do chunk, não pelo id, para valerem em outros pools.
    private void CarregarMemoria()
    {
        if (M.Memoria == null) return;
        var porHash = M.Pool.Chunks.GroupBy(c => c.Hash).ToDictionary(g => g.Key, g => g.First().Id);
        int incompat = 0, vetos = 0;
        foreach (var e in M.Memoria.Carregar(M.Nivel.Bioma.ToString()))
        {
            if (e.Tipo == "incompativel" && e.Chave.Split('|') is [var ha, var hb] && porHash.TryGetValue(ha, out var a) && porHash.TryGetValue(hb, out var b))
            {
                Crencas.Definir("incompativel", $"{a}|{b}", e.Valor, "memória");
                incompat++;
            }
            else if (e.Tipo == "vetado" && porHash.TryGetValue(e.Chave, out var v))
            {
                Crencas.Definir("vetado", v, e.Valor, "memória");
                vetos++;
            }
        }
        Registrar("memoria.carregada", new { execucao = M.Memoria.ExecucaoAtual, incompatibilidades = incompat, vetos });
    }

    // Grava os episódios observados nesta execução (reforça o TTL dos que se repetiram).
    public void Memorizar()
    {
        if (M.Memoria == null) return;
        string bioma = M.Nivel.Bioma.ToString();
        int gravados = 0;
        foreach (var (tipo, chave) in _observados)
        {
            if (tipo == "incompativel" && chave.Split('|') is [var a, var b])
                M.Memoria.Lembrar(tipo, $"{M.Pool.Obter(a).Hash}|{M.Pool.Obter(b).Hash}", Crencas.Valor<string>(tipo, chave) ?? "", bioma);
            else if (tipo == "vetado")
                M.Memoria.Lembrar(tipo, M.Pool.Obter(chave).Hash, Crencas.Valor<string>(tipo, chave) ?? "", bioma);
            else continue;
            gravados++;
        }
        Registrar("memoria.gravada", new { execucao = M.Memoria.ExecucaoAtual, episodios = gravados });
    }

    public BaseCrencas Crencas { get; }
    public MotorBdi Motor { get; }
    public bool Terminou => Crencas.Tem("nivel_concluido", "nivel") || Crencas.Tem("nivel_falhou", "nivel");
    public bool Sucesso => Crencas.Tem("nivel_concluido", "nivel");
    public string? MotivoFalha => Crencas.Valor<string>("nivel_falhou", "nivel");
    public int TotalBacktracks => _backtracks.Values.Sum();

    public override void Ciclo()
    {
        foreach (var m in Receber()) Perceber(m);
        if (!Terminou) Motor.Ciclo();
    }

    // ------------------------------------------------------------------ percepção e revisão de crenças

    private void Perceber(Mensagem m)
    {
        switch (m.Performativa, m.Conteudo)
        {
            case (Performativa.Inform, RespostaPlaytest r):
                Crencas.Definir("teste", $"{r.Rotulo}|{r.Resultado.Perfil}", r.Resultado, m.Remetente);
                InferirCostura(r);
                break;
            case (Performativa.Inform, RespostaEstilo e):
                Crencas.Definir("parecer", m.Conversa, e, m.Remetente);
                foreach (var v in e.Pareceres.Where(x => x.Veto != null))
                {
                    Crencas.Definir("vetado", v.ChunkId, v.Veto!, m.Remetente);
                    _observados.Add(("vetado", v.ChunkId));
                }
                break;
            case (Performativa.Agree, Aprovacao a):
                Crencas.Definir("decisao", m.Conversa, a, m.Remetente);
                break;
            case (Performativa.Refuse, RecusaAprovacao r):
                Crencas.Definir("decisao", m.Conversa, r, m.Remetente);
                Crencas.Definir("orcamento", "nivel", r.OrcamentoRestante, m.Remetente);
                break;
            case (Performativa.Propose, PropostaReparo p) when !_cnAbertas.Contains(m.Conversa):
                // Janela já fechada: a proposta não entra na decisão e a conversa é encerrada.
                Registrar("cn.descartada", new { conversa = m.Conversa, reparador = p.Reparador, enviada_em = p.EnviadaEm, motivo = "chegou após o fechamento da janela" });
                Enviar(m.Remetente, Performativa.RejectProposal, "proposta tardia", m.Conversa);
                break;
            case (Performativa.Propose, PropostaReparo p):
                Crencas.Definir("resposta_cn", $"{m.Conversa}|{m.Remetente}", p, m.Remetente);
                break;
            case (Performativa.Refuse, string motivo):
                Crencas.Definir("resposta_cn", $"{m.Conversa}|{m.Remetente}", "recusa: " + motivo, m.Remetente);
                break;
            case (Performativa.Inform, EdicaoAplicada e):
                Crencas.Definir("edicao", m.Conversa, e.EdicaoId, m.Remetente);
                break;
            case (Performativa.Failure, string motivo) when m.Conversa.StartsWith("reparo:"):
                Crencas.Definir("edicao", m.Conversa, "falha: " + motivo, m.Remetente);
                break;
            case (Performativa.Failure, string motivo):
                Registrar("msg.falha", new { de = m.Remetente, conversa = m.Conversa, motivo });
                break;
            case (Performativa.Inform, RestricaoCurador r):
                Crencas.Definir("restricao", r.Slot, r.Tipo, m.Remetente);
                break;
            default:
                Registrar("msg.inesperada", new { id = m.Id, de = m.Remetente, performativa = m.Performativa.ToString(), conversa = m.Conversa });
                break;
        }
    }

    // Inferência: se candidatos diferentes falham logo após a fronteira, a suspeita recai sobre a
    // saída do slot anterior — conhecimento novo que muda o plano (recuar em vez de reparar).
    private void InferirCostura(RespostaPlaytest r)
    {
        if (!M.P.Cognicao) return;
        var partes = r.Rotulo.Split('|');
        if (partes.Length != 2 || !partes[0].StartsWith('s') || !partes[0].Contains('r')) return;
        int k = int.Parse(partes[0][1..partes[0].IndexOf('r')]);
        string chunk = partes[1];
        if (k <= 1 || r.Resultado.Concluiu || r.Resultado.Causa == CausaFalha.Inconclusivo) return;
        int local = r.Resultado.ColunaFalha - Nivel.ColunaInicial(k);
        if (local < 0 || local > M.P.JanelaCostura) return;

        var anterior = M.Nivel.ChunkNoSlot(k - 1);
        if (anterior != null)
        {
            Crencas.Definir("incompativel", $"{anterior.Id}|{chunk}", r.Resultado.Causa.ToString(), "inferência");
            _observados.Add(("incompativel", $"{anterior.Id}|{chunk}"));
        }
        var falhas = Falhas(_falhasCostura, k);
        falhas.Add(chunk);
        Crencas.Definir("falha_costura", $"{k}|{chunk}", $"{r.Resultado.Causa} para {r.Resultado.Perfil} na coluna local {local}", "inferência");
        if (falhas.Count >= M.P.FalhasParaCulpa && !Crencas.Tem("culpa_costura", k))
        {
            Crencas.Definir("culpa_costura", k,
                $"{falhas.Count} candidatos diferentes falham nas primeiras colunas do slot {k}; a saída do slot {k - 1} é a causa provável",
                "inferência");
            if (r.Resultado.Causa == CausaFalha.Parede)
                Crencas.Definir("exigencia_saida", k - 1, "alta", "inferência");
        }
    }

    // ------------------------------------------------------------------ reconsideração

    private Reconsideracao? Reconsiderar(Intencao i, BaseCrencas c)
    {
        if (i.Objetivos.Count == 0 || i.Objetivos[0].Objetivo != "reparar_slot") return null;
        int k = (int)i.Objetivos[0].Args[0];
        if (k > 1 && c.Tem("culpa_costura", k) && Backtracks(k) < M.P.BBacktracks)
            return new AbandonarObjetivo("preencher",
                $"crença nova — {c.Valor<string>("culpa_costura", k)}; reparar o candidato trataria o sintoma, não a causa");
        if (c.Valor<int>("orcamento", "nivel") <= 0)
            return new AbandonarObjetivo("reparar_slot", "o curador informou que o orçamento de edição do nível acabou");
        return null;
    }

    // ------------------------------------------------------------------ planos

    private IEnumerable<Passo> MontarNivel(ContextoPlano ctx)
    {
        if (_ranking.Count == 0) yield return new SubObjetivo("ranquear_pool");
        while (M.Nivel.PrimeiroSlotVazio() is int k)
        {
            if (++_iteracoes > 300) yield return new Falhar("limite de iterações de montagem atingido");
            yield return new SubObjetivo("preencher", k);
        }
        yield return new SubObjetivo("validar_nivel");
        Crencas.Definir("nivel_concluido", "nivel", true, "deliberação");
    }

    private IEnumerable<Passo> RegistrarFalha(ContextoPlano ctx)
    {
        Crencas.Definir("nivel_falhou", "nivel", ctx.MotivoFalha ?? "falha", "deliberação");
        yield break;
    }

    private IEnumerable<Passo> RanquearPool(ContextoPlano ctx)
    {
        var pedidos = M.Pool.Chunks.SelectMany(c => M.PerfisAlvo.Select(p => Pedido(Mundo.IdJogador(p),
            new PedidoPlaytest($"rank|{c.Id}", c.Mapa), "ranking", () => Crencas.Tem("teste", $"rank|{c.Id}|{p.Id}")))).ToList();
        foreach (var passo in PedirComReenvio("playtest individual de todo o pool", "P1", pedidos)) yield return passo;
        // Sem resposta mesmo após os reenvios, o chunk vai para o fim do ranking (progresso 0).
        double Pontuacao(Chunk c) => M.PerfisAlvo.Average(p => Crencas.Valor<ResultadoPlaytest>("teste", $"rank|{c.Id}|{p.Id}")?.Progresso ?? 0);
        _ranking = M.Pool.Chunks.OrderByDescending(Pontuacao).ThenBy(c => c.Id, StringComparer.Ordinal).Select(c => c.Id).ToList();
        Crencas.Definir("ranking", "pool", string.Join(", ", _ranking.Take(6)) + (_ranking.Count > 6 ? ", …" : ""), "inferência");
    }

    private IEnumerable<Passo> Preencher(ContextoPlano ctx)
    {
        int k = ctx.Arg<int>(0);
        var candidatos = Shortlist(k);
        string rodada = $"s{k}r{++_rodadas}";
        foreach (var c in candidatos) Falhas(_tentados, k).Add(c.Id);
        var pedidos = candidatos.SelectMany(c => M.PerfisAlvo.Select(p => Pedido(Mundo.IdJogador(p),
            new PedidoPlaytest($"{rodada}|{c.Id}", M.Nivel.Prefixo(k, c.Mapa), TrechoAberto: k < M.Nivel.Slots), $"slot:{k}",
            () => Crencas.Tem("teste", $"{rodada}|{c.Id}|{p.Id}")))).ToList();
        foreach (var passo in PedirComReenvio($"playtest da rodada {rodada} ({candidatos.Count} candidatos)", "P1", pedidos)) yield return passo;
        // Candidato sem resposta mesmo após os reenvios conta como não aprovado (Resultado devolve Inconclusivo).

        var aprovados = candidatos.Where(c => M.PerfisAlvo.All(p => Resultado(rodada, c.Id, p).Concluiu)).ToList();
        if (aprovados.Count == 0)
        {
            yield return new SubObjetivo("reparar_slot", k, rodada, candidatos.Select(c => c.Id).ToList());
            yield break;
        }

        var vizinho = k > 1 ? M.Nivel.ConteudoDoSlot(k - 1) : null;
        string conversa = $"estilo:{rodada}";
        var pedidoEstilo = Pedido(Mundo.IdCurador,
            new PedidoEstilo(k, M.Nivel.Slots, aprovados.Select(c => new CandidatoEstilo(c.Id, c.Mapa)).ToList(), vizinho), conversa,
            () => Crencas.Tem("parecer", conversa));
        foreach (var passo in PedirComReenvio("parecer de estilo do curador", "P4", new[] { pedidoEstilo })) yield return passo;

        if (Crencas.Valor<RespostaEstilo>("parecer", conversa) is not { } parecer)
        {
            yield return new Falhar("o curador não respondeu ao pedido de parecer, mesmo após os reenvios");
            yield break;
        }
        var escolhido = parecer.Pareceres.Where(x => x.Aceitavel)
            .OrderByDescending(x => AtendeExigencia(k, M.Pool.Obter(x.ChunkId)))
            .ThenBy(x => x.Avaliacao.Distancia + 0.5 * x.Avaliacao.Costura)
            .ThenBy(x => x.ChunkId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (escolhido == null)
        {
            yield return new Falhar($"o curador recusou os {aprovados.Count} candidato(s) aprovados no playtest");
            yield break;
        }

        var chunk = M.Pool.Obter(escolhido.ChunkId);
        M.Nivel.Fixar(Cred, k, chunk, escolhido.Aceite ?? -1);
        Crencas.Definir("slot", k, chunk.Id, "ação");
        Registrar("nivel.fixar", new { slot = k, chunk = chunk.Id, motivo = escolhido.Justificativa });
        Enviar(Mundo.IdCurador, Performativa.Inform, new SlotFechado(k, chunk.Id, 0), $"slot:{k}");
    }

    private IEnumerable<Passo> NovaRodada(ContextoPlano ctx)
    {
        Registrar("decisao", new { decisao = $"nova rodada de candidatos para o slot {ctx.Arg<int>(0)}", motivo = ctx.MotivoFalha });
        yield break;
    }

    private IEnumerable<Passo> Recuo(int k, string motivo)
    {
        yield return new SubObjetivo("recuar", k, motivo);
    }

    private IEnumerable<Passo> Recuar(ContextoPlano ctx)
    {
        int k = ctx.Arg<int>(0);
        string motivo = ctx.Arg<string>(1);
        var anterior = M.Nivel.ChunkNoSlot(k - 1)!;
        M.Nivel.Liberar(Cred, k - 1);
        Crencas.Remover("slot", k - 1);
        Falhas(_tentados, k - 1).Add(anterior.Id);
        Crencas.Definir("culpado", $"{k - 1}|{anterior.Id}", motivo, "inferência");
        _backtracks[k] = Backtracks(k) + 1;
        Crencas.Definir("backtracks", k, _backtracks[k], "ação");
        Falhas(_tentados, k).Clear();
        Falhas(_falhasCostura, k).Clear();
        Crencas.Remover("culpa_costura", k);
        Registrar("nivel.recuar", new { slot_reaberto = k - 1, chunk_removido = anterior.Id, motivo });
        Enviar(Mundo.IdCurador, Performativa.Inform, new SlotReaberto(k - 1, motivo), $"slot:{k - 1}");
        yield break;
    }

    private IEnumerable<Passo> RepararSlot(ContextoPlano ctx)
    {
        int k = ctx.Arg<int>(0);
        string rodada = ctx.Arg<string>(1);
        var ids = ctx.Arg<List<string>>(2);
        yield return new Ceder(); // ponto de deliberação: a reconsideração pode desistir do reparo aqui

        var alvo = ids.Where(id => !Crencas.Tem("vetado", id))
            .Select(id => (Id: id, Resultados: M.PerfisAlvo.Select(p => Resultado(rodada, id, p)).ToList()))
            .OrderBy(x => x.Resultados.Count(r => !r.Concluiu))
            .ThenByDescending(x => x.Resultados.Min(r => r.Progresso))
            .ThenBy(x => x.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (alvo.Id == null)
        {
            yield return new Falhar("nenhum candidato elegível para reparo");
            yield break;
        }

        var chunk = M.Pool.Obter(alvo.Id);
        var pior = alvo.Resultados.Where(r => !r.Concluiu).OrderBy(r => r.Progresso).First();
        var prefixo = M.Nivel.Prefixo(k, chunk.Mapa);
        int inicio = Nivel.ColunaInicial(k);
        var perfil = M.PerfisAlvo.First(p => p.Id == pior.Perfil);
        var falha = new FalhaJogabilidade(k, chunk.Id, pior, Math.Clamp(pior.ColunaFalha - inicio, 0, Grade.LarguraChunk - 1),
            pior.ColunaFalha > 0 ? Grade.AlturaChao(prefixo, pior.ColunaFalha - 1) : 0, perfil.AlturaSalto, perfil.AlcanceSalto);
        string conversa = $"reparo:{k}:{++_reparos}";
        long replyBy = Tick + M.P.ReplyBy;
        foreach (var r in M.Reparadores)
            Enviar(r, Performativa.Cfp, new ChamadaReparo(falha, chunk.Mapa, (int)replyBy), conversa);
        _cnAbertas.Add(conversa);

        var pendentes = new List<PropostaReparo>();
        try
        {
            yield return new Aguardar($"propostas do Contract Net {conversa} até reply_by = {replyBy}",
                () => Tick > replyBy || M.Reparadores.All(r => Crencas.Tem("resposta_cn", $"{conversa}|{r}")));

            _cnAbertas.Remove(conversa);
            var recebidas = M.Reparadores.Select(r => Crencas.Valor<PropostaReparo>("resposta_cn", $"{conversa}|{r}")).OfType<PropostaReparo>().ToList();
            foreach (var tardia in recebidas.Where(p => p.EnviadaEm > replyBy))
                Registrar("cn.descartada", new { conversa, reparador = tardia.Reparador, enviada_em = tardia.EnviadaEm, reply_by = replyBy });
            var propostas = recebidas.Where(p => p.EnviadaEm <= replyBy)
                .OrderBy(p => p.Diff.Custo).ThenBy(p => p.Reparador, StringComparer.Ordinal).ToList();
            pendentes.AddRange(propostas);
            Registrar("cn.propostas", new { conversa, falha = pior.ToString(), propostas = propostas.Select(p => $"{p.Reparador}: {p.Diff.Estrategia}, {p.Diff.Custo} tiles").ToArray() });
            if (propostas.Count == 0)
            {
                yield return new Falhar($"nenhuma proposta válida no Contract Net {conversa}");
                yield break;
            }

            var vizinho = k > 1 ? M.Nivel.ConteudoDoSlot(k - 1) : null;
            foreach (var proposta in propostas)
            {
                string conversaAprov = $"aprov:{conversa}:{proposta.Reparador}";
                var pedidoAprov = Pedido(Mundo.IdCurador,
                    new PedidoAprovacao(k, chunk.Id, proposta.Diff, proposta.Diff.AplicarEm(chunk.Mapa), M.Nivel.Slots, vizinho, conversa), conversaAprov,
                    () => Crencas.Tem("decisao", conversaAprov));
                foreach (var passo in PedirComReenvio($"decisão do curador sobre a proposta de {proposta.Reparador}", "P4", new[] { pedidoAprov }))
                    yield return passo;
                if (Crencas.Valor<Aprovacao>("decisao", conversaAprov) is not { } aprovacao) continue;

                pendentes.Remove(proposta);
                Enviar(proposta.Reparador, Performativa.AcceptProposal, chunk.Id, conversa);
                yield return new Aguardar($"edição provisória de {proposta.Reparador}",
                    () => Crencas.Tem("edicao", conversa), Tick + M.P.PrazoResposta, FalharNoPrazo: false);
                if (Crencas.Valor<int?>("edicao", conversa) is not int idEdicao)
                {
                    Registrar("decisao", new { decisao = "descartar proposta", motivo = Crencas.Valor<string>("edicao", conversa) });
                    Crencas.Remover("edicao", conversa);
                    continue;
                }
                Crencas.Remover("edicao", conversa);

                var edicao = M.Nivel.Edicao(idEdicao);
                string rotulo = $"s{k}e{idEdicao}|{chunk.Id}";
                var pedidosReval = M.PerfisAlvo.Select(p => Pedido(Mundo.IdJogador(p),
                    new PedidoPlaytest(rotulo, M.Nivel.Prefixo(k, edicao.Resultado), TrechoAberto: k < M.Nivel.Slots), $"slot:{k}",
                    () => Crencas.Tem("teste", $"{rotulo}|{p.Id}"))).ToList();
                foreach (var passo in PedirComReenvio($"revalidação da edição provisória #{idEdicao}", "P1", pedidosReval)) yield return passo;

                // Revalidação sem resposta conta como falha: a edição é desfeita.
                var revalidacao = M.PerfisAlvo.Select(p => Resultado(rotulo.Split('|')[0], chunk.Id, p)).ToList();
                if (revalidacao.All(r => r.Concluiu))
                {
                    M.Nivel.Commit(Cred, edicao, aprovacao.Id);
                    Crencas.Definir("slot", k, chunk.Id, "ação");
                    Crencas.Definir("orcamento", "nivel", Crencas.Valor<int>("orcamento", "nivel") - edicao.Diff.Custo, "ação");
                    Registrar("nivel.commit", new { edicao = idEdicao, slot = k, chunk = chunk.Id, reparador = proposta.Reparador, tiles = edicao.Diff.Custo });
                    Enviar(Mundo.IdCurador, Performativa.Inform, new SlotFechado(k, chunk.Id, edicao.Diff.Custo), $"slot:{k}");
                    yield break;
                }
                M.Nivel.Rollback(Cred, edicao);
                Registrar("nivel.rollback", new
                {
                    edicao = idEdicao, slot = k, reparador = proposta.Reparador,
                    motivo = "a revalidação falhou: " + string.Join("; ", revalidacao.Where(r => !r.Concluiu)),
                });
            }
            yield return new Falhar("nenhuma proposta aprovada resolveu a falha");
        }
        finally
        {
            _cnAbertas.Remove(conversa);
            // Nenhuma edição provisória desta negociação sobrevive a ela: se a confirmação do reparador
            // se perdeu (prazo esgotado) ou a intenção foi abandonada, a edição é desfeita e a reserva liberada.
            foreach (var aberta in M.Nivel.EdicoesAbertas(conversa))
            {
                M.Nivel.Rollback(Cred, aberta);
                Registrar("nivel.rollback", new { edicao = aberta.Id, slot = aberta.Slot, reparador = aberta.Reparador, motivo = "negociação encerrada sem confirmação da edição" });
            }
            // Encerra as conversas pendentes mesmo quando a intenção é abandonada (reconsideração).
            foreach (var p in pendentes) Enviar(p.Reparador, Performativa.RejectProposal, chunk.Id, conversa);
        }
    }

    private IEnumerable<Passo> ValidarNivel(ContextoPlano ctx)
    {
        var nivel = M.Nivel.Completo();
        string rotulo = $"final{++_validacoes}|nivel";
        var pedidos = M.PerfisAlvo.Select(p => Pedido(Mundo.IdJogador(p), new PedidoPlaytest(rotulo, nivel, TrechoAberto: false), "validacao",
            () => Crencas.Tem("teste", $"{rotulo}|{p.Id}"))).ToList();
        foreach (var passo in PedirComReenvio("playtest do nível completo", "P1", pedidos)) yield return passo;
        var falhas = M.PerfisAlvo.Select(p => Resultado(rotulo.Split('|')[0], "nivel", p)).Where(r => !r.Concluiu).ToList();
        if (falhas.Count > 0) yield return new Falhar("nível completo não concluído: " + string.Join("; ", falhas));
    }

    // ------------------------------------------------------------------ apoio

    private List<Chunk> Shortlist(int k)
    {
        var usados = M.Nivel.ChunksUsados.Select(c => c.Id).ToHashSet();
        var anterior = k > 1 ? M.Nivel.ChunkNoSlot(k - 1) : null;
        var tentados = Falhas(_tentados, k);
        IEnumerable<Chunk> candidatos = _ranking.Select(M.Pool.Obter).Where(c =>
            !usados.Contains(c.Id) && !tentados.Contains(c.Id) && !Crencas.Tem("vetado", c.Id) &&
            (anterior == null || !Crencas.Tem("incompativel", $"{anterior.Id}|{c.Id}")));
        if (Crencas.Valor<string>("exigencia_saida", k) == "alta")
            candidatos = candidatos.OrderByDescending(c => Grade.AlturaChao(c.Mapa, c.Mapa.Width - 1));
        switch (Crencas.Valor<string>("restricao", k))
        {
            case "final":
                candidatos = candidatos.OrderByDescending(SaidaPlana);
                break;
            case "esparso":
                candidatos = candidatos.OrderBy(c => AssinaturaJanela.De(c.Mapa).Densidade);
                break;
        }
        return candidatos.Take(M.P.MCandidatos).ToList();
    }

    // ------------------------------------------------------------------ pedidos com reenvio

    private sealed record PedidoPendente(string Para, object Conteudo, string Conversa, Func<bool> Respondido);

    private static PedidoPendente Pedido(string para, object conteudo, string conversa, Func<bool> respondido) =>
        new(para, conteudo, conversa, respondido);

    // Envia os pedidos, aguarda até o prazo e reenvia só os que ficaram sem resposta, até Reenvios
    // vezes. Não falha no prazo: quem chama decide o que fazer com o que faltou. O reenvio é seguro
    // porque quem responde é idempotente (cache do jogador; respostas repetidas só reafirmam crenças).
    private IEnumerable<Passo> PedirComReenvio(string descricao, string protocolo, IReadOnlyList<PedidoPendente> pedidos)
    {
        var pendentes = pedidos.ToList();
        for (int tentativa = 0; pendentes.Count > 0 && tentativa <= M.P.Reenvios; tentativa++)
        {
            if (tentativa > 0) Registrar("reenvio", new { protocolo, descricao, tentativa, pedidos = pendentes.Count });
            foreach (var p in pendentes) Enviar(p.Para, Performativa.Request, p.Conteudo, p.Conversa);
            var aguardando = pendentes;
            yield return new Aguardar(descricao, () => aguardando.All(p => p.Respondido()), Tick + M.P.PrazoResposta, FalharNoPrazo: false);
            pendentes = pendentes.Where(p => !p.Respondido()).ToList();
        }
        if (pendentes.Count > 0) Registrar("reenvio.esgotado", new { protocolo, descricao, sem_resposta = pendentes.Count });
    }

    private bool AtendeExigencia(int k, Chunk c) =>
        Crencas.Valor<string>("exigencia_saida", k) != "alta" || Grade.AlturaChao(c.Mapa, c.Mapa.Width - 1) >= 5;

    private static int SaidaPlana(Chunk c)
    {
        int h = Grade.AlturaChao(c.Mapa, c.Mapa.Width - 1);
        int n = 0;
        for (int x = c.Mapa.Width - 1; x >= c.Mapa.Width - 4 && h > 0 && Grade.AlturaChao(c.Mapa, x) == h; x--) n++;
        return n;
    }

    private ResultadoPlaytest Resultado(string rodada, string chunk, PerfilJogador p) =>
        Crencas.Valor<ResultadoPlaytest>("teste", $"{rodada}|{chunk}|{p.Id}")
        ?? new ResultadoPlaytest(p.Id, false, 0, 1, CausaFalha.Inconclusivo, 0, 0);

    private int Backtracks(int k) => _backtracks.GetValueOrDefault(k);

    private static HashSet<string> Falhas(Dictionary<int, HashSet<string>> d, int k) =>
        d.TryGetValue(k, out var s) ? s : d[k] = new HashSet<string>();
}
