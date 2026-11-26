using ChunkLink.Ambiente;
using ChunkLink.Bdi;
using ChunkLink.Comunicacao;
using ChunkLink.Estilo;

namespace ChunkLink.Agentes;

// Curador — agente cognitivo (BDI).
//   Crenças: slots fechados e seus custos, orçamento restante, densidade dos trechos, impasses de
//            estilo por slot, limiar de estilo vigente, restrições já emitidas, pedidos pendentes.
//   Conhecimento: estatísticas dos níveis originais do bioma (ConhecimentoEstilo) e normas N1–N5.
//   Desejos: atender_pedidos (reativo, prioridade 10) e parecer_original (proativo, prioridade 5).
//   Reconsideração: após repetidas recusas por estilo num slot, concede (aumenta o limiar) e, no
//            limite, suspende o desejo parecer_original naquele slot — nunca as normas N1, N2, N4.
public sealed class CuratorAgent : Agente
{
    private readonly Dictionary<long, Mensagem> _pedidos = new();

    public CuratorAgent(Mundo mundo) : base(Mundo.IdCurador, Papel.Curador, mundo)
    {
        Crencas = new BaseCrencas(Id, M.Tracer, () => Tick);
        Motor = new MotorBdi(Id, Crencas, M.Tracer, () => Tick);
        Crencas.Definir("orcamento", "nivel", M.P.EMax, "configuração");
        Motor.Desejos.Add(new Desejo("atender_pedidos", 10, "atender_pedido",
            _ => _pedidos.Count > 0, _ => new object[] { _pedidos.Keys.Min() }));
        Motor.Desejos.Add(new Desejo("parecer_original", 5, "emitir_restricao",
            _ => ProximaRestricao() != null,
            _ => { var r = ProximaRestricao()!.Value; return new object[] { r.Slot, r.Tipo, r.Motivo }; }));

        Motor.Planos.Add(Plano.Para("atender_pedido", "avaliar_estilo",
            (_, a) => _pedidos.TryGetValue((long)a[0], out var m) && m.Conteudo is PedidoEstilo, AvaliarEstilo));
        Motor.Planos.Add(Plano.Para("atender_pedido", "decidir_aprovacao",
            (_, a) => _pedidos.TryGetValue((long)a[0], out var m) && m.Conteudo is PedidoAprovacao, DecidirAprovacao));
        Motor.Planos.Add(Plano.Para("emitir_restricao", "emitir_restricao", (_, _) => true, EmitirRestricao));
    }

    public BaseCrencas Crencas { get; }
    public MotorBdi Motor { get; }

    public override void Ciclo()
    {
        foreach (var m in Receber()) Perceber(m);
        Deliberar();
        Motor.Ciclo();
    }

    public double Limiar(int slot) => Crencas.Valor<double?>("limiar", slot) ?? M.P.LimiarEstilo;

    private void Perceber(Mensagem m)
    {
        switch (m.Conteudo)
        {
            case PedidoEstilo p when m.Performativa == Performativa.Request:
                _pedidos[m.Id] = m;
                Crencas.Definir("pedido", m.Id, $"avaliar estilo de {p.Candidatos.Count} candidato(s) no slot {p.Slot}", m.Remetente);
                break;
            case PedidoAprovacao p when m.Performativa == Performativa.Request:
                _pedidos[m.Id] = m;
                Crencas.Definir("pedido", m.Id, $"aprovar diff de {p.Diff.Custo} tiles no slot {p.Slot}", m.Remetente);
                break;
            case SlotFechado s:
                Crencas.Definir("slot", s.Slot, s.ChunkId, m.Remetente);
                Crencas.Definir("custo", s.Slot, s.TilesEditados, m.Remetente);
                var conteudo = M.Nivel.ConteudoDoSlot(s.Slot);
                if (conteudo != null)
                    Crencas.Definir("densidade", s.Slot, Math.Round(AssinaturaJanela.De(conteudo).Densidade, 3), "percepção do nível");
                AtualizarOrcamento();
                break;
            case SlotReaberto r:
                for (int k = r.Slot; k <= M.Nivel.Slots; k++)
                {
                    Crencas.Remover("slot", k);
                    Crencas.Remover("custo", k);
                    Crencas.Remover("densidade", k);
                }
                AtualizarOrcamento();
                break;
            default:
                // Mensagem fora do protocolo P4: registrada e ignorada, sem alterar crenças.
                Registrar("msg.inesperada", new { id = m.Id, de = m.Remetente, performativa = m.Performativa.ToString(), conversa = m.Conversa });
                break;
        }
    }

