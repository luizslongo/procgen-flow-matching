using c2_pcg.flowMatchingDataloader;
using ChunkLink.Ambiente;

namespace ChunkLink.Movimento;

// Perfil de jogador: corpo (células de altura), altura máxima e alcance horizontal do salto.
// QuebraBlocos: o Mario grande quebra tijolos (Breakable) ao batê-los por baixo durante o salto.
public sealed record PerfilJogador(string Id, int Corpo, int AlturaSalto, int AlcanceSalto, int OrcamentoNos = 60000, bool QuebraBlocos = false)
{
    // Alcance 5 calibrado contra os níveis originais (comando validar-movimento): o jogo exige pular
    // buracos de 4 colunas correndo (por exemplo, o 2-1), o que o alcance 4 da DSM2 não permitia.
    public static readonly PerfilJogador Pequeno = new("P-Peq", 1, 4, 5);
    public static readonly PerfilJogador Grande = new("P-Gde", 2, 4, 5, QuebraBlocos: true);
    public static readonly PerfilJogador Iniciante = new("P-Ini", 1, 3, 3);
}

public enum CausaFalha { Nenhuma, Buraco, Parede, Teto, Inimigo, SemEntrada, Inconclusivo, Indeterminada }

public sealed record ResultadoPlaytest(
    string Perfil, bool Concluiu, int ColunaMaxima, int Largura, CausaFalha Causa, int ColunaFalha, int NosExpandidos)
{
    public double Progresso => Largura <= 1 ? 1.0 : Math.Min(1.0, (double)ColunaMaxima / (Largura - 1));

    public override string ToString() =>
        Concluiu ? $"{Perfil}: concluiu" : $"{Perfil}: falhou ({Causa}, col. {ColunaFalha}, progresso {Progresso:P0})";
}

// Modelo de movimento discreto. Estados são posições "em pé" (x, y) dos pés do Mario.
// Movimentos: andar ±1 (com queda), queda vertical e saltos parametrizados por altura j ≤ h
// e deslocamento horizontal k ≤ d (metade na subida, metade na descida). Inimigos são
// estáticos: encostar num inimigo de lado ou por baixo é morte; cair sobre ele é um pisão, que serve
// de apoio para um novo salto (quique) ou mata o inimigo e deixa o Mario descer no lugar dele.
public sealed class SimuladorMovimento
{
    private enum Celula { Livre, Solida, Inimigo, Fora }

    private readonly TileMap _m;
    private readonly PerfilJogador _p;

    public SimuladorMovimento(TileMap mapa, PerfilJogador perfil)
    {
        _m = mapa;
        _p = perfil;
    }

    private Celula Tipo(int x, int y)
    {
        if (x < 0 || x >= _m.Width || y < 0 || y >= _m.Height) return Celula.Fora;
        var t = Grade.Obter(_m, x, y);
        if (t == TileTypeEnum.Enemy) return Celula.Inimigo;
        return Grade.Solido(t) ? Celula.Solida : Celula.Livre;
    }

    // Resultado de tentar ocupar (x, y) com o corpo: livre, bloqueado ou morte (inimigo).
    // `quebrado`: tijolo já partido durante o salto em curso, tratado como livre até o fim dele.
    private Celula Corpo(int x, int y, (int X, int Y)? quebrado = null)
    {
        var pior = Celula.Livre;
        for (int i = 0; i < _p.Corpo; i++)
        {
            if (quebrado is { } q && q.X == x && q.Y == y - i) continue;
            var c = Tipo(x, y - i);
            if (c == Celula.Inimigo) return Celula.Inimigo;
            if (c != Celula.Livre) pior = Celula.Solida;
        }
        return pior;
    }

    public bool CorpoLivre(int x, int y) => Corpo(x, y) == Celula.Livre;

    // Apoio: chão sólido ou um inimigo, sobre o qual o Mario quica (pisão).
    public bool Apoiado(int x, int y) => y + 1 < _m.Height && Tipo(x, y + 1) is Celula.Solida or Celula.Inimigo;

    private bool SobreInimigo(int x, int y) => y + 1 < _m.Height && Tipo(x, y + 1) == Celula.Inimigo;

    public bool EmPe(int x, int y) => CorpoLivre(x, y) && Apoiado(x, y);

    // Pontos de entrada: todas as posições em pé da coluna x, da mais baixa para a mais alta.
    public List<(int X, int Y)> Entradas(int x)
    {
        var lista = new List<(int, int)>();
        for (int y = _m.Height - 2; y >= _p.Corpo - 1; y--)
            if (EmPe(x, y)) lista.Add((x, y));
        return lista;
    }

    // Existe alguma superfície sólida com espaço acima na coluna x (mesmo que um inimigo esteja ali).
    public bool TemChaoNaColuna(int x)
    {
        for (int y = _m.Height - 2; y >= 0; y--)
            if (Tipo(x, y) != Celula.Solida && Tipo(x, y + 1) == Celula.Solida) return true;
        return false;
    }

