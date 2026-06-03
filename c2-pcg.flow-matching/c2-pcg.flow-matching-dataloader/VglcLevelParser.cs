using System;
using System.Collections.Generic;
using System.IO;

namespace c2_pcg.flowMatchingDataloader;

// Parses VGLC text files into TileMap instances.
// Each text file contains one level: rows of characters, each character = one tile.
// All rows in a level have the same length. Mario levels are 14 rows tall.
public class VglcLevelParser
{
    // Parses a single VGLC text file into a TileMap.
    // Returns null if the file has no non-empty lines.
    public static TileMap ParseFile(string filePath)
    {
        string[] lines = File.ReadAllLines(filePath);

        List<string> nonEmptyLines = new List<string>();
        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            if (lines[lineIndex].Length > 0)
            {
                nonEmptyLines.Add(lines[lineIndex]);
            }
        }

        if (nonEmptyLines.Count == 0)
        {
            Console.WriteLine("[WARN] VglcLevelParser.ParseFile: no non-empty lines in " + filePath);
            return null;
        }

        int height = nonEmptyLines.Count;
        int width = nonEmptyLines[0].Length;

        TileMap map = new TileMap();
        map.Width = width;
        map.Height = height;
        map.Tiles = new TileTypeEnum[width * height];
        // Biome derived from the file name stem so chunks inherit it via
        // TileMapChunker. Used as the conditioning input by the
        // biome-conditional UnetBaseline (Experiment D).
        string fileStem = Path.GetFileNameWithoutExtension(filePath);
        map.BiomeLabel = BiomeFromVglcLevelName.BiomeOfLevel(fileStem);

        for (int y = 0; y < height; y++)
        {
            string line = nonEmptyLines[y];
            for (int x = 0; x < width; x++)
            {
                char c = '-';
                if (x < line.Length)
                {
                    c = line[x];
                }
                TileTypeEnum tile = VglcTileCharMap.CharToTileType(c);
                map.Tiles[y * width + x] = tile;
            }
        }

        return map;
    }

    // Parses all VGLC text files in a directory.
    public static List<TileMap> ParseDirectory(string directoryPath)
    {
        string[] files = Directory.GetFiles(directoryPath, "*.txt");
        Array.Sort(files);

        List<TileMap> maps = new List<TileMap>();
        for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
        {
            TileMap map = ParseFile(files[fileIndex]);
            if (map != null)
            {
                maps.Add(map);
                Console.WriteLine("[INFO] VglcLevelParser: loaded "
                    + Path.GetFileName(files[fileIndex])
                    + " (" + map.Width + "x" + map.Height + ")");
            }
        }

        Console.WriteLine("[INFO] VglcLevelParser: loaded " + maps.Count
            + " levels from " + directoryPath);
        return maps;
    }
}
