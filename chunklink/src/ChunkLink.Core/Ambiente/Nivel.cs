using c2_pcg.flowMatchingDataloader;
using ChunkLink.Comunicacao;

namespace ChunkLink.Ambiente;

public sealed class ViolacaoNorma(string norma, string mensagem) : Exception($"{norma}: {mensagem}")
{
    public string Norma { get; } = norma;
}

public sealed class ConflitoReserva(string mensagem) : Exception(mensagem);

// Uma alteração de tile em coordenadas locais do chunk.
public sealed record AlteracaoTile(int X, int Y, TileTypeEnum De, TileTypeEnum Para);

// Conjunto de alterações proposto por um reparador. Custo = número de tiles alterados.
public sealed record Diff(IReadOnlyList<AlteracaoTile> Alteracoes, string Estrategia)
{
    public int Custo => Alteracoes.Count;

    public string Hash => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.ASCII.GetBytes(Estrategia + "|" + string.Join(';', Alteracoes.Select(a => $"{a.X},{a.Y},{(int)a.Para}")))))[..16];

    public int ColunaMin => Alteracoes.Count == 0 ? 0 : Alteracoes.Min(a => a.X);
    public int ColunaMax => Alteracoes.Count == 0 ? 0 : Alteracoes.Max(a => a.X);

    public TileMap AplicarEm(TileMap origem)
    {
        var copia = Grade.Clonar(origem);
        foreach (var a in Alteracoes) Grade.Definir(copia, a.X, a.Y, a.Para);
        return copia;
    }

    public static Diff Entre(TileMap antes, TileMap depois, string estrategia)
    {
        var alteracoes = new List<AlteracaoTile>();
        for (int y = 0; y < antes.Height; y++)
            for (int x = 0; x < antes.Width; x++)
            {
                var a = Grade.Obter(antes, x, y);
                var b = Grade.Obter(depois, x, y);
                if (a != b) alteracoes.Add(new AlteracaoTile(x, y, a, b));
            }
        return new Diff(alteracoes, estrategia);
    }
}

// Autorização do curador registrada no próprio Nível (N5). Vale para uma negociação, um slot, um
// chunk e um diff (hash) específicos, e só pode ser consumida uma vez.
public sealed record Aprovacao(long Id, string Emissor, string Conversa, int Slot, string ChunkId, string HashDiff, long Tick);

// Aceite de estilo do curador para fixar um chunk sem edição num slot; também de uso único.
public sealed record Aceite(long Id, string Emissor, int Slot, string ChunkId, long Tick);

// Regras que o próprio Nível impõe em toda operação, sem depender da disciplina dos agentes.
public sealed record RegrasNivel(int EMax, int EMaxChunk, double TauCopia, Func<TileMap, double>? DistanciaOriginais);

// Reserva de colunas do nível [ColunaInicio, ColunaFim) — intervalo meio-aberto (RS01).
public sealed record Reserva(string Dono, int ColunaInicio, int ColunaFim);

public sealed class TabelaReservas
{
    private readonly List<Reserva> _ativas = new();

    public IReadOnlyList<Reserva> Ativas => _ativas;

    public void Reservar(Reserva r)
    {
        if (r.ColunaFim <= r.ColunaInicio) throw new ArgumentException("intervalo de reserva vazio");
        foreach (var atual in _ativas)
        {
            bool sobrepoe = r.ColunaInicio < atual.ColunaFim && atual.ColunaInicio < r.ColunaFim;
            if (sobrepoe && atual.Dono != r.Dono)
                throw new ConflitoReserva($"colunas [{r.ColunaInicio},{r.ColunaFim}) já reservadas por {atual.Dono}");
        }
        _ativas.Add(r);
    }

    public void Liberar(string dono) => _ativas.RemoveAll(r => r.Dono == dono);
}