    public bool ExisteApoioNaColuna(int x)
    {
        for (int y = _m.Height - 2; y >= _p.Corpo - 1; y--)
            if (EmPe(x, y)) return true;
        return false;
    }

    public IEnumerable<(int X, int Y, int Custo)> Sucessores(int x, int y)
    {
        var vistos = new HashSet<(int, int)>();
        foreach (int s in new[] { 1, -1 })
        {
            if (Corpo(x + s, y) == Celula.Livre && Cair(x + s, y) is { } andando)
            {
                if (vistos.Add(andando)) yield return (andando.Item1, andando.Item2, 1);
                if (SobreInimigo(andando.Item1, andando.Item2) && Pisar(andando.Item1, andando.Item2) is { } pisado && vistos.Add(pisado))
                    yield return (pisado.Item1, pisado.Item2, 1);
            }
            for (int j = 1; j <= _p.AlturaSalto; j++)
                for (int k = 0; k <= _p.AlcanceSalto; k++)
                    if (Saltar(x, y, s, j, k) is { } pouso && pouso != (x, y))
                    {
                        if (vistos.Add(pouso)) yield return (pouso.Item1, pouso.Item2, 2);
                        if (SobreInimigo(pouso.Item1, pouso.Item2) && Pisar(pouso.Item1, pouso.Item2) is { } pisado && vistos.Add(pisado))
                            yield return (pisado.Item1, pisado.Item2, 2);
                    }
        }
    }

    // Cai verticalmente a partir de (x, y) até encontrar apoio (chão ou inimigo); null se cair do mapa.
    private (int, int)? Cair(int x, int y)
    {
        while (!Apoiado(x, y))
        {
            if (y + 1 >= _m.Height) return null;
            if (Tipo(x, y + 1) != Celula.Livre) return null;
            y++;
        }
        return (x, y);
    }

    // Pisão que mata: a partir do quique sobre um inimigo em (x, y + 1), o inimigo some e o Mario
    // desce para o lugar dele (e cai até o chão). Sem guardar estado: o inimigo só some neste ramo.
    private (int, int)? Pisar(int x, int y)
    {
        int yy = y + 1;
        while (yy + 1 < _m.Height && Tipo(x, yy + 1) == Celula.Livre) yy++;
        return yy + 1 < _m.Height && Tipo(x, yy + 1) is Celula.Solida or Celula.Inimigo ? (x, yy) : null;
    }

    // Na subida, o corpo entra uma linha acima: a única célula nova é a do topo da cabeça. Se ela é
    // um tijolo e o perfil quebra blocos, o tijolo se parte e a subida continua. Aproximação: no jogo,
    // a quebra interrompe o salto e a passagem fica livre para o salto seguinte; aqui as duas etapas
    // são colapsadas num salto só, sem guardar no estado quais tijolos já foram quebrados.
    // Uma quebra por salto: devolve a posição do tijolo partido, ou null se não há o que quebrar.
    private (int X, int Y)? QuebraPorBaixo(int x, int yNovo, (int X, int Y)? jaQuebrado)
    {
        if (!_p.QuebraBlocos || jaQuebrado != null) return null;
        int topo = yNovo - (_p.Corpo - 1);
        if (topo < 0 || x < 0 || x >= _m.Width || Grade.Obter(_m, x, topo) != TileTypeEnum.Breakable) return null;
        for (int i = 0; i < _p.Corpo - 1; i++)
            if (Tipo(x, yNovo - i) != Celula.Livre) return null;
        return (x, topo);
    }

    private (int, int)? Saltar(int x0, int y0, int s, int j, int k)
    {
        (int X, int Y)? quebrado = null;
        int subidaH = (k + 1) / 2;
        int descidaH = k - subidaH;
        int cx = x0, cy = y0, andados = 0;

        // Subida: sobe uma linha e então avança na horizontal o que couber até aquela altura.
        for (int i = 1; i <= j; i++)
        {
            var acima = Corpo(cx, cy - 1, quebrado);
            if (acima == Celula.Inimigo) return null;
            if (acima != Celula.Livre && QuebraPorBaixo(cx, cy - 1, quebrado) is { } partido) quebrado = partido;
            else if (acima != Celula.Livre)
            {
                descidaH += subidaH - andados; // bateu a cabeça: o resto do impulso vai para a descida
                break;
            }
            cy--;
            int alvo = i * subidaH / j;
            while (andados < alvo)
            {
                var lado = Corpo(cx + s, cy, quebrado);
                if (lado == Celula.Inimigo) return null;
                if (lado != Celula.Livre) { andados = subidaH; descidaH = 0; break; }
                cx += s;
                andados++;
            }
        }

        // Descida: primeiro avança na horizontal, depois desce; pousa ao encontrar apoio.
        int descidos = 0, andadosDescida = 0;
        while (true)
        {
            if (Apoiado(cx, cy)) return (cx, cy);
            descidos++;
            // Impulso: depois das j linhas do arco, a queda continua levando o Mario adiante (uma coluna
            // a cada duas linhas caídas), como a velocidade horizontal mantida no jogo.
            int jj = Math.Max(1, j);
            int alvo = descidos <= jj ? descidos * descidaH / jj : descidaH + (descidaH > 0 ? (descidos - jj) / 2 : 0);
            while (andadosDescida < alvo)
            {
                var lado = Corpo(cx + s, cy, quebrado);
                if (lado == Celula.Inimigo) return null;
                if (lado != Celula.Livre) { descidaH = andadosDescida; break; }
                cx += s;
                andadosDescida++;
                if (Apoiado(cx, cy)) return (cx, cy);
            }
            if (cy + 1 >= _m.Height) return null;
            if (Tipo(cx, cy + 1) != Celula.Livre) return (cx, cy);
            cy++;
        }
    }
}

