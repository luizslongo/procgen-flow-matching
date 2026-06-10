using c2_pcg.flowMatchingDataloader;

namespace c2_pcg.flowMatchingEval;

// Counts complete structural elements in a TileMap and provides safe
// tile accessors used by repair classes.
// Action class: pure functions, no state.
//
// A complete pipe is a 2-tile-wide column with PipeTopLeft and
// PipeTopRight on the top row and at least one PipeBodyLeft and
// PipeBodyRight pair below.
//
// A complete bullet bill is a 1-tile-wide column with BulletBillLauncher
// on top and at least one BulletBillBody below.
public class StructureCounter
{
    // Counts the number of complete pipe structures in the chunk.
    // The minimum complete pipe is 2 tiles wide by 2 tiles tall.
    // Pipes taller than the minimum still count exactly once per top.
    public static int CountCompletePipes(TileMap chunk)
    {
        int count = 0;
        for (int y = 0; y < chunk.Height; y++)
        {
            for (int x = 0; x < chunk.Width; x++)
            {
                bool topLeft = GetTile(chunk, x, y) == TileTypeEnum.PipeTopLeft;
                bool topRight = GetTile(chunk, x + 1, y) == TileTypeEnum.PipeTopRight;
                bool bodyLeft = GetTile(chunk, x, y + 1) == TileTypeEnum.PipeBodyLeft;
                bool bodyRight = GetTile(chunk, x + 1, y + 1) == TileTypeEnum.PipeBodyRight;
                if (topLeft && topRight && bodyLeft && bodyRight)
                {
                    count++;
                }
            }
        }
        return count;
    }

    // Counts the number of complete bullet bill structures in the chunk.
    // The minimum complete bullet bill is 1 tile wide by 2 tiles tall.
    public static int CountCompleteBulletBills(TileMap chunk)
    {
        int count = 0;
        for (int y = 0; y < chunk.Height; y++)
        {
            for (int x = 0; x < chunk.Width; x++)
            {
                bool launcher = GetTile(chunk, x, y) == TileTypeEnum.BulletBillLauncher;
                bool body = GetTile(chunk, x, y + 1) == TileTypeEnum.BulletBillBody;
                if (launcher && body)
                {
                    count++;
                }
            }
        }
        return count;
    }

    // Safe tile getter. Returns TileTypeEnum.Error for out-of-bounds
    // positions so callers can treat off-map cells as a sentinel value
    // without bounds-checking at every neighbor lookup.
    public static TileTypeEnum GetTile(TileMap chunk, int x, int y)
    {
        if (x < 0 || x >= chunk.Width)
        {
            return TileTypeEnum.Error;
        }
        if (y < 0 || y >= chunk.Height)
        {
            return TileTypeEnum.Error;
        }
        return chunk.Tiles[y * chunk.Width + x];
    }

    // Safe tile setter. Silently ignores out-of-bounds writes.
    // Repair logic relies on this to avoid bounds checks on every write.
    public static void SetTile(TileMap chunk, int x, int y, TileTypeEnum tile)
    {
        if (x < 0 || x >= chunk.Width)
        {
            return;
        }
        if (y < 0 || y >= chunk.Height)
        {
            return;
        }
        chunk.Tiles[y * chunk.Width + x] = tile;
    }

    // Returns true if the tile is part of a pipe structure.
    public static bool IsPipeTile(TileTypeEnum tile)
    {
        if (tile == TileTypeEnum.PipeTopLeft) return true;
        if (tile == TileTypeEnum.PipeTopRight) return true;
        if (tile == TileTypeEnum.PipeBodyLeft) return true;
        if (tile == TileTypeEnum.PipeBodyRight) return true;
        return false;
    }

    // Returns true if the tile is part of a bullet bill structure.
    public static bool IsBulletBillTile(TileTypeEnum tile)
    {
        if (tile == TileTypeEnum.BulletBillLauncher) return true;
        if (tile == TileTypeEnum.BulletBillBody) return true;
        return false;
    }

    // Returns the appropriate replacement tile when a loose structural
    // piece is removed. Near the bottom of the chunk (within 3 rows of
    // ground) the replacement is Breakable, matching the canonical
    // jump-height brick pattern in real SMB levels. Higher positions
    // become Empty (sky).
    public static TileTypeEnum SelectReplacementTile(int y, int chunkHeight)
    {
        if (y >= chunkHeight - 3)
        {
            return TileTypeEnum.Breakable;
        }
        return TileTypeEnum.Empty;
    }
}
