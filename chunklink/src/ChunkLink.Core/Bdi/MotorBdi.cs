using ChunkLink.Comunicacao;

namespace ChunkLink.Bdi;

// Crença: proposição (predicado, chave) com valor, fonte (percepção, mensagem, inferência) e tempo.
public sealed record Crenca(string Predicado, string Chave, object? Valor, string Fonte, long Tick);

// Base de crenças com versão: toda revisão incrementa Versao e é registrada no trace.
public sealed class BaseCrencas
{
    private readonly Dictionary<(string, string), Crenca> _crencas = new();
    private readonly string _agente;
    private readonly Tracer _tracer;
    private readonly Func<long> _relogio;

    public BaseCrencas(string agente, Tracer tracer, Func<long> relogio)
    {
        _agente = agente;
        _tracer = tracer;
        _relogio = relogio;
    }

    public long Versao { get; private set; }

    public void Definir(string predicado, object chave, object? valor, string fonte)
    {
        var k = (predicado, Convert.ToString(chave, System.Globalization.CultureInfo.InvariantCulture)!);
        if (_crencas.TryGetValue(k, out var atual) && Equals(atual.Valor, valor)) return;
        _crencas[k] = new Crenca(k.predicado, k.Item2, valor, fonte, _relogio());
        Versao++;
        _tracer.Registrar(_relogio(), _agente, "crenca+", new { predicado, chave = k.Item2, valor = Resumir(valor), fonte });
    }

    public bool Remover(string predicado, object chave)
    {
        var k = (predicado, Convert.ToString(chave, System.Globalization.CultureInfo.InvariantCulture)!);
        if (!_crencas.Remove(k)) return false;
        Versao++;
        _tracer.Registrar(_relogio(), _agente, "crenca-", new { predicado, chave = k.Item2 });
        return true;
    }

    public bool Tem(string predicado, object chave) =>
        _crencas.ContainsKey((predicado, Convert.ToString(chave, System.Globalization.CultureInfo.InvariantCulture)!));

    public T? Valor<T>(string predicado, object chave) =>
        _crencas.TryGetValue((predicado, Convert.ToString(chave, System.Globalization.CultureInfo.InvariantCulture)!), out var c) && c.Valor is T v ? v : default;

    public IEnumerable<Crenca> Todas(string predicado) => _crencas.Values.Where(c => c.Predicado == predicado);

    private static object? Resumir(object? v) => v switch
    {
        null => null,
        string or int or long or double or bool => v,
        _ => v.ToString(),
    };
}

// Passos que o corpo de um plano (corrotina) devolve ao interpretador.
public abstract record Passo;
// FalharNoPrazo = false: esgotado o prazo, o plano apenas continua e decide o que fazer com o que faltou.
public sealed record Aguardar(string Descricao, Func<bool> Condicao, long? Prazo = null, bool FalharNoPrazo = true) : Passo;
public sealed record SubObjetivo : Passo
{
    public SubObjetivo(string objetivo, params object[] args)
    {
        Objetivo = objetivo;
        Args = args;
    }

    public string Objetivo { get; }
    public object[] Args { get; }
}
public sealed record Falhar(string Motivo) : Passo;
public sealed record Ceder() : Passo;

public sealed class FalhaPlano(string motivo) : Exception(motivo);

public sealed class ContextoPlano
{
    internal ContextoPlano(object[] args, string? motivoFalha) { Args = args; MotivoFalha = motivoFalha; }

    public object[] Args { get; }
    public string? MotivoFalha { get; }
    public T Arg<T>(int i) => (T)Args[i];
}

// Plano: gatilho (+!objetivo ou -!objetivo), contexto de aplicabilidade e corpo (corrotina).
public sealed class Plano
{
    public Plano(string nome, string gatilho, Func<BaseCrencas, object[], bool> contexto, Func<ContextoPlano, IEnumerable<Passo>> corpo)
    {
        Nome = nome;
        Gatilho = gatilho;
        Contexto = contexto;
        Corpo = corpo;
    }

    public string Nome { get; }
    public string Gatilho { get; }
    public Func<BaseCrencas, object[], bool> Contexto { get; }
    public Func<ContextoPlano, IEnumerable<Passo>> Corpo { get; }

    public static Plano Para(string objetivo, string nome, Func<BaseCrencas, object[], bool> contexto, Func<ContextoPlano, IEnumerable<Passo>> corpo) =>
        new(nome, "+!" + objetivo, contexto, corpo);

    public static Plano SeFalhar(string objetivo, string nome, Func<BaseCrencas, object[], bool> contexto, Func<ContextoPlano, IEnumerable<Passo>> corpo) =>
        new(nome, "-!" + objetivo, contexto, corpo);
}

// Desejo: estado desejável, com prioridade; vira opção quando Ativo(crenças) é verdadeiro.
public sealed record Desejo(string Nome, int Prioridade, string Objetivo, Func<BaseCrencas, bool> Ativo, Func<BaseCrencas, object[]>? Argumentos = null);

public enum EstadoIntencao { Ativa, Concluida, Abandonada, Falhou }

