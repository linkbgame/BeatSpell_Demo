using BeatSpell.Rhythm;

namespace BeatSpell.Spell
{
    /// <summary>
    /// 缓冲中的一条咏唱/施法输入。
    /// </summary>
    public readonly struct RhythmInputEntry
    {
        public readonly BeatInputType InputType;
        public readonly BeatTimingGrade Grade;
        public readonly int BeatIndex;
        public readonly int VertexIndex;
        public readonly float BeatFloat;
        public readonly float DeltaMs;
        public readonly float IntervalBeatsFromPrevious;
        public readonly double DspTime;
        public readonly BeatTimingGrade PromptGrade;
        public readonly float PromptAnchorBeatFloat;

        public RhythmInputEntry(
            BeatInputType inputType,
            BeatTimingGrade grade,
            BeatTimingGrade promptGrade,
            int beatIndex,
            int vertexIndex,
            float deltaMs,
            float intervalBeatsFromPrevious,
            double dspTime = 0d,
            float beatFloat = float.NaN,
            float promptAnchorBeatFloat = float.NaN)
        {
            InputType = inputType;
            Grade = grade;
            PromptGrade = promptGrade;
            BeatIndex = beatIndex;
            VertexIndex = vertexIndex;
            DeltaMs = deltaMs;
            IntervalBeatsFromPrevious = intervalBeatsFromPrevious;
            DspTime = dspTime;
            BeatFloat = float.IsNaN(beatFloat) ? beatIndex : beatFloat;
            PromptAnchorBeatFloat = float.IsNaN(promptAnchorBeatFloat) ? BeatFloat : promptAnchorBeatFloat;
        }
    }
}
