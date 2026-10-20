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
                    $"[Beat] Tick | beat={timing.BeatIndex} | vertex={timing.VertexIndex}({BeatTimingInfo.VertexName(timing.VertexIndex)})");
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
                return new BeatTimingInfo(0, 0, 0f, dspNow);
            }

            double elapsed = dspNow - songStartDspTime;
            float secondsPerBeat = SecondsPerBeat;
            double beatFloat = elapsed / secondsPerBeat;
            int beatIndex = Mathf.FloorToInt((float)beatFloat);
            int nearestBeat = Mathf.RoundToInt((float)beatFloat);
            double nearestBeatTime = songStartDspTime + nearestBeat * (double)secondsPerBeat;
            float deltaMs = (float)((dspNow - nearestBeatTime) * 1000.0);
            int vertexIndex = BeatTimingInfo.PositiveMod(Mathf.Max(beatIndex, 0), 4);
            return new BeatTimingInfo(Mathf.Max(beatIndex, 0), vertexIndex, deltaMs, dspNow);
        }

        /// <summary>
        /// 测试用：保留当前拍号，但用注入偏差替代真实 delta。
        /// </summary>
        public BeatTimingInfo GetSimulatedTiming(float offsetMs)
        {
            BeatTimingInfo live = GetTimingAt(AudioSettings.dspTime);
            return new BeatTimingInfo(live.BeatIndex, live.VertexIndex, offsetMs, live.DspTime);
        }

        /// <summary>
        /// MCP 测试：指定 beatIndex 与相对该拍理论点的偏差（毫秒）。
        /// </summary>
        public BeatTimingInfo GetTimingForBeatIndex(int beatIndex, float offsetMs)
        {
            beatIndex = Mathf.Max(0, beatIndex);
            int vertexIndex = BeatTimingInfo.PositiveMod(beatIndex, 4);
            if (!isRunning)
            {
                return new BeatTimingInfo(beatIndex, vertexIndex, offsetMs, AudioSettings.dspTime);
            }

            double beatTime = songStartDspTime + beatIndex * (double)SecondsPerBeat;
            double dspNow = beatTime + offsetMs * 0.001;
            return new BeatTimingInfo(beatIndex, vertexIndex, offsetMs, dspNow);
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