internal sealed class Quadro
{
    public required string Objetivo { get; init; }
    public required object[] Args { get; init; }
    public required Plano Plano { get; init; }
    public required IEnumerator<Passo> Execucao { get; init; }
    public bool EhTratador { get; init; }
    public Aguardar? Esperando { get; set; }
}

// Intenção: compromisso com um desejo, realizado por uma pilha de (sub)objetivos e planos.
public sealed class Intencao
{
    internal Intencao(int id, Desejo desejo) { Id = id; Desejo = desejo; }

    public int Id { get; }
    public Desejo Desejo { get; }
    public EstadoIntencao Estado { get; internal set; } = EstadoIntencao.Ativa;
    public string? Motivo { get; internal set; }
    internal Stack<Quadro> Pilha { get; } = new();

    // Objetivos em execução, do topo para a base.
    public IReadOnlyList<(string Objetivo, object[] Args)> Objetivos => Pilha.Select(q => (q.Objetivo, q.Args)).ToList();

    public bool Contem(string objetivo, Func<object[], bool>? filtro = null) =>
        Pilha.Any(q => q.Objetivo == objetivo && (filtro == null || filtro(q.Args)));
}

// Decisões que a política de reconsideração pode tomar sobre uma intenção.
public abstract record Reconsideracao(string Motivo);
public sealed record AbandonarObjetivo(string Objetivo, string Motivo) : Reconsideracao(Motivo);
public sealed record AbandonarIntencao(string Motivo) : Reconsideracao(Motivo);

// Interpretador BDI mínimo, no estilo AgentSpeak: crenças -> opções (desejos) -> intenções -> planos.
// Ciclo: (1) adota intenções para desejos ativos sem intenção; (2) aplica a política de
// reconsideração; (3) avança cada intenção ativa até ela aguardar, ceder, concluir ou falhar.
public sealed class MotorBdi
{
    private readonly string _agente;
    private readonly Tracer _tracer;
    private readonly Func<long> _relogio;
    private readonly List<Intencao> _intencoes = new();
    private int _proximaIntencao = 1;

    public MotorBdi(string agente, BaseCrencas crencas, Tracer tracer, Func<long> relogio)
    {
        _agente = agente;
        Crencas = crencas;
        _tracer = tracer;
        _relogio = relogio;
    }

    public BaseCrencas Crencas { get; }
    public List<Plano> Planos { get; } = new();
    public List<Desejo> Desejos { get; } = new();
    public Func<Intencao, BaseCrencas, Reconsideracao?>? Politica { get; set; }

    public IReadOnlyList<Intencao> Intencoes => _intencoes;

    public bool Ocioso => _intencoes.All(i => i.Estado != EstadoIntencao.Ativa);

    public void Ciclo()
    {
        foreach (var d in Desejos.OrderByDescending(d => d.Prioridade))
            if (d.Ativo(Crencas) && !_intencoes.Any(i => i.Estado == EstadoIntencao.Ativa && i.Desejo.Nome == d.Nome))
                Adotar(d);

        if (Politica != null)
            foreach (var i in Ativas())
                if (Politica(i, Crencas) is { } r) Aplicar(i, r);

        foreach (var i in Ativas()) Executar(i);
    }

    private List<Intencao> Ativas() =>
        _intencoes.Where(i => i.Estado == EstadoIntencao.Ativa).OrderByDescending(i => i.Desejo.Prioridade).ThenBy(i => i.Id).ToList();

    private void Registrar(string tipo, object dados) => _tracer.Registrar(_relogio(), _agente, tipo, dados);

    private void Adotar(Desejo d)
    {
        var args = d.Argumentos?.Invoke(Crencas) ?? Array.Empty<object>();
        var plano = Selecionar("+!" + d.Objetivo, args);
        if (plano == null)
        {
            Registrar("desejo.sem_plano", new { desejo = d.Nome, objetivo = d.Objetivo });
            return;
        }
        var i = new Intencao(_proximaIntencao++, d);
        _intencoes.Add(i);
        Registrar("intencao.adotada", new { intencao = i.Id, desejo = d.Nome, objetivo = Rotulo(d.Objetivo, args) });
        Empilhar(i, d.Objetivo, args, plano, tratador: false, motivoFalha: null);
    }

    private Plano? Selecionar(string gatilho, object[] args) =>
        Planos.FirstOrDefault(p => p.Gatilho == gatilho && p.Contexto(Crencas, args));

    private void Empilhar(Intencao i, string objetivo, object[] args, Plano plano, bool tratador, string? motivoFalha)
    {
        i.Pilha.Push(new Quadro
        {
            Objetivo = objetivo, Args = args, Plano = plano, EhTratador = tratador,
            Execucao = plano.Corpo(new ContextoPlano(args, motivoFalha)).GetEnumerator(),
        });
        Registrar("plano.selecionado", new { intencao = i.Id, objetivo = Rotulo(objetivo, args), plano = plano.Nome, tratador });
    }

