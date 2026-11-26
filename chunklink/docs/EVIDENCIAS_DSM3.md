# Evidências da DSM3: comportamento coletivo, corretude e robustez

Todos os números deste documento foram gerados pelos comandos indicados, com saída guardada em
`exemplos/`. A suíte automatizada tem **68 testes** (`dotnet test`), que passam também num contêiner
Docker construído a partir do zip da entrega (`docker build .`), sem .NET instalado na máquina.

## 1. Validação do simulador contra o jogo

**Pedido do parecer:** "revise o modelo de movimento [e] confronte seus resultados com situações
conhecidas do jogo".

**Critério:** todo nível original do VGLC deveria ser completável pelos perfis-alvo. O comando é
`chunklink validar-movimento --vglc <Processed>`, com saída em `exemplos/validacao-movimento.txt`.

| Modelo | Originais completáveis (nível × perfil) |
|---|---|
| DSM2: alcance 4, sem pisão, sem quebra de blocos | **8 / 30** |
| DSM3: quebra de tijolos pelo Mario grande, pisão em inimigos, impulso na queda, alcance 5 | **21 / 30** |

O que mudou no modelo (`Movimento/BuscaAEstrela.cs`), e por quê:

- **O Mario grande quebra tijolos por baixo** (`QuebraBlocos`), como descrito na DSM1 e não
  implementado na DSM2. O teste `Mov_MarioGrandeQuebraTijoloPorBaixoPequenoNao` cobre isso.
- **Pisão:** cair sobre um inimigo é apoio. O Mario pode quicar nele para um novo salto, como sobre
  os Paratroopas do 3-1, ou matá-lo e descer no lugar dele, como nos Goombas do 1-2. Encostar de lado
  ou por baixo continua sendo morte. Teste: `Mov_InimigosSaoPisadosOuPulados…`.
- **Impulso:** depois do arco do salto, a queda continua levando o Mario adiante.
- **Alcance 5:** o jogo exige pular buracos de 4 colunas correndo, como no 2-1. Com o alcance 4 da
  DSM2, eles eram impossíveis. Teste: `Mov_BuracoPulavelAteAlcanceMenosUm`.

As 9 combinações que ainda falham têm causa identificada e ficam como limitação:

| Nível | Perfis | Causa |
|---|---|---|
| 1-3, 5-3, 6-3 (treetop) | ambos | plataformas móveis: o VGLC não as representa |
| 8-1 | ambos | buraco de 6 colunas, que pede o pulo de corrida máximo |
| 1-2 | P-Gde | passagem de 1 tile de altura: o jogo permite deslizar agachado, o modelo não |

Excluídas as fases com plataformas móveis, o modelo completa 21 de 24 combinações.

## 2. Robustez a falhas de entrega

Cenário controlado com falhas injetadas no barramento, sementes 1 a 20
(`chunklink depurar --perder p --duplicar q --semente s`; saída em `exemplos/falhas/`).

| Falha injetada | Resultado |
|---|---|
| 30% das mensagens duplicadas | 20/20 montam **o mesmo nível** da execução sem falhas (idempotência) |
| 5% das mensagens perdidas | 20/20 montam o nível |
| 10% das mensagens perdidas | 19/20 |
| 20% das mensagens perdidas | 15/20; nas demais, falha explícita com motivo e sem violar invariantes |

Antes do reenvio de pedidos sem resposta (DSM3), **uma única** resposta de playtest perdida no ranking
inicial derrubava a execução inteira (0 de 3 sementes com 5% de perda). Os testes de injeção de
falhas também revelaram um defeito: uma confirmação de edição perdida deixava uma reserva de colunas
presa. Hoje, toda edição aberta de uma negociação é desfeita quando ela termina
(`Rob_PerdaDeMensagensEhRecuperadaPorReenvio`).

## 3. Comportamento coletivo em dados reais

`chunklink lote` repete as quatro estratégias em 10 subconjuntos aleatórios do pool de cada bioma,
cada semente com um subconjunto diferente:

- **E0:** chunks aleatórios mais o reparo do TCC;
- **E1:** algoritmo central guloso, sem curador e sem negociação;
- **E2:** ChunkLink;
- **E2-a:** ChunkLink sem cognição.

