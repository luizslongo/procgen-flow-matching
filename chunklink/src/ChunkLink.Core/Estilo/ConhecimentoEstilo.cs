using c2_pcg.flowMatchingDataloader;
using ChunkLink.Ambiente;

namespace ChunkLink.Estilo;

public enum PosicaoNoNivel { Inicio, Meio, Fim }

// Características visuais de uma janela de 28 colunas.
public sealed record AssinaturaJanela(double Densidade, double ChaoContinuo, double Estruturas, double Inimigos, double Blocos)
{
    public static readonly string[] Nomes = { "densidade", "chão contínuo", "estruturas", "inimigos", "blocos" };

    public double[] Vetor => new[] { Densidade, ChaoContinuo, Estruturas, Inimigos, Blocos };

    public static AssinaturaJanela De(TileMap janela)
    {
        int h = janela.Height, w = janela.Width, ocupadas = 0, estruturas = 0, inimigos = 0, blocos = 0, chao = 0;
        for (int x = 0; x < w; x++)
        {
            if (Grade.AlturaChao(janela, x) > 0) chao++;
            for (int y = 0; y < h - 2; y++)
            {
                var t = Grade.Obter(janela, x, y);
                if (t != TileTypeEnum.Empty && t != TileTypeEnum.Coin) ocupadas++;
                if (t is TileTypeEnum.PipeTopLeft or TileTypeEnum.BulletBillLauncher) estruturas++;
                if (t == TileTypeEnum.Enemy) inimigos++;
                if (t is TileTypeEnum.Breakable or TileTypeEnum.QuestionFull or TileTypeEnum.QuestionEmpty) blocos++;
            }
        }
        return new AssinaturaJanela((double)ocupadas / (w * (h - 2)), (double)chao / w, estruturas, inimigos, blocos);
    }
}

// Avaliação de estilo de um candidato numa posição do nível.
public sealed record AvaliacaoEstilo(double Distancia, double Costura, string CaracteristicaMaisDistante);

// Conhecimento do curador: estatísticas dos níveis originais do bioma (CorpusReferencia).
public sealed class ConhecimentoEstilo
{
    private readonly Dictionary<PosicaoNoNivel, (double[] Media, double[] Desvio)> _estatisticas = new();
    private readonly Dictionary<int, double> _padroesOriginais;
    private readonly List<TileMap> _janelasOriginais = new();
    private readonly List<double> _densidades = new();

    public ConhecimentoEstilo(CorpusReferencia corpus)
    {
        var porPosicao = new Dictionary<PosicaoNoNivel, List<double[]>>
        {
            [PosicaoNoNivel.Inicio] = new(), [PosicaoNoNivel.Meio] = new(), [PosicaoNoNivel.Fim] = new(),
        };
        var saltosDeChao = new List<int>();
        var contagens = new Dictionary<int, double>();

        foreach (var (_, mapa) in corpus.Niveis)
        {
            int nJanelas = mapa.Width / Grade.LarguraChunk;
            for (int i = 0; i < nJanelas; i++)
            {
                var janela = Grade.Janela(mapa, i * Grade.LarguraChunk, Grade.LarguraChunk);
                var pos = i == 0 ? PosicaoNoNivel.Inicio : i == nJanelas - 1 ? PosicaoNoNivel.Fim : PosicaoNoNivel.Meio;
                var assinatura = AssinaturaJanela.De(janela);
                porPosicao[pos].Add(assinatura.Vetor);
                _densidades.Add(assinatura.Densidade);
            }
            for (int x0 = 0; x0 + Grade.LarguraChunk <= mapa.Width; x0++)
                _janelasOriginais.Add(Grade.Janela(mapa, x0, Grade.LarguraChunk));
            for (int x = 1; x < mapa.Width; x++)
                saltosDeChao.Add(Math.Abs(Grade.AlturaChao(mapa, x) - Grade.AlturaChao(mapa, x - 1)));
            ContarPadroes(mapa, contagens);
        }

        var todas = porPosicao.Values.SelectMany(v => v).ToList();
        foreach (var (pos, vetores) in porPosicao)
            _estatisticas[pos] = Estatistica(vetores.Count > 0 ? vetores : todas);

        saltosDeChao.Sort();
        LimiarCostura = saltosDeChao.Count == 0 ? 1 : Math.Max(1, saltosDeChao[(int)(0.95 * (saltosDeChao.Count - 1))]);
        _padroesOriginais = Normalizar(contagens);
        NiveisReferencia = corpus.Niveis.Count;
    }

    public int NiveisReferencia { get; }

    // Percentil 95 das mudanças de altura do chão entre colunas vizinhas nos originais.
    public double LimiarCostura { get; }

