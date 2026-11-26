# Registro progressivo dos checkpoints (CP1–CP13)

Registro pedido no parecer da DSM1. Cada checkpoint segue o livro-texto (§5.16) e registra data,
estado, artefatos, evidência verificável e as decisões alteradas desde o checkpoint anterior.

**Estados**: ✅ concluído · 🟡 parcial · ⏳ pendente.

| CP | Tema | Estado | Data | Entrega |
|---|---|---|---|---|
| CP1 | Cenário e justificativa | ✅ | 2026-10-19 | DSM1 |
| CP2 | Requisitos, objetivos e papéis | ✅ | 2026-10-19 | DSM1 |
| CP3 | Modelo formal e protocolos | ✅ (revisado na DSM2) | 2026-10-19 → 2026-11-03 | DSM1 / DSM2 |
| CP4 | Repositório, ambiente e *baseline* | ✅ | 2026-11-03 | DSM2 |
| CP5 | Comunicação multiagente | ✅ (reforçado na DSM3) | 2026-11-03 → 2026-11-18 | DSM2 / DSM3 |
| CP6 | Coordenação e alocação | ✅ (reforçado na DSM3) | 2026-11-03 → 2026-11-18 | DSM2 / DSM3 |
| CP7 | Ferramentas | ✅ | 2026-11-18 | DSM3 |
| CP8 | Conhecimento | 🟡 | 2026-11-03 | DSM2 |
| CP9 | Memória e estado persistente | ✅ | 2026-11-18 | DSM3 |
| CP10 | Segurança e observabilidade | ✅ | 2026-11-18 | DSM3 |
| CP11 | Testes | ✅ | 2026-11-18 | DSM3 |
| CP12 | Experimentos e avaliação | 🟡 (lotes de 10 sementes; estatística na DSM4) | 2026-11-18 | DSM3 |
| CP13 | Integração, documentação e apresentação | ⏳ | — | — |

---

## CP1 — Cenário e justificativa (DSM1)

- **Artefato**: relatório DSM1, Seção 1 (`DSM1_ChunkLink_LuizSlongo.pdf`) e `modelos/modelo.yaml`.
- **Evidência**: justificativa do SMA pelo checklist da Tabela 5.1, com condição de rejeição explícita.
- **Decisão**: o objetivo final é a imagem PNG de um nível completo indistinguível de um original
  (definido pelo autor).

## CP2 — Requisitos, objetivos e papéis (DSM1)

- **Artefato**: 20 requisitos (RF, RNF, RS), 4 papéis e 9 instâncias, normas N1–N5.
- **Evidência**: rastreabilidade verificada automaticamente pelo *build* do DSM1.

## CP3 — Modelo formal e protocolos (DSM1 → revisado na DSM2)

- **Artefato**: protocolos P1–P5; máquina de estados do preenchimento de slot.
- **Revisões na DSM2, motivadas pelo parecer**:
  - arquiteto e curador passam a ter **arquitetura cognitiva BDI explícita**: crenças com fonte,
    desejos, intenções, biblioteca de planos e política de reconsideração (`docs/ARQUITETURA.md` §4);
  - P4 vira negociação: recusa com motivo, concessão gradual do curador, suspensão do desejo de estilo
    após impasses, e restrições proativas do curador;
  - ordem explícita de prioridade dos conflitos (normas > jogabilidade > estilo > fidelidade);
  - no protótipo, apenas P-Peq e P-Gde são perfis-alvo; P-Ini fica fora (pedido do parecer: dois
    perfis).

## CP4 — Repositório, ambiente e *baseline* (DSM2)

- **Artefatos**: solução `ChunkLink.sln`; `Ambiente/` (Grade, Pool, Nível); pools congelados com
  *hash* em `dados/pool/<bioma>/{bruto,reparado}`, gerados pelo checkpoint final do TCC;
  `Baselines.E0` e `Baselines.E1`.
