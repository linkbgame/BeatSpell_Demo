namespace BeatSpell.Rhythm
{
    /// <summary>
    /// 相对最近理论节拍点的判定档位。
    /// </summary>
    public enum BeatTimingGrade
    {
        Perfect = 0,
        Good = 1,
        /// <summary>弱命中；不打断咏唱。Miss 为兼容别名。</summary>
        Weak = 2,
        Miss = Weak
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
    /// BeatFloat / TrackPhase 供半拍、1/4 拍配方与队首生命周期；VertexIndex 仍供四顶点白点 UI。
    /// </summary>
    public readonly struct BeatTimingInfo
    {
        public readonly int BeatIndex;
        public readonly int VertexIndex;
        public readonly float BeatFloat;
        public readonly float TrackPhase;
        public readonly float DeltaMs;
        public readonly double DspTime;

        public BeatTimingInfo(
            int beatIndex,
            int vertexIndex,
            float deltaMs,
            double dspTime,
            float beatFloat = float.NaN,
            float trackPhase = float.NaN)
        {
            BeatIndex = beatIndex;
            VertexIndex = vertexIndex;
            DeltaMs = deltaMs;
            DspTime = dspTime;
            BeatFloat = float.IsNaN(beatFloat) ? beatIndex : beatFloat;
            TrackPhase = float.IsNaN(trackPhase) ? PositiveModFloat(BeatFloat, 4f) : trackPhase;
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

        public static float PositiveModFloat(float value, float modulo)
        {
            float remainder = value % modulo;
            return remainder < 0f ? remainder + modulo : remainder;
        }
    }
}
