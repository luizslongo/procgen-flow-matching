using c2_pcg.flowMatchingDataloader;
using c2_pcg.flowMatchingEval;
using ChunkLink.Ambiente;
using ChunkLink.Comunicacao;
using ChunkLink.Movimento;

namespace ChunkLink.Agentes;

// Estratégia local de um reparador: propõe um diff para a falha, ou null se não sabe reparar.
public delegate Diff? EstrategiaReparo(FalhaJogabilidade falha, TileMap candidato);

public sealed record EspecificacaoReparador(string Id, IReadOnlySet<CausaFalha> Causas, EstrategiaReparo Estrategia, int AtrasoResposta = 0);

// Estratégias de reparo. As de costura e folga são novas; a "suporte" reutiliza a pipeline do TCC.
public static class Reparos
{
    public static readonly EspecificacaoReparador Costura = new("reparador-costura",
        new HashSet<CausaFalha> { CausaFalha.Buraco, CausaFalha.Parede }, ReparoDeCostura);

    public static readonly EspecificacaoReparador Folga = new("reparador-folga",
        new HashSet<CausaFalha> { CausaFalha.Teto }, AbrirAlturaLivre);

    // Generalista: aplica as 11 primitivas do TCC a qualquer falha, sem saber qual é a causa.
    public static readonly EspecificacaoReparador Suporte = new("reparador-suporte",
        Enum.GetValues<CausaFalha>().ToHashSet(), PipelineTcc);

    public static IReadOnlyList<EspecificacaoReparador> Padrao => new[] { Costura, Folga, Suporte };

    // Buraco: estreita o buraco só até a largura que o perfil consegue pular (alcance − 1), preservando
    // o desafio; se ele já é pulável (a falha vem da diferença de altura), fecha-o inteiro.
    // Parede: rebaixa a parede à frente do Mario até a altura alcançável pelo salto.
    public static Diff? ReparoDeCostura(FalhaJogabilidade f, TileMap c)
    {
        int x = Math.Clamp(f.ColunaLocal, 0, c.Width - 1);
        var alteracoes = new List<AlteracaoTile>();
        if (f.Resultado.Causa == CausaFalha.Buraco)
        {
            int g = x;
            while (g > 0 && Grade.AlturaChao(c, g - 1) == 0) g--;
            while (g < c.Width && Grade.AlturaChao(c, g) > 0) g++;
            int fim = g;
            while (fim < c.Width && Grade.AlturaChao(c, fim) == 0) fim++;
            int largura = fim - g, pulavel = Math.Max(1, f.AlcanceSalto - 1);
            bool estreitar = largura > pulavel;
            int preencher = estreitar ? largura - pulavel : largura;
            for (int col = g; col < g + preencher; col++)
                alteracoes.Add(new AlteracaoTile(col, c.Height - 1, Grade.Obter(c, col, c.Height - 1), TileTypeEnum.Solid));
            return alteracoes.Count == 0 ? null : new Diff(alteracoes, estreitar ? $"estreitar buraco de {largura} para {pulavel}" : "ponte sobre buraco");
        }
        if (f.Resultado.Causa == CausaFalha.Parede)
        {
            int alturaMaxima = f.AlturaChaoAntes + f.AlturaSalto - 1;
            for (int col = x; col < c.Width && Grade.AlturaChao(c, col) > alturaMaxima; col++)
            {
                int altura = Grade.AlturaChao(c, col);
                for (int y = c.Height - altura; y < c.Height - alturaMaxima; y++)
                {
                    var t = Grade.Obter(c, col, y);
                    if (t is not (TileTypeEnum.Solid or TileTypeEnum.Breakable)) return null; // não corta tubos nem canhões
                    alteracoes.Add(new AlteracaoTile(col, y, t, TileTypeEnum.Empty));
                }
            }
            return alteracoes.Count == 0 ? null : new Diff(alteracoes, "rebaixar parede");
        }
        return null;
    }

    // Remove o teto baixo que impede o Mario grande de passar (altura livre de duas células).
    public static Diff? AbrirAlturaLivre(FalhaJogabilidade f, TileMap c)
    {
        var alteracoes = new List<AlteracaoTile>();
        for (int col = Math.Clamp(f.ColunaLocal, 0, c.Width - 1); col < c.Width; col++)
        {
            int y = c.Height - 1 - Grade.AlturaChao(c, col); // célula logo acima do chão
            if (y < 1 || Grade.Solido(Grade.Obter(c, col, y))) break;
            var teto = Grade.Obter(c, col, y - 1);
            if (!Grade.Solido(teto)) break;
            if (teto is not (TileTypeEnum.Solid or TileTypeEnum.Breakable or TileTypeEnum.QuestionEmpty or TileTypeEnum.QuestionFull)) break;
            alteracoes.Add(new AlteracaoTile(col, y - 1, teto, TileTypeEnum.Empty));
        }
        return alteracoes.Count == 0 ? null : new Diff(alteracoes, "abrir altura livre");
    }

