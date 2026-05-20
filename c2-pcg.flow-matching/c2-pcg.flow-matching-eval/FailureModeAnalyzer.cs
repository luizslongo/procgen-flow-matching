using System.Collections.Generic;
using c2_pcg.flowMatchingDataloader;

namespace c2_pcg.flowMatchingEval;

// Analyzes a TileMap for constraint violations
public class FailureModeAnalyzer
{
    public static FailureModeAnalysisResult Analyze(TileMap map)
    {
        FailureModeAnalysisResult result = new FailureModeAnalysisResult();
        result.Violations = new List<FailureModeViolation>();

        result.BrokenPipeHorizontalCount = CheckBrokenPipeHorizontal(map, result.Violations);
        result.BrokenPipeTopLeftCount = CheckBrokenPipeTopLeft(map, result.Violations);
        result.BrokenPipeTopRightCount = CheckBrokenPipeTopRight(map, result.Violations);
        result.BrokenBulletBillCount = CheckBrokenBulletBill(map, result.Violations);
        result.FloatingEnemyCount = CheckFloatingEnemy(map, result.Violations);
        result.DiscontinuousGroundCount = CheckDiscontinuousGround(map, result.Violations);

        result.TotalViolations = result.Violations.Count;
        result.TotalTiles = map.Width * map.Height;

        if (result.TotalTiles > 0)
        {
            result.ViolationRate = (double)result.TotalViolations / result.TotalTiles;
        }

        return result;
    }

    private static int CheckBrokenPipeHorizontal(TileMap map, List<FailureModeViolation> violations)
    {
        int count = 0;
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                TileTypeEnum tile = map.Tiles[y * map.Width + x];
                if (tile == TileTypeEnum.PipeBodyLeft)
                {
                    bool hasRightPart = false;
                    if (x + 1 < map.Width)
                    {
                        TileTypeEnum rightTile = map.Tiles[y * map.Width + x + 1];

                        if (rightTile == TileTypeEnum.PipeBodyRight)
                        {
                            hasRightPart = true;
                        }
                    }

                    if (!hasRightPart)
                    {
                        FailureModeViolation violation = new FailureModeViolation();
                        violation.X = x;
                        violation.Y = y;
                        violation.Mode = FailureModeEnum.BrokenPipeHorizontal;
                        violations.Add(violation);
                        count++;
                    }
                }
            }
        }

        return count;
    }

    // PipeTopLeft must have PipeBodyLeft directly below it.
    private static int CheckBrokenPipeTopLeft(TileMap map, List<FailureModeViolation> violations)
    {
        int count = 0;
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                TileTypeEnum tile = map.Tiles[y * map.Width + x];
                if (tile == TileTypeEnum.PipeTopLeft)
                {
                    bool hasBodyBelow = false;
                    if (y + 1 < map.Height)
                    {
                        TileTypeEnum belowTile = map.Tiles[(y + 1) * map.Width + x];
                        if (belowTile == TileTypeEnum.PipeBodyLeft)
                        {
                            hasBodyBelow = true;
                        }
                    }

                    if (!hasBodyBelow)
                    {
                        FailureModeViolation violation = new FailureModeViolation();
                        violation.X = x;
                        violation.Y = y;
                        violation.Mode = FailureModeEnum.BrokenPipeTopLeft;
                        violations.Add(violation);
                        count++;
                    }
                }
            }
        }

        return count;
    }

    // PipeTopRight must have PipeBodyRight directly below it.
    private static int CheckBrokenPipeTopRight(TileMap map, List<FailureModeViolation> violations)
    {
        int count = 0;
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                TileTypeEnum tile = map.Tiles[y * map.Width + x];
                if (tile == TileTypeEnum.PipeTopRight)
                {
                    bool hasBodyBelow = false;
                    if (y + 1 < map.Height)
                    {
                        TileTypeEnum belowTile = map.Tiles[(y + 1) * map.Width + x];
                        if (belowTile == TileTypeEnum.PipeBodyRight)
                        {
                            hasBodyBelow = true;
                        }
                    }

                    if (!hasBodyBelow)
                    {
                        FailureModeViolation violation = new FailureModeViolation();
                        violation.X = x;
                        violation.Y = y;
                        violation.Mode = FailureModeEnum.BrokenPipeTopRight;
                        violations.Add(violation);
                        count++;
                    }
                }
            }
        }

        return count;
    }

    // BulletBillLauncher must have BulletBillBody directly below it.
    private static int CheckBrokenBulletBill(TileMap map, List<FailureModeViolation> violations)
    {
        int count = 0;
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                TileTypeEnum tile = map.Tiles[y * map.Width + x];
                if (tile == TileTypeEnum.BulletBillLauncher)
                {
                    bool hasBodyBelow = false;
                    if (y + 1 < map.Height)
                    {
                        TileTypeEnum belowTile = map.Tiles[(y + 1) * map.Width + x];
                        if (belowTile == TileTypeEnum.BulletBillBody)
                        {
                            hasBodyBelow = true;
                        }
                    }

                    if (!hasBodyBelow)
                    {
                        FailureModeViolation violation = new FailureModeViolation();
                        violation.X = x;
                        violation.Y = y;
                        violation.Mode = FailureModeEnum.BrokenBulletBill;
                        violations.Add(violation);
                        count++;
                    }
                }
            }
        }

        return count;
    }

    // An Enemy must have a supporting tile directly below it, or be on the
    // bottom row. Solid, Breakable, and question blocks count as support.
    private static int CheckFloatingEnemy(TileMap map, List<FailureModeViolation> violations)
    {
        int count = 0;
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                TileTypeEnum tile = map.Tiles[y * map.Width + x];
                if (tile == TileTypeEnum.Enemy)
                {
                    bool hasSupportBelow = false;

                    // An enemy on the bottom row is supported by the ground edge.
                    if (y == map.Height - 1)
                    {
                        hasSupportBelow = true;
                    }

                    if (y + 1 < map.Height)
                    {
                        TileTypeEnum belowTile = map.Tiles[(y + 1) * map.Width + x];
                        if (belowTile == TileTypeEnum.Solid)
                        {
                            hasSupportBelow = true;
                        }
                        if (belowTile == TileTypeEnum.Breakable)
                        {
                            hasSupportBelow = true;
                        }
                        if (belowTile == TileTypeEnum.QuestionFull)
                        {
                            hasSupportBelow = true;
                        }
                        if (belowTile == TileTypeEnum.QuestionEmpty)
                        {
                            hasSupportBelow = true;
                        }
                    }

                    if (!hasSupportBelow)
                    {
                        FailureModeViolation violation = new FailureModeViolation();
                        violation.X = x;
                        violation.Y = y;
                        violation.Mode = FailureModeEnum.FloatingEnemy;
                        violations.Add(violation);
                        count++;
                    }
                }
            }
        }

        return count;
    }

    // Each Empty tile in the bottom row is a gap in the floor.
    private static int CheckDiscontinuousGround(TileMap map, List<FailureModeViolation> violations)
    {
        int count = 0;
        int bottomY = map.Height - 1;
        for (int x = 0; x < map.Width; x++)
        {
            TileTypeEnum tile = map.Tiles[bottomY * map.Width + x];
            if (tile == TileTypeEnum.Empty)
            {
                FailureModeViolation violation = new FailureModeViolation();
                violation.X = x;
                violation.Y = bottomY;
                violation.Mode = FailureModeEnum.DiscontinuousGround;
                violations.Add(violation);
                count++;
            }
        }

        return count;
    }
}