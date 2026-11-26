# Protocolo experimental revisado

Revisão do plano experimental do DSM1 após o parecer: *"um intervalo de confiança que contenha 50% não
demonstra, por si só, indistinguibilidade estatística"*. O parecer está correto: não rejeitar a
hipótese de diferença é diferente de demonstrar equivalência. Este documento substitui a meta M11 do
DSM1 por um teste de **equivalência** com margem definida antes da coleta.

## 1. Pergunta e hipóteses

**Pergunta.** Os níveis montados pelo ChunkLink, renderizados pelo mesmo `MapPngRenderer` que os
originais, são indistinguíveis dos níveis originais do mesmo bioma, e a cognição dos agentes contribui
para isso em relação a uma pipeline?

| Hipótese | Enunciado | Teste |
|---|---|---|
| **H1** (principal) | para E2, a acurácia de identificação do original está dentro de [50 − δ, 50 + δ] | TOST (equivalência) |
| H2 | E0 é identificado acima do acaso | teste unilateral contra 50% |
| H3 | E2 tem divergência de estilo (M12) menor que E1 e que E2-sem-cognição | comparação pareada por semente |
| H4 | sem cognição, a taxa de montagem (M2) cai nos biomas com conflitos | proporção de montagens por estratégia |

## 2. Estratégias comparadas

| Id | Estratégia | Implementação |
|---|---|---|
| E0 | concatenação aleatória de chunks reparados pela pipeline do TCC | `Baselines.E0` |
| E1 | centralizado guloso: *ranking* + reparo do TCC + checagem de prefixo; sem curador, sem negociação | `Baselines.E1` |
| E2 | ChunkLink completo | `SistemaChunkLink` |
| E2-a | ChunkLink **sem cognição**: sem inferência de culpa, sem reconsideração, sem concessões | `Parametros { Cognicao = false }` |

E2-a é a ablação que responde ao desafio do parecer, *"demonstrar que a inteligência e a coordenação
dos agentes efetivamente acrescentam capacidades que não seriam obtidas por uma simples pipeline"*:
mesmo código e mesmos agentes, sem os mecanismos cognitivos.

## 3. Teste cego com avaliadores (M11, RF10, RS05)

- **Tarefa**: escolha forçada entre duas imagens (2AFC). Cada par empilha um nível original e um
  montado, com o mesmo comprimento, os mesmos *sprites* e a mesma escala; a ordem é sorteada por par
  (`RenderizarParCego`), e o gabarito fica em arquivo separado.
- **Margem de equivalência**: δ = 10 pontos percentuais, fixada antes da coleta. A justificativa
  prática: acertar até 60% significa que o observador erra quase metade das vezes.
- **Regra de decisão (TOST, α = 0,05)**: E2 é declarado indistinguível se o **intervalo de confiança
  de 90%** da acurácia estiver inteiramente contido em [40%, 60%]. Um IC que apenas contém 50% **não**
  basta.
- **Tamanho da amostra**: com acurácia verdadeira de 50%, δ = 10 pp e poder de 80%, são necessários
  cerca de n ≈ (1,645 + 1,282)² · 0,25 / 0,10² ≈ **215 julgamentos independentes por estratégia**.
  Plano: cerca de 20 avaliadores × 12 pares por estratégia, com pares intercalados entre E0, E1, E2 e
  E2-a.
- **Dependência entre julgamentos**: o mesmo avaliador e o mesmo original aparecem em vários pares. A
  análise usa regressão logística de efeitos mistos (`acerto ~ estratégia + (1|avaliador) + (1|par)`)
  e reporta o IC da acurácia marginal; como verificação, *bootstrap* por avaliador.
- **Tamanho da amostra corrigido pela dependência** (recomendação do parecer da DSM2):
  - os ~215 julgamentos acima supõem independência;
  - com m julgamentos por avaliador e correlação intraclasse ρ, o efeito de desenho é
    DE = 1 + (m − 1)·ρ;
  - com m = 12 e ρ = 0,1 (valor conservador, a reestimar no piloto), DE = 2,1, o que leva a cerca de
    **450 julgamentos por estratégia** (cerca de 38 avaliadores × 12 pares).

  Plano: um piloto com 8 avaliadores para estimar ρ antes da coleta principal. Se não houver
  avaliadores suficientes, o resultado será reportado como estimativa com IC, sem afirmar
  equivalência.
