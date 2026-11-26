using Microsoft.Data.Sqlite;

namespace ChunkLink.Memoria;

// Um episódio lembrado entre execuções: um fato aprendido (tipo, chave, valor) no contexto de um
// bioma, com a execução em que foi visto pela última vez.
public sealed record Episodio(string Tipo, string Chave, string Valor, string Bioma, long Execucao);

// Memória episódica persistente (CP9) em SQLite. O tempo de vida (TTL) é medido em execuções, não
// em relógio de parede: um episódio que não é reforçado em `ttlExecucoes` execuções expira. Assim
// o comportamento continua reprodutível a partir do mesmo arquivo de memória.
public sealed class MemoriaEpisodica : IDisposable
{
    private readonly SqliteConnection _conexao;

    public MemoriaEpisodica(string caminho, int ttlExecucoes)
    {
        if (ttlExecucoes < 1) throw new ArgumentOutOfRangeException(nameof(ttlExecucoes));
        TtlExecucoes = ttlExecucoes;
        _conexao = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = caminho, Pooling = false }.ToString());
        _conexao.Open();
        Executar("""
            CREATE TABLE IF NOT EXISTS meta (chave TEXT PRIMARY KEY, valor INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS episodios (
                tipo TEXT NOT NULL, chave TEXT NOT NULL, bioma TEXT NOT NULL,
                valor TEXT NOT NULL, execucao INTEGER NOT NULL,
                PRIMARY KEY (tipo, chave, bioma));
            """);
    }

    public int TtlExecucoes { get; }

    public long ExecucaoAtual { get; private set; }

    // Abre uma nova execução: incrementa o contador e apaga os episódios expirados.
    public long IniciarExecucao()
    {
        using var cmd = _conexao.CreateCommand();
        cmd.CommandText = """
            INSERT INTO meta (chave, valor) VALUES ('execucao', 1)
                ON CONFLICT(chave) DO UPDATE SET valor = valor + 1;
            SELECT valor FROM meta WHERE chave = 'execucao';
            """;
        ExecucaoAtual = (long)cmd.ExecuteScalar()!;
        using var limpar = _conexao.CreateCommand();
        limpar.CommandText = "DELETE FROM episodios WHERE execucao <= $limite";
        limpar.Parameters.AddWithValue("$limite", ExecucaoAtual - TtlExecucoes - 1);
        limpar.ExecuteNonQuery();
        return ExecucaoAtual;
    }

    public IReadOnlyList<Episodio> Carregar(string bioma)
    {
        using var cmd = _conexao.CreateCommand();
        cmd.CommandText = "SELECT tipo, chave, valor, bioma, execucao FROM episodios WHERE bioma = $bioma ORDER BY tipo, chave";
        cmd.Parameters.AddWithValue("$bioma", bioma);
        var lista = new List<Episodio>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) lista.Add(new Episodio(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetInt64(4)));
        return lista;
    }

    // Grava ou reforça um episódio na execução atual (o reforço renova o TTL).
    public void Lembrar(string tipo, string chave, string valor, string bioma)
    {
        if (ExecucaoAtual == 0) throw new InvalidOperationException("chame IniciarExecucao antes de lembrar episódios");
        using var cmd = _conexao.CreateCommand();
        cmd.CommandText = """
            INSERT INTO episodios (tipo, chave, bioma, valor, execucao) VALUES ($tipo, $chave, $bioma, $valor, $execucao)
                ON CONFLICT(tipo, chave, bioma) DO UPDATE SET valor = excluded.valor, execucao = excluded.execucao;
            """;
        cmd.Parameters.AddWithValue("$tipo", tipo);
        cmd.Parameters.AddWithValue("$chave", chave);
        cmd.Parameters.AddWithValue("$bioma", bioma);
        cmd.Parameters.AddWithValue("$valor", valor);
        cmd.Parameters.AddWithValue("$execucao", ExecucaoAtual);
        cmd.ExecuteNonQuery();
    }

    private void Executar(string sql)
    {
        using var cmd = _conexao.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _conexao.Dispose();
}
