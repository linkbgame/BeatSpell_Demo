using System.Collections.Generic;
using BeatSpell.Prototype;
using BeatSpell.Rhythm;
using BeatSpell.Spell;
using UnityEngine;
using UnityEngine.UI;

namespace BeatSpell.UI
{
    /// <summary>
    /// 第 4 轮：按输入时刻在菱形轨迹上连续留痕。不吸附四顶点。
    /// 队列 Marker 走队首一圈生命周期；空施法黑色痕为短效 Marker，寿命等于封锁时长。
    /// </summary>
    public class BeatInputMarkerView : MonoBehaviour
    {
        private struct TransientMarker
        {
            public Image image;
            public double expireDspTime;
        }

        private struct PerfectRingVisual
        {
            public RectTransform rect;
            public Image image;
            public float spawnBeatFloat;
        }

        [SerializeField] private RhythmPrototypeBootstrap bootstrap;
        [SerializeField] private DiamondBeatTrackView trackView;
        [SerializeField] private RectTransform markerRoot;
        [SerializeField] private Sprite chantSprite;
        [SerializeField] private Sprite castSprite;
        [SerializeField] private Sprite perfectRingSprite;
        [Tooltip("圆环直径相对 Marker 的倍数，需略大于咏唱/施法点以包在外圈。")]
        [SerializeField] private float perfectRingScale = 1.22f;
        [Tooltip("每拍旋转角度；360 表示与节拍同相转一圈。")]
        [SerializeField] private float rotationDegreesPerBeat = 360f;
        [Tooltip("每拍颜色循环次数；2 表示半拍切一段色。")]
        [SerializeField] private float colorCyclesPerBeat = 2f;
        [Tooltip("咏唱/施法帧动画参考帧率；1f = 1 / animationFrameRate 秒。")]
        [SerializeField] private float animationFrameRate = 30f;

        private const float QueueLifecycleBeats = 4f;
        private const float PerfectRingAlpha = 0.25f;
        private static readonly float[] ChantScales = { 1.5f, 2f, 1.3f, 1f };
        private static readonly float[] CastScalesA = { 1.5f, 2f, 1.3f, 1f };
        private static readonly float[] CastScalesB = { 1.5f, 1.3f, 1f };

        // 浅色闪烁色板：用户给定 + 同调补充，运行时 Alpha 固定 25%。
        private static readonly Color[] PerfectRingPalette =
        {
            new Color(0xBB / 255f, 0x9C / 255f, 0x99 / 255f, PerfectRingAlpha),
            new Color(0xEA / 255f, 0xC5 / 255f, 0xEA / 255f, PerfectRingAlpha),
            new Color(0xD1 / 255f, 0xAF / 255f, 0xA9 / 255f, PerfectRingAlpha),
            new Color(0xF0 / 255f, 0xD4 / 255f, 0xC8 / 255f, PerfectRingAlpha),
            new Color(0xC9 / 255f, 0xB8 / 255f, 0xE8 / 255f, PerfectRingAlpha),
            new Color(0xE8 / 255f, 0xD5 / 255f, 0xE0 / 255f, PerfectRingAlpha),
            new Color(0xDF / 255f, 0xC9 / 255f, 0xD2 / 255f, PerfectRingAlpha)
        };

        private struct MarkerFrameAnim
        {
            public RectTransform rect;
            public float[] scales;
            public int frame;
            public float elapsed;
            public string kind;
            public string variant;
        }

        private SpellInputBuffer buffer;
        private readonly List<Image> spawnedMarkers = new List<Image>(8);
        private readonly List<TransientMarker> transients = new List<TransientMarker>(4);
        private readonly List<PerfectRingVisual> activePerfectRings = new List<PerfectRingVisual>(8);
        private readonly List<MarkerFrameAnim> markerAnims = new List<MarkerFrameAnim>(8);
        private bool hasVisualLifecycle;
        private float headBeatFloat;
        private int nextCastVariantOverride = -1;

