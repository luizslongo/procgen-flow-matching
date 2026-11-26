using System.Security.Cryptography;
using System.Text;
using c2_pcg.flowMatchingDataloader;

namespace ChunkLink.Ambiente;

// Operações sobre a grade de tiles (TileMap do TCC). Coordenadas: x = coluna, y = linha,
// y = 0 no topo e y = Height - 1 na linha do chão.
public static class Grade
{
    public const int AlturaPadrao = 14;
    public const int LarguraChunk = 28;
    public const string Vocabulario = "-XS?QE<>[]obB";

    public static TileMap Criar(int largura, int altura = AlturaPadrao, BiomeTypeEnum bioma = BiomeTypeEnum.Overworld)
    {
        var mapa = new TileMap { Width = largura, Height = altura, BiomeLabel = bioma, Tiles = new TileTypeEnum[largura * altura] };
        Array.Fill(mapa.Tiles, TileTypeEnum.Empty);
        return mapa;
    }

    public static TileTypeEnum Obter(TileMap m, int x, int y) => m.Tiles[y * m.Width + x];

    public static void Definir(TileMap m, int x, int y, TileTypeEnum t) => m.Tiles[y * m.Width + x] = t;

    public static TileMap Clonar(TileMap m) => new()
    {
        Width = m.Width, Height = m.Height, BiomeLabel = m.BiomeLabel, Tiles = (TileTypeEnum[])m.Tiles.Clone(),
    };

    public static TileMap Concatenar(IReadOnlyList<TileMap> partes)
    {
        if (partes.Count == 0) throw new ArgumentException("nada a concatenar");
        int altura = partes[0].Height;
        var saida = Criar(partes.Sum(p => p.Width), altura, partes[0].BiomeLabel);
        int x0 = 0;
        foreach (var p in partes)
        {
            if (p.Height != altura) throw new ArgumentException("alturas diferentes");
            for (int y = 0; y < altura; y++)
                for (int x = 0; x < p.Width; x++)
                    Definir(saida, x0 + x, y, Obter(p, x, y));
            x0 += p.Width;
        }
        return saida;
    }

    public static TileMap Janela(TileMap m, int x0, int largura)
    {
        var saida = Criar(largura, m.Height, m.BiomeLabel);
        for (int y = 0; y < m.Height; y++)
            for (int x = 0; x < largura; x++)
                Definir(saida, x, y, Obter(m, x0 + x, y));
        return saida;
    }

    // Converte linhas ASCII do VGLC em TileMap; caractere fora do vocabulário é erro (RF01).
    public static TileMap DeAscii(IReadOnlyList<string> linhas, BiomeTypeEnum bioma)
    {
        if (linhas.Count == 0) throw new FormatException("trecho vazio");
        int largura = linhas[0].Length;
        var mapa = Criar(largura, linhas.Count, bioma);
        for (int y = 0; y < linhas.Count; y++)
        {
            if (linhas[y].Length != largura) throw new FormatException($"linha {y} com largura {linhas[y].Length}, esperado {largura}");
            for (int x = 0; x < largura; x++)
            {
                // O mapeamento do TCC converte caracteres desconhecidos em Empty; aqui eles são erro (RF01).
                if (!Vocabulario.Contains(linhas[y][x]))
                    throw new FormatException($"caractere '{linhas[y][x]}' fora do vocabulário VGLC em ({x},{y})");
                var t = VglcTileCharMap.CharToTileType(linhas[y][x]);
                Definir(mapa, x, y, t);
            }
        }
        return mapa;
    }

    public static string[] ParaAscii(TileMap m)
    {
        var linhas = new string[m.Height];
        var sb = new StringBuilder(m.Width);
        for (int y = 0; y < m.Height; y++)
        {
            sb.Clear();
            for (int x = 0; x < m.Width; x++) sb.Append(VglcTileCharMap.TileTypeToChar(Obter(m, x, y)));
            linhas[y] = sb.ToString();
        }
        return linhas;
    }

    public static string Hash(TileMap m)
    {
        var bytes = SHA256.HashData(Encoding.ASCII.GetBytes(string.Join('\n', ParaAscii(m))));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    // Tiles que bloqueiam o corpo do Mario e servem de apoio.
    public static bool Solido(TileTypeEnum t) => t switch
    {
        TileTypeEnum.Empty or TileTypeEnum.Coin or TileTypeEnum.Enemy => false,
        _ => true,
    };

    // Altura do chão na coluna x: blocos sólidos contíguos a partir da linha inferior (0 = buraco).
    public static int AlturaChao(TileMap m, int x)
    {
        int h = 0;
        for (int y = m.Height - 1; y >= 0 && Solido(Obter(m, x, y)); y--) h++;
        return h;
    }

    public static int Diferencas(TileMap a, TileMap b)
    {
        if (a.Width != b.Width || a.Height != b.Height) throw new ArgumentException("dimensões diferentes");
        int n = 0;
        for (int i = 0; i < a.Tiles.Length; i++) if (a.Tiles[i] != b.Tiles[i]) n++;
        return n;
    }
}
