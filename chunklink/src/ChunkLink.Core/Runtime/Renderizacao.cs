using c2_pcg.flowMatchingDataloader;
using c2_pcg.flowMatchingEval;
using ChunkLink.Ambiente;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ChunkLink.Runtime;

// Renderização com o MapPngRenderer do TCC (RF09). O par cego empilha original e montado, com a
// mesma largura e os mesmos sprites, em ordem sorteada; o gabarito vai para um arquivo separado.
public static class Renderizacao
{
    public static void RenderizarNivel(TileMap mapa, string diretorioSprites, string arquivoPng)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(arquivoPng))!);
        MapPngRenderer.RenderMapToPng(mapa, new MapPngRendererConfig { SpriteDir = diretorioSprites, TileSizePixels = 16 }, arquivoPng);
    }

    public static string RenderizarParCego(
        TileMap montado, TileMap original, string diretorioSprites, string diretorioSaida, string idPar, int semente)
    {
        Directory.CreateDirectory(diretorioSaida);
        int largura = Math.Min(montado.Width, original.Width);
        var a = Grade.Janela(montado, 0, largura);
        var b = Grade.Janela(original, 0, largura);
        // Posição sorteada por par: semente da execução combinada com um hash estável do id do par.
        int sal = BitConverter.ToInt32(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(idPar)), 0);
        bool originalEmCima = new Random(semente ^ sal).Next(2) == 0;

        string tmp = Path.Combine(Path.GetTempPath(), "chunklink-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            string pa = Path.Combine(tmp, "x.png"), pb = Path.Combine(tmp, "y.png");
            RenderizarNivel(originalEmCima ? b : a, diretorioSprites, pa);
            RenderizarNivel(originalEmCima ? a : b, diretorioSprites, pb);
            using var cima = Image.Load<Rgba32>(pa);
            using var baixo = Image.Load<Rgba32>(pb);
            const int separador = 16;
            using var par = new Image<Rgba32>(cima.Width, cima.Height + separador + baixo.Height, new Rgba32(255, 255, 255));
            par.Mutate(ctx => ctx.DrawImage(cima, new Point(0, 0), 1f).DrawImage(baixo, new Point(0, cima.Height + separador), 1f));
            string arquivo = Path.Combine(diretorioSaida, $"par-{idPar}.png");
            par.Save(arquivo);
            File.AppendAllLines(Path.Combine(diretorioSaida, "gabarito.csv"),
                new[] { $"{idPar};{(originalEmCima ? "cima" : "baixo")}" });
            return arquivo;
        }
        finally
        {
            Directory.Delete(tmp, recursive: true);
        }
    }
}