        public float AnimationFrameRate => Mathf.Max(1f, animationFrameRate);
        public int QueueMarkerCount => spawnedMarkers.Count;
        public int TransientMarkerCount => transients.Count;
        public int MarkerCount => spawnedMarkers.Count + transients.Count;
        public int ActivePerfectRingCount => activePerfectRings.Count;
        public Vector2 LastMarkerPosition { get; private set; }
        public string LastMarkerKind { get; private set; } = string.Empty;
        public bool LastQueueMarkerHasPerfectRing { get; private set; }
        public bool LastQueueMarkerHasRays => LastQueueMarkerHasPerfectRing;
        public string LastQueueMarkerGrade { get; private set; } = string.Empty;
        public string LastQueueMarkerPromptGrade { get; private set; } = string.Empty;
        public float LastPromptAnchorBeatFloat { get; private set; }
        public Vector2 LastPromptAnchorPosition { get; private set; }
        public float HeadMarkerBeatFloat => hasVisualLifecycle ? headBeatFloat : 0f;
        public string LastMarkerAnimKind { get; private set; } = string.Empty;
        public string LastCastAnimVariant { get; private set; } = string.Empty;
        public string LastMarkerAnimScaleSequence { get; private set; } = string.Empty;
        public int LastMarkerAnimFrame { get; private set; }
        public float LastMarkerAnimScale { get; private set; } = 1f;
        public bool IsLastMarkerAnimPlaying { get; private set; }

        public void Bind(RhythmPrototypeBootstrap host)
        {
            Unsubscribe();
            bootstrap = host;
            Subscribe();
        }

        private void Awake()
        {
            CacheRefs();
        }

        private void OnEnable()
        {
            CacheRefs();
            Subscribe();
        }