    private void AtualizarOrcamento()
    {
        int gasto = Crencas.Todas("custo").Sum(c => (int)c.Valor!);
        Crencas.Definir("orcamento", "nivel", M.P.EMax - gasto, "inferência");
    }

    // Reconsideração dos próprios compromissos diante do impasse com o arquiteto.
    private void Deliberar()
    {
        if (!M.P.Cognicao) return;
        foreach (var c in Crencas.Todas("impasse").ToList())
        {
            int slot = int.Parse(c.Chave);
            int n = (int)c.Valor!;
            if (n >= M.P.ImpassesParaConceder && !Crencas.Tem("estilo_relaxado", slot))
            {
                Crencas.Definir("estilo_relaxado", slot, true, "deliberação");
                Registrar("reconsideracao", new
                {
                    decisao = $"suspender o desejo parecer_original no slot {slot}",
                    motivo = $"{n} recusas por estilo seguidas bloqueiam a montagem; as normas N1, N2 e N4 continuam valendo",
                });
            }
            else if (n > 0 && !Crencas.Tem("estilo_relaxado", slot))
            {
                double novo = Math.Round(M.P.LimiarEstilo * (1 + 0.25 * n), 2);
                if (Crencas.Valor<double?>("limiar", slot) != novo)
                {
                    Crencas.Definir("limiar", slot, novo, "deliberação");
                    Registrar("concessao", new { slot, limiar = novo, motivo = $"{n} recusa(s) por estilo neste slot" });
                }
            }
        }
    }

    private IEnumerable<Passo> AvaliarEstilo(ContextoPlano ctx)
    {
        long id = ctx.Arg<long>(0);
        var m = _pedidos[id];
        var p = (PedidoEstilo)m.Conteudo;
        var posicao = ConhecimentoEstilo.Posicao(p.Slot, p.TotalSlots);
        double limiar = Limiar(p.Slot);
        bool relaxado = Crencas.Tem("estilo_relaxado", p.Slot);
        var pareceres = new List<ParecerEstilo>();
        int recusasPorEstilo = 0;

        foreach (var c in p.Candidatos)
        {
            string? veto = null;
            double copia = 1;
            if (M.Nivel.ChunksUsados.Any(u => u.Id == c.ChunkId)) veto = "N2";
            else if ((copia = M.Estilo.MenorDistanciaAOriginais(c.Conteudo)) < M.P.TauCopia) veto = "N4";
            var av = M.Estilo.Avaliar(c.Conteudo, posicao, p.VizinhoEsquerdo);
            bool estiloOk = av.Distancia <= limiar && av.Costura <= M.Estilo.LimiarCostura;
            bool aceitavel = veto == null && (relaxado || estiloOk);
            if (veto == null && !aceitavel) recusasPorEstilo++;
            if (veto != null) Crencas.Definir("vetado", c.ChunkId, veto, "avaliação");

            string justificativa = veto switch
            {
                "N4" => $"veto N4: cópia de janela original (distância estrutural {copia:P0})",
                "N2" => "veto N2: chunk já usado no nível",
                _ when aceitavel && estiloOk => $"típico para {posicao}: distância {av.Distancia:F2} ≤ {limiar:F2}",
                _ when aceitavel => $"aceito por concessão (estilo relaxado): distância {av.Distancia:F2}",
                _ when av.Distancia > limiar => $"atípico para {posicao}: {av.CaracteristicaMaisDistante} (distância {av.Distancia:F2} > {limiar:F2})",
                _ => $"costura visível: salto de chão {av.Costura} > {M.Estilo.LimiarCostura}",
            };
            // O aceite fica registrado no Nível: sem ele, o arquiteto não consegue fixar o chunk.
            long? aceite = aceitavel ? M.Nivel.RegistrarAceite(Cred, p.Slot, c.ChunkId, Tick).Id : null;
            pareceres.Add(new ParecerEstilo(c.ChunkId, av, aceitavel, veto, justificativa, aceite));
        }

        if (pareceres.All(x => !x.Aceitavel) && recusasPorEstilo > 0)
            Crencas.Definir("impasse", p.Slot, Crencas.Valor<int>("impasse", p.Slot) + 1, "avaliação");

        Enviar(m.Remetente, Performativa.Inform, new RespostaEstilo(p.Slot, pareceres, limiar, relaxado), m.Conversa);
        Concluir(id);
        yield break;
    }