O comando também conta, no trace de E2, as interações entre os agentes. A saída está em
`exemplos/lote-*/` (`resumo.txt`, `metricas.csv`, `coletivo-e2.csv`).

### 3.1 Pools reparados pelo TCC (24 de 30 chunks por semente)

Formato das células: montou · KL 2×2 médio · tiles editados. KL é a divergência dos padrões de tiles
em relação aos originais: quanto menor, mais parecido.

| Bioma | E0 | E1 | **E2** | E2-a |
|---|---|---|---|---|
| overworld | 10/10 · 0,44 · 12,7 | 10/10 · 0,43 · 9,6 | **10/10 · 0,37 · 0** | 10/10 · 0,37 · 0 |
| underground | 10/10 · 0,71 · 8,2 | 10/10 · 0,70 · 9,2 | **10/10 · 0,74 · 0** | 9/10 · 0,78 · 0 |
| treetop | 6/10 · 1,38 · 4,3 | 10/10 · 1,32 · 5,1 | **10/10 · 1,12 · 0** | **0/10** |

No treetop, as costuras visíveis médias foram: E0 1,17; E1 1,00; E2 0,50.

### 3.2 Pools brutos, sem o reparo do TCC (24 de 30)

| Bioma | E0 | E1 | **E2** | E2-a |
|---|---|---|---|---|
| overworld | 10/10 · 0,42 · 270 | 10/10 · 0,43 · 270 | **10/10 · 0,64 · 0** | 10/10 · 0,64 · 0 |
| underground | 10/10 · 0,72 · 324 | 10/10 · 0,72 · 314 | **10/10 · 0,90 · 0** | **0/10** |
| treetop | 6/10 · 1,34 · 132 | 10/10 · 1,27 · 144 | **10/10 · 0,76 · 0** | **0/10** |

### 3.3 Escassez: pools brutos de só 10 chunks

| Bioma | E0 | E1 | E2 | E2-a |
|---|---|---|---|---|
| overworld | 10/10 · 272 tiles | 10/10 · 265 tiles | 7/10 · 0 tiles | 6/10 |
| underground | 10/10 · 311 tiles | 10/10 · 301 tiles | 8/10 · 0 tiles | 0/10 |
| treetop | 1/10 | 9/10 · 148 tiles | **0/10** | 0/10 |

### 3.4 Interações observadas em E2 (soma nas 10 sementes)

| Interação | ow | ug | tt | ow bruto | ug bruto | tt bruto | ow escassez | ug escassez | tt escassez |
|---|---|---|---|---|---|---|---|---|---|
| playtests pedidos (P1) | 1200 | 1100 | 1820 | 1210 | 1210 | 1728 | 1498 | 1154 | 492 |
| pareceres de estilo (P4) | 70 | 60 | 132 | 71 | 71 | 122 | 127 | 95 | 29 |
| restrições proativas do curador | 10 | 10 | 11 | 10 | 10 | 10 | 11 | 9 | 0 |
| concessões do curador | 0 | 9 | 41 | 1 | 20 | 35 | 12 | 18 | 16 |
| suspensões do desejo de estilo | 0 | 1 | 41 | 0 | 1 | 24 | 2 | 8 | 7 |
| reconsiderações do arquiteto | 0 | 0 | 0 | 0 | 0 | 1 | 0 | 1 | 0 |
| recuos (backtracking) | 0 | 0 | 0 | 0 | 0 | 13 | 55 | 29 | 6 |
| Contract Nets abertos (P3) | 0 | 0 | 0 | 0 | 0 | 0 | 25 | 20 | 1 |
| propostas / recusas de reparadores | 0/0 | 0/0 | 0/0 | 0/0 | 0/0 | 0/0 | 25/50 | 25/35 | 1/2 |
| aprovações / recusas do curador | 0/0 | 0/0 | 0/0 | 0/0 | 0/0 | 0/0 | 0/25 | 0/25 | 0/1 |
| mensagens | 2620 | 2380 | 3965 | 2642 | 2622 | 3786 | 3654 | 2800 | 1063 |

### 3.5 Leitura

1. **A coordenação faz diferença onde há conflito.**
   - Onde estilo e jogabilidade não conflitam (overworld), E2 e E2-a são iguais.
   - No treetop e no underground bruto, a versão sem cognição monta **0/10** e o ChunkLink **10/10**.
   - O mecanismo responsável é a negociação do curador: 41 e 35 concessões, 41 e 24 suspensões.
     Sem ela, curador e arquiteto travam no primeiro slot.