        private void Update()
        {
            double dspNow = AudioSettings.dspTime;
            ExpireTransientIfNeeded(dspNow);
            TickMarkerAnimations(Time.deltaTime);
            if (bootstrap == null || bootstrap.Clock == null)
            {
                return;
            }

            if (bootstrap.Clock.IsRunning)
            {
                TickPerfectRings(bootstrap.Clock.BeatFloat);
            }

            if (!hasVisualLifecycle)
            {
                return;
            }

            ExpireIfNeeded(bootstrap.Clock.BeatFloat);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void ClearMarkers()
        {
            if (spawnedMarkers.Count == 0 && !hasVisualLifecycle)
            {
                return;
            }

            for (int i = 0; i < spawnedMarkers.Count; i++)
            {
                DestroyMarkerImage(spawnedMarkers[i]);
            }

            spawnedMarkers.Clear();
            activePerfectRings.Clear();
            RemoveOrphanMarkerAnims();
            hasVisualLifecycle = false;
            headBeatFloat = 0f;
            LastQueueMarkerHasPerfectRing = false;
            LastQueueMarkerGrade = string.Empty;
            LastQueueMarkerPromptGrade = string.Empty;
            LastPromptAnchorBeatFloat = 0f;
            LastPromptAnchorPosition = Vector2.zero;
            if (transients.Count == 0)
            {
                LastMarkerKind = string.Empty;
                LastMarkerPosition = Vector2.zero;
            }

            Debug.Log("[Track] MarkersCleared");
        }

        /// <summary>
        /// 队首 BeatFloat 绕一圈（+4 拍）后销毁留痕。匹配成功后缓冲可能已空，仍靠这里清 UI。
        /// </summary>
        public void ExpireIfNeeded(float currentBeatFloat)
        {
            if (!hasVisualLifecycle || spawnedMarkers.Count == 0)
            {
                return;
            }

            if (currentBeatFloat < headBeatFloat + QueueLifecycleBeats)
            {
                return;
            }

            ClearMarkers();
        }

        public Vector2 ShowMarkerForTest(float beatFloat, BeatInputType type)
        {
            return SpawnMarker(beatFloat, type, BeatTimingGrade.Weak);
        }

        /// <summary>
        /// 空施法短效黑色 Cast 痕：不入队列、不走一圈生命周期，到期按 dsp 自销毁。
        /// </summary>
        public void ShowTransientCastMarker(BeatTimingInfo timing, float lifeSeconds)
        {
            Image image = CreateMarkerImage(timing.BeatFloat, BeatInputType.Cast, BeatTimingGrade.Perfect, isQueue: false);
            if (image == null)
            {
                return;
            }

            transients.Add(new TransientMarker
            {
                image = image,
                expireDspTime = timing.DspTime + Mathf.Max(0f, lifeSeconds)
            });
            Debug.Log($"[Track] TransientCast beatFloat={timing.BeatFloat:0.##} life={lifeSeconds:0.###}s");
        }

        public void ExpireTransientIfNeeded(double dspNow)
        {
            bool anyExpired = false;
            for (int i = transients.Count - 1; i >= 0; i--)
            {
                if (dspNow < transients[i].expireDspTime)
                {
                    continue;
                }

                DestroyMarkerImage(transients[i].image);
                transients.RemoveAt(i);
                anyExpired = true;
            }

            if (anyExpired)
            {
                Debug.Log("[Track] TransientCastCleared");
            }
        }

        public void ClearTransientMarkers()
        {
            if (transients.Count == 0)
            {
                return;
            }

            for (int i = 0; i < transients.Count; i++)
            {
                DestroyMarkerImage(transients[i].image);
            }

            transients.Clear();
            RemoveOrphanMarkerAnims();
            Debug.Log("[Track] TransientCastCleared");
        }

        public void ClearAllInputMarkers()
        {
            ClearMarkers();
            ClearTransientMarkers();
            LastMarkerKind = string.Empty;
            LastMarkerPosition = Vector2.zero;
            LastQueueMarkerHasPerfectRing = false;
            LastQueueMarkerGrade = string.Empty;
            LastQueueMarkerPromptGrade = string.Empty;
            LastPromptAnchorBeatFloat = 0f;
            LastPromptAnchorPosition = Vector2.zero;
            RefreshLastMarkerAnimQuery();
        }

        private void Subscribe()
        {
            if (bootstrap == null)
            {
                return;
            }

            buffer = bootstrap.InputBuffer;
            if (buffer == null)
            {
                return;
            }

            buffer.OnEntryAdded -= HandleEntryAdded;
            buffer.OnCleared -= HandleCleared;
            buffer.OnEntryAdded += HandleEntryAdded;
            buffer.OnCleared += HandleCleared;
        }

        private void Unsubscribe()
        {
            if (buffer == null)
            {
                return;
            }

            buffer.OnEntryAdded -= HandleEntryAdded;
            buffer.OnCleared -= HandleCleared;
            buffer = null;
        }

        private void HandleEntryAdded(RhythmInputEntry entry)
        {
            bool newChantRound = buffer != null && buffer.Count <= 1;
            if (newChantRound && spawnedMarkers.Count > 0)
            {
                ClearMarkers();
            }

            if (!hasVisualLifecycle)
            {
                hasVisualLifecycle = true;
                headBeatFloat = entry.BeatFloat;
            }

            SpawnMarker(entry);
        }

        private void HandleCleared()
        {
            ClearMarkers();
        }

        private Vector2 SpawnMarker(float beatFloat, BeatInputType type, BeatTimingGrade grade = BeatTimingGrade.Weak)
        {
            Image image = CreateMarkerImage(beatFloat, type, grade, isQueue: true);
            if (image == null)
            {
                return Vector2.zero;
            }

            spawnedMarkers.Add(image);
            return image.rectTransform.anchoredPosition;
        }

        private Vector2 SpawnMarker(RhythmInputEntry entry)
        {
            bool promptPerfect = entry.PromptGrade == BeatTimingGrade.Perfect;
            float displayBeatFloat = promptPerfect ? entry.PromptAnchorBeatFloat : entry.BeatFloat;
            Image image = CreateMarkerImage(
                displayBeatFloat,
                entry.InputType,
                entry.PromptGrade,
                isQueue: true,
                actualBeatFloat: entry.BeatFloat,
                promptAnchorBeatFloat: entry.PromptAnchorBeatFloat);
            if (image == null)
            {
                return Vector2.zero;
            }

            spawnedMarkers.Add(image);
            return image.rectTransform.anchoredPosition;
        }

        private Image CreateMarkerImage(
            float beatFloat,
            BeatInputType type,
            BeatTimingGrade grade,
            bool isQueue,
            float actualBeatFloat = float.NaN,
            float promptAnchorBeatFloat = float.NaN)
        {
            CacheRefs();
            if (markerRoot == null || trackView == null)
            {
                Debug.LogWarning("[Track] MarkerRoot 或 TrackView 缺失，无法留痕。");
                return null;
            }

            Vector2 pos = trackView.GetPositionOnTrack(beatFloat);
            string name = type == BeatInputType.Cast ? "CastMarker" : "ChantMarker";
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(markerRoot, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            float size = Mathf.Max(32f, trackView.MarkerSize);
            rect.sizeDelta = new Vector2(size, size);

            Image image = go.GetComponent<Image>();
            image.sprite = type == BeatInputType.Cast ? castSprite : chantSprite;
            image.raycastTarget = false;
            image.preserveAspect = true;

            bool hasPerfectRing = isQueue && grade == BeatTimingGrade.Perfect
                && AttachPerfectRing(rect, size, beatFloat);

            LastMarkerPosition = pos;
            LastMarkerKind = type.ToString();
            if (isQueue)
            {
                LastQueueMarkerHasPerfectRing = hasPerfectRing;
                LastQueueMarkerGrade = grade.ToString();
                LastQueueMarkerPromptGrade = grade.ToString();
                LastPromptAnchorBeatFloat = float.IsNaN(promptAnchorBeatFloat) ? beatFloat : promptAnchorBeatFloat;
                LastPromptAnchorPosition = trackView.GetPositionOnTrack(LastPromptAnchorBeatFloat);
            }

            float loggedActualBeatFloat = float.IsNaN(actualBeatFloat) ? beatFloat : actualBeatFloat;
            float loggedAnchorBeatFloat = float.IsNaN(promptAnchorBeatFloat) ? beatFloat : promptAnchorBeatFloat;
            Debug.Log(
                $"[Track] Marker {type} {grade} actualBeatFloat={loggedActualBeatFloat:0.##} displayBeatFloat={beatFloat:0.##} anchorBeatFloat={loggedAnchorBeatFloat:0.##} perfectRing={(hasPerfectRing ? 1 : 0)} pos=({pos.x:0},{pos.y:0})");
            StartMarkerAnimation(rect, type);
            return image;
        }

        public void SetNextCastAnimVariant(string variant)
        {
            if (string.Equals(variant, "A", System.StringComparison.OrdinalIgnoreCase))
            {
                nextCastVariantOverride = 0;
                return;
            }

            if (string.Equals(variant, "B", System.StringComparison.OrdinalIgnoreCase))
            {
                nextCastVariantOverride = 1;
                return;
            }

            nextCastVariantOverride = -1;
        }

        /// <summary>
        /// MCP：按秒推进所有 Marker 帧动画。
        /// </summary>
        public void TickMarkerAnimations(float seconds)
        {
            float dt = Mathf.Max(0f, seconds);
            float frameDuration = 1f / AnimationFrameRate;
            for (int i = markerAnims.Count - 1; i >= 0; i--)
            {
                MarkerFrameAnim anim = markerAnims[i];
                if (anim.rect == null || anim.scales == null || anim.scales.Length == 0)
                {
                    markerAnims.RemoveAt(i);
                    continue;
                }

                if (anim.frame >= anim.scales.Length - 1)
                {
                    continue;
                }

                anim.elapsed += dt;
                while (anim.frame < anim.scales.Length - 1 && anim.elapsed >= frameDuration)
                {
                    anim.elapsed -= frameDuration;
                    anim.frame++;
                    ApplyMarkerAnim(anim);
                    if (anim.frame == anim.scales.Length - 1)
                    {
                        Debug.Log($"[Track] MarkerAnim complete {anim.kind} variant={anim.variant} scale=1");
                    }
                }

                markerAnims[i] = anim;
            }

            RefreshLastMarkerAnimQuery();
        }

        /// <summary>
        /// MCP：推进最近一次 Marker 动画指定帧数。生成后立刻处于第 1 帧。
        /// </summary>
        public void AdvanceLastMarkerAnimFrames(int frames)
        {
            if (frames <= 0 || markerAnims.Count == 0)
            {
                return;
            }

            int index = markerAnims.Count - 1;
            MarkerFrameAnim anim = markerAnims[index];
            if (anim.rect == null || anim.scales == null)
            {
                return;
            }

            for (int i = 0; i < frames; i++)
            {
                if (anim.frame >= anim.scales.Length - 1)
                {
                    break;
                }

                anim.frame++;
                anim.elapsed = 0f;
                ApplyMarkerAnim(anim);
                if (anim.frame == anim.scales.Length - 1)
                {
                    Debug.Log($"[Track] MarkerAnim complete {anim.kind} variant={anim.variant} scale=1");
                    break;
                }
            }

            markerAnims[index] = anim;
            RefreshLastMarkerAnimQuery();
        }

        private void StartMarkerAnimation(RectTransform rect, BeatInputType type)
        {
            float[] scales;
            string variant = string.Empty;
            if (type == BeatInputType.Cast)
            {
                int variantIndex = nextCastVariantOverride >= 0
                    ? nextCastVariantOverride
                    : Random.Range(0, 2);
                nextCastVariantOverride = -1;
                bool useA = variantIndex == 0;
                scales = useA ? CastScalesA : CastScalesB;
                variant = useA ? "A" : "B";
            }
            else
            {
                scales = ChantScales;
            }

            var anim = new MarkerFrameAnim
            {
                rect = rect,
                scales = scales,
                frame = 0,
                elapsed = 0f,
                kind = type.ToString(),
                variant = variant
            };
            markerAnims.Add(anim);
            ApplyMarkerAnim(anim);
            RefreshLastMarkerAnimQuery();
            Debug.Log(
                $"[Track] MarkerAnim start {anim.kind} variant={anim.variant} sequence={LastMarkerAnimScaleSequence} frame=1 scale={LastMarkerAnimScale:0.##}");
        }

        private void ApplyMarkerAnim(MarkerFrameAnim anim)
        {
            if (anim.rect == null || anim.scales == null || anim.frame < 0 || anim.frame >= anim.scales.Length)
            {
                return;
            }

            float scale = anim.scales[anim.frame];
            anim.rect.localScale = Vector3.one * scale;
        }

        private void RefreshLastMarkerAnimQuery()
        {
            if (markerAnims.Count == 0)
            {
                LastMarkerAnimKind = string.Empty;
                LastCastAnimVariant = string.Empty;
                LastMarkerAnimScaleSequence = string.Empty;
                LastMarkerAnimFrame = 0;
                LastMarkerAnimScale = 1f;
                IsLastMarkerAnimPlaying = false;
                return;
            }

            MarkerFrameAnim anim = markerAnims[markerAnims.Count - 1];
            LastMarkerAnimKind = anim.kind ?? string.Empty;
            LastCastAnimVariant = anim.variant ?? string.Empty;
            LastMarkerAnimScaleSequence = FormatScaleSequence(anim.scales);
            LastMarkerAnimFrame = anim.frame + 1;
            LastMarkerAnimScale = anim.scales != null && anim.frame >= 0 && anim.frame < anim.scales.Length
                ? anim.scales[anim.frame]
                : 1f;
            IsLastMarkerAnimPlaying = anim.scales != null && anim.frame < anim.scales.Length - 1;
        }

        private void RemoveOrphanMarkerAnims()
        {
            for (int i = markerAnims.Count - 1; i >= 0; i--)
            {
                if (markerAnims[i].rect == null)
                {
                    markerAnims.RemoveAt(i);
                }
            }

            if (markerAnims.Count == 0)
            {
                RefreshLastMarkerAnimQuery();
            }
        }

        private static string FormatScaleSequence(float[] scales)
        {
            if (scales == null || scales.Length == 0)
            {
                return string.Empty;
            }

            var parts = new string[scales.Length];
            for (int i = 0; i < scales.Length; i++)
            {
                parts[i] = scales[i].ToString("0.#");
            }

            return string.Join(",", parts);
        }

        /// <summary>
        /// Perfect 圆环：包在咏唱/施法 Marker 外圈，旋转与浅色闪烁由 BeatFloat 驱动。
        /// </summary>
        private bool AttachPerfectRing(RectTransform parent, float markerSize, float spawnBeatFloat)
        {
            if (perfectRingSprite == null)
            {
                Debug.LogWarning("[Track] PerfectRing Sprite 未绑定，无法显示完美节拍圆环。");
                return false;
            }

            var go = new GameObject("PerfectRing", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.transform.SetAsFirstSibling();

            float ringSize = markerSize * Mathf.Max(1.01f, perfectRingScale);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(ringSize, ringSize);

            Image image = go.GetComponent<Image>();
            image.sprite = perfectRingSprite;
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.color = EvaluatePerfectRingColor(0f);

            activePerfectRings.Add(new PerfectRingVisual
            {
                rect = rect,
                image = image,
                spawnBeatFloat = spawnBeatFloat
            });
            return true;
        }

        private void TickPerfectRings(float currentBeatFloat)
        {
            for (int i = activePerfectRings.Count - 1; i >= 0; i--)
            {
                PerfectRingVisual visual = activePerfectRings[i];
                if (visual.rect == null || visual.image == null)
                {
                    activePerfectRings.RemoveAt(i);
                    continue;
                }

                float elapsedBeats = currentBeatFloat - visual.spawnBeatFloat;
                visual.rect.localEulerAngles = new Vector3(0f, 0f, elapsedBeats * rotationDegreesPerBeat);
                visual.image.color = EvaluatePerfectRingColor(elapsedBeats);
            }
        }

        private Color EvaluatePerfectRingColor(float elapsedBeats)
        {
            int count = PerfectRingPalette.Length;
            float cycles = Mathf.Max(0.01f, colorCyclesPerBeat);
            float phase = Mathf.Repeat(elapsedBeats * cycles, count);
            int index = Mathf.FloorToInt(phase);
            float t = phase - index;
            Color a = PerfectRingPalette[index % count];
            Color b = PerfectRingPalette[(index + 1) % count];
            Color mixed = Color.Lerp(a, b, t);
            mixed.a = PerfectRingAlpha;
            return mixed;
        }

        private static void DestroyMarkerImage(Image image)
        {
            if (image != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(image.gameObject);
                }
                else
                {
                    DestroyImmediate(image.gameObject);
                }
            }
        }

        private void CacheRefs()
        {
            if (bootstrap == null)
            {
                bootstrap = FindFirstObjectByType<RhythmPrototypeBootstrap>();
            }

            if (trackView == null)
            {
                trackView = GetComponent<DiamondBeatTrackView>();
            }

            if (markerRoot == null)
            {
                Transform found = transform.Find("MarkerRoot");
                if (found != null)
                {
                    markerRoot = found as RectTransform;
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            perfectRingScale = Mathf.Max(1.01f, perfectRingScale);
            rotationDegreesPerBeat = Mathf.Max(0f, rotationDegreesPerBeat);
            colorCyclesPerBeat = Mathf.Max(0.01f, colorCyclesPerBeat);
            animationFrameRate = Mathf.Max(1f, animationFrameRate);
        }
#endif
    }
}
