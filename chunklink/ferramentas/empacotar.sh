#!/usr/bin/env bash
# Empacota a entrega (DSM3) em um zip autocontido.
#
# Copia o projeto (sem bin/, obj/, saida/) para uma área temporária, inclui em tcc-vendor/ um
# snapshot dos fontes do TCC que o ChunkLink compila por link (dataloader e eval, sem o conversor
# de tensores que depende de TorchSharp) e gera o zip. A pasta de trabalho não é alterada.
#
# Uso: ferramentas/empacotar.sh [caminho-do-procgen-flow-matching] [arquivo-zip]
set -euo pipefail

raiz="$(cd "$(dirname "$0")/.." && pwd)"
# TCC: argumento; a pasta pai (ChunkLink dentro do repositório do TCC); ou o clone irmão.
if [[ -n "${1:-}" ]]; then tcc_padrao="$1"
elif [[ -d "$raiz/../c2-pcg.flow-matching" ]]; then tcc_padrao="$raiz/.."
else tcc_padrao="$raiz/../procgen-flow-matching"; fi
tcc="$(cd "$tcc_padrao" && pwd)"
zip_saida="$(realpath -m "${2:-$raiz/../DSM3_ChunkLink_LuizSlongo.zip}")"
nome="ChunkLink"

area="$(mktemp -d)"
trap 'rm -rf "$area"' EXIT
destino="$area/$nome"

tar -C "$raiz" \
    --exclude='./saida' --exclude='bin' --exclude='obj' --exclude='*.zip' --exclude='./tcc-vendor' \
    -cf - . | (mkdir -p "$destino" && tar -C "$destino" -xf -)

origem_dl="$tcc/c2-pcg.flow-matching/c2-pcg.flow-matching-dataloader"
origem_ev="$tcc/c2-pcg.flow-matching/c2-pcg.flow-matching-eval"
vendor_dl="$destino/tcc-vendor/c2-pcg.flow-matching/c2-pcg.flow-matching-dataloader"
vendor_ev="$destino/tcc-vendor/c2-pcg.flow-matching/c2-pcg.flow-matching-eval"
mkdir -p "$vendor_dl" "$vendor_ev"
for f in "$origem_dl"/*.cs; do
    [[ "$(basename "$f")" == "TileMapTensorConverter.cs" ]] || cp "$f" "$vendor_dl/"
done
cp "$origem_ev"/*.cs "$vendor_ev/"

commit="$(git -C "$tcc" log -1 --format='%H (%ad)' --date=short 2>/dev/null || echo 'desconhecido')"
cat > "$destino/tcc-vendor/ORIGEM.md" <<EOF
# Snapshot do código do TCC

Fontes C# de https://github.com/luizslongo/procgen-flow-matching (mesmo autor), commit $commit.
Copiados por \`ferramentas/empacotar.sh\` apenas para que o pacote compile sem o clone do repositório.
Não inclui modelo treinado, *sprites* nem níveis do VGLC.

- \`c2-pcg.flow-matching-dataloader/*.cs\`, exceto \`TileMapTensorConverter.cs\` (depende de TorchSharp)
- \`c2-pcg.flow-matching-eval/*.cs\`
EOF

rm -f "$zip_saida"
(cd "$area" && zip -qr "$zip_saida" "$nome")
echo "pacote: $zip_saida ($(du -h "$zip_saida" | cut -f1)) · TCC: $commit"
echo "arquivos do TCC: $(ls "$vendor_dl" "$vendor_ev" | grep -c '\.cs$')"
