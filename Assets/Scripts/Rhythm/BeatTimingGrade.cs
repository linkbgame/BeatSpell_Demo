namespace BeatSpell.Rhythm
{
    /// <summary>
    /// 相对最近理论节拍点的判定档位。
    /// </summary>
    public enum BeatTimingGrade
    {
        Perfect = 0,
        Good = 1,
        Miss = 2
    }

    /// <summary>
    /// 玩家节拍输入类型：咏唱或施法。
    /// </summary>
    public enum BeatInputType
    {
        Chant = 0,
        Cast = 1
    }

    /// <summary>
    /// 某次 dsp 时刻相对 BPM 时间轴的采样结果。
    /// </summary>
    public readonly struct BeatTimingInfo
    {
        public readonly int BeatIndex;
        public readonly int VertexIndex;
        public readonly float DeltaMs;
        public readonly double DspTime;

        public BeatTimingInfo(int beatIndex, int vertexIndex, float deltaMs, double dspTime)
        {
            BeatIndex = beatIndex;
            VertexIndex = vertexIndex;
            DeltaMs = deltaMs;
            DspTime = dspTime;
        }

        public static string VertexName(int vertexIndex)
        {
            switch (PositiveMod(vertexIndex, 4))
            {
                case 0: return "Top";
                case 1: return "Right";
                case 2: return "Bottom";
                default: return "Left";
            }
        }

        public static int PositiveMod(int value, int modulo)
        {
            int remainder = value % modulo;
            return remainder < 0 ? remainder + modulo : remainder;
        }
    }
}
