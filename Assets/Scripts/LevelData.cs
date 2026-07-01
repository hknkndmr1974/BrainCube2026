using System;

[Serializable]
public class LevelData
{
    public int worldIndex;
    public int levelIndex;
    public int minMoves;
    public string hintMoves; // Yan yana "R R B R" şeklinde hamleler
    public string[] layout; // Her eleman haritanın bir satırını temsil eder
}
