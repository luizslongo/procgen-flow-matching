# ChunkLink — DSM3: comunicação, coordenação e protótipo funcional

Entrega DSM3 da disciplina **INE5628 — Sistemas Multiagentes** (UFSC, 2026.2, Prof. Rafael Luiz Cancian).
Autor: Luiz Gabriel Slongo.

[![ChunkLink](https://github.com/luizslongo/procgen-flow-matching/actions/workflows/chunklink.yml/badge.svg?branch=chunklink)](https://github.com/luizslongo/procgen-flow-matching/actions/workflows/chunklink.yml?query=branch%3Achunklink)
Código público na branch [`chunklink`](https://github.com/luizslongo/procgen-flow-matching/tree/chunklink/chunklink)
do repositório do TCC. Ver [Integração contínua](#integração-contínua).

**ChunkLink** é um Sistema Multiagente que monta níveis completos de Super Mario Bros a partir dos *chunks*
gerados pelo modelo de *Conditional Flow Matching* do meu TCC. O objetivo final é uma imagem PNG do nível
montado que, ao lado de um nível original do mesmo bioma renderizado pelo mesmo renderizador, não
possa ser distinguida dele, e que seja jogável.

Sete agentes cooperam:

- o **arquiteto** (BDI), que monta o nível slot a slot;
- o **curador** (BDI), que zela pelo estilo e pelas normas;
- dois **jogadores** (busca A*), que testam a jogabilidade;
- três **reparadores**, que disputam reparos num Contract Net.

O protótipo monta níveis jogáveis nos três biomas. **Os níveis ainda são distinguíveis dos
originais** (ver [Limitações](#limitações)).

## Requisitos da DSM3 e onde estão atendidos

| Requisito do enunciado | Onde está |
|---|---|
| Comunicação com conteúdo e finalidade claros | mensagens ⟨s, r, p, c, o, k⟩ com ontologia tipada e remetente autenticado; [`docs/PROTOCOLOS.md`](docs/PROTOCOLOS.md) §1 |
| Protocolo de interação documentado | P1 a P5, com finalidade, participantes, mensagens, diagramas, máquina de estados, prazos e falhas: [`docs/PROTOCOLOS.md`](docs/PROTOCOLOS.md) §3–7 |
| Coordenação, negociação, cooperação ou planejamento | Contract Net de reparo (P3), negociação estilo × jogabilidade com concessões (P4), planejamento BDI com recuo (P2) |
| Tratamento de conflito, falha ou alternativa | tratadores `-!objetivo`, reconsideração, *rollback*, reenvio de pedidos, prazos, falhas de protocolo: [`docs/PROTOCOLOS.md`](docs/PROTOCOLOS.md) §8 |
| Testes integrados ou cenários automatizados | 68 testes xUnit (unidade, cognição, integração, adversariais, propriedades, injeção de falhas, memória); cenário controlado `chunklink depurar` |
| Evidências de comportamento coletivo | lotes de 10 sementes × 3 biomas × 3 condições, com a contagem das interações: [`docs/EVIDENCIAS_DSM3.md`](docs/EVIDENCIAS_DSM3.md) |

## Resposta ao parecer da DSM2

| Ponto do parecer | O que foi feito | Evidência |
|---|---|---|
| O barramento não valida o remetente | credencial não forjável emitida no registro; o remetente é carimbado pelo barramento; credencial alheia é recusada | `Seg_CredencialDeOutroBarramentoEhRecusada` |
| A aprovação usa só curador + hash, sem vínculo com negociação e slot | o curador registra a aprovação **no Nível**, ligada a conversa, slot, chunk e hash, e ela é **de uso único** | `Seg_AprovacaoNaoPodeSerReutilizada`, `RS03_*` |
| Fixar um chunk não verifica as regras no ambiente | `Fixar` exige um **aceite** do curador para aquele slot e chunk; o Nível impõe N1, N2, N4, ordem e papel em toda operação | `Seg_FixarExige…`, `Seg_NivelImpoeN1N2EN4…` |
| O Mario grande não quebra blocos | quebra tijolos por baixo; o modelo foi **validado contra os níveis originais**: de 8/30 para 21/30 combinações completáveis, com pisão, impulso e alcance calibrado | `chunklink validar-movimento`; [`EVIDENCIAS_DSM3.md`](docs/EVIDENCIAS_DSM3.md) §1 |
| Faltam testes adversariais e de propriedade | 36 testes novos: mensagens forjadas e fora do papel, aprovações reutilizadas, duplicação e perda de mensagens, 12 combinações aleatórias de pool, parâmetros e semente, casos-limite do movimento | `tests/ChunkLink.Tests/TestesRobustez.cs` |
| Não foi possível executar a suíte (sem SDK .NET) | `Dockerfile` que compila e roda os 68 testes durante o build; CI no GitHub Actions que testa e gera o pacote (ver [Integração contínua](#integração-contínua)) | `docker build .`; aba Actions |
| Política por papel, idempotência, falhas de protocolo | *allowlist* (performativa, conteúdo) por papel; descarte de mensagens repetidas; resposta `Failure` e registro em transições inválidas; reenvio de pedidos sem resposta | [`PROTOCOLOS.md`](docs/PROTOCOLOS.md) §1.3 |
| Concluir comunicação, coordenação, cognição, memória e observabilidade | memória episódica persistente em SQLite com TTL (CP9); eventos novos no *trace* (recusas, duplicatas, perdas, reenvios, memória) | `Mem_*` |

Defeitos encontrados pelos testes novos e pela revisão do código, todos corrigidos:

- a classificação de falha chamava de BURACO uma fila de inimigos;
- uma confirmação de edição perdida deixava uma reserva presa;
- uma única resposta perdida no ranking derrubava a execução;
- o arquiteto não registrava os vetos do curador e podia tentar de novo um chunk vetado;
- a assinatura do *trace* mudava com o idioma da máquina (`33%` em en-US, `33 %` na cultura
  invariante do contêiner). Agora a execução usa sempre a cultura invariante
  (`RNF01_AssinaturaDoTraceIndependeDoIdiomaDaMaquina`).

## Requisitos para rodar

- **.NET 8 SDK**, ou só **Docker** (veja abaixo). O projeto não depende de rede, de GPU nem de
  serviço pago.
- **Código do TCC:** é compilado por *link*. A ordem de busca é:
  1. o *snapshot* em `tcc-vendor/`, que vem no zip da entrega;
  2. a pasta pai, quando o ChunkLink está dentro do repositório do TCC (branch `chunklink`);
  3. um clone de [luizslongo/procgen-flow-matching](https://github.com/luizslongo/procgen-flow-matching)
     ao lado desta pasta;
  4. `-p:TccDir=<caminho>/`.
- **Só para comandos com níveis reais:**
  - o diretório `Processed` do [VGLC](https://github.com/TheVGLC/TheVGLC), que não é redistribuído;
  - para o PNG, os *sprites* gerados por `scripts/extract-sprites.py` do TCC.

## Como executar

```bash
# sem .NET instalado: compila e roda os 68 testes no build da imagem, depois o cenário controlado
docker build -t chunklink . && docker run --rm chunklink depurar

# com .NET 8
dotnet build ChunkLink.sln
dotnet test                                                  # 68 testes, ~2 s

# cenário controlado: narrativa cognitiva + trace JSONL
dotnet run --project src/ChunkLink.Cli -- depurar --trace saida/depuracao/trace.jsonl
#   … com falhas de entrega injetadas, ou com memória persistente
dotnet run --project src/ChunkLink.Cli -- depurar --perder 0.10 --duplicar 0.3 --semente 2
dotnet run --project src/ChunkLink.Cli -- depurar --memoria saida/memoria.db

# confronta o simulador com os níveis originais
dotnet run --project src/ChunkLink.Cli -- validar-movimento --vglc "<TheVGLC>/Super Mario Bros/Processed"

# montagem real: PNG e par cego (com --memoria, aprende entre execuções)
dotnet run --project src/ChunkLink.Cli -- montar \
    --pool dados/pool/treetop/reparado --vglc "<TheVGLC>/Super Mario Bros/Processed" \
    --bioma treetop --original mario-1-3 --sprites "<procgen-flow-matching>/sprites" --saida saida/treetop

# E0 × E1 × E2 × E2 sem cognição num pool; e o lote com 10 sementes e as contagens de interação
dotnet run --project src/ChunkLink.Cli -- comparar --pool dados/pool/treetop/reparado --vglc "<…>" --bioma treetop --original mario-1-3
dotnet run --project src/ChunkLink.Cli -- lote --pool dados/pool/treetop/bruto --vglc "<…>" --bioma treetop --original mario-1-3 --sementes 10
```

Os exemplos em `exemplos/` foram gerados com esses comandos:

| Pasta ou arquivo | Conteúdo |
|---|---|
| `depuracao/` | cenário controlado: narrativa e *trace* |
| `falhas/` | cenário com duplicação e perda de mensagens, e as taxas de sucesso |
| `memoria/` | três execuções reais compartilhando a memória |
| `validacao-movimento.txt` | os originais completáveis por perfil |
| `montagem-*/` | nível em ASCII e PNG, par cego com gabarito, métricas e *trace* |
| `comparacao-baselines.txt` | uma execução por bioma |
| `lote-*/` | 10 sementes por bioma e condição |

Os PNGs usam os *sprites* do jogo (gráficos da Nintendo). Eles estão no pacote somente para avaliação
acadêmica, seguindo a política do repositório do TCC, e não devem ser redistribuídos.

Para regenerar todos os exemplos: `VGLC=<…/Processed> SPRITES=<…/sprites> ferramentas/gerar-exemplos.sh`.
O zip da entrega é gerado por `ferramentas/empacotar.sh`, e os diagramas, por
`node ferramentas/diagramas.mjs`.

## Integração contínua

O *workflow* [`chunklink.yml`](https://github.com/luizslongo/procgen-flow-matching/actions/workflows/chunklink.yml?query=branch%3Achunklink)
roda no GitHub Actions a cada push na branch `chunklink`, sem nada instalado na máquina de quem
avalia:

1. compila e roda os 68 testes;
2. executa o cenário controlado e o cenário com falhas de entrega injetadas;
3. gera o zip da entrega, constrói a imagem Docker a partir dele (o *build* roda os testes de novo)
   e executa o cenário dentro do contêiner.

Na página de cada execução ficam o resumo, os resultados dos testes (`.trx`), os *traces* e o zip da
entrega para download. Na branch pública, os PNGs de `exemplos/` (sprites da Nintendo) não são
publicados.

## Cenário controlado de depuração

`chunklink depurar` executa um pool de 7 chunks escritos à mão contra 2 níveis de referência
(`Runtime/CenarioControlado.cs`). Cada chunk foi desenhado para disparar um mecanismo:

1. **Ranking** do pool por *playtest* individual (P1).
2. **Slot 1:** o curador dá parecer e **registra um aceite no Nível**; `a-baixo` é fixado com esse
   aceite.
3. **Slot 2:** os dois muros falham por PAREDE na entrada. O arquiteto **infere**
   `culpa_costura(2)` e `exigencia_saida(1)=alta`, **abandona** a intenção de reparo, **recua** o
   slot 1 e escolhe `d-escada`.
4. **Slot 3:** o curador emite a restrição **proativa** "esparso"; o arquiteto registra
   `incompativel(b-muro1|b-muro2)`.
5. **Slot 4:** com `a-baixo` à esquerda, `b-muro2` nem é testado (memória
   `incompativel(a-baixo|b-muro2)`). `c-teto` falha só para o Mario grande e `f-copia` é **vetado**
   (N4). O buraco de `e-buraco1` abre um **Contract Net**:
   - a folga recusa;
   - a costura propõe **estreitar o buraco de 5 para 4 colunas (1 tile)**;
   - a pipeline do TCC propõe 11 tiles.

   O curador registra a aprovação da mais barata no Nível. Seguem a reserva, a edição provisória, a
   revalidação e o **commit** com a aprovação de uso único.
6. **Validação** do nível completo por todos os perfis-alvo.

## Resultados (10 sementes por bioma; detalhes em [`EVIDENCIAS_DSM3.md`](docs/EVIDENCIAS_DSM3.md))

Formato das células: montou · KL 2×2 médio (menor = mais parecido com os originais) · tiles editados.

| Pools reparados pelo TCC | E0 aleatória + TCC | E1 central guloso | **E2 ChunkLink** | E2 sem cognição |
|---|---|---|---|---|
| overworld | 10/10 · 0,44 · 12,7 | 10/10 · 0,43 · 9,6 | **10/10 · 0,37 · 0** | 10/10 · 0,37 · 0 |
| underground | 10/10 · 0,71 · 8,2 | 10/10 · 0,70 · 9,2 | 10/10 · 0,74 · 0 | 9/10 · 0,78 · 0 |
| treetop | 6/10 · 1,38 · 4,3 | 10/10 · 1,32 · 5,1 | **10/10 · 1,12 · 0** | **0/10** |

Com os pools brutos, sem o reparo do TCC:

- os baselines reescrevem de 130 a 320 tiles por nível, e o ChunkLink monta 10/10 sem editar nada;
- no treetop, o ChunkLink chega a KL 0,76, contra 1,27 do E1;
- sem cognição, falha em 10/10 no underground e no treetop.

Sob escassez (pools de 10 chunks), aparecem os Contract Nets, mas o curador recusa os reparos acima
do limite de edição, e o ChunkLink monta menos que o E1. É um trade-off entre fidelidade e
completude, a calibrar na DSM4. São 10 sementes, sem testes estatísticos: o protocolo completo está
em [`docs/PROTOCOLO_EXPERIMENTAL.md`](docs/PROTOCOLO_EXPERIMENTAL.md).

## Estrutura

```text
ChunkLink/
├── src/ChunkLink.Core/
│   ├── Agentes/        LevelArchitect e Curator (BDI), Playtester, Repair, Parametros, Mundo
│   ├── Ambiente/       Grade, Pool, Corpus, Nível (registro de autorizações, reservas, edição, commit)
│   ├── Bdi/            interpretador BDI (crenças, desejos, intenções, planos, reconsideração)
│   ├── Comunicacao/    mensagens, credenciais, política por papel, MessageBus, Contract Net, Tracer
│   ├── Estilo/         conhecimento de estilo do curador (KL 2×2, costura, cópia)
│   ├── Memoria/        memória episódica persistente (SQLite, TTL em execuções)
│   ├── Movimento/      modelo de movimento e jogador A*
│   └── Runtime/        sistema, escalonador, baselines, cenário controlado, renderização
├── src/ChunkLink.Cli/  depurar, montar, comparar, lote, validar-movimento, importar-pool
├── tests/              68 testes xUnit
├── dados/              geração bruta do TCC (logs) e pools congelados por bioma
├── docs/               ARQUITETURA, PROTOCOLOS, EVIDENCIAS_DSM3, PROTOCOLO_EXPERIMENTAL, diagramas
├── exemplos/           saídas dos comandos acima
├── ferramentas/        empacotar.sh (zip), gerar-exemplos.sh (exemplos/), diagramas.mjs (Mermaid → SVG e PNG)
├── Dockerfile          verificação independente dos testes, sem .NET
├── CHECKPOINTS.md      registro progressivo CP1–CP13
└── USO_DE_IA.md
```

## Limitações

1. **Os níveis montados ainda são distinguíveis dos originais:** o gerador do TCC produz tijolos
   isolados e poucos buracos ou escadas, e o curador mede só características simples de estilo.
2. **Movimento discreto:** sem plataformas móveis (o VGLC não as representa), sem deslizar agachado
   e com o pulo de corrida limitado a 4 colunas. Por isso, 9 de 30 combinações nível original × perfil
   ainda não são completáveis.
3. **Sob escassez, a norma de fidelidade (e_max = 10 tiles) impede reparos grandes,** e o sistema
   monta menos que o baseline central, que edita centenas de tiles.
4. **Interpretador BDI mínimo e runtime de um processo:** sem semântica AgentSpeak completa e sem
   distribuição. Credenciais protegem contra erro e mau uso dentro do processo, não contra código
   arbitrário com acesso à memória.
5. **Avisos de build NU1902/NU1903:**
   - o ImageSharp 3.1.12 (mesma versão do TCC) tem alertas para decodificação de imagens não
     confiáveis;
   - o ChunkLink só decodifica os *sprites* locais;
   - a versão 3.2.0 exige chave de licença em builds Release.
6. **Ainda não feitos:** experimento com estatística, C2ST e teste cego com avaliadores (DSM4).

## Autoria e uso de IA

Ver [`USO_DE_IA.md`](USO_DE_IA.md).
