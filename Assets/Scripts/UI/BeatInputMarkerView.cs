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
    /// Weak 与 Perfect/Good 一样留痕；新一轮咏唱会先清掉上一轮残留。
    /// </summary>
    public class BeatInputMarkerView : MonoBehaviour
    {
        [SerializeField] private RhythmPrototypeBootstrap bootstrap;
        [SerializeField] private DiamondBeatTrackView trackView;
        [SerializeField] private RectTransform markerRoot;
        [SerializeField] private Sprite chantSprite;
        [SerializeField] private Sprite castSprite;

        private SpellInputBuffer buffer;
        private readonly List<Image> spawnedMarkers = new List<Image>(8);
        private bool hasVisualLifecycle;
        private double visualStartDspTime;

        public int MarkerCount => spawnedMarkers.Count;
        public Vector2 LastMarkerPosition { get; private set; }
        public string LastMarkerKind { get; private set; } = string.Empty;

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
            if (!hasVisualLifecycle || bootstrap == null || bootstrap.Clock == null)
            {
                return;
            }

            ExpireIfNeeded(AudioSettings.dspTime, bootstrap.ChantLifetimeSeconds);
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
            LastMarkerKind = string.Empty;
            LastMarkerPosition = Vector2.zero;
            hasVisualLifecycle = false;
            visualStartDspTime = 0d;
            Debug.Log("[Track] MarkersCleared");
        }

        /// <summary>
        /// 从首个 Marker 的 dsp 起算，满一圈后销毁留痕。匹配成功后缓冲可能已空，仍靠这里清 UI。
        /// </summary>
        public void ExpireIfNeeded(double dspNow, float lifecycleSeconds)
        {
            if (!hasVisualLifecycle || spawnedMarkers.Count == 0 || lifecycleSeconds <= 0f)
            {
                return;
            }

            if (dspNow < visualStartDspTime + lifecycleSeconds)
            {
                return;
            }

            ClearMarkers();
        }

        public Vector2 ShowMarkerForTest(float beatFloat, BeatInputType type)
        {
            return SpawnMarker(beatFloat, type);
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
                visualStartDspTime = entry.DspTime;
            }

            float beatFloat = entry.BeatIndex + entry.DeltaMs / (1000f * ResolveSecondsPerBeat());
            SpawnMarker(beatFloat, entry.InputType);
        }

        private void HandleCleared()
        {
            ClearMarkers();
        }

        private Vector2 SpawnMarker(float beatFloat, BeatInputType type)
        {
            CacheRefs();
            if (markerRoot == null || trackView == null)
            {
                Debug.LogWarning("[Track] MarkerRoot 或 TrackView 缺失，无法留痕。");
                return Vector2.zero;
            }

            Vector2 pos = trackView.GetPositionOnTrack(beatFloat);
            var go = new GameObject(type == BeatInputType.Cast ? "CastMarker" : "ChantMarker", typeof(RectTransform), typeof(Image));
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

            spawnedMarkers.Add(image);
            LastMarkerPosition = pos;
            LastMarkerKind = type.ToString();
            Debug.Log($"[Track] Marker {type} beatFloat={beatFloat:0.##} pos=({pos.x:0},{pos.y:0})");
            return pos;
        }

        private float ResolveSecondsPerBeat()
        {
            if (bootstrap != null && bootstrap.Clock != null)
            {
                return bootstrap.Clock.SecondsPerBeat;
            }

            return 0.5f;
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