// Edição provisória: o chunk candidato com o diff aplicado numa cópia, ainda fora do nível.
public sealed class EdicaoProvisoria
{
    internal EdicaoProvisoria(int id, string conversa, int slot, Chunk chunk, Diff diff, string reparador)
    {
        Id = id;
        Conversa = conversa;
        Slot = slot;
        Chunk = chunk;
        Diff = diff;
        Reparador = reparador;
        Resultado = diff.AplicarEm(chunk.Mapa);
    }

    public int Id { get; }
    public string Conversa { get; }
    public int Slot { get; }
    public Chunk Chunk { get; }
    public Diff Diff { get; }
    public string Reparador { get; }
    public TileMap Resultado { get; }
    public bool Encerrada { get; internal set; }
}

// O nível em construção: artefato compartilhado do ambiente. Guarda os slots fixados, a tabela de
// reservas, as edições provisórias e o registro de autorizações do curador. Toda operação confere a
// credencial e o papel de quem a pede e as normas N1, N2, N4 e N5 antes de alterar qualquer estado:
// se uma regra falha, nada muda (fail closed).
public sealed class Nivel
{
    private readonly Chunk?[] _chunks;
    private readonly TileMap?[] _conteudo;
    private readonly int[] _editados;
    private readonly Dictionary<int, EdicaoProvisoria> _edicoes = new();
    private readonly Dictionary<long, Aprovacao> _aprovacoes = new();
    private readonly Dictionary<long, Aceite> _aceites = new();
    private readonly HashSet<long> _consumidas = new();
    private readonly MessageBus _bus;
    private int _proximaEdicao = 1;
    private long _proximaAutorizacao = 1;

    public Nivel(int slots, BiomeTypeEnum bioma, MessageBus bus, RegrasNivel regras)
    {
        if (slots < 1) throw new ArgumentException("nível precisa de ao menos um slot");
        Slots = slots;
        Bioma = bioma;
        Regras = regras;
        _bus = bus;
        _chunks = new Chunk?[slots];
        _conteudo = new TileMap?[slots];
        _editados = new int[slots];
    }

    public int Slots { get; }
    public BiomeTypeEnum Bioma { get; }
    public RegrasNivel Regras { get; }
    public TabelaReservas Reservas { get; } = new();
    public int TilesEditados => _editados.Sum();
    public List<string> Historico { get; } = new();

    public static int ColunaInicial(int slot) => (slot - 1) * Grade.LarguraChunk;

    public Chunk? ChunkNoSlot(int slot) => _chunks[slot - 1];

    public TileMap? ConteudoDoSlot(int slot) => _conteudo[slot - 1];

    public int SlotsPreenchidos => _chunks.Count(c => c != null);

    public int? PrimeiroSlotVazio()
    {
        for (int k = 1; k <= Slots; k++) if (_chunks[k - 1] == null) return k;
        return null;
    }

    public IEnumerable<Chunk> ChunksUsados => _chunks.Where(c => c != null)!;

    // ---------------------------------------------------------------- autorizações do curador

    public Aprovacao RegistrarAprovacao(Credencial curador, string conversa, int slot, string chunkId, string hashDiff, long tick)
    {
        Exigir(curador, Papel.Curador, "registrar aprovação");
        var a = new Aprovacao(_proximaAutorizacao++, curador.Id, conversa, slot, chunkId, hashDiff, tick);
        _aprovacoes[a.Id] = a;
        Historico.Add($"aprovação #{a.Id}: {conversa}, slot {slot}, {chunkId}, diff {hashDiff}");
        return a;
    }

    public Aceite RegistrarAceite(Credencial curador, int slot, string chunkId, long tick)
    {
        Exigir(curador, Papel.Curador, "registrar aceite");
        var a = new Aceite(_proximaAutorizacao++, curador.Id, slot, chunkId, tick);
        _aceites[a.Id] = a;
        return a;
    }

    // ---------------------------------------------------------------- operações sobre os slots