2. **Fidelidade ao gerador:**
   - Com pools brutos, os baselines reescrevem de 130 a 320 tiles por nível, porque aplicam a pipeline
     de reparo do TCC a tudo. O ChunkLink monta 10/10 sem editar nenhum tile, escolhendo e ordenando
     chunks que já se encaixam.
   - No treetop bruto, ainda fica bem mais perto do estilo original: KL 0,76 contra 1,27.
3. **O recuo do arquiteto entra em ação quando o pool é difícil:** 13 recuos no treetop bruto, 55 e
   29 sob escassez.
4. **A negociação de reparo só aparece sob escassez, e ali expõe um conflito de normas.**
   - Os reparadores propõem, ou recusam fora da especialidade (50 e 35 recusas).
   - O curador recusou **todas** as propostas:
     - por N1, porque a pipeline do TCC propõe diffs de 57 a 86 tiles e o limite por chunk é 10;
     - por estilo, quando estreitar o buraco deixa o trecho com chão contínuo demais.
   - Por isso, sob escassez E2 monta menos (7/10, 8/10, 0/10) que E1, que monta editando cerca de
     150 a 300 tiles.
   - É uma consequência direta da norma de fidelidade, não um defeito de protocolo. A calibração de
     e_max fica para os experimentos da DSM4.
5. **Ponto fraco:** no underground, o KL de E2 fica um pouco pior que o dos baselines (0,74 contra
   0,70–0,71), porque o critério de estilo do curador ainda é simples.

## 4. Memória entre execuções (CP9)

Três montagens seguidas do treetop com o pool bruto, compartilhando `--memoria memoria-demo.db`
(saída em `exemplos/memoria/`):

| Execução | Episódios carregados | Recuos | Mensagens |
|---|---|---|---|
| 1 | 0 | 1 | 352 |
| 2 | 5 incompatibilidades | 0 | 306 (−13%) |
| 3 | 5 incompatibilidades | 0 | 306 |

O arquiteto evita, desde o início, os pares que já sabe que não se encaixam. O TTL é contado em
execuções: um episódio que não é observado de novo expira (`Mem_EpisodioExpiraDepoisDoTtlSemReforco`).

## 5. Cenário controlado: ciclo completo

`chunklink depurar`, com saída em `exemplos/depuracao/` e assinatura do trace `64ABEE0053DB3764`. O
ciclo, em ordem:

1. Ranking do pool (P1).
2. Parecer de estilo com **aceite registrado no Nível** (P4).
3. Inferência de culpa da costura, reconsideração e recuo (arquiteto).
4. Restrições proativas do curador.
5. Veto N4 de uma cópia.
6. Contract Net (P3): uma recusa por especialidade e duas propostas.
7. **Aprovação registrada no Nível**, reserva das colunas e edição provisória (P5).
8. Revalidação e commit com a aprovação de uso único.

O reparo de buraco agora **estreita o buraco de 5 para 4 colunas (1 tile)**, em vez de tapá-lo
inteiro (5 tiles): o nível mantém o desafio (`Rep_CosturaEstreitaOBuracoEmVezDeTapar`).

A mesma assinatura é obtida no contêiner Docker, construído a partir do zip, e na máquina do autor.
Antes da DSM3 ela dependia do idioma do sistema (formatação de porcentagens), o que foi corrigido e
é coberto por `RNF01_AssinaturaDoTraceIndependeDoIdiomaDaMaquina`.

## 6. Ameaças à validade

- **Amostra:** 10 sementes, e os subconjuntos vêm de um único pool de 30 chunks por bioma, gerado
  uma vez. Sem testes estatísticos: a comparação com intervalos e testes pareados fica para a DSM4.
- **Determinismo:** E2 é determinístico dado o pool; a variação entre sementes vem só do subconjunto.
- **Estilo:** o conhecimento de estilo depende de 2 a 9 níveis de referência por bioma.
- **KL:** mede padrões locais (2×2), não ritmo nem estrutura de longo alcance.
- **Movimento:** o modelo segue discreto, e as 9 combinações da §1 continuam sem solução.
