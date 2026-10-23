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

        [SerializeField] private RhythmPrototypeBootstrap bootstrap;
        [SerializeField] private DiamondBeatTrackView trackView;
        [SerializeField] private RectTransform markerRoot;
        [SerializeField] private Sprite chantSprite;
        [SerializeField] private Sprite castSprite;

        private const int PerfectRayCount = 8;
        private const float QueueLifecycleBeats = 4f;

        private SpellInputBuffer buffer;
        private readonly List<Image> spawnedMarkers = new List<Image>(8);
        private readonly List<TransientMarker> transients = new List<TransientMarker>(4);
        private bool hasVisualLifecycle;
        private float headBeatFloat;

        public int QueueMarkerCount => spawnedMarkers.Count;
        public int TransientMarkerCount => transients.Count;
        public int MarkerCount => spawnedMarkers.Count + transients.Count;
        public Vector2 LastMarkerPosition { get; private set; }
        public string LastMarkerKind { get; private set; } = string.Empty;
        public bool LastQueueMarkerHasRays { get; private set; }
        public string LastQueueMarkerGrade { get; private set; } = string.Empty;
        public float HeadMarkerBeatFloat => hasVisualLifecycle ? headBeatFloat : 0f;

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
            if (!hasVisualLifecycle || bootstrap == null || bootstrap.Clock == null)
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
                if (spawnedMarkers[i] != null)
                {
                    Destroy(spawnedMarkers[i].gameObject);
                }
            }

            spawnedMarkers.Clear();
            hasVisualLifecycle = false;
            headBeatFloat = 0f;
            LastQueueMarkerHasRays = false;
            LastQueueMarkerGrade = string.Empty;
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
            Debug.Log("[Track] TransientCastCleared");
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

            SpawnMarker(entry.BeatFloat, entry.InputType, entry.Grade);
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

        private Image CreateMarkerImage(
            float beatFloat,
            BeatInputType type,
            BeatTimingGrade grade,
            bool isQueue)
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

            bool hasRays = isQueue && grade == BeatTimingGrade.Perfect;
            if (hasRays)
            {
                AttachPerfectRays(rect, size);
            }

            LastMarkerPosition = pos;
            LastMarkerKind = type.ToString();
            if (isQueue)
            {
                LastQueueMarkerHasRays = hasRays;
                LastQueueMarkerGrade = grade.ToString();
            }

            Debug.Log(
                $"[Track] Marker {type} {grade} beatFloat={beatFloat:0.##} rays={(hasRays ? PerfectRayCount : 0)} pos=({pos.x:0},{pos.y:0})");
            return image;
        }

        /// <summary>
        /// Perfect 放射短线：从圆心向外，不依赖额外 Sprite。
        /// </summary>
        private static void AttachPerfectRays(RectTransform parent, float markerSize)
        {
            float rayLength = markerSize * 0.55f;
            float rayThickness = Mathf.Max(2f, markerSize * 0.08f);
            Color rayColor = new Color(0.965f, 0.957f, 0.114f, 0.95f);
            float startRadius = markerSize * 0.42f;

            for (int i = 0; i < PerfectRayCount; i++)
            {
                var go = new GameObject($"Ray_{i}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);
                go.transform.SetAsFirstSibling();

                RectTransform rect = go.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.sizeDelta = new Vector2(rayThickness, rayLength);

                float angle = i * (360f / PerfectRayCount);
                rect.localEulerAngles = new Vector3(0f, 0f, angle);
                float rad = angle * Mathf.Deg2Rad;
                rect.anchoredPosition = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * startRadius;

                Image image = go.GetComponent<Image>();
                image.color = rayColor;
                image.raycastTarget = false;
            }
        }

        private static void DestroyMarkerImage(Image image)
        {
            if (image != null)
            {
                Destroy(image.gameObject);
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
    }
}