    // Fixa um chunk sem edição. Exige um aceite do curador para este slot e este chunk (uso único).
    public void Fixar(Credencial arquiteto, int slot, Chunk chunk, long idAceite)
    {
        Exigir(arquiteto, Papel.Arquiteto, "fixar chunk");
        GarantirSequencia(slot);
        if (!_aceites.TryGetValue(idAceite, out var aceite) || _consumidas.Contains(idAceite))
            throw new ViolacaoNorma("N5", $"fixar {chunk.Id} no slot {slot} sem aceite válido do curador");
        if (aceite.Slot != slot || aceite.ChunkId != chunk.Id)
            throw new ViolacaoNorma("N5", $"o aceite #{idAceite} vale para {aceite.ChunkId} no slot {aceite.Slot}, não para {chunk.Id} no slot {slot}");
        VerificarConteudo(chunk, chunk.Mapa);
        _consumidas.Add(idAceite);
        _chunks[slot - 1] = chunk;
        _conteudo[slot - 1] = Grade.Clonar(chunk.Mapa);
        Historico.Add($"fixar slot {slot} = {chunk.Id} (aceite #{idAceite})");
    }

    public void Liberar(Credencial arquiteto, int slot)
    {
        Exigir(arquiteto, Papel.Arquiteto, "liberar slot");
        if (slot < 1 || slot > Slots || _chunks[slot - 1] == null) throw new ViolacaoNorma("SEQ", $"slot {slot} já está vazio");
        for (int k = slot; k <= Slots; k++)
        {
            _chunks[k - 1] = null;
            _conteudo[k - 1] = null;
            _editados[k - 1] = 0;
        }
        Historico.Add($"liberar slot {slot}");
    }

    // Prefixo 1..slot−1 do nível, seguido do trecho candidato (se houver).
    public TileMap Prefixo(int slot, TileMap? candidato)
    {
        var partes = new List<TileMap>();
        for (int k = 1; k < slot; k++)
            partes.Add(_conteudo[k - 1] ?? throw new InvalidOperationException($"slot {k} vazio antes do slot {slot}"));
        if (candidato != null) partes.Add(candidato);
        return Grade.Concatenar(partes);
    }

    public TileMap Completo()
    {
        if (PrimeiroSlotVazio() is int vazio) throw new InvalidOperationException($"slot {vazio} ainda vazio");
        return Grade.Concatenar(_conteudo.Select(c => c!).ToList());
    }

    // P5: o reparador vencedor reserva as colunas que vai editar e abre uma edição provisória.
    public EdicaoProvisoria AbrirEdicao(Credencial reparador, string conversa, int slot, Chunk chunk, Diff diff)
    {
        Exigir(reparador, Papel.Reparador, "abrir edição");
        GarantirSequencia(slot);
        if (_chunks.Any(c => c?.Id == chunk.Id)) throw new ViolacaoNorma("N2", $"{chunk.Id} já está no nível");
        int inicio = ColunaInicial(slot);
        Reservas.Reservar(new Reserva(reparador.Id, inicio + diff.ColunaMin, inicio + diff.ColunaMax + 1));
        var edicao = new EdicaoProvisoria(_proximaEdicao++, conversa, slot, chunk, diff, reparador.Id);
        _edicoes[edicao.Id] = edicao;
        Historico.Add($"edição provisória #{edicao.Id} no slot {slot} por {reparador.Id} ({diff.Custo} tiles)");
        return edicao;
    }

    public EdicaoProvisoria Edicao(int id) => _edicoes[id];

    // Edições ainda abertas de uma negociação (usado para não deixar reservas presas quando uma
    // confirmação se perde ou a intenção de reparo é abandonada).
    public IReadOnlyList<EdicaoProvisoria> EdicoesAbertas(string conversa) =>
        _edicoes.Values.Where(e => e.Conversa == conversa && !e.Encerrada).ToList();