    private void Executar(Intencao i)
    {
        for (int passos = 0; passos < 200 && i.Estado == EstadoIntencao.Ativa; passos++)
        {
            if (i.Pilha.Count == 0)
            {
                Concluir(i);
                return;
            }
            var q = i.Pilha.Peek();
            if (q.Esperando is { } espera)
            {
                if (espera.Condicao()) q.Esperando = null;
                else if (espera.Prazo is long prazo && _relogio() > prazo)
                {
                    q.Esperando = null;
                    if (!espera.FalharNoPrazo) continue;
                    Falha(i, $"prazo esgotado aguardando {espera.Descricao}");
                    continue;
                }
                else return;
            }

            bool continua;
            try
            {
                continua = q.Execucao.MoveNext();
            }
            catch (Exception e) when (e is FalhaPlano or Ambiente.ViolacaoNorma or Ambiente.ConflitoReserva or ViolacaoProtocolo or ViolacaoPolitica or CredencialInvalida)
            {
                Falha(i, e.Message);
                continue;
            }

            if (!continua)
            {
                i.Pilha.Pop();
                q.Execucao.Dispose();
                Registrar("objetivo.atingido", new { intencao = i.Id, objetivo = Rotulo(q.Objetivo, q.Args), plano = q.Plano.Nome });
                continue;
            }

            switch (q.Execucao.Current)
            {
                case Aguardar a:
                    q.Esperando = a;
                    break;
                case SubObjetivo s:
                    var plano = Selecionar("+!" + s.Objetivo, s.Args);
                    if (plano != null)
                    {
                        Empilhar(i, s.Objetivo, s.Args, plano, tratador: false, motivoFalha: null);
                        break;
                    }
                    // Sem plano aplicável: o próprio subobjetivo falha e pode ser tratado por -!objetivo.
                    string semPlano = $"nenhum plano aplicável para !{Rotulo(s.Objetivo, s.Args)}";
                    Registrar("plano.ausente", new { intencao = i.Id, objetivo = Rotulo(s.Objetivo, s.Args) });
                    if (Selecionar("-!" + s.Objetivo, s.Args) is { } tratadorSemPlano)
                        Empilhar(i, s.Objetivo, s.Args, tratadorSemPlano, tratador: true, motivoFalha: semPlano);
                    else
                        Falha(i, semPlano);
                    break;
                case Falhar f:
                    Falha(i, f.Motivo);
                    break;
                case Ceder:
                    return;
            }
        }
    }

    private void Concluir(Intencao i)
    {
        i.Estado = EstadoIntencao.Concluida;
        Registrar("intencao.concluida", new { intencao = i.Id, desejo = i.Desejo.Nome });
    }

    // Falha do quadro do topo: procura um plano -!objetivo aplicável; se não houver, propaga.
    private void Falha(Intencao i, string motivo)
    {
        var q = i.Pilha.Pop();
        q.Execucao.Dispose();
        Registrar("plano.falhou", new { intencao = i.Id, objetivo = Rotulo(q.Objetivo, q.Args), plano = q.Plano.Nome, motivo });

        if (!q.EhTratador && Selecionar("-!" + q.Objetivo, q.Args) is { } tratador)
        {
            Empilhar(i, q.Objetivo, q.Args, tratador, tratador: true, motivoFalha: motivo);
            return;
        }
        if (i.Pilha.Count == 0)
        {
            i.Estado = EstadoIntencao.Falhou;
            i.Motivo = motivo;
            Registrar("intencao.falhou", new { intencao = i.Id, desejo = i.Desejo.Nome, motivo });
            return;
        }
        i.Pilha.Peek().Esperando = null;
        Falha(i, $"subobjetivo !{Rotulo(q.Objetivo, q.Args)} falhou: {motivo}");
    }

    private void Aplicar(Intencao i, Reconsideracao r)
    {
        switch (r)
        {
            case AbandonarIntencao a:
                Registrar("reconsideracao", new { intencao = i.Id, decisao = "abandonar intenção", motivo = a.Motivo });
                while (i.Pilha.Count > 0) i.Pilha.Pop().Execucao.Dispose();
                i.Estado = EstadoIntencao.Abandonada;
                i.Motivo = a.Motivo;
                Registrar("intencao.abandonada", new { intencao = i.Id, desejo = i.Desejo.Nome, motivo = a.Motivo });
                break;
            case AbandonarObjetivo o when i.Contem(o.Objetivo):
                Registrar("reconsideracao", new { intencao = i.Id, decisao = $"abandonar !{o.Objetivo}", motivo = o.Motivo });
                while (i.Pilha.Peek().Objetivo != o.Objetivo)
                {
                    var acima = i.Pilha.Pop();
                    acima.Execucao.Dispose();
                    Registrar("objetivo.abandonado", new { intencao = i.Id, objetivo = Rotulo(acima.Objetivo, acima.Args) });
                }
                i.Pilha.Peek().Esperando = null;
                Falha(i, "reconsideração: " + o.Motivo);
                break;
        }
    }

    private static string Rotulo(string objetivo, object[] args) =>
        args.Length == 0 ? objetivo : $"{objetivo}({string.Join(", ", args.Select(a => a is System.Collections.ICollection ? "…" : a))})";
}