- **Evidência**: `RF01_*`, `Baselines_ExecutamSobreOMesmoPool`; `chunklink comparar`.
- **Decisões**:
  - reuso do TCC por *link* de código-fonte, sem cópia e sem TorchSharp;
  - pool congelado porque o gerador do TCC não expõe semente;
  - **prefixo aberto à direita**: durante a montagem, o jogador precisa chegar a até d−1 colunas da
    borda. Isso foi descoberto no treetop, onde chunks terminam em buraco transponível;
  - entrada a partir de qualquer posição em pé da coluna 0 (corrige um artefato de *spawn* dentro de
    bolsões fechados).

## CP5 — Comunicação (DSM2 → DSM3)

- **Artefatos (DSM2)**: `Mensagem` ⟨s, r, p, c, o, k⟩, `MessageBus` com uma *mailbox* por agente,
  conversas identificadas e prazos em tempo lógico.
- **DSM3, motivado pelo parecer**:
  - remetente autenticado por credencial não forjável;
  - política por papel;
  - coerência entre o conteúdo e quem envia;
  - idempotência (descarte por `id`);
  - resposta `Failure` a mensagens fora do protocolo;
  - injeção de falhas de entrega;
  - especificação completa em `docs/PROTOCOLOS.md`.
- **Evidência**: `RNF03_*`, `P3_*`, `Seg_CredencialDeOutroBarramento…`, `Seg_PoliticaDePapeis…`,
  `Rob_MensagensDuplicadas…`, `Rob_ViolacaoDeProtocolo…`.

## CP6 — Coordenação e alocação (DSM2)

- **Artefatos**: Contract Net de reparo com `reply_by`; aprovação do curador antes do aceite; reserva de
  colunas; edição provisória; revalidação; *commit* ou *rollback*; recuo com atribuição de culpa.
- **Evidência**: `RF05_RF06_ContractNetComReservaEdicaoProvisoriaRevalidacaoECommit`,
  `RF05_PropostaTardiaNaoEhConsideradaERS02_CuradorRecusaAcimaDoLimite`,
  `RF06_RevalidacaoFalhaFazRollbackETentaAProximaProposta`, `RS01_*`, `RS03_*`,
  `Arquiteto_InfereCulpaDaCosturaReconsideraERecua`.
- **Decisões**:
  - propostas tardias são registradas (`cn.descartada`) e encerradas com `reject-proposal`; o caso
    foi descoberto por teste;
  - após um *rollback*, tenta-se a próxima proposta aprovada.
- **DSM3**:
  - aprovações e aceites do curador são registrados no Nível, ligados a conversa, slot, chunk e
    *hash*, e são de uso único;
  - o Nível impõe N1, N2, N4, a ordem dos slots e o papel em toda operação;
  - pedidos sem resposta são reenviados;
  - edições abertas de uma negociação encerrada são desfeitas;
  - o reparo de buraco passou a estreitar o buraco até a largura pulável, em vez de tapá-lo.
  Evidência: `Seg_*`, `Rob_PerdaDeMensagens…`, `Rep_CosturaEstreita…` e os lotes em
  `docs/EVIDENCIAS_DSM3.md`.

## CP7 — Ferramentas (DSM2 → DSM3)

- **Feito**: A*, conhecimento de estilo, 11 primitivas do TCC (via reparador generalista) e o
  renderizador do TCC, todos invocados por agentes e sem papel de agente.
- **DSM3**:
  - *allowlist* por papel (`PoliticaPapeis`), aplicada no barramento, e checagem de papel nas
    operações do Nível;
  - reenvio idempotente de pedidos;
  - o simulador de movimento ganhou quebra de tijolos, pisão em inimigos e impulso, e foi validado
    contra os originais (`chunklink validar-movimento`: de 8/30 para 21/30).

## CP8 — Conhecimento (DSM2, parcial)

