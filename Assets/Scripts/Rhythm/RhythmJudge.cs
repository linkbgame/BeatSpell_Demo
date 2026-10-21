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
    }
}
