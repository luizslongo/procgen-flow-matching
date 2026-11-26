using System.Text.Encodings.Web;
using System.Text.Json;

namespace ChunkLink.Comunicacao;

public sealed record EventoTrace(long Seq, long Tick, string Agente, string Tipo, string Dados);

// Trace estruturado (RNF03): toda mensagem, mudança de crença, intenção, plano, reconsideração e
// alteração do nível vira uma linha JSONL. Os testes e o cenário de depuração leem os mesmos eventos.
public sealed class Tracer
{
    private static readonly JsonSerializerOptions Opcoes = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly List<EventoTrace> _eventos = new();

    public IReadOnlyList<EventoTrace> Eventos => _eventos;

    public void Registrar(long tick, string agente, string tipo, object dados)
    {
        string json = dados is string s ? JsonSerializer.Serialize(s, Opcoes) : JsonSerializer.Serialize(dados, dados.GetType(), Opcoes);
        _eventos.Add(new EventoTrace(_eventos.Count + 1, tick, agente, tipo, json));
    }

    public IEnumerable<EventoTrace> DoTipo(string tipo) => _eventos.Where(e => e.Tipo == tipo);

    public void SalvarJsonl(string caminho)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(caminho))!);
        using var w = new StreamWriter(caminho);
        foreach (var e in _eventos)
            w.WriteLine($"{{\"seq\":{e.Seq},\"tick\":{e.Tick},\"agente\":{JsonSerializer.Serialize(e.Agente, Opcoes)},\"tipo\":{JsonSerializer.Serialize(e.Tipo, Opcoes)},\"dados\":{e.Dados}}}");
    }

    // Impressão digital do trace: execuções reprodutíveis produzem a mesma (RNF01).
    public string Assinatura()
    {
        var texto = string.Join('\n', _eventos.Select(e => $"{e.Tick}|{e.Agente}|{e.Tipo}|{e.Dados}"));
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(texto)))[..16];
    }
}
