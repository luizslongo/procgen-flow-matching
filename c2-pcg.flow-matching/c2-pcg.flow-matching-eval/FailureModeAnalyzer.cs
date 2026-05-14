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
}