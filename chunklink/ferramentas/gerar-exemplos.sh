#!/usr/bin/env bash
# Regenera todas as saídas de exemplos/ a partir do código atual.
#   VGLC=<TheVGLC/Super Mario Bros/Processed> SPRITES=<procgen-flow-matching/sprites> ferramentas/gerar-exemplos.sh
set -euo pipefail
cd "$(dirname "$0")/.."
: "${VGLC:?defina VGLC=<diretório Processed do VGLC>}"
SPRITES="${SPRITES:-}"
dotnet build ChunkLink.sln -nologo -v q > /dev/null
cli() { dotnet run --project src/ChunkLink.Cli --no-build -- "$@"; }
E=exemplos

echo "cenário controlado"
mkdir -p $E/depuracao
cli depurar --trace $E/depuracao/trace.jsonl > $E/depuracao/narrativa.txt

echo "falhas de entrega"
rm -rf $E/falhas; mkdir -p $E/falhas
cli depurar --duplicar 0.3 --semente 7 --trace $E/falhas/duplicacao-30pct-trace.jsonl > $E/falhas/duplicacao-30pct.txt
cli depurar --perder 0.10 --semente 2 --trace $E/falhas/perda-10pct-trace.jsonl > $E/falhas/perda-10pct.txt
{
  echo "Taxa de sucesso do cenário controlado sob falhas de entrega (chunklink depurar, sementes 1–20)"; echo
  for p in 0.05 0.10 0.20; do
    ok=0; for s in $(seq 1 20); do cli depurar --perder $p --semente $s --trace /dev/null > /dev/null 2>&1 && ok=$((ok+1)) || true; done
    echo "perda $p: $ok/20 montaram o nível"
  done
  referencia=$(grep '^sequência' $E/depuracao/narrativa.txt)
  ok=0; for s in $(seq 1 20); do cli depurar --duplicar 0.3 --semente $s --trace /dev/null 2>/dev/null | grep -qF "$referencia" && ok=$((ok+1)) || true; done
  echo "duplicação 0.3: $ok/20 com o mesmo nível da execução sem falhas"
} > $E/falhas/taxas.txt

echo "validação do movimento"
cli validar-movimento --vglc "$VGLC" > $E/validacao-movimento.txt || true

echo "montagens e comparação"
: > $E/comparacao-baselines.txt
for par in "overworld mario-1-1" "underground mario-1-2" "treetop mario-1-3"; do
  set -- $par
  rm -rf $E/montagem-$1; mkdir -p $E/montagem-$1
  cli montar --pool dados/pool/$1/reparado --vglc "$VGLC" --bioma $1 --original $2 ${SPRITES:+--sprites "$SPRITES"} --saida $E/montagem-$1 > $E/montagem-$1/saida.txt || true
  cli comparar --pool dados/pool/$1/reparado --vglc "$VGLC" --bioma $1 --original $2 >> $E/comparacao-baselines.txt; echo >> $E/comparacao-baselines.txt
  for v in "reparado 24 lote-$1" "bruto 24 lote-$1-bruto" "bruto 10 lote-$1-escassez"; do
    set -- $1 $2 $v
    rm -rf $E/$5; mkdir -p $E/$5
    cli lote --pool dados/pool/$1/$3 --vglc "$VGLC" --bioma $1 --original $2 --sementes 10 --tamanho $4 --saida $E/$5 > $E/$5/resumo.txt
  done
done

echo "memória entre execuções"
rm -rf $E/memoria; mkdir -p $E/memoria
for i in 1 2 3; do
  mkdir -p $E/memoria/execucao-$i
  cli montar --pool dados/pool/treetop/bruto --vglc "$VGLC" --bioma treetop --original mario-1-3 --memoria $E/memoria/memoria-demo.db \
    --saida $E/memoria/execucao-$i > $E/memoria/execucao-$i/saida.txt || true
done
echo "pronto"
