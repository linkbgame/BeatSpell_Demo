using BeatSpell.Prototype;
using BeatSpell.Rhythm;
using BeatSpell.Spell;
using UnityEngine;
using UnityEngine.UI;

namespace BeatSpell.UI
{
    /// <summary>
    /// 第 4 轮：在实际按键顶点留下咏唱/施法痕迹。Miss 不留痕。
    /// </summary>
    public class BeatInputMarkerView : MonoBehaviour
    {
        [SerializeField] private RhythmPrototypeBootstrap bootstrap;
        [SerializeField] private Image[] markerImages = new Image[4];
        [SerializeField] private Sprite chantSprite;
        [SerializeField] private Sprite castSprite;

        private SpellInputBuffer buffer;
        private readonly string[] markerKinds = { string.Empty, string.Empty, string.Empty, string.Empty };

        public void Bind(RhythmPrototypeBootstrap host)
        {
            Unsubscribe();
            bootstrap = host;
            Subscribe();
        }

        private void Awake()
        {
            if (bootstrap == null)
            {
                bootstrap = FindFirstObjectByType<RhythmPrototypeBootstrap>();
            }

            CacheMarkerImages();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public string GetMarkerKind(int vertexIndex)
        {
            return markerKinds[BeatTimingInfo.PositiveMod(vertexIndex, 4)];
        }

        public bool IsMarkerVisible(int vertexIndex)
        {
            int index = BeatTimingInfo.PositiveMod(vertexIndex, 4);
            return markerImages[index] != null && markerImages[index].enabled;
        }

        public void ClearMarkers()
        {
            for (int i = 0; i < markerImages.Length; i++)
            {
                HideMarker(i);
            }

            Debug.Log("[Track] MarkersCleared");
        }

        public void ShowMarkerForTest(int vertexIndex, BeatInputType type)
        {
            ApplyMarker(BeatTimingInfo.PositiveMod(vertexIndex, 4), type);
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
            if (entry.Grade == BeatTimingGrade.Miss)
            {
                return;
            }

            ApplyMarker(BeatTimingInfo.PositiveMod(entry.VertexIndex, 4), entry.InputType);
        }

        private void HandleCleared()
        {
            ClearMarkers();
        }

        private void ApplyMarker(int vertexIndex, BeatInputType type)
        {
            Image image = markerImages[vertexIndex];
            if (image == null)
            {
                return;
            }

            image.sprite = type == BeatInputType.Cast ? castSprite : chantSprite;
            image.enabled = true;
            markerKinds[vertexIndex] = type.ToString();
            Debug.Log($"[Track] Marker {type} vertex={vertexIndex} ({BeatTimingInfo.VertexName(vertexIndex)})");
        }

        private void HideMarker(int vertexIndex)
        {
            if (markerImages[vertexIndex] != null)
            {
                markerImages[vertexIndex].enabled = false;
            }

            markerKinds[vertexIndex] = string.Empty;
        }

        private void CacheMarkerImages()
        {
            if (markerImages == null || markerImages.Length != 4)
            {
                markerImages = new Image[4];
            }

            string[] names = { "Vertex_Top", "Vertex_Right", "Vertex_Bottom", "Vertex_Left" };
            Transform slots = transform.Find("VertexSlots");
            for (int i = 0; i < 4; i++)
            {
                if (markerImages[i] != null)
                {
                    continue;
                }

                Transform vertex = slots != null ? slots.Find(names[i]) : transform.Find(names[i]);
                Transform marker = vertex != null ? vertex.Find("Marker") : null;
                if (marker != null)
                {
                    markerImages[i] = marker.GetComponent<Image>();
                }
            }
        }
    }
}
