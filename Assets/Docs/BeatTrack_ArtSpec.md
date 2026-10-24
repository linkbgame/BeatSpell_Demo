# 菱形音轨美术规格（第 4 轮）

对照 `Assets/Docs/RefPhoto` 三张反馈图吸管定稿。仅覆盖中央菱形音轨，不含侧栏技能、血条、角色与背景。

## 参考对照

| 文件 | 用途 |
|------|------|
| `菱形音轨_1.png` | 当前拍：白半透明底 + 放射短线 |
| `菱形音轨_2.png` | 咏唱痕迹：黄实心底 + 青蓝包边 |
| `全局参考图片.PNG` | 施法痕迹：近黑实心底 + 青蓝包边；整体比例与主色 |

## 色板（定稿）

| 名称 | Hex / RGBA | 来源 |
|------|------------|------|
| `TrackCyan` | `#5AEBEE`（90, 235, 238） | 三图青蓝像素均值 |
| `VertexGreyFill` | `#C8CDD2` Alpha 32% | 规格：灰底 25%–40% |
| `BeatWhiteFill` | `#FAF9FB` Alpha 62% | `_1` 白色均值，规格 50%–70% |
| `ChantYellow` | `#F6F41D`（246, 244, 29） | `_2` 黄点均值 |
| `CastBlack` | `#1E1E1E` | 规格近黑 |
| `GlowRing` | `#5AEBEE` Alpha 18%–35% | 装饰，不参与交互 |

## 相对尺寸（菱形外接边长 = 1）

| 元素 | 比例 | 半径 360 UI 时约等于 |
|------|------|----------------------|
| 顶点圆直径 | 0.20 | 102 |
| 边中点圆直径 | 0.09 | 46 |
| 边线粗细 | 0.05 | 25 |
| 当前拍放射短线外扩 | 0.30 | 153 |

顶点坐标：`(0, r)`, `(r, 0)`, `(0, -r)`, `(-r, 0)`。边中点为相邻顶点中点。

## 第 4 轮呈现规则

- **当前拍**：只在四个顶点离散跳动（`VertexIndex = beatIndex % 4`），不在边中点显示白点。
- **输入留痕**：`beatFloat = beatIndex + deltaMs / (1000 * secondsPerBeat)`，再对 4 取模后在相邻顶点间 `Lerp`。不吸附 4/8 个预设槽。
- 示例（半径 360，BPM 120）：`0/0ms` → Top `(0,360)`；`0/+250ms` → Top→Right 中点 `(180,180)`；`1/0ms` → Right `(360,0)`。

## Sprite 清单

目录：`Assets/Art/UI/BeatTrack/Sprites/`  
导入：Sprite (2D and UI)，PPU **100**，Mesh Type **Full Rect**，Filter **Bilinear**。

| 文件 | 像素 | 用途 |
|------|------|------|
| `track_edge.png` | 512×64 | 单条边，Prefab 旋转 45°×4 |
| `track_midpoint.png` | 128×128 | 实心青蓝小圆 |
| `track_vertex_idle.png` | 256×256 | 灰透明底 + 青蓝圈 |
| `track_vertex_beat.png` | 256×256 | 白透明底 + 放射短线 |
| `track_marker_chant.png` | 256×256 | 黄实心 + 青蓝包边 |
| `track_marker_cast.png` | 256×256 | 黑实心 + 青蓝包边 |
| `track_glow_ring.png` | 512×512 | 背后同心光晕（可选） |

### 正式绑定（`*_new`，2026-03）

`DiamondBeatTrack.prefab` 已指向下列资源；导入：**Single**、**Full Rect**、Pivot **Center**、PPU **100**。`track_edge_new` 平台 Max Size **4096**（原图宽 3825）。`track_glow_ring.png` 仍为旧资源。

| 文件 | 像素 | 用途 |
|------|------|------|
| `track_edge_new.png` | 3825×134 | 菱形四边条带 |
| `track_midpoint_new.png` | 741×741 | 边中点 |
| `track_vertex_idle_new.png` | 740×741 | 顶点 idle |
| `track_vertex_beat_new.png` | 741×741 | 当前拍 BeatPulse；**PMA 贴图** + `BeatPulsePremultiplied` 材质 |
| `track_marker_chant_new.png` | 741×741 | 咏唱留痕 |
| `track_marker_cast_new.png` | 741×742 | 施法留痕 |

布局：`DiamondBeatTrackView.spriteContentScale` 默认 **0.832**（新图满画布，对齐旧版约 83% 画内占比）。可调至 1 若美术加透明边。

预制体：`Assets/Art/UI/BeatTrack/Prefabs/DiamondBeatTrack.prefab`
