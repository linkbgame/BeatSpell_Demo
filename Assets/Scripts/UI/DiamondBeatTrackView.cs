using BeatSpell.Rhythm;
using UnityEngine;
using UnityEngine.UI;

namespace BeatSpell.UI
{
    /// <summary>
    /// 第 4 轮：只读 BeatClock 四顶点当前拍。提供轨迹插值给输入留痕，不参与判定。
    /// </summary>
    public class DiamondBeatTrackView : MonoBehaviour
    {
        [SerializeField] private BeatClock beatClock;
        [SerializeField] private float diamondRadius = 360f;
        [Tooltip("上侧两条边沿菱形外法线外移，使实心边落在光晕外侧。")]
        [SerializeField] private float topEdgeOutwardOffset = 18f;
        [SerializeField] private RectTransform glowRect;
        [SerializeField] private RectTransform[] edgeRects = new RectTransform[4];
        [SerializeField] private RectTransform[] midpointRects = new RectTransform[4];
        [SerializeField] private RectTransform[] vertexRects = new RectTransform[4];
        [SerializeField] private GameObject[] beatPulses = new GameObject[4];

        private int lastVertexIndex = -1;
        private Vector2[] cachedVertices = new Vector2[4];
        private float cachedVertexSize;
        private Image[] beatPulseImages;

        public int ActiveBeatVertex { get; private set; }
        public float DiamondRadius => diamondRadius;
        public float MarkerSize => cachedVertexSize * 0.85f;

        public void BindClock(BeatClock clock)
        {
            beatClock = clock;
            lastVertexIndex = -1;
            RefreshBeatVertexIfRunning();
        }

        private void Awake()
        {
            CacheBeatPulseImages();
            CacheBeatClock();
            ApplyLayout();
            ResetBeatPulseVisuals();
        }

        private void Start()
        {
            CacheBeatClock();
            lastVertexIndex = -1;
            RefreshBeatVertexIfRunning();
        }

        private void OnEnable()
        {
            CacheBeatClock();
            if (beatClock != null && beatClock.IsRunning)
            {
                lastVertexIndex = -1;
                SetBeatVertex(beatClock.VertexIndex);
            }
        }

        private void Update()
        {
            CacheBeatClock();
            if (beatClock == null || !beatClock.IsRunning)
            {
                return;
            }

            SetBeatVertex(beatClock.VertexIndex);
        }

        public void SetBeatVertex(int vertexIndex)
        {
            int next = BeatTimingInfo.PositiveMod(vertexIndex, 4);
            if (next == lastVertexIndex)
            {
                return;
            }

            lastVertexIndex = next;
            ActiveBeatVertex = next;
            for (int i = 0; i < beatPulses.Length; i++)
            {
                if (beatPulses[i] == null)
                {
                    continue;
                }

                bool on = i == next;
                beatPulses[i].SetActive(on);
                if (on && beatPulseImages != null && i < beatPulseImages.Length && beatPulseImages[i] != null)
                {
                    beatPulseImages[i].enabled = true;
                }
            }

            Debug.Log($"[Track] BeatVertex={next} ({BeatTimingInfo.VertexName(next)})");
        }

        public bool IsBeatPulseActive(int vertexIndex)
        {
            int index = BeatTimingInfo.PositiveMod(vertexIndex, 4);
            return beatPulses[index] != null && beatPulses[index].activeSelf;
        }

        /// <summary>
        /// 菱形轨迹连续坐标：一圈 4 拍，按 beatFloat 在边上线性插值。仅用于 UI。
        /// </summary>
        public Vector2 GetPositionOnTrack(float beatFloat)
        {
            EnsureCachedVertices();
            float phase = PositiveModFloat(beatFloat, 4f);
            int edgeIndex = Mathf.Min(3, Mathf.FloorToInt(phase));
            float t = phase - edgeIndex;
            Vector2 a = cachedVertices[edgeIndex];
            Vector2 b = cachedVertices[(edgeIndex + 1) % 4];
            return Vector2.Lerp(a, b, t);
        }

        public void ApplyLayout()
        {
            float r = Mathf.Max(32f, diamondRadius);
            cachedVertices = new[]
            {
                new Vector2(0f, r),
                new Vector2(r, 0f),
                new Vector2(0f, -r),
                new Vector2(-r, 0f)
            };

            float side = r * Mathf.Sqrt(2f);
            cachedVertexSize = side * 0.20f;
            float midSize = side * 0.09f;
            float edgeThickness = side * 0.05f;

            if (glowRect != null)
            {
                glowRect.anchoredPosition = Vector2.zero;
                glowRect.sizeDelta = new Vector2(r * 2.4f, r * 2.4f);
            }

            PlaceEdges(cachedVertices, side, edgeThickness);
            PlaceMidpoints(cachedVertices, midSize);
            PlaceVertices(cachedVertices, cachedVertexSize);
        }