    private IEnumerable<Passo> DecidirAprovacao(ContextoPlano ctx)
    {
        long id = ctx.Arg<long>(0);
        var m = _pedidos[id];
        var p = (PedidoAprovacao)m.Conteudo;
        int orcamento = Crencas.Valor<int>("orcamento", "nivel");
        RecusaAprovacao? recusa = null;

        if (p.Diff.Custo > M.P.EMaxChunk)
            recusa = new($"o diff altera {p.Diff.Custo} tiles; o limite por chunk é e_max = {M.P.EMaxChunk}", "N1", orcamento);
        else if (p.Diff.Custo > orcamento)
            recusa = new($"orçamento do nível insuficiente: restam {orcamento} tiles", "N1", orcamento);
        else if (M.Estilo.MenorDistanciaAOriginais(p.Resultado) < M.P.TauCopia)
            recusa = new("o resultado copiaria uma janela de nível original", "N4", orcamento);
        else if (!Crencas.Tem("estilo_relaxado", p.Slot))
        {
            var av = M.Estilo.Avaliar(p.Resultado, ConhecimentoEstilo.Posicao(p.Slot, p.TotalSlots), p.VizinhoEsquerdo);
            if (av.Distancia > Limiar(p.Slot))
                recusa = new($"a edição deixaria o trecho atípico ({av.CaracteristicaMaisDistante}, distância {av.Distancia:F2})", "estilo", orcamento);
        }

        if (recusa != null) Enviar(m.Remetente, Performativa.Refuse, recusa, m.Conversa);
        else Enviar(m.Remetente, Performativa.Agree,
            M.Nivel.RegistrarAprovacao(Cred, p.ConversaReparo, p.Slot, p.ChunkId, p.Diff.Hash, Tick), m.Conversa);
        Concluir(id);
        yield break;
    }

    private IEnumerable<Passo> EmitirRestricao(ContextoPlano ctx)
    {
        int slot = ctx.Arg<int>(0);
        string tipo = ctx.Arg<string>(1);
        Enviar(Mundo.IdArquiteto, Performativa.Inform, new RestricaoCurador(slot, tipo, ctx.Arg<string>(2)), $"restricao:{slot}");
        Crencas.Definir("restricao_emitida", slot, tipo, "deliberação");
        yield break;
    }

    private void Concluir(long idPedido)
    {
        _pedidos.Remove(idPedido);
        Crencas.Remover("pedido", idPedido);
    }

    // Iniciativa: o curador antecipa restrições de estilo antes de ser consultado.
    private (int Slot, string Tipo, string Motivo)? ProximaRestricao()
    {
        int fixos = 0;
        while (Crencas.Tem("slot", fixos + 1)) fixos++;
        int proximo = fixos + 1;
        if (fixos == 0 || proximo > M.Nivel.Slots || Crencas.Tem("restricao_emitida", proximo)) return null;
        if (proximo == M.Nivel.Slots)
            return (proximo, "final", "nos níveis originais o trecho final termina com chão contínuo; priorize candidatos de saída plana");
        double limite = M.Estilo.PercentilDensidade(0.75);
        if (fixos >= 2 && Crencas.Valor<double>("densidade", fixos) > limite && Crencas.Valor<double>("densidade", fixos - 1) > limite)
            return (proximo, "esparso", $"os dois últimos trechos são mais densos que 75% das janelas originais (> {limite:F3})");
        return null;
    }
}
