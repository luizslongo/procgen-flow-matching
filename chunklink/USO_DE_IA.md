# Uso de IA — DSM2 e DSM3 (ChunkLink)

Declaração do uso de inteligência artificial generativa nesta entrega, separando o que foi decidido
pelo autor do que foi produzido pela ferramenta, e registrando como o resultado foi verificado.

## Ferramenta

- **Claude** (Anthropic), modelo Claude Opus 5.5, usado pelo **Claude Code** (assistente em linha de
  comando com acesso aos arquivos locais e ao terminal).
- Sessões de 19/10/2026 (DSM1), 03/11/2026 (DSM2), 11/11/2026 (apresentação) 18/11/2026 (DSM3) e 26/11/2026 (publicação na branch `chunklink` e CI).

## Papel do autor

- **Projeto e objetivo**: a ideia original (jogadores A* que avaliam e montam *chunks*) e o objetivo
  final (um PNG indistinguível de um nível original).
- **Trabalho prévio próprio**: o TCC e o repositório `procgen-flow-matching`, com o gerador, as
  primitivas de reparo, o renderizador, o modelo treinado e os *sprites*.
- **Direção da DSM2**: o parecer do professor e a decisão de seguir as recomendações dele (agentes
  cognitivos, cenário mínimo, protocolo estatístico).
- **Responsabilidade**: revisar o código, os resultados e a documentação, e aceitar ou rejeitar as
  decisões técnicas listadas abaixo.

## O que a IA fez na DSM2

1. **Análise do parecer**: transformou cada ponto do parecer em ações (tabela no `README.md`).
2. **Decisões técnicas propostas e implementadas**:
   - *runtime* próprio em C#/.NET 8, reutilizando o código do TCC por *link* e sem TorchSharp;
   - interpretador BDI com planos como corrotinas e política de reconsideração;
   - inferência de culpa da costura e exigência de saída para o arquiteto;
   - concessão gradual e restrições proativas para o curador;
   - ablação "sem cognição";
   - cenário controlado de depuração;
   - protocolo estatístico com TOST.
3. **Código**: escreveu todo o código de `src/` e `tests/`, os diagramas Mermaid e a documentação
   (`README.md`, `docs/`, `CHECKPOINTS.md`, este arquivo).
4. **Dados**: executou a CLI de geração do TCC, com o *checkpoint* `unet-conditional-checkpoint.bin`
   do autor, e congelou 30 *chunks* por bioma em `dados/pool/`.

## Verificações feitas

- **Testes**: a suíte de 32 testes foi executada e passou. Durante a sessão, os testes e as execuções
  revelaram quatro defeitos que foram corrigidos:
  - a métrica de cópia vetava qualquer janela de céu e chão;
  - caracteres fora do vocabulário VGLC eram aceitos, porque o mapeamento do TCC os converte em
    vazio;
  - propostas tardias do Contract Net eram ignoradas sem registro;
  - os pares do teste cego usavam sempre a mesma semente para a ordem.
- **Revisão na preparação da apresentação**: ao conferir o *trace* com a documentação, apareceram
  três problemas, todos corrigidos:
  - o caminho de *rollback* do arquiteto não era exercitado por nenhum teste, e a documentação
    afirmava que ele ocorria no treetop. Foi acrescentado o teste
    `RF06_RevalidacaoFalhaFazRollbackETentaAProximaProposta`, e a afirmação foi corrigida;
  - a descrição dos slots 3 e 4 do cenário controlado no `README.md` estava imprecisa;
  - o comando `comparar` passou a mostrar o motivo da falha de cada estratégia que não monta o
    nível.
- **Dados reais**: as montagens com dados reais foram executadas nos três biomas, com os números
  reportados no `README.md`. Os PNGs foram inspecionados visualmente, e a constatação de que os
  níveis ainda são distinguíveis está registrada como limitação.
- **Limites desta verificação**: os números de comparação vêm de uma execução por bioma e não têm
  valor estatístico. As referências citadas na documentação devem ser conferidas pelo autor.

## O que a IA fez na DSM3 (sessão de 18/11/2026)

