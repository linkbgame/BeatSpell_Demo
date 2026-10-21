using UnityEngine;

namespace BeatSpell.Rhythm
{
    /// <summary>
    /// 全局 BPM 节拍时钟。时间轴使用 AudioSettings.dspTime，不依赖 UI。
    /// 有 AudioClip 时跟随 AudioSource.Play；无 Clip 时可自动起跑。
    /// </summary>
    public class BeatClock : MonoBehaviour
    {
        [SerializeField] private float bpm = 120f;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private bool autoStartWithoutClip = true;
        [SerializeField] private float dspOffsetMs;
        [SerializeField] private bool logBeatTicks;

        private double songStartDspTime;
        private bool isRunning;
        private int lastLoggedBeatIndex = int.MinValue;

        public float Bpm
        {
            get => bpm;
            set => bpm = Mathf.Max(1f, value);
        }

        public float SecondsPerBeat => 60f / Mathf.Max(1f, bpm);

        public bool IsRunning => isRunning;

        public int BeatIndex => GetTimingAt(AudioSettings.dspTime).BeatIndex;

        public int VertexIndex => GetTimingAt(AudioSettings.dspTime).VertexIndex;

        public float BeatFloat => GetTimingAt(AudioSettings.dspTime).BeatFloat;

        public float TrackPhase => GetTimingAt(AudioSettings.dspTime).TrackPhase;

        public double SongStartDspTime => songStartDspTime;

        public AudioSource AudioSource => audioSource;

        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
        }

        private void Start()
        {
            TryStartClock();
        }

        private void Update()
        {
            if (!isRunning)
            {
                return;
            }

            BeatTimingInfo timing = GetTimingAt(AudioSettings.dspTime);

            if (logBeatTicks && timing.BeatIndex != lastLoggedBeatIndex && timing.BeatIndex >= 0)
            {
                lastLoggedBeatIndex = timing.BeatIndex;
                Debug.Log(
                    $"[Beat] Tick | beat={timing.BeatIndex} | beatFloat={timing.BeatFloat:0.###} | trackPhase={timing.TrackPhase:0.###} | vertex={timing.VertexIndex}({BeatTimingInfo.VertexName(timing.VertexIndex)})");
            }
        }

        public void BindAudioSource(AudioSource source)
        {
            audioSource = source;
        }

        public void SetLogBeatTicks(bool enabled)
        {
            logBeatTicks = enabled;
        }

        /// <summary>
        /// 手动启动时钟。有 Clip 则 Play 并采样 dspTime；否则用当前 dspTime。
        /// </summary>
        public void StartClock()
        {
            TryStartClock(force: true);
        }

        public BeatTimingInfo GetTimingAt(double dspNow)
        {
            if (!isRunning)
            {
                return BuildTiming(0f, dspNow, 0f);
            }

            double elapsed = dspNow - songStartDspTime;
            float secondsPerBeat = SecondsPerBeat;
            float beatFloat = (float)(elapsed / secondsPerBeat);
            return BuildTiming(beatFloat, dspNow, secondsPerBeat);
        }

        /// <summary>
        /// 测试用：保留当前拍号，但用注入偏差替代真实 delta。
        /// </summary>
        public BeatTimingInfo GetSimulatedTiming(float offsetMs)
        {
            BeatTimingInfo live = GetTimingAt(AudioSettings.dspTime);
            float secondsPerBeat = SecondsPerBeat;
            int nearestBeat = Mathf.RoundToInt(live.BeatFloat);
            float beatFloat = nearestBeat + offsetMs / (1000f * secondsPerBeat);
            return BuildTiming(beatFloat, live.DspTime, secondsPerBeat);
        }

        /// <summary>
        /// MCP 测试：指定 beatIndex 与相对该拍理论点的偏差（毫秒）。
        /// </summary>
        public BeatTimingInfo GetTimingForBeatIndex(int beatIndex, float offsetMs)
        {
            beatIndex = Mathf.Max(0, beatIndex);
            float secondsPerBeat = SecondsPerBeat;
            float beatFloat = beatIndex + offsetMs / (1000f * secondsPerBeat);
            if (!isRunning)
            {
                return BuildTiming(beatFloat, AudioSettings.dspTime, secondsPerBeat);
            }

            double beatTime = songStartDspTime + beatIndex * (double)secondsPerBeat;
            double dspNow = beatTime + offsetMs * 0.001;
            return BuildTiming(beatFloat, dspNow, secondsPerBeat);
        }

        /// <summary>
        /// MCP：指定连续拍位置（可含 0.5 / 0.25 等非整数拍）。
        /// </summary>
        public BeatTimingInfo GetTimingForBeatFloat(float beatFloat)
        {
            beatFloat = Mathf.Max(0f, beatFloat);
            float secondsPerBeat = SecondsPerBeat;
            if (!isRunning)
            {
                return BuildTiming(beatFloat, AudioSettings.dspTime, secondsPerBeat);
            }

            double dspNow = songStartDspTime + beatFloat * secondsPerBeat;
            return BuildTiming(beatFloat, dspNow, secondsPerBeat);
        }

        private static BeatTimingInfo BuildTiming(float beatFloat, double dspNow, float secondsPerBeat)
        {
            int beatIndex = Mathf.Max(0, Mathf.FloorToInt(beatFloat));
            int nearestBeat = Mathf.RoundToInt(beatFloat);
            float deltaMs = secondsPerBeat > 0f
                ? (beatFloat - nearestBeat) * secondsPerBeat * 1000f
                : 0f;
            int vertexIndex = BeatTimingInfo.PositiveMod(beatIndex, 4);
            float trackPhase = BeatTimingInfo.PositiveModFloat(beatFloat, 4f);
            return new BeatTimingInfo(beatIndex, vertexIndex, deltaMs, dspNow, beatFloat, trackPhase);
        }

        private void TryStartClock(bool force = false)
        {
            if (isRunning && !force)
            {
                return;
            }

            if (audioSource != null && audioSource.clip != null)
            {
                audioSource.Play();
                songStartDspTime = AudioSettings.dspTime + dspOffsetMs * 0.001;
                isRunning = true;
                return;
            }

            if (autoStartWithoutClip || force)
            {
                songStartDspTime = AudioSettings.dspTime + dspOffsetMs * 0.001;
                isRunning = true;
            }
        }
    }
}
