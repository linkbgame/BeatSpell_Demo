namespace BeatSpell.Spell
{
    /// <summary>
    /// 菱形谱面顶点，与 BeatClock vertexIndex 0=上→1=右→2=下→3=左 对齐。第 4 轮 UI 使用。
    /// </summary>
    public enum SpellRecipeVertex
    {
        Top = 0,
        Right = 1,
        Bottom = 2,
        Left = 3
    }
}
