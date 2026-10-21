using BeatSpell.Rhythm;
using UnityEngine;
using UnityEngine.UI;

namespace BeatSpell.UI
{
    /// <summary>
    /// 第 4 轮：只读 BeatClock 顶点，切换当前拍高亮。不参与判定。
    /// </summary>
    public class DiamondBeatTrackView : MonoBehaviour
    {
        [SerializeField] private BeatClock beatClock;
        [SerializeField] private float diamondRadius = 360f;
        [SerializeField] private RectTransform glowRect;
        [SerializeField] private RectTransform[] edgeRects = new RectTransform[4];
        [SerializeField] private RectTransform[] midpointRects = new RectTransform[4];
        [SerializeField] private RectTransform[] vertexRects = new RectTransform[4];
        [SerializeField] private GameObject[] beatPulses = new GameObject[4];

        private int lastVertexIndex = -1;

        public int ActiveBeatVertex { get; private set; }
        public float DiamondRadius => diamondRadius;

        public void BindClock(BeatClock clock)
        {
            beatClock = clock;
        }

        private void Awake()
        {
            if (beatClock == null)
            {
                beatClock = FindFirstObjectByType<BeatClock>();
            }

            ApplyLayout();
        }

        private void Update()
        {
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
                if (beatPulses[i] != null)
                {
                    beatPulses[i].SetActive(i == next);
                }
            }

            Debug.Log($"[Track] BeatVertex={next} ({BeatTimingInfo.VertexName(next)})");
        }

        public bool IsBeatPulseActive(int vertexIndex)
        {
            int index = BeatTimingInfo.PositiveMod(vertexIndex, 4);
            return beatPulses[index] != null && beatPulses[index].activeSelf;
        }

        public Image GetMarkerImage(int vertexIndex)
        {
            int index = BeatTimingInfo.PositiveMod(vertexIndex, 4);
            if (vertexRects[index] == null)
            {
                return null;
            }

            Transform marker = vertexRects[index].Find("Marker");
            return marker != null ? marker.GetComponent<Image>() : null;
        }

        public void ApplyLayout()
        {
            float r = Mathf.Max(32f, diamondRadius);
            Vector2[] vertices =
            {
                new Vector2(0f, r),
                new Vector2(r, 0f),
                new Vector2(0f, -r),
                new Vector2(-r, 0f)
            };

            float side = r * Mathf.Sqrt(2f);
            float vertexSize = side * 0.20f;
            float midSize = side * 0.09f;
            float edgeThickness = side * 0.05f;

            if (glowRect != null)
            {
                glowRect.anchoredPosition = Vector2.zero;
                glowRect.sizeDelta = new Vector2(r * 2.4f, r * 2.4f);
            }

            PlaceEdges(vertices, side, edgeThickness);
            PlaceMidpoints(vertices, midSize);
            PlaceVertices(vertices, vertexSize);
        }

        private void PlaceEdges(Vector2[] vertices, float side, float thickness)
        {
            float[] angles = { -45f, 45f, -45f, 45f };
            for (int i = 0; i < 4; i++)
            {
                if (edgeRects[i] == null)
                {
                    continue;
                }

                Vector2 a = vertices[i];
                Vector2 b = vertices[(i + 1) % 4];
                edgeRects[i].anchoredPosition = (a + b) * 0.5f;
                edgeRects[i].localEulerAngles = new Vector3(0f, 0f, angles[i]);
                edgeRects[i].sizeDelta = new Vector2(side, thickness);
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
    }
}