// Jogador A*: procura um caminho de qualquer posição em pé da coluna 0 até a saída do trecho.
// Trecho fechado (nível completo): a saída é qualquer posição em pé na última coluna.
// Trecho aberto (prefixo em montagem): basta chegar em pé a até d−1 colunas da borda direita,
// de onde o próximo chunk é alcançável por um salto; a costura é verificada no prefixo seguinte.
public static class BuscaAEstrela
{
    public static ResultadoPlaytest Jogar(TileMap mapa, PerfilJogador perfil, bool trechoAberto = false)
    {
        var sim = new SimuladorMovimento(mapa, perfil);
        int largura = mapa.Width;
        int saida = trechoAberto ? Math.Max(0, largura - perfil.AlcanceSalto) : largura - 1;
        var entradas = sim.Entradas(0);
        if (entradas.Count == 0)
            return new ResultadoPlaytest(perfil.Id, false, 0, largura, CausaFalha.SemEntrada, 0, 0);

        var custo = new Dictionary<(int, int), int>();
        var fechados = new HashSet<(int, int)>();
        var abertos = new PriorityQueue<(int X, int Y), (int F, int NegX, int Y, long Seq)>();
        long seq = 0;
        foreach (var e in entradas)
        {
            custo[e] = 0;
            abertos.Enqueue(e, (Heuristica(e.X, largura, perfil), -e.X, e.Y, seq++));
        }
        int expandidos = 0;
        (int X, int Y) melhor = entradas[0];

        while (abertos.TryDequeue(out var atual, out _))
        {
            if (!fechados.Add(atual)) continue;
            if (++expandidos > perfil.OrcamentoNos)
                return new ResultadoPlaytest(perfil.Id, false, melhor.X, largura, CausaFalha.Inconclusivo, melhor.X, expandidos);
            if (atual.X > melhor.X || (atual.X == melhor.X && atual.Y > melhor.Y)) melhor = atual;
            if (atual.X >= saida)
                return new ResultadoPlaytest(perfil.Id, true, largura - 1, largura, CausaFalha.Nenhuma, -1, expandidos);

            int g = custo[atual];
            foreach (var (x, y, c) in sim.Sucessores(atual.X, atual.Y))
            {
                var prox = (x, y);
                int ng = g + c;
                if (fechados.Contains(prox) || (custo.TryGetValue(prox, out int velho) && velho <= ng)) continue;
                custo[prox] = ng;
                abertos.Enqueue(prox, (ng + Heuristica(x, largura, perfil), -x, y, seq++));
            }
        }

        var (causa, coluna) = Classificar(mapa, sim, perfil, melhor);
        return new ResultadoPlaytest(perfil.Id, false, melhor.X, largura, causa, coluna, expandidos);
    }

    private static int Heuristica(int x, int largura, PerfilJogador p) =>
        (largura - 1 - x + p.AlcanceSalto - 1) / p.AlcanceSalto;

    // Explica a falha observando o que existe logo depois da coluna mais avançada alcançada.
    private static (CausaFalha, int) Classificar(TileMap m, SimuladorMovimento sim, PerfilJogador p, (int X, int Y) fronteira)
    {
        int c = Math.Min(m.Width - 1, fronteira.X + 1);
        int fim = Math.Min(m.Width - 1, fronteira.X + p.AlcanceSalto);
        // Buraco = nenhuma coluna ao alcance tem chão; uma coluna com chão ocupada por inimigos não é buraco.
        bool algumApoio = false;
        for (int x = fronteira.X + 1; x <= fim; x++) algumApoio |= sim.TemChaoNaColuna(x);
        if (!algumApoio) return (CausaFalha.Buraco, c);

        int y = fronteira.Y;
        for (int dx = 1; dx <= 2 && fronteira.X + dx < m.Width; dx++)
            for (int i = 0; i < p.Corpo; i++)
                if (y - i >= 0 && Grade.Obter(m, fronteira.X + dx, y - i) == TileTypeEnum.Enemy)
                    return (CausaFalha.Inimigo, fronteira.X + dx);

        bool pesLivres = !Grade.Solido(Grade.Obter(m, c, y));
        if (p.Corpo > 1 && pesLivres && y - 1 >= 0 && Grade.Solido(Grade.Obter(m, c, y - 1)))
            return (CausaFalha.Teto, c);
        if (!pesLivres) return (CausaFalha.Parede, c);
        return (CausaFalha.Indeterminada, c);
    }
}