    // Efetiva a edição. Exige a aprovação registrada pelo curador para esta negociação, este slot,
    // este chunk e exatamente este diff, ainda não usada (N5, RS03), e o orçamento de edição (N1).
    public void Commit(Credencial arquiteto, EdicaoProvisoria edicao, long idAprovacao)
    {
        Exigir(arquiteto, Papel.Arquiteto, "efetivar edição");
        if (edicao.Encerrada) throw new ViolacaoNorma("SEQ", $"edição #{edicao.Id} já encerrada");
        if (!_aprovacoes.TryGetValue(idAprovacao, out var a) || _consumidas.Contains(idAprovacao))
            throw new ViolacaoNorma("N5", $"commit da edição #{edicao.Id} sem aprovação válida (inexistente ou já usada)");
        if (a.Conversa != edicao.Conversa || a.Slot != edicao.Slot || a.ChunkId != edicao.Chunk.Id || a.HashDiff != edicao.Diff.Hash)
            throw new ViolacaoNorma("N5", $"a aprovação #{a.Id} não corresponde à edição #{edicao.Id} (conversa, slot, chunk ou diff diferentes)");
        if (edicao.Diff.Custo > Regras.EMaxChunk)
            throw new ViolacaoNorma("N1", $"o diff altera {edicao.Diff.Custo} tiles; o limite por chunk é {Regras.EMaxChunk}");
        if (TilesEditados + edicao.Diff.Custo > Regras.EMax)
            throw new ViolacaoNorma("N1", $"o orçamento do nível ({Regras.EMax} tiles) seria excedido");
        GarantirSequencia(edicao.Slot);
        VerificarConteudo(edicao.Chunk, edicao.Resultado);
        _consumidas.Add(idAprovacao);
        _chunks[edicao.Slot - 1] = edicao.Chunk;
        _conteudo[edicao.Slot - 1] = Grade.Clonar(edicao.Resultado);
        _editados[edicao.Slot - 1] = edicao.Diff.Custo;
        edicao.Encerrada = true;
        Reservas.Liberar(edicao.Reparador);
        Historico.Add($"commit edição #{edicao.Id}: slot {edicao.Slot} = {edicao.Chunk.Id} (+{edicao.Diff.Custo} tiles, aprovação #{a.Id})");
    }

    public void Rollback(Credencial arquiteto, EdicaoProvisoria edicao)
    {
        Exigir(arquiteto, Papel.Arquiteto, "desfazer edição");
        if (edicao.Encerrada) return;
        edicao.Encerrada = true;
        Reservas.Liberar(edicao.Reparador);
        Historico.Add($"rollback edição #{edicao.Id}");
    }

    // ---------------------------------------------------------------- invariantes

    private void Exigir(Credencial? c, Papel papel, string operacao)
    {
        if (!_bus.Autentico(c, papel))
        {
            Historico.Add($"recusado: {operacao} por {c?.ToString() ?? "credencial nula"}");
            throw new ViolacaoNorma("PAPEL", $"{operacao} exige credencial válida de {papel}; recebida: {c?.ToString() ?? "nenhuma"}");
        }
    }

    // N2 (sem repetição) e N4 (sem cópia de original), verificados no ponto de efeito.
    private void VerificarConteudo(Chunk chunk, TileMap resultado)
    {
        if (_chunks.Any(c => c?.Id == chunk.Id)) throw new ViolacaoNorma("N2", $"{chunk.Id} já está no nível");
        if (chunk.Origem == OrigemChunk.VglcOriginal) throw new ViolacaoNorma("N4", $"{chunk.Id} é trecho de nível original");
        if (Regras.DistanciaOriginais is { } distancia && distancia(resultado) < Regras.TauCopia)
            throw new ViolacaoNorma("N4", $"{chunk.Id} copiaria uma janela de nível original");
    }

    private void GarantirSequencia(int slot)
    {
        if (slot < 1 || slot > Slots) throw new ViolacaoNorma("SEQ", $"slot {slot} fora do nível");
        if (_chunks[slot - 1] != null) throw new ViolacaoNorma("SEQ", $"slot {slot} já preenchido");
        if (slot > 1 && _chunks[slot - 2] == null) throw new ViolacaoNorma("SEQ", $"slot {slot} antes do slot {slot - 1}");
    }
}
