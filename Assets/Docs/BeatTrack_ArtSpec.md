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

预制体：`Assets/Art/UI/BeatTrack/Prefabs/DiamondBeatTrack.prefab`
