using System.Text.Json;
using c2_pcg.flowMatchingDataloader;
using ChunkLink.Ambiente;
using ChunkLink.Estilo;
using ChunkLink.Movimento;

namespace ChunkLink.Comunicacao;

public enum Performativa { Request, Inform, Failure, Cfp, Propose, Refuse, AcceptProposal, RejectProposal, Agree }

// Papéis do SMA (DSM1, Seção 4). O papel define o que um agente pode enviar (política, CP10).
public enum Papel { Arquiteto, Curador, Jogador, Reparador }

// m = ⟨s, r, p, c, o, k⟩ (DSM1, Seção 6) + id e tempo lógico de envio.
public sealed record Mensagem(
    long Id, string Remetente, string Destinatario, Performativa Performativa, object Conteudo, string Conversa, long Tick,
    string Ontologia = "smb.level");

public sealed class DestinatarioDesconhecido(string id) : Exception($"destinatário desconhecido: {id}");

public sealed class ViolacaoProtocolo(string mensagem) : Exception(mensagem);

// Credencial emitida pelo barramento no registro do agente. É uma referência não forjável: só o
// barramento a constrói, e ele só aceita as que ele mesmo emitiu. O remetente de toda mensagem é o
// dono da credencial, não um texto informado por quem envia.
public sealed class Credencial
{
    internal Credencial(string id, Papel papel, MessageBus emissor)
    {
        Id = id;
        Papel = papel;
        Emissor = emissor;
    }

    public string Id { get; }
    public Papel Papel { get; }
    internal MessageBus Emissor { get; }

    public override string ToString() => $"{Id} ({Papel})";
}

public sealed class CredencialInvalida(string mensagem) : Exception(mensagem);

public sealed class ViolacaoPolitica(string mensagem) : Exception(mensagem);

// Política de ações por papel (CP10): pares (performativa, tipo de conteúdo) que cada papel pode
// enviar. Qualquer papel pode responder Failure com texto (pedido não entendido, erro de protocolo).
public static class PoliticaPapeis
{
    private static readonly Dictionary<Papel, (Performativa P, Type T)[]> Permitidas = new()
    {
        [Papel.Arquiteto] = new[]
        {
            (Performativa.Request, typeof(PedidoPlaytest)), (Performativa.Request, typeof(PedidoEstilo)),
            (Performativa.Request, typeof(PedidoAprovacao)), (Performativa.Inform, typeof(SlotFechado)),
            (Performativa.Inform, typeof(SlotReaberto)), (Performativa.Cfp, typeof(ChamadaReparo)),
            (Performativa.AcceptProposal, typeof(string)), (Performativa.RejectProposal, typeof(string)),
        },
        [Papel.Curador] = new[]
        {
            (Performativa.Inform, typeof(RespostaEstilo)), (Performativa.Inform, typeof(RestricaoCurador)),
            (Performativa.Agree, typeof(Aprovacao)), (Performativa.Refuse, typeof(RecusaAprovacao)),
        },
        [Papel.Jogador] = new[] { (Performativa.Inform, typeof(RespostaPlaytest)) },
        [Papel.Reparador] = new[]
        {
            (Performativa.Propose, typeof(PropostaReparo)), (Performativa.Refuse, typeof(string)),
            (Performativa.Inform, typeof(EdicaoAplicada)),
        },
    };

    public static string? Violacao(Credencial de, Performativa p, object conteudo)
    {
        bool permitida = p == Performativa.Failure && conteudo is string
            || Permitidas[de.Papel].Any(x => x.P == p && x.T == conteudo.GetType());
        if (!permitida) return $"o papel {de.Papel} não pode enviar {p}({conteudo.GetType().Name})";
        // Coerência entre conteúdo e remetente: ninguém fala em nome de outro agente.
        return conteudo switch
        {
            PropostaReparo pr when pr.Reparador != de.Id => $"proposta em nome de {pr.Reparador} enviada por {de.Id}",
            Aprovacao a when a.Emissor != de.Id => $"aprovação em nome de {a.Emissor} enviada por {de.Id}",
            _ => null,
        };
    }
}

// Injeção de falhas de entrega para testes de robustez: duplica ou perde mensagens com
// probabilidades fixas e semente determinística.
public sealed class InjetorFalhas(int semente, double probabilidadeDuplicar, double probabilidadePerder)
{
    private readonly Random _rng = new(semente);

