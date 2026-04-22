namespace c2_pcg.flowMatchingDataloader;

// Maps VGLC text file characters to TileTypeEnum values and back.
// The VGLC uses single ASCII characters to represent each tile type.
public class VglcTileCharMap
{
    public const int TileTypeCount = 14; // 13 real tile types + Error=0

    public static TileTypeEnum CharToTileType(char c)
    {
        if (c == '-')
        {
            return TileTypeEnum.Empty;
        }
        if (c == 'X')
        {
            return TileTypeEnum.Solid;
        }
        if (c == 'S')
        {
            return TileTypeEnum.Breakable;
        }
        if (c == '?')
        {
            return TileTypeEnum.QuestionFull;
        }
        if (c == 'Q')
        {
            return TileTypeEnum.QuestionEmpty;
        }
        if (c == 'E')
        {
            return TileTypeEnum.Enemy;
        }
        if (c == '<')
        {
            return TileTypeEnum.PipeTopLeft;
        }
        if (c == '>')
        {
            return TileTypeEnum.PipeTopRight;
        }
        if (c == '[')
        {
            return TileTypeEnum.PipeBodyLeft;
        }
        if (c == ']')
        {
            return TileTypeEnum.PipeBodyRight;
        }
        if (c == 'o')
        {
            return TileTypeEnum.Coin;
        }
        if (c == 'B')
        {
            return TileTypeEnum.BulletBillLauncher;
        }
        if (c == 'b')
        {
            return TileTypeEnum.BulletBillBody;
        }

        return TileTypeEnum.Empty;
    }

    public static char TileTypeToChar(TileTypeEnum tile)
    {
        if (tile == TileTypeEnum.Empty)
        {
            return '-';
        }
        if (tile == TileTypeEnum.Solid)
        {
            return 'X';
        }
        if (tile == TileTypeEnum.Breakable)
        {
            return 'S';
        }
        if (tile == TileTypeEnum.QuestionFull)
        {
            return '?';
        }
        if (tile == TileTypeEnum.QuestionEmpty)
        {
            return 'Q';
        }
        if (tile == TileTypeEnum.Enemy)
        {
            return 'E';
        }
        if (tile == TileTypeEnum.PipeTopLeft)
        {
            return '<';
        }
        if (tile == TileTypeEnum.PipeTopRight)
        {
            return '>';
        }
        if (tile == TileTypeEnum.PipeBodyLeft)
        {
            return '[';
        }
        if (tile == TileTypeEnum.PipeBodyRight)
        {
            return ']';
        }
        if (tile == TileTypeEnum.Coin)
        {
            return 'o';
        }
        if (tile == TileTypeEnum.BulletBillLauncher)
        {
            return 'B';
        }
        if (tile == TileTypeEnum.BulletBillBody)
        {
            return 'b';
        }

        return '-';
    }
}
