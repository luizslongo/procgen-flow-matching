namespace c2_pcg.flowMatchingDataloader;

// Tile types from the Video Game Level Corpus (VGLC) Super Mario Bros encoding.
// Each value maps to one character in the VGLC text file format.
// Value 0 is Error (uninitialized state) per standard-enum-zero-is-error.txt.
public enum TileTypeEnum
{
    Error = 0,          // uninitialized / invalid state
    Empty,              // '-' air/sky
    Solid,              // 'X' solid ground block
    Breakable,          // 'S' breakable brick
    QuestionFull,       // '?' question block with item
    QuestionEmpty,      // 'Q' already-hit question block
    Enemy,              // 'E' enemy (Goomba)
    PipeTopLeft,        // '<' pipe entrance left
    PipeTopRight,       // '>' pipe entrance right
    PipeBodyLeft,       // '[' pipe body left
    PipeBodyRight,      // ']' pipe body right
    Coin,               // 'o' collectible coin
    BulletBillLauncher, // 'B' bullet bill cannon
    BulletBillBody,     // 'b' bullet bill column
}
