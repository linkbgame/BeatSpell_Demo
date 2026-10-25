using BeatSpell.Rhythm;
using UnityEngine;
using UnityEngine.UI;

namespace BeatSpell.UI
{
    /// <summary>
    /// 第 4 轮：只读 BeatClock 四顶点当前拍。白点只在 Top/Right/Bottom/Left 跳动，
    /// 不读取 TrackPhase，不沿边连续移动。轨迹插值仅给输入留痕，不参与判定。
    /// </summary>
    public class DiamondBeatTrackView : MonoBehaviour
    {
        [SerializeField] private BeatClock beatClock;
        [Tooltip("勾选后在顶点切换时输出 [Track] BeatVertex=...")]
        [SerializeField] private bool logBeatVertexChanges;
        [SerializeField] private float diamondRadius = 360f;
        [Tooltip("新 *_new 贴图满画布、旧图有透明边时，用约 0.83 对齐旧版视觉占比；满画布新美术可调到 1。")]
        [SerializeField] private float spriteContentScale = 0.832f;

        [Header("相对边长比例（见 BeatTrack_ArtSpec / RefPhoto）")]
        [SerializeField] private float vertexSizeRatio = 0.20f;
        [SerializeField] private float midpointSizeRatio = 0.09f;
        [SerializeField] private float edgeThicknessRatio = 0.05f;
        [Tooltip("四条边 Rect 长度（sizeDelta.x）。0 表示自动使用 diamondRadius×√2；>0 固定边条长度（如 450）。")]
        [SerializeField] private float edgeStripLength;
        [Tooltip("输入留痕直径 ≈ 顶点直径 × 本系数（BeatInputMarkerView 读取 MarkerSize）。")]
        [SerializeField] private float inputMarkerSizeRatio = 0.85f;
        [Tooltip("当前拍 BeatPulse 相对顶点 Rect 的缩放；1 与 idle 圈同大。")]
        [SerializeField] private float beatPulseSizeRatio = 1f;

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
        public float MarkerSize => cachedVertexSize * Mathf.Max(0.1f, inputMarkerSizeRatio);

        /// <summary>
        /// 当前激活的白点数量，验收用：应始终为 0 或 1。
        /// </summary>
        public int ActiveBeatPulseCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < beatPulses.Length; i++)
                {
                    if (beatPulses[i] != null && beatPulses[i].activeSelf)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

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

            // 白点只跟 VertexIndex，禁止用 TrackPhase 做沿边游标。
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

            if (logBeatVertexChanges)
            {
                Debug.Log($"[Track] BeatVertex={next} ({BeatTimingInfo.VertexName(next)})");
            }
        }

        public void SetLogBeatVertexChanges(bool enabled)
        {
            logBeatVertexChanges = enabled;
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
            float scale = Mathf.Max(0.5f, spriteContentScale);
            cachedVertexSize = side * Mathf.Max(0.01f, vertexSizeRatio) * scale;
            float midSize = side * Mathf.Max(0.01f, midpointSizeRatio) * scale;
            float edgeThickness = side * Mathf.Max(0.01f, edgeThicknessRatio) * scale;
            float edgeLen = edgeStripLength > 0f ? edgeStripLength : side;

            if (glowRect != null)
            {
                glowRect.anchoredPosition = Vector2.zero;
                glowRect.sizeDelta = new Vector2(r * 2.4f, r * 2.4f);
            }

            PlaceEdges(cachedVertices, edgeLen, edgeThickness);
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

        private void PlaceEdges(Vector2[] vertices, float stripLength, float thickness)
        {
            // 0=TopRight, 1=BottomRight, 2=BottomLeft, 3=TopLeft
            float[] angles = { -45f, 45f, -45f, 45f };

            for (int i = 0; i < 4; i++)
            {
                if (edgeRects[i] == null)
                {
                    continue;
                }

                Vector2 mid = GetEdgeCenter(vertices, i);

                edgeRects[i].anchoredPosition = mid;
                edgeRects[i].localEulerAngles = new Vector3(0f, 0f, angles[i]);
                edgeRects[i].sizeDelta = new Vector2(stripLength, thickness);
                edgeRects[i].localScale = Vector3.one;
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

                midpointRects[i].anchoredPosition = GetEdgeCenter(vertices, i);
                midpointRects[i].sizeDelta = new Vector2(size, size);
            }
        }

        /// <summary>
        /// 边条与中点共用：相邻顶点连线的几何中点。
        /// </summary>
        private static Vector2 GetEdgeCenter(Vector2[] vertices, int edgeIndex)
        {
            Vector2 a = vertices[edgeIndex];
            Vector2 b = vertices[(edgeIndex + 1) % 4];
            return (a + b) * 0.5f;
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
                LayoutVertexChildren(vertexRects[i], size, i);
            }
        }

        private void LayoutVertexChildren(RectTransform parent, float vertexSize, int vertexIndex)
        {
            float pulseScale = Mathf.Max(0.1f, beatPulseSizeRatio);
            float pulseSize = vertexSize * pulseScale;
            GameObject pulseGo = vertexIndex >= 0 && vertexIndex < beatPulses.Length ? beatPulses[vertexIndex] : null;

            for (int i = 0; i < parent.childCount; i++)
            {
                RectTransform child = parent.GetChild(i) as RectTransform;
                if (child == null)
                {
                    continue;
                }

                if (pulseGo != null && child.gameObject == pulseGo)
                {
                    child.anchorMin = new Vector2(0.5f, 0.5f);
                    child.anchorMax = new Vector2(0.5f, 0.5f);
                    child.pivot = new Vector2(0.5f, 0.5f);
                    child.anchoredPosition = Vector2.zero;
                    child.sizeDelta = new Vector2(pulseSize, pulseSize);
                    continue;
                }

                StretchToParent(child);
            }
        }

        private static void StretchToParent(RectTransform child)
        {
            child.anchorMin = Vector2.zero;
            child.anchorMax = Vector2.one;
            child.offsetMin = Vector2.zero;
            child.offsetMax = Vector2.zero;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            vertexSizeRatio = Mathf.Max(0.01f, vertexSizeRatio);
            midpointSizeRatio = Mathf.Max(0.01f, midpointSizeRatio);
            edgeThicknessRatio = Mathf.Max(0.01f, edgeThicknessRatio);
            edgeStripLength = Mathf.Max(0f, edgeStripLength);
            inputMarkerSizeRatio = Mathf.Max(0.1f, inputMarkerSizeRatio);
            beatPulseSizeRatio = Mathf.Max(0.1f, beatPulseSizeRatio);
            ApplyLayout();
        }
#endif

        private static float PositiveModFloat(float value, float modulo)
        {
            float remainder = value % modulo;
            return remainder < 0f ? remainder + modulo : remainder;
        }
    }
}