        private void CacheBeatClock()
        {
            if (beatClock == null)
            {
                beatClock = FindFirstObjectByType<BeatClock>();
            }
        }

        private void EnsureCachedVertices()
        {
            if (cachedVertices != null && cachedVertices.Length == 4 && cachedVertices[0] != Vector2.zero)
            {
                return;
            }

            ApplyLayout();
        }

        private void PlaceEdges(Vector2[] vertices, float side, float thickness)
        {
            // 0=TopRight, 1=BottomRight, 2=BottomLeft, 3=TopLeft
            float[] angles = { -45f, 45f, -45f, 45f };
            Vector2[] outwardNormals =
            {
                new Vector2(1f, 1f).normalized,
                new Vector2(1f, -1f).normalized,
                new Vector2(-1f, -1f).normalized,
                new Vector2(-1f, 1f).normalized
            };

            for (int i = 0; i < 4; i++)
            {
                if (edgeRects[i] == null)
                {
                    continue;
                }

                Vector2 a = vertices[i];
                Vector2 b = vertices[(i + 1) % 4];
                Vector2 mid = (a + b) * 0.5f;
                if (topEdgeOutwardOffset > 0f && (i == 0 || i == 3))
                {
                    mid += outwardNormals[i] * topEdgeOutwardOffset;
                }

                edgeRects[i].anchoredPosition = mid;
                edgeRects[i].localEulerAngles = new Vector3(0f, 0f, angles[i]);
                edgeRects[i].sizeDelta = new Vector2(side, thickness);
                // track_edge 亮边在条带一侧；上两条边镜像 local Y，使蓝边朝菱形外侧。
                bool flipOutward = i == 0 || i == 3;
                edgeRects[i].localScale = flipOutward ? new Vector3(1f, -1f, 1f) : Vector3.one;
            }
        }

        private void CacheBeatPulseImages()
        {
            if (beatPulseImages != null && beatPulseImages.Length == beatPulses.Length)
            {
                return;
            }

            beatPulseImages = new Image[beatPulses.Length];
            for (int i = 0; i < beatPulses.Length; i++)
            {
                if (beatPulses[i] != null)
                {
                    beatPulseImages[i] = beatPulses[i].GetComponent<Image>();
                }
            }
        }

        private void ResetBeatPulseVisuals()
        {
            CacheBeatPulseImages();
            for (int i = 0; i < beatPulses.Length; i++)
            {
                if (beatPulses[i] != null)
                {
                    beatPulses[i].SetActive(false);
                }
            }

            lastVertexIndex = -1;
        }

        private void RefreshBeatVertexIfRunning()
        {
            if (beatClock != null && beatClock.IsRunning)
            {
                SetBeatVertex(beatClock.VertexIndex);
            }
        }

        private void PlaceMidpoints(Vector2[] vertices, float size)
        {
            for (int i = 0; i < 4; i++)
            {
                if (midpointRects[i] == null)
                {
                    continue;
                }

                Vector2 a = vertices[i];
                Vector2 b = vertices[(i + 1) % 4];
                midpointRects[i].anchoredPosition = (a + b) * 0.5f;
                midpointRects[i].sizeDelta = new Vector2(size, size);
            }
        }

        private void PlaceVertices(Vector2[] vertices, float size)
        {
            for (int i = 0; i < 4; i++)
            {
                if (vertexRects[i] == null)
                {
                    continue;
                }

                vertexRects[i].anchoredPosition = vertices[i];
                vertexRects[i].sizeDelta = new Vector2(size, size);
                StretchChildren(vertexRects[i]);
            }
        }

        private static void StretchChildren(RectTransform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                RectTransform child = parent.GetChild(i) as RectTransform;
                if (child == null)
                {
                    continue;
                }

                child.anchorMin = Vector2.zero;
                child.anchorMax = Vector2.one;
                child.offsetMin = Vector2.zero;
                child.offsetMax = Vector2.zero;
            }
        }

        private static float PositiveModFloat(float value, float modulo)
        {
            float remainder = value % modulo;
            return remainder < 0f ? remainder + modulo : remainder;
        }
    }
}
