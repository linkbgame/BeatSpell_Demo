using BeatSpell.Rhythm;
using UnityEngine;

namespace BeatSpell.Animation
{
    /// <summary>
    /// P1：Idle 帧循环 + 每个整数拍一次短促颤抖。只读 BeatClock，不读取音轨 UI。
    /// 脉冲只作用在 visualRoot，避免带动逻辑根节点。
    /// </summary>
    public class BeatIdleAnimator : MonoBehaviour
    {
        [SerializeField] private BeatClock beatClock;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Sprite[] idleFrames;
        [SerializeField] private float idleFrameRate = 8f;
        [SerializeField] private float pulseDurationBeats = 0.2f;
        [SerializeField] private float pulseYOffset = 0.08f;
        [SerializeField] private float pulseScaleY = 1.06f;
        [SerializeField] private bool pulseOnEnable;
        [SerializeField] private bool logBeatPulse;
        [SerializeField] private AnimationCurve pulseCurve = new AnimationCurve(
            new Keyframe(0f, 1f, 0f, -5f),
            new Keyframe(1f, 0f, 0f, 0f));

        private Vector3 restLocalPosition;
        private Vector3 restLocalScale;
        private int lastBeatIndex = int.MinValue;
        private int currentFrameIndex;
        private float idleElapsed;
        private float pulseElapsed;
        private float pulseDurationSeconds;
        private bool pulsePlaying;
        private bool restPoseCached;

        public int IdleFrameCount => idleFrames != null ? idleFrames.Length : 0;
        public int CurrentFrameIndex => currentFrameIndex;
        public int BeatPulseCount { get; private set; }
        public int LastPulseBeatIndex { get; private set; } = -1;
        public bool IsPulsePlaying => pulsePlaying;
        public float CurrentPulseOffsetY { get; private set; }
        public float CurrentPulseScaleY { get; private set; } = 1f;
        public float PulseDurationSeconds => pulseDurationSeconds;
        public BeatClock Clock => beatClock;

        public void BindClock(BeatClock clock)
        {
            beatClock = clock;
        }

        public void ResetDebugState()
        {
            lastBeatIndex = int.MinValue;
            BeatPulseCount = 0;
            LastPulseBeatIndex = -1;
            currentFrameIndex = 0;
            idleElapsed = 0f;
            StopPulse(applyRest: true);
            ApplyIdleFrame(0);
        }

        /// <summary>
        /// MCP：只推进本组件的 Idle 帧与脉冲衰减，不改 BeatClock。
        /// </summary>
        public void TickForTest(float seconds)
        {
            TickVisual(Mathf.Max(0f, seconds));
        }

        /// <summary>
        /// MCP：按给定整数拍评估是否触发脉冲。首次采样只记录拍号，不颤抖。
        /// </summary>
        public void EvaluateBeatIndex(int beatIndex)
        {
            if (beatIndex < 0)
            {
                return;
            }

            if (lastBeatIndex == int.MinValue)
            {
                lastBeatIndex = beatIndex;
                if (pulseOnEnable)
                {
                    StartPulse(beatIndex);
                }

                return;
            }

            if (beatIndex == lastBeatIndex)
            {
                return;
            }

            lastBeatIndex = beatIndex;
            StartPulse(beatIndex);
        }

        private void Awake()
        {
            CacheRefs();
            CacheRestPose();
            ApplyIdleFrame(0);
        }

        private void OnEnable()
        {
            CacheRefs();
            CacheRestPose();
        }

        private void Update()
        {
            CacheRefs();
            if (beatClock != null && beatClock.IsRunning)
            {
                EvaluateBeatIndex(beatClock.BeatIndex);
            }

            TickVisual(Time.deltaTime);
        }

        private void TickVisual(float seconds)
        {
            if (seconds <= 0f)
            {
                return;
            }

            TickIdleFrames(seconds);
            TickPulse(seconds);
        }

        private void TickIdleFrames(float seconds)
        {
            if (IdleFrameCount <= 1)
            {
                return;
            }

            float fps = Mathf.Max(1f, idleFrameRate);
            idleElapsed += seconds;
            float frameDuration = 1f / fps;
            while (idleElapsed >= frameDuration)
            {
                idleElapsed -= frameDuration;
                currentFrameIndex = (currentFrameIndex + 1) % IdleFrameCount;
                ApplyIdleFrame(currentFrameIndex);
            }
        }

        private void TickPulse(float seconds)
        {
            if (!pulsePlaying)
            {
                return;
            }

            pulseElapsed += seconds;
            if (pulseDurationSeconds <= 0f || pulseElapsed >= pulseDurationSeconds)
            {
                StopPulse(applyRest: true);
                return;
            }

            ApplyPulse(EvaluatePulseAmount(pulseElapsed / pulseDurationSeconds));
        }

        private void StartPulse(int beatIndex)
        {
            CacheRestPose();
            float secondsPerBeat = beatClock != null ? beatClock.SecondsPerBeat : 0.5f;
            pulseDurationSeconds = Mathf.Max(0.01f, pulseDurationBeats) * Mathf.Max(0.01f, secondsPerBeat);
            pulseElapsed = 0f;
            pulsePlaying = true;
            BeatPulseCount++;
            LastPulseBeatIndex = beatIndex;
            ApplyPulse(EvaluatePulseAmount(0f));

            if (logBeatPulse)
            {
                Debug.Log($"[Anim] IdlePulse | beat={beatIndex} | count={BeatPulseCount} | life={pulseDurationSeconds:0.###}s");
            }
        }

        private void StopPulse(bool applyRest)
        {
            pulsePlaying = false;
            pulseElapsed = 0f;
            CurrentPulseOffsetY = 0f;
            CurrentPulseScaleY = 1f;
            if (applyRest)
            {
                ApplyPulse(0f);
            }
        }

        private float EvaluatePulseAmount(float normalized)
        {
            float t = Mathf.Clamp01(normalized);
            if (pulseCurve != null && pulseCurve.length > 0)
            {
                return Mathf.Clamp01(pulseCurve.Evaluate(t));
            }

            return 1f - t;
        }

        private void ApplyPulse(float amount)
        {
            CacheRestPose();
            CurrentPulseOffsetY = pulseYOffset * amount;
            CurrentPulseScaleY = Mathf.Lerp(1f, pulseScaleY, amount);
            if (visualRoot == null)
            {
                return;
            }

            Vector3 position = restLocalPosition;
            position.y += CurrentPulseOffsetY;
            visualRoot.localPosition = position;

            Vector3 scale = restLocalScale;
            scale.y = restLocalScale.y * CurrentPulseScaleY;
            visualRoot.localScale = scale;
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

        private void CacheRestPose()
        {
            if (visualRoot == null || restPoseCached)
            {
                return;
            }

            restLocalPosition = visualRoot.localPosition;
            restLocalScale = visualRoot.localScale;
            if (restLocalScale.y == 0f)
            {
                restLocalScale.y = 1f;
            }

            restPoseCached = true;
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

            if (visualRoot == null && spriteRenderer != null)
            {
                visualRoot = spriteRenderer.transform;
            }
        }
    }
}
