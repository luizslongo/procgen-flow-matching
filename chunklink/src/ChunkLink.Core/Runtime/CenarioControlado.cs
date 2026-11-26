using c2_pcg.flowMatchingDataloader;
using ChunkLink.Agentes;
using ChunkLink.Ambiente;
using ChunkLink.Movimento;

namespace ChunkLink.Runtime;

// Cenário controlado para depuração: pool pequeno, escrito à mão, em que cada mecanismo do SMA
// aparece de forma determinística e verificável no trace:
//   a-baixo   chão plano, saída baixa (altura 2)              → quem vem depois precisa começar baixo
//   b-muro1/2 começam com muro de altura 7                     → falham na costura após saída baixa
//   c-teto    túnel de uma célula de altura                    → só o Mario grande falha (TETO)
//   d-escada  termina numa escada até a altura 7               → saída alta: resolve os muros
//   e-buraco1 buraco de 5 colunas (> alcance 4)                → falha para todos (BURACO)
//   f-copia   cópia quase exata de uma janela original         → veto N4 do curador
// Referência: dois "níveis originais" de 84 colunas com início plano, meio com tubo e fim plano.
public static class CenarioControlado
{
    private static readonly string Vazio = new('-', 28);
    private static readonly string Chao = new('X', 28);

    public static Parametros Parametros => new()
    {
        Slots = 4, MCandidatos = 2, BBacktracks = 3, EMax = 40, EMaxChunk = 10, ReplyBy = 2, FalhasParaCulpa = 2,
    };

    public static IReadOnlyList<PerfilJogador> Perfis => new[] { PerfilJogador.Pequeno, PerfilJogador.Grande };

    public static SistemaChunkLink Criar(
        IReadOnlyList<EspecificacaoReparador>? reparadores = null, Memoria.MemoriaEpisodica? memoria = null,
        Comunicacao.InjetorFalhas? falhas = null) =>
        new(Pool(), Referencia(), Parametros, Perfis, BiomeTypeEnum.Overworld, reparadores, memoria, falhas);

    public static PoolChunks Pool() => new(new[]
    {
        Novo("a-baixo", (8, "--------S?S-----------------"), (11, "--------------------E-------")),
        Novo("b-muro1", (4, "-----------ooo--------------"), (7, Muro()), (8, Muro()), (9, Muro()), (10, Muro()), (11, Muro())),
        Novo("b-muro2", (7, Muro()), (8, Muro("-----------------?")), (9, Muro()), (10, Muro()), (11, Muro())),
        Novo("c-teto", Enumerable.Range(3, 8).Select(y => (y, "--------XXXXXXXXX-----------")).ToArray()),
        Novo("d-escada",
            (7, "------------------------XXXX"), (8, "------?----------------XXXXX"), (9, "----------------------XXXXXX"),
            (10, "---------------------XXXXXXX"), (11, "--------------------XXXXXXXX")),
        Novo("e-buraco1", (8, "----?-----------------------"), (12, "XXXXXXXXXX-----XXXXXXXXXXXXX"), (13, "XXXXXXXXXX-----XXXXXXXXXXXXX")),
        new Chunk("f-copia", CopiaDeOriginal(), OrigemChunk.Manual),
    });

    public static CorpusReferencia Referencia() => new(new[] { ("orig-1", Original1()), ("orig-2", Original2()) });

    private static string Muro(string resto = "") => ("XXX" + resto).PadRight(28, '-');

    private static Chunk Novo(string id, params (int Linha, string Conteudo)[] linhas)
    {
        var rows = Enumerable.Range(0, 14).Select(y => y >= 12 ? Chao : Vazio).ToArray();
        foreach (var (linha, conteudo) in linhas) rows[linha] = conteudo;
        return new Chunk(id, Grade.DeAscii(rows, BiomeTypeEnum.Overworld), OrigemChunk.Manual);
    }

    private static void Faixa(TileMap m, int y, int x0, int x1, TileTypeEnum t)
    {
        for (int x = x0; x <= x1; x++) Grade.Definir(m, x, y, t);
    }

    private static TileMap NivelBase()
    {
        var m = Grade.Criar(84);
        Faixa(m, 12, 0, 83, TileTypeEnum.Solid);
        Faixa(m, 13, 0, 83, TileTypeEnum.Solid);
        return m;
    }

    // Início plano com blocos "?", meio com tijolos, tubo e inimigo, fim com escada e chão plano.
    private static TileMap Original1()
    {
        var m = NivelBase();
        Faixa(m, 8, 10, 12, TileTypeEnum.QuestionFull);
        Grade.Definir(m, 14, 11, TileTypeEnum.Coin);
        Faixa(m, 8, 32, 35, TileTypeEnum.Breakable);
        Grade.Definir(m, 40, 9, TileTypeEnum.PipeTopLeft);
        Grade.Definir(m, 41, 9, TileTypeEnum.PipeTopRight);
        Faixa(m, 10, 40, 40, TileTypeEnum.PipeBodyLeft);
        Faixa(m, 10, 41, 41, TileTypeEnum.PipeBodyRight);
        Faixa(m, 11, 40, 40, TileTypeEnum.PipeBodyLeft);
        Faixa(m, 11, 41, 41, TileTypeEnum.PipeBodyRight);
        Grade.Definir(m, 50, 11, TileTypeEnum.Enemy);
        for (int i = 0; i < 5; i++)
            for (int y = 11 - i; y <= 11; y++) Grade.Definir(m, 61 + i, y, TileTypeEnum.Solid);
        return m;
    }

    // Início com tijolos, meio com tubo alto, inimigo e buraco de 2 colunas, fim com tijolos e moedas.
    private static TileMap Original2()
    {
        var m = NivelBase();
        Grade.Definir(m, 7, 4, TileTypeEnum.QuestionFull);
        Faixa(m, 8, 5, 9, TileTypeEnum.Breakable);
        Grade.Definir(m, 42, 8, TileTypeEnum.PipeTopLeft);
        Grade.Definir(m, 43, 8, TileTypeEnum.PipeTopRight);
        for (int y = 9; y <= 11; y++)
        {
            Grade.Definir(m, 42, y, TileTypeEnum.PipeBodyLeft);
            Grade.Definir(m, 43, y, TileTypeEnum.PipeBodyRight);
        }
        Grade.Definir(m, 47, 11, TileTypeEnum.Enemy);
        Faixa(m, 12, 53, 54, TileTypeEnum.Empty);
        Faixa(m, 13, 53, 54, TileTypeEnum.Empty);
        Faixa(m, 8, 60, 64, TileTypeEnum.Breakable);
        Grade.Definir(m, 62, 8, TileTypeEnum.QuestionFull);
        Faixa(m, 9, 71, 73, TileTypeEnum.Coin);
        return m;
    }

    // Janela 28..55 do original 1 com um único tile alterado: deve ser vetada (N4).
    private static TileMap CopiaDeOriginal()
    {
        var janela = Grade.Janela(Original1(), 28, 28);
        Grade.Definir(janela, 0, 4, TileTypeEnum.Coin);
        return janela;
    }
}