    public (bool Perder, bool Duplicar) Sortear()
    {
        double r = _rng.NextDouble();
        return (r < probabilidadePerder, r >= probabilidadePerder && r < probabilidadePerder + probabilidadeDuplicar);
    }
}

// Barramento local: uma caixa postal por agente, entrega determinística em ordem de envio.
// Autentica o remetente pela credencial, aplica a política do papel e registra recusas no trace.
public sealed class MessageBus
{
    private readonly Dictionary<string, Queue<Mensagem>> _caixas = new();
    private readonly Dictionary<string, Credencial> _credenciais = new();
    private readonly Tracer _tracer;
    private long _proximoId = 1;

    public MessageBus(Tracer tracer) => _tracer = tracer;

    public long Enviadas { get; private set; }

    public InjetorFalhas? Falhas { get; set; }

    public Credencial Registrar(string agente, Papel papel)
    {
        if (_caixas.ContainsKey(agente)) throw new ArgumentException($"agente {agente} já registrado");
        var credencial = new Credencial(agente, papel, this);
        _caixas[agente] = new Queue<Mensagem>();
        _credenciais[agente] = credencial;
        return credencial;
    }

    // Verdadeiro se a credencial foi emitida por este barramento (e, se pedido, tem o papel exigido).
    public bool Autentico(Credencial? c, Papel? papel = null) =>
        c != null && ReferenceEquals(c.Emissor, this) && _credenciais.TryGetValue(c.Id, out var registrada)
        && ReferenceEquals(registrada, c) && (papel == null || c.Papel == papel);

    public Mensagem Enviar(Credencial de, string para, Performativa p, object conteudo, string conversa, long tick)
    {
        if (!Autentico(de))
        {
            _tracer.Registrar(tick, de?.Id ?? "?", "msg.rejeitada", new { para, performativa = p.ToString(), motivo = "credencial não emitida por este barramento" });
            throw new CredencialInvalida($"credencial de {de?.Id} não foi emitida por este barramento");
        }
        if (!_caixas.TryGetValue(para, out var caixa)) throw new DestinatarioDesconhecido(para);
        if (PoliticaPapeis.Violacao(de, p, conteudo) is { } violacao)
        {
            _tracer.Registrar(tick, de.Id, "msg.rejeitada", new { para, performativa = p.ToString(), motivo = violacao });
            throw new ViolacaoPolitica(violacao);
        }
        var m = new Mensagem(_proximoId++, de.Id, para, p, conteudo, conversa, tick);
        Enviadas++;
        _tracer.Registrar(tick, de.Id, "msg", new
        {
            id = m.Id, para, performativa = p.ToString(), conversa, conteudo = Conteudos.Resumo(conteudo),
        });
        var (perder, duplicar) = Falhas?.Sortear() ?? (false, false);
        if (perder)
        {
            _tracer.Registrar(tick, de.Id, "falha.perda", new { id = m.Id, para });
            return m;
        }
        caixa.Enqueue(m);
        if (duplicar)
        {
            caixa.Enqueue(m);
            _tracer.Registrar(tick, de.Id, "falha.duplicacao", new { id = m.Id, para });
        }
        return m;
    }

    // Só o dono lê a própria caixa postal.
    public List<Mensagem> Retirar(Credencial dono)
    {
        if (!Autentico(dono)) throw new CredencialInvalida($"credencial de {dono?.Id} não foi emitida por este barramento");
        var caixa = _caixas[dono.Id];
        var lista = caixa.ToList();
        caixa.Clear();
        return lista;
    }

    public bool TemPendentes => _caixas.Values.Any(c => c.Count > 0);
}

// Máquina de estados de uma conversa Contract Net vista pelo participante (P3).
public sealed class ConversaContractNet
{
    public enum Estado { Inicio, CfpRecebido, Proposto, Aceito, Encerrado, Concluido, Falhou }

    private static readonly Dictionary<(Estado, Performativa), Estado> Transicoes = new()
    {
        [(Estado.Inicio, Performativa.Cfp)] = Estado.CfpRecebido,
        [(Estado.CfpRecebido, Performativa.Propose)] = Estado.Proposto,
        [(Estado.CfpRecebido, Performativa.Refuse)] = Estado.Encerrado,
        [(Estado.Proposto, Performativa.AcceptProposal)] = Estado.Aceito,
        [(Estado.Proposto, Performativa.RejectProposal)] = Estado.Encerrado,
        [(Estado.Aceito, Performativa.Inform)] = Estado.Concluido,
        [(Estado.Aceito, Performativa.Failure)] = Estado.Falhou,
    };

