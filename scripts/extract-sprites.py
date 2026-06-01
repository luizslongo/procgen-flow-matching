#!/usr/bin/env python3
"""Extract VGLC Super Mario Bros tile sprites from level screenshots.

Reads pairs of .txt level files (Processed/) and .png level images (Original/)
from a TheVGLC Super Mario Bros directory and produces one 16x16 PNG sprite
per tile type. Output filenames match the TileType enum names defined in
c2-pcg.flow-matching-dataloader/TileTypeEnum.cs (e.g., Solid.png, Coin.png).

VGLC convention: each .txt file has 14 rows of ASCII characters; each .png is
208 pixels tall = 13 rows of 16-pixel tiles. The top row of the .txt is sky
padding that exists above the visible screen and has no .png equivalent.
Therefore: txt_row (0-indexed) maps to png_row (0-indexed) via
    png_row = txt_row - 1
The first occurrence of each tile type (iterating levels in lexicographic
order, top-left to bottom-right within a level) is the one extracted.

Usage:
    python extract-sprites.py <vglc-mario-root> <output-sprites-dir>

Example:
    python extract-sprites.py /workspace/TCC/TheVGLC/Super\\ Mario\\ Bros sprites
"""
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    print("Pillow is required. Install with: pip install pillow")
    sys.exit(1)


# Maps VGLC ASCII character to TileType enum value name.
# Must stay in sync with VglcTileCharMap.CharToTileType in the C# code.
CHAR_TO_TILE = {
    '-': 'Empty',
    'X': 'Solid',
    'S': 'Breakable',
    '?': 'QuestionFull',
    'Q': 'QuestionEmpty',
    'E': 'Enemy',
    '<': 'PipeTopLeft',
    '>': 'PipeTopRight',
    '[': 'PipeBodyLeft',
    ']': 'PipeBodyRight',
    'o': 'Coin',
    'B': 'BulletBillLauncher',
    'b': 'BulletBillBody',
}

TILE_PIXELS = 16

# VGLC vertical alignment: txt has one more row at the top than the png.
TXT_TO_PNG_ROW_OFFSET = -1


def crop_tile(png, txt_row, txt_col):
    """Crops a 16x16 sprite at the position implied by a (txt_row, txt_col).

    Returns None if the implied PNG position falls outside the image.
    """
    png_row = txt_row + TXT_TO_PNG_ROW_OFFSET
    if png_row < 0:
        return None
    width, height = png.size
    if (png_row + 1) * TILE_PIXELS > height:
        return None
    if (txt_col + 1) * TILE_PIXELS > width:
        return None
    left = txt_col * TILE_PIXELS
    upper = png_row * TILE_PIXELS
    return png.crop((left, upper, left + TILE_PIXELS, upper + TILE_PIXELS))


def scan_level(txt_path, png_path, found):
    """Updates `found` (tile_name -> sprite Image) with new tiles from this level.

    Iterates rows BOTTOM-UP so that the first occurrence of each tile type is
    taken from the lowest row where it appears. This matters because:
      - Solid (X): the bottom row is the canonical "ground floor" tile,
        whereas upper occurrences are staircase corners or building roofs
        that visually contain partial tile edges from the screenshot.
      - Enemy (E), Coin (o), Pipe parts: their canonical visual representation
        sits near the bottom of the playable area, where they are
        grid-aligned with the tile background.
      - Empty (-): the bottom playable row of the txt is also mostly '-'
        near the start of a level, so iterating bottom-up does not lose
        access to clean sky tiles.
    """
    with open(txt_path, 'r', encoding='utf-8') as f:
        rows = [line.rstrip('\r\n') for line in f]
    png = Image.open(png_path).convert('RGBA')
    for txt_row in range(len(rows) - 1, -1, -1):
        row_str = rows[txt_row]
        for txt_col, ch in enumerate(row_str):
            tile_name = CHAR_TO_TILE.get(ch)
            if tile_name is None or tile_name in found:
                continue
            sprite = crop_tile(png, txt_row, txt_col)
            if sprite is None:
                continue
            found[tile_name] = sprite
            print(f"  found {ch!r} -> {tile_name} ({txt_path.stem} row={txt_row} col={txt_col})")


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        sys.exit(1)

    mario_root = Path(sys.argv[1])
    out_dir = Path(sys.argv[2])

    processed_dir = mario_root / 'Processed'
    original_dir = mario_root / 'Original'
    if not processed_dir.is_dir() or not original_dir.is_dir():
        print(f"Expected {processed_dir} and {original_dir} to exist.")
        sys.exit(1)

    out_dir.mkdir(parents=True, exist_ok=True)

    found = {}
    for txt_path in sorted(processed_dir.glob('*.txt')):
        png_path = original_dir / (txt_path.stem + '.png')
        if not png_path.is_file():
            print(f"  (no PNG for {txt_path.name}, skipping)")
            continue
        print(f"Scanning {txt_path.name}")
        scan_level(txt_path, png_path, found)
        if len(found) == len(CHAR_TO_TILE):
            print("All tile types found.")
            break

    print()
    print(f"Saving {len(found)} sprites to {out_dir}/")
    for tile_name, sprite in found.items():
        out_path = out_dir / f"{tile_name}.png"
        sprite.save(out_path)
        print(f"  {out_path}")

    missing = set(CHAR_TO_TILE.values()) - set(found.keys())
    if missing:
        print()
        print(f"WARNING: no sprite found for: {', '.join(sorted(missing))}")
        sys.exit(2)

    print()
    print(f"Done: {len(found)} / {len(CHAR_TO_TILE)} tile types extracted.")


if __name__ == '__main__':
    main()