- **Feito**: `ConhecimentoEstilo`, que deriva estatísticas por posição, padrões 2×2, limiar de costura
  e janelas originais do corpus de referência (níveis do VGLC identificados pelo nome).
- **Decisão**: a cópia de originais (N4) passou a ser medida pela **distância estrutural acima da faixa
  do chão**. A distância de Hamming bruta vetava qualquer janela "céu + chão"; o problema foi
  descoberto no cenário controlado.
- **Pendente**: RAG textual não se aplica ao domínio; avaliar uma base de regras de design de nível.

## CP9 — Memória (DSM3)

- **Artefato**: `Memoria/MemoriaEpisodica.cs`. Episódios (incompatibilidades e vetos) ficam em SQLite,
  indexados pelo *hash* do chunk, com TTL contado em execuções.
- **Uso**: o arquiteto carrega os episódios como crenças com fonte `memória` e grava os que observou.
  Parâmetro `--memoria` em `montar` e `depurar`.
- **Evidência**: `Mem_SegundaExecucaoReusaIncompatibilidadesEVetos`,
  `Mem_EpisodioExpiraDepoisDoTtlSemReforco`. Em `exemplos/memoria/`, a 2ª execução real do treetop
  dispensa o recuo e usa 13% menos mensagens.

## CP10 — Segurança e observabilidade (DSM2, parcial)

- **Feito**:
  - N1 (orçamento), N2 (repetição) e N4 (cópia), aplicadas pelo curador;
  - N3 (reserva) e N5 (*commit* só com aprovação), aplicadas pelo artefato Nível;
  - *trace* JSONL de mensagens, crenças, intenções, planos, reconsiderações e alterações do nível;
  - narrativa legível (`chunklink depurar`).
- **DSM3**:
  - credenciais e *allowlist* por papel;
  - registro de autorizações no Nível;
  - eventos novos no *trace*: `msg.rejeitada`, `msg.duplicada`, `msg.inesperada`, `falha.perda`,
    `falha.duplicacao`, `reenvio`, `nivel.recusa`, `memoria.*`.
- **Pendente**: política de retenção do *trace*.

## CP11 — Testes (DSM2, parcial)

- **Feito**: 32 testes xUnit, *offline*, executados em cerca de 0,3 s.
  - Unidade: ambiente, movimento, normas e protocolo.
  - Cognição: interpretador BDI, decisões do arquiteto e do curador.
  - Integração: cenário controlado completo.
  - Ablação.
  - Reprodutibilidade: assinatura do *trace*.
- **DSM3**: 68 testes. Os 36 novos (`TestesRobustez.cs`) são:
  - adversariais: credencial forjada, mensagem fora do papel, aprovação reutilizada, aceite de outro
    slot, norma burlada;
  - injeção de falhas: duplicação e perda de mensagens;
  - violação de protocolo;
  - propriedades: 12 combinações aleatórias de pool, parâmetros e semente;
  - casos-limite do movimento;
  - memória.

  A verificação independente é feita por `Dockerfile` e CI (GitHub Actions).
- **Pendente**: regressão contra *traces* de referência.

## CP12 — Experimentos (DSM2, parcial)

- **Feito**:
  - protocolo revisado com TOST, margem de equivalência, tamanho de amostra, modelo misto e C2ST
    (`docs/PROTOCOLO_EXPERIMENTAL.md`);
  - ablação E2-a (sem cognição);
  - primeira execução por bioma (`exemplos/comparacao-baselines.txt`).
- **DSM3**: `chunklink lote`, com 10 sementes × 3 biomas × 3 condições (pool reparado, bruto e
  escasso), as 4 estratégias e a contagem das interações (`docs/EVIDENCIAS_DSM3.md`).
- **Pendente (DSM4)**: testes estatísticos, baseline de limiar fixo, calibração de e_max,
  discriminador automático e teste cego com avaliadores.

## CP13 — Integração e apresentação (pendente)
