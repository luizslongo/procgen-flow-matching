using ChunkLink.Ambiente;
using ChunkLink.Comunicacao;
using ChunkLink.Estilo;
using ChunkLink.Movimento;

namespace ChunkLink.Agentes;

// Parâmetros de projeto (DSM1, modelo.yaml) — valores iniciais, a calibrar no experimento.
public sealed record Parametros
{
    public int Slots { get; init; } = 4;
    public int MCandidatos { get; init; } = 5;          // M: candidatos testados por rodada
    public int BBacktracks { get; init; } = 3;          // B: retrocessos permitidos por slot
    public int EMax { get; init; } = 40;                // E_max: tiles editáveis no nível (N1)
    public int EMaxChunk { get; init; } = 10;           // e_max: tiles editáveis por chunk (N1)
    public int ReplyBy { get; init; } = 2;              // prazo do Contract Net, em ticks
    public double TauCopia { get; init; } = 0.10;       // distância mínima a janelas originais (N4)
    public double LimiarEstilo { get; init; } = 1.5;    // distância de estilo aceita pelo curador
    public int ImpassesParaConceder { get; init; } = 2; // recusas por estilo antes da concessão
    public int PrazoResposta { get; init; } = 6;        // ticks para aguardar respostas
    public int Reenvios { get; init; } = 2;             // reenvios de pedidos sem resposta antes de desistir
    public int JanelaCostura { get; init; } = 2;        // colunas após a fronteira que contam como costura
    public int FalhasParaCulpa { get; init; } = 2;      // candidatos distintos falhando na costura
    public int MaxTicks { get; init; } = 4000;
    public int TtlMemoria { get; init; } = 5;           // execuções que um episódio sobrevive sem ser reforçado (CP9)
    // Ablação: false desliga inferência de culpa, reconsideração do arquiteto e concessões do curador.
    public bool Cognicao { get; init; } = true;
}

public sealed class Relogio
{
    public long Tick { get; set; }
}

// Contexto compartilhado do sistema: barramento, trace, relógio e artefatos do ambiente.
public sealed class Mundo
{
    public required MessageBus Bus { get; init; }
    public required Tracer Tracer { get; init; }
    public required Relogio Relogio { get; init; }
    public required Nivel Nivel { get; init; }
    public required PoolChunks Pool { get; init; }
    public required ConhecimentoEstilo Estilo { get; init; }
    public required Parametros P { get; init; }
    public required IReadOnlyList<PerfilJogador> PerfisAlvo { get; init; }
    public List<string> Reparadores { get; } = new();
    public Memoria.MemoriaEpisodica? Memoria { get; init; }

    public const string IdArquiteto = "arquiteto";
    public const string IdCurador = "curador";

    public static string IdJogador(PerfilJogador p) => "jogador-" + p.Id;
}

public abstract class Agente
{
    private readonly HashSet<long> _recebidas = new();

    protected Agente(string id, Papel papel, Mundo mundo)
    {
        Id = id;
        M = mundo;
        Cred = mundo.Bus.Registrar(id, papel);
    }

    public string Id { get; }
    protected Mundo M { get; }
    // Credencial emitida pelo barramento: identifica o agente nas mensagens e no artefato Nível.
    protected Credencial Cred { get; }
    protected long Tick => M.Relogio.Tick;

    public abstract void Ciclo();

    protected void Enviar(string para, Performativa p, object conteudo, string conversa) =>
        M.Bus.Enviar(Cred, para, p, conteudo, conversa, Tick);

    // Idempotência: uma mensagem entregue de novo (mesmo id) é registrada e descartada.
    protected IEnumerable<Mensagem> Receber()
    {
        foreach (var m in M.Bus.Retirar(Cred))
        {
            if (_recebidas.Add(m.Id)) yield return m;
            else Registrar("msg.duplicada", new { id = m.Id, de = m.Remetente, performativa = m.Performativa.ToString(), conversa = m.Conversa });
        }
    }

    protected void Registrar(string tipo, object dados) => M.Tracer.Registrar(Tick, Id, tipo, dados);
}

// Jogador A*: agente deliberativo (busca) que responde a pedidos de playtest com o resultado do
// seu perfil. Mantém um cache por trecho (estado) e decide sozinho como classificar a falha.
public sealed class PlaytesterAgent : Agente
{
    private readonly Dictionary<string, ResultadoPlaytest> _cache = new();

    public PlaytesterAgent(PerfilJogador perfil, Mundo mundo) : base(Mundo.IdJogador(perfil), Papel.Jogador, mundo) => Perfil = perfil;

    public PerfilJogador Perfil { get; }

    public override void Ciclo()
    {
        foreach (var m in Receber())
        {
            if (m.Performativa == Performativa.Request && m.Conteudo is PedidoPlaytest p)
            {
                string chave = Grade.Hash(p.Trecho) + "/" + p.Trecho.Width + (p.TrechoAberto ? "/aberto" : "/fechado");
                if (!_cache.TryGetValue(chave, out var r))
                {
                    r = BuscaAEstrela.Jogar(p.Trecho, Perfil, p.TrechoAberto);
                    _cache[chave] = r;
                }
                Enviar(m.Remetente, Performativa.Inform, new RespostaPlaytest(p.Rotulo, r), m.Conversa);
            }
            else if (m.Performativa != Performativa.Failure)
            {
                Registrar("msg.inesperada", new { id = m.Id, de = m.Remetente, performativa = m.Performativa.ToString() });
                Enviar(m.Remetente, Performativa.Failure, "pedido não entendido pelo jogador", m.Conversa);
            }
        }
    }
}
