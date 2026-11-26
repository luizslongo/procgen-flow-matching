using System.Globalization;
using c2_pcg.flowMatchingDataloader;

namespace ChunkLink.Ambiente;

public enum OrigemChunk { Bruto, ReparadoTcc, Manual, VglcOriginal }

// Um chunk do pool de montagem. O hash identifica o conteúdo congelado (RNF01).
public sealed class Chunk
{
    public Chunk(string id, TileMap mapa, OrigemChunk origem)
    {
        Id = id;
        Mapa = mapa;
        Origem = origem;
        Hash = Grade.Hash(mapa);
    }

    public string Id { get; }
    public TileMap Mapa { get; }
    public OrigemChunk Origem { get; }
    public string Hash { get; }
    public BiomeTypeEnum Bioma => Mapa.BiomeLabel;

    public override string ToString() => Id;
}

// Pool congelado de chunks gerados para um bioma (RF01). Formato em disco: um arquivo
// ASCII VGLC (14 × 28) por chunk e um manifesto pool.csv com id;arquivo;origem;bioma;hash.
public sealed class PoolChunks
{
    private readonly List<Chunk> _chunks;

    public PoolChunks(IEnumerable<Chunk> chunks)
    {
        _chunks = chunks.ToList();
        foreach (var c in _chunks) Validar(c);
        var repetidos = _chunks.GroupBy(c => c.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (repetidos.Count > 0) throw new FormatException("ids repetidos no pool: " + string.Join(", ", repetidos));
    }

    public IReadOnlyList<Chunk> Chunks => _chunks;

    public Chunk Obter(string id) => _chunks.First(c => c.Id == id);

    public static void Validar(Chunk c)
    {
        if (c.Mapa.Width != Grade.LarguraChunk || c.Mapa.Height != Grade.AlturaPadrao)
            throw new FormatException($"chunk {c.Id}: dimensão {c.Mapa.Width}×{c.Mapa.Height}, esperado {Grade.LarguraChunk}×{Grade.AlturaPadrao}");
        if (c.Origem == OrigemChunk.VglcOriginal)
            throw new FormatException($"chunk {c.Id}: origem VGLC-original não pode entrar no pool de montagem (N4)");
        if (c.Mapa.Tiles.Any(t => t == TileTypeEnum.Error))
            throw new FormatException($"chunk {c.Id}: contém tile inválido");
    }

    public static PoolChunks Carregar(string diretorio)
    {
        string manifesto = Path.Combine(diretorio, "pool.csv");
        if (!File.Exists(manifesto)) throw new FileNotFoundException("manifesto pool.csv não encontrado", manifesto);
        var chunks = new List<Chunk>();
        foreach (var linha in File.ReadAllLines(manifesto).Skip(1).Where(l => l.Trim().Length > 0))
        {
            var campos = linha.Split(';');
            if (campos.Length < 5) throw new FormatException("linha inválida no manifesto: " + linha);
            var bioma = Enum.Parse<BiomeTypeEnum>(campos[3], ignoreCase: true);
            var linhas = File.ReadAllLines(Path.Combine(diretorio, campos[1])).Where(l => l.Length > 0).ToList();
            var chunk = new Chunk(campos[0], Grade.DeAscii(linhas, bioma), Enum.Parse<OrigemChunk>(campos[2], ignoreCase: true));
            if (!string.Equals(chunk.Hash, campos[4], StringComparison.OrdinalIgnoreCase))
                throw new FormatException($"chunk {chunk.Id}: hash {chunk.Hash} difere do manifesto ({campos[4]}); pool alterado");
            chunks.Add(chunk);
        }
        return new PoolChunks(chunks);
    }

    public void Salvar(string diretorio)
    {
        Directory.CreateDirectory(diretorio);
        var manifesto = new List<string> { "id;arquivo;origem;bioma;hash" };
        foreach (var c in _chunks)
        {
            string arquivo = c.Id + ".txt";
            File.WriteAllLines(Path.Combine(diretorio, arquivo), Grade.ParaAscii(c.Mapa));
            manifesto.Add(string.Join(';', c.Id, arquivo, c.Origem, c.Bioma, c.Hash));
        }
        File.WriteAllLines(Path.Combine(diretorio, "pool.csv"), manifesto);
    }

    // Lê a saída de texto da CLI de geração do TCC (blocos "=== Generated chunk i / n ===").
    public static List<TileMap> LerSaidaDoGerador(string arquivoLog, BiomeTypeEnum bioma)
    {
        var mapas = new List<TileMap>();
        var linhas = File.ReadAllLines(arquivoLog);
        for (int i = 0; i < linhas.Length; i++)
        {
            if (!linhas[i].StartsWith("=== Generated chunk", StringComparison.Ordinal)) continue;
            var bloco = linhas.Skip(i + 1).Take(Grade.AlturaPadrao).ToList();
            mapas.Add(Grade.DeAscii(bloco, bioma));
            i += Grade.AlturaPadrao;
        }
        return mapas;
    }

    public static string IdPara(BiomeTypeEnum bioma, OrigemChunk origem, int indice) =>
        string.Create(CultureInfo.InvariantCulture, $"{bioma.ToString().ToLowerInvariant()[..3]}-{(origem == OrigemChunk.Bruto ? "b" : "r")}{indice:D3}");
}

// Corpus de referência: níveis originais do VGLC de um bioma. Fonte do estilo perseguido
// pelo curador e do lado original do teste cego; nunca material de montagem (N4).
public sealed class CorpusReferencia
{
    public CorpusReferencia(IReadOnlyList<(string Nome, TileMap Mapa)> niveis) => Niveis = niveis;

    public IReadOnlyList<(string Nome, TileMap Mapa)> Niveis { get; }

    // Carrega os níveis de um bioma a partir do diretório Processed do VGLC.
    public static CorpusReferencia DoVglc(string diretorioProcessed, BiomeTypeEnum bioma)
    {
        var niveis = new List<(string, TileMap)>();
        foreach (var arquivo in Directory.GetFiles(diretorioProcessed, "mario-*.txt").OrderBy(f => f, StringComparer.Ordinal))
        {
            string nome = Path.GetFileNameWithoutExtension(arquivo);
            if (BiomeFromVglcLevelName.BiomeOfLevel(nome) != bioma) continue;
            var linhas = File.ReadAllLines(arquivo).Where(l => l.Length > 0).ToList();
            niveis.Add((nome, Grade.DeAscii(linhas, bioma)));
        }
        if (niveis.Count == 0) throw new FileNotFoundException($"nenhum nível do bioma {bioma} em {diretorioProcessed}");
        return new CorpusReferencia(niveis);
    }

    // Carrega um corpus de arquivos ASCII quaisquer (usado pelo cenário controlado e pelos testes).
    public static CorpusReferencia DeArquivos(IEnumerable<string> arquivos, BiomeTypeEnum bioma) =>
        new(arquivos.OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => (Path.GetFileNameWithoutExtension(f), Grade.DeAscii(File.ReadAllLines(f).Where(l => l.Length > 0).ToList(), bioma)))
            .ToList());
}