1. **Análise do parecer da DSM2**: transformou cada ponto em mudanças de código, testes e
   documentação (tabela "Resposta ao parecer da DSM2" no `README.md`).
2. **Código**:
   - credenciais e política por papel no barramento;
   - registro de autorizações (aceites e aprovações de uso único) no artefato Nível;
   - normas impostas pelo Nível;
   - idempotência e injeção de falhas;
   - reenvio de pedidos sem resposta;
   - memória episódica em SQLite;
   - mudanças no modelo de movimento: quebra de tijolos, pisão, impulso, alcance 5;
   - reparo de buraco que estreita em vez de tapar;
   - comandos `validar-movimento` e `lote`;
   - `Dockerfile` e *workflow* de CI.
3. **Testes**: os 36 testes novos de `TestesRobustez.cs`.
4. **Documentação**: `docs/PROTOCOLOS.md`, `docs/EVIDENCIAS_DSM3.md` e as atualizações do `README.md`,
   do `docs/ARQUITETURA.md`, do `docs/PROTOCOLO_EXPERIMENTAL.md` e do `CHECKPOINTS.md`.
5. **Execuções**: validação do simulador contra os 15 níveis originais, lotes de 10 sementes em
   9 condições, demonstração da memória e taxas de sucesso sob falhas injetadas.

### Verificações feitas na DSM3

- **Suíte e build**:
  - os 68 testes passam com `dotnet test`;
  - também passam dentro de um contêiner Docker construído a partir do zip da entrega, em build
    Release e sem o SDK instalado na máquina.
- **Defeitos encontrados e corrigidos durante a sessão**:
  - uma fila de inimigos era classificada como BURACO;
  - uma confirmação de edição perdida deixava uma reserva de colunas presa (revelado pelo teste de
    perda de mensagens);
  - uma única resposta perdida no ranking derrubava a execução (revelado ao medir as taxas de
    sucesso sob perda);
  - o arquiteto não registrava os vetos do curador;
  - a assinatura do *trace* dependia do idioma da máquina, revelado ao comparar a execução local com
    a do contêiner Docker (agora é a mesma: `64ABEE0053DB3764`).
- **Decisão de calibração**:
  - o alcance do salto passou de 4 para 5 colunas porque os níveis originais exigem pular buracos de
    4 colunas;
  - a decisão foi tomada medindo quantos originais ficam completáveis (8/30 → 21/30), e não para
    favorecer os resultados do sistema;
  - as 9 combinações que continuam falhando estão explicadas em `docs/EVIDENCIAS_DSM3.md` §1.
- **Resultados desfavoráveis mantidos no texto**:
  - sob escassez de chunks, o ChunkLink monta menos que o baseline central;
  - no underground, o KL do ChunkLink é um pouco pior que o dos baselines.
- **Limites**: os lotes têm 10 sementes e não têm testes estatísticos.
- **Integração contínua (26/11/2026)**: o projeto foi publicado, a pedido do autor, na branch
  `chunklink` do repositório público do TCC.
  - Na primeira execução do *workflow* `chunklink.yml` no GitHub Actions, os 68 testes passaram com
    .NET e dentro do Docker construído a partir do zip.
  - A assinatura do cenário controlado foi `64ABEE0053DB3764`, a mesma da máquina do autor.

## Checklist do autor antes da entrega

- [ ] Li e entendi o código dos agentes (`Agentes/`) e do interpretador BDI (`Bdi/`).
- [ ] Executei `dotnet test` e `chunklink depurar` na minha máquina.
- [ ] Revisei o `README.md`, o `docs/ARQUITETURA.md`, o `docs/PROTOCOLOS.md`, o `docs/EVIDENCIAS_DSM3.md`
      e o `docs/PROTOCOLO_EXPERIMENTAL.md`.
- [ ] Entendi as mudanças da DSM3: credenciais, registro de autorizações no Nível, reenvio, memória e
      modelo de movimento.
- [ ] Ajustei este arquivo para refletir as alterações que fiz depois da sessão com a IA.

O autor assume a responsabilidade integral pelo conteúdo entregue.