    public double PercentilDensidade(double q)
    {
        var ordenadas = _densidades.OrderBy(d => d).ToList();
        return ordenadas.Count == 0 ? 1 : ordenadas[(int)Math.Round(q * (ordenadas.Count - 1))];
    }

    public static PosicaoNoNivel Posicao(int slot, int totalSlots) =>
        slot == 1 ? PosicaoNoNivel.Inicio : slot == totalSlots ? PosicaoNoNivel.Fim : PosicaoNoNivel.Meio;

    // Distância média em desvios-padrão entre a janela e os originais naquela posição (0 = típico).
    public AvaliacaoEstilo Avaliar(TileMap janela, PosicaoNoNivel posicao, TileMap? vizinhoEsquerdo)
    {
        var (media, desvio) = _estatisticas[posicao];
        var v = AssinaturaJanela.De(janela).Vetor;
        double soma = 0, maior = -1;
        int pior = 0;
        for (int i = 0; i < v.Length; i++)
        {
            double z = Math.Abs(v[i] - media[i]) / desvio[i];
            soma += z;
            if (z > maior) { maior = z; pior = i; }
        }
        double costura = vizinhoEsquerdo == null ? 0 : Costura(vizinhoEsquerdo, janela);
        return new AvaliacaoEstilo(soma / v.Length, costura, AssinaturaJanela.Nomes[pior]);
    }

    // Salto de altura do chão na fronteira entre dois trechos (M13).
    public static double Costura(TileMap esquerda, TileMap direita) =>
        Math.Abs(Grade.AlturaChao(esquerda, esquerda.Width - 1) - Grade.AlturaChao(direita, 0));

    // Menor distância entre a estrutura do chunk e a de qualquer janela original (N4, RS04): fração de
    // discordâncias entre as posições acima da faixa do chão em que algum dos dois tem conteúdo.
    // Céu e chão contínuo são comuns a quase toda janela e não podem caracterizar cópia.
    public double MenorDistanciaAOriginais(TileMap chunk)
    {
        double menor = 1;
        foreach (var janela in _janelasOriginais) menor = Math.Min(menor, DistanciaEstrutural(chunk, janela));
        return menor;
    }

    public static double DistanciaEstrutural(TileMap a, TileMap b)
    {
        int uniao = 0, discordancias = 0;
        for (int y = 0; y < a.Height - 2; y++)
            for (int x = 0; x < a.Width; x++)
            {
                var ta = Grade.Obter(a, x, y);
                var tb = Grade.Obter(b, x, y);
                if (ta == TileTypeEnum.Empty && tb == TileTypeEnum.Empty) continue;
                uniao++;
                if (ta != tb) discordancias++;
            }
        return uniao < 5 ? 1 : (double)discordancias / uniao;
    }

    // Divergência KL dos padrões 2×2 do nível em relação aos originais (M12; Lucas e Volz, 2019).
    public double KlPadroes2x2(TileMap nivel)
    {
        var p = Normalizar(ContarPadroes(nivel, new Dictionary<int, double>()));
        const double eps = 1e-6;
        double kl = 0;
        foreach (var (padrao, prob) in p)
        {
            double q = _padroesOriginais.TryGetValue(padrao, out var qq) ? qq : 0;
            kl += prob * Math.Log(prob / (q + eps));
        }
        return kl;
    }

    private static Dictionary<int, double> ContarPadroes(TileMap m, Dictionary<int, double> contagens)
    {
        for (int y = 0; y + 1 < m.Height; y++)
            for (int x = 0; x + 1 < m.Width; x++)
            {
                int chave = (int)Grade.Obter(m, x, y) | ((int)Grade.Obter(m, x + 1, y) << 4)
                          | ((int)Grade.Obter(m, x, y + 1) << 8) | ((int)Grade.Obter(m, x + 1, y + 1) << 12);
                contagens[chave] = contagens.GetValueOrDefault(chave) + 1;
            }
        return contagens;
    }

    private static Dictionary<int, double> Normalizar(Dictionary<int, double> contagens)
    {
        double total = contagens.Values.Sum();
        return contagens.ToDictionary(kv => kv.Key, kv => kv.Value / total);
    }

    private static (double[], double[]) Estatistica(List<double[]> vetores)
    {
        int n = vetores[0].Length;
        var media = new double[n];
        var desvio = new double[n];
        for (int i = 0; i < n; i++)
        {
            media[i] = vetores.Average(v => v[i]);
            double var = vetores.Average(v => (v[i] - media[i]) * (v[i] - media[i]));
            // Piso do desvio: poucas janelas de referência não podem tornar o curador intolerante.
            // Frações (densidade, chão contínuo) usam piso 0,05; contagens usam piso de 1 unidade.
            desvio[i] = Math.Max(Math.Sqrt(var), i < 2 ? 0.05 : 1.0);
        }
        return (media, desvio);
    }
}