    public Estado Atual { get; private set; } = Estado.Inicio;

    public Estado Avancar(Performativa p)
    {
        if (!Transicoes.TryGetValue((Atual, p), out var proximo))
            throw new ViolacaoProtocolo($"P3: {p} inválido no estado {Atual}");
        return Atual = proximo;
    }
}

// Conteúdos das mensagens (ontologia smb.level).
public sealed record PedidoPlaytest(string Rotulo, TileMap Trecho, bool TrechoAberto = true);
public sealed record RespostaPlaytest(string Rotulo, ResultadoPlaytest Resultado);

public sealed record CandidatoEstilo(string ChunkId, TileMap Conteudo);
public sealed record PedidoEstilo(int Slot, int TotalSlots, IReadOnlyList<CandidatoEstilo> Candidatos, TileMap? VizinhoEsquerdo);
public sealed record ParecerEstilo(string ChunkId, AvaliacaoEstilo Avaliacao, bool Aceitavel, string? Veto, string Justificativa, long? Aceite = null);
public sealed record RespostaEstilo(int Slot, IReadOnlyList<ParecerEstilo> Pareceres, double LimiarUsado, bool EstiloRelaxado);

// ConversaReparo amarra a aprovação à negociação Contract Net que produziu o diff.
public sealed record PedidoAprovacao(int Slot, string ChunkId, Diff Diff, TileMap Resultado, int TotalSlots, TileMap? VizinhoEsquerdo, string ConversaReparo);
public sealed record RecusaAprovacao(string Motivo, string Norma, int OrcamentoRestante);

// Falha a reparar: coluna local ao chunk, altura do chão antes da falha e alcance vertical do perfil.
public sealed record FalhaJogabilidade(int Slot, string ChunkId, ResultadoPlaytest Resultado, int ColunaLocal, int AlturaChaoAntes, int AlturaSalto, int AlcanceSalto = 4);
public sealed record ChamadaReparo(FalhaJogabilidade Falha, TileMap Candidato, int ReplyBy);
public sealed record PropostaReparo(string Reparador, Diff Diff, long EnviadaEm);
public sealed record EdicaoAplicada(int EdicaoId);

public sealed record RestricaoCurador(int Slot, string Tipo, string Motivo);
public sealed record SlotFechado(int Slot, string ChunkId, int TilesEditados);
public sealed record SlotReaberto(int Slot, string Motivo);

internal static class Conteudos
{
    // Resumo serializável do conteúdo para o trace (sem despejar grades inteiras).
    public static object Resumo(object c) => c switch
    {
        PedidoPlaytest p => new { p.Rotulo, largura = p.Trecho.Width },
        RespostaPlaytest r => new { r.Rotulo, resultado = r.Resultado.ToString() },
        PedidoEstilo p => new { p.Slot, candidatos = p.Candidatos.Select(x => x.ChunkId).ToArray() },
        RespostaEstilo r => new
        {
            r.Slot, limiar = Math.Round(r.LimiarUsado, 2), r.EstiloRelaxado,
            pareceres = r.Pareceres.Select(x => $"{x.ChunkId}: {(x.Aceitavel ? "aceitável" : "recusado")} ({x.Justificativa})").ToArray(),
        },
        PedidoAprovacao p => new { p.Slot, p.ChunkId, estrategia = p.Diff.Estrategia, custo = p.Diff.Custo },
        Aprovacao a => new { a.Id, a.Emissor, a.Conversa, a.Slot, a.ChunkId, a.HashDiff },
        RecusaAprovacao r => new { r.Motivo, r.Norma, r.OrcamentoRestante },
        ChamadaReparo cr => new { cr.Falha.Slot, cr.Falha.ChunkId, causa = cr.Falha.Resultado.Causa.ToString(), perfil = cr.Falha.Resultado.Perfil, cr.Falha.ColunaLocal, cr.ReplyBy },
        PropostaReparo p => new { p.Reparador, estrategia = p.Diff.Estrategia, custo = p.Diff.Custo },
        _ => c is string or int or long or double or bool ? c : JsonSerializer.SerializeToElement(c, c.GetType()),
    };
}