- **Viés de resposta**: além da acurácia, reportar d′ = √2 · z(Pc) da teoria de detecção de sinal,
  para separar discriminação de preferência por uma posição.
- **Privacidade (RS05)**: participação voluntária; o arquivo registra apenas par, estratégia, bioma e
  acerto, sem nome, e-mail ou outro identificador.

## 4. Discriminador automático (complemento escalável)

O teste humano é caro e tem amostra pequena. Como evidência complementar, será usado um **teste de
duas amostras por classificador** (C2ST): um classificador simples (regressão logística sobre
histogramas de padrões 2×2 de janelas de 28 colunas) é treinado para separar janelas originais de
janelas montadas.

- A acurácia é medida por validação cruzada agrupada por nível, para não haver vazamento.
- Indistinguibilidade automática é avaliada pelo mesmo TOST, com δ = 5 pp, porque aqui a amostra é
  grande.
- Isso **não** substitui o teste humano: um classificador pode detectar regularidades que pessoas não
  percebem, e o contrário também ocorre.

## 5. Métricas automáticas com equivalência prática

Para M12 (KL 2×2) e M13 (costuras), a faixa de referência é a dos próprios originais (*leave-one-out*:
cada original contra os demais do bioma). Um nível montado é "equivalente na prática" quando sua métrica
cai dentro do percentil 5–95 dessa faixa. A comparação entre estratégias é **pareada por semente**
(teste de postos sinalizados de Wilcoxon), com 10 sementes por bioma.

## 6. Resultados iniciais (DSM2, uma execução por bioma)

> **Histórico.** Esta tabela usa o modelo de movimento da DSM2 (alcance 4, sem pisão nem quebra de
> blocos). Os resultados da DSM3, com o modelo validado contra os originais e 10 sementes por bioma
> em três condições, estão em [`EVIDENCIAS_DSM3.md`](EVIDENCIAS_DSM3.md) §3.

Gerados por `chunklink comparar` sobre os pools congelados (`exemplos/comparacao-baselines.txt`). A
coluna KL é a divergência de padrões 2×2 em relação aos originais do bioma (quanto menor, mais
parecido).

| Bioma (original pareado) | Estratégia | Montou | P-Peq | P-Gde | Tiles editados | KL 2×2 | Costuras visíveis | Mensagens |
|---|---|---|---|---|---|---|---|---|
| overworld (1-1, 7 slots) | E0 | sim | sim | sim | 7 | 0,538 | 0 | — |
| | E1 | sim | sim | sim | 8 | 0,417 | 0 | — |
| | **E2** | sim | sim | sim | **0** | **0,362** | 0 | 286 |
| | E2-a | sim | sim | sim | 0 | 0,362 | 0 | 286 |
| underground (1-2, 5 slots) | E0 | sim | sim | sim | 10 | 0,665 | 0 | — |
| | E1 | sim | sim | sim | 10 | 0,708 | 0 | — |
| | **E2** | sim | sim | sim | **0** | 0,767 | 0 | 262 |
| | E2-a | sim | sim | sim | 0 | 0,806 | 0 | 328 |
| treetop (1-3, 5 slots) | E0 | **não** | não | não | 4 | 1,308 | 1 | — |
| | E1 | sim | sim | sim | 6 | 1,512 | 1 | — |
| | **E2** | sim | sim | sim | 6 | **1,161** | **0** | 496 |
| | E2-a | **não** | — | — | 0 | — | — | 263 |

**Leitura cautelosa (n = 1 por célula; nenhuma conclusão estatística):**

- **Overworld**: não houve conflito, e E2 e E2-a montam o mesmo nível.
- **Underground**: a cognição reduziu a divergência de estilo e as mensagens.
- **Treetop**: só a versão com cognição montou o nível; as concessões do curador e o recuo por culpa
  da costura evitaram o esgotamento do pool.
- **Custo**: E2 nunca editou mais tiles que E1, mas usa centenas de mensagens.
- **H1 não pode ser avaliada ainda**: inspeção visual dos pares em
  `exemplos/montagem-*/teste-cego/` mostra que os níveis montados são facilmente distinguíveis
  (tijolos isolados, poucos buracos e escadas). Esse é o principal problema a atacar.
