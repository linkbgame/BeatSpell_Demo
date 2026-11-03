using BeatSpell.Rhythm;
using UnityEngine;

namespace BeatSpell.Animation
{
    /// <summary>
    /// P1 精简：按 BeatClock.BeatFloat 在每拍内播完 N 帧 Idle，无拍点脉冲或 deltaTime 累加。
    /// </summary>
    public class BeatIdleAnimator : MonoBehaviour
    {
        [SerializeField] private BeatClock beatClock;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite[] idleFrames;

        private int currentFrameIndex;

        public int IdleFrameCount => idleFrames != null ? idleFrames.Length : 0;
        public int CurrentFrameIndex => currentFrameIndex;
        public BeatClock Clock => beatClock;

        public void BindClock(BeatClock clock)
        {
            beatClock = clock;
        }

        public void ResetDebugState()
        {
            currentFrameIndex = 0;
            ApplyIdleFrame(0);
        }

        /// <summary>
        /// MCP：按当前时钟采样并刷新帧。
        /// </summary>
        public void SyncFrameFromClock()
        {
            if (beatClock == null || !beatClock.IsRunning)
            {
                return;
            }

            ApplyFrameForBeatFloat(beatClock.BeatFloat);
        }

        /// <summary>
        /// MCP：指定连续拍位置刷新帧（不依赖 Play 时间流逝）。
        /// </summary>
        public void SyncFrameForBeatFloat(float beatFloat)
        {
            ApplyFrameForBeatFloat(beatFloat);
        }

        private void Awake()
        {
            CacheRefs();
            ApplyIdleFrame(0);
        }

        private void OnEnable()
        {
            CacheRefs();
        }

        private void Update()
        {
            CacheRefs();
            if (beatClock == null || !beatClock.IsRunning || IdleFrameCount <= 0)
            {
                return;
            }

            ApplyFrameForBeatFloat(beatClock.BeatFloat);
        }

        private void ApplyFrameForBeatFloat(float beatFloat)
        {
            int n = IdleFrameCount;
            if (n <= 0)
            {
                return;
            }

            float beatFraction = beatFloat - Mathf.Floor(beatFloat);
            if (beatFraction < 0f)
            {
                beatFraction = 0f;
            }

            // 拍内 [0,1) 映射到 0..N-1；新拍起点 beatFraction=0 → 帧 0
            int index = Mathf.Clamp(Mathf.FloorToInt(beatFraction * n), 0, n - 1);
            if (index == currentFrameIndex)
            {
                return;
            }

            currentFrameIndex = index;
            ApplyIdleFrame(index);
        }

        private void ApplyIdleFrame(int index)
        {
            if (spriteRenderer == null || IdleFrameCount == 0)
            {
                return;
            }

            int wrapped = ((index % IdleFrameCount) + IdleFrameCount) % IdleFrameCount;
            Sprite frame = idleFrames[wrapped];
            if (frame != null)
            {
                spriteRenderer.sprite = frame;
            }
        }

        private void CacheRefs()
        {
            if (beatClock == null)
            {
                beatClock = FindFirstObjectByType<BeatClock>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }
    }
}