    public static Diff? PipelineTcc(FalhaJogabilidade f, TileMap c)
    {
        var reparado = ChunkStructureRepair.RepairAll(c, new ChunkStructureRepairConfig());
        var diff = Diff.Entre(c, reparado, "pipeline do TCC (11 primitivas)");
        return diff.Custo == 0 ? null : diff;
    }
}

// Reparador: participa do Contract Net (P3) com conhecimento e custo locais; ao vencer, reserva
// as colunas no nível (P5) e abre uma edição provisória (o commit é decisão do arquiteto).
public sealed class RepairAgent : Agente
{
    private readonly Dictionary<string, ConversaContractNet> _conversas = new();
    private readonly Dictionary<string, (ChamadaReparo Chamada, Diff Diff)> _propostas = new();
    private readonly List<(Mensagem Msg, long Quando)> _adiadas = new();

    public RepairAgent(EspecificacaoReparador spec, Mundo mundo) : base(spec.Id, Papel.Reparador, mundo) => Spec = spec;

    public EspecificacaoReparador Spec { get; }

    public override void Ciclo()
    {
        foreach (var m in Receber())
        {
            if (m.Performativa == Performativa.Cfp && Spec.AtrasoResposta > 0) _adiadas.Add((m, Tick + Spec.AtrasoResposta));
            else Tratar(m);
        }
        foreach (var a in _adiadas.Where(a => a.Quando <= Tick).ToList())
        {
            _adiadas.Remove(a);
            Tratar(a.Msg);
        }
    }

    private void Tratar(Mensagem m)
    {
        if (m.Performativa == Performativa.Failure) return; // não se responde a uma falha (evita eco)
        if (!_conversas.TryGetValue(m.Conversa, out var conversa)) _conversas[m.Conversa] = conversa = new ConversaContractNet();
        try
        {
            conversa.Avancar(m.Performativa);
            switch (m.Performativa)
            {
                case Performativa.Cfp when m.Conteudo is ChamadaReparo c:
                    ResponderCfp(m, c, conversa);
                    break;
                case Performativa.AcceptProposal:
                    Executar(m, conversa);
                    break;
                case Performativa.RejectProposal:
                    _propostas.Remove(m.Conversa);
                    break;
            }
        }
        catch (ViolacaoProtocolo e)
        {
            Registrar("protocolo.violacao", new { conversa = m.Conversa, performativa = m.Performativa.ToString(), erro = e.Message });
            Enviar(m.Remetente, Performativa.Failure, e.Message, m.Conversa);
        }
    }

    private void ResponderCfp(Mensagem m, ChamadaReparo c, ConversaContractNet conversa)
    {
        Diff? diff = Spec.Causas.Contains(c.Falha.Resultado.Causa) ? Spec.Estrategia(c.Falha, c.Candidato) : null;
        if (diff == null)
        {
            string motivo = Spec.Causas.Contains(c.Falha.Resultado.Causa)
                ? "não encontrei reparo local para esta falha"
                : $"falha {c.Falha.Resultado.Causa} fora da minha especialidade";
            conversa.Avancar(Performativa.Refuse);
            Enviar(m.Remetente, Performativa.Refuse, motivo, m.Conversa);
            return;
        }
        conversa.Avancar(Performativa.Propose);
        _propostas[m.Conversa] = (c, diff);
        Enviar(m.Remetente, Performativa.Propose, new PropostaReparo(Id, diff, Tick), m.Conversa);
    }

    private void Executar(Mensagem m, ConversaContractNet conversa)
    {
        var (chamada, diff) = _propostas[m.Conversa];
        try
        {
            var edicao = M.Nivel.AbrirEdicao(Cred, m.Conversa, chamada.Falha.Slot, M.Pool.Obter(chamada.Falha.ChunkId), diff);
            Registrar("nivel.reserva", new { conversa = m.Conversa, edicao = edicao.Id, reservas = M.Nivel.Reservas.Ativas.Select(r => $"{r.Dono}:[{r.ColunaInicio},{r.ColunaFim})").ToArray() });
            conversa.Avancar(Performativa.Inform);
            Enviar(m.Remetente, Performativa.Inform, new EdicaoAplicada(edicao.Id), m.Conversa);
        }
        catch (Exception e) when (e is ConflitoReserva or ViolacaoNorma)
        {
            Registrar("nivel.recusa", new { conversa = m.Conversa, erro = e.Message });
            conversa.Avancar(Performativa.Failure);
            Enviar(m.Remetente, Performativa.Failure, e.Message, m.Conversa);
        }
    }
}
