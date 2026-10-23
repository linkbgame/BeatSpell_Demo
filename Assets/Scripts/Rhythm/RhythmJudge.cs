using System;
using UnityEngine;

namespace BeatSpell.Rhythm
{
    /// <summary>
    /// 准确度判定：根据与最近节拍点的毫秒偏差输出 Perfect / Good / Weak。
    /// Weak 仍可入队，不打断咏唱。
    /// </summary>
    [Serializable]
    public class RhythmJudge
    {
        [SerializeField] private float perfectWindowMs = 50f;
        [SerializeField] private float goodWindowMs = 120f;

        public float PerfectWindowMs
        {
            get => perfectWindowMs;
            set => perfectWindowMs = Mathf.Max(0f, value);
        }

        public float GoodWindowMs
        {
            get => goodWindowMs;
            set => goodWindowMs = Mathf.Max(perfectWindowMs, value);
        }

        public BeatTimingGrade Evaluate(float deltaMs)
        {
            float absMs = Mathf.Abs(deltaMs);
            if (absMs <= perfectWindowMs)
            {
                return BeatTimingGrade.Perfect;
            }

            if (absMs <= goodWindowMs)
            {
                return BeatTimingGrade.Good;
            }

            return BeatTimingGrade.Weak;
        }

        /// <summary>
        /// UI 提示用：把输入投到最近的 0.5 拍锚点（4 顶点 + 4 中点）后判定。
        /// 核心节拍日志仍可保留整拍判定；这里只决定 Perfect 提示是否吸附到 8 点。
        /// </summary>
        public BeatTimingGrade EvaluateHalfBeatPrompt(
            float beatFloat,
            float secondsPerBeat,
            out float anchorBeatFloat,
            out float deltaMs)
        {
            anchorBeatFloat = Mathf.Floor(beatFloat * 2f + 0.5f) * 0.5f;
            anchorBeatFloat = Mathf.Max(0f, anchorBeatFloat);
            deltaMs = secondsPerBeat > 0f
                ? (beatFloat - anchorBeatFloat) * secondsPerBeat * 1000f
                : 0f;
            return Evaluate(deltaMs);
        }
    }
}
