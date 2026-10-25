# 菱形音轨 RefPhoto 视觉对齐计划

以 [`Assets/Docs/RefPhoto`](.) 三张参考图为目视目标，在**不改变**第 4 轮玩法与节点组成（4 边 + 4 顶点 + 4 边中点 + BeatPulse + 留痕）的前提下，用已绑定的 `*_new` 素材与 Inspector 参数把尺寸调到满意。

规格与色板仍见 [`Assets/Docs/BeatTrack_ArtSpec.md`](../BeatTrack_ArtSpec.md)。冻结基准 [`第4轮魔法完整补完plan.md`](../第4轮魔法完整补完plan.md) 不修改；落地记录写 [`项目跟进plan.md`](../项目跟进plan.md)。

## 参考对照

| RefPhoto | 应对齐表现 |
|----------|------------|
| `菱形音轨_1.png` | BeatPulse（白半透明 + 放射） |
| `菱形音轨_2.png` | 咏唱留痕（黄 + 青边） |
| `全局参考图片.PNG` | 整体比例、边/顶点/边中点、施法黑痕、光晕 |

不含：角色、背景、侧栏技能、血条等 HUD。

## Inspector 调试项（`DiamondBeatTrackView`）

| 字段 | 作用 |
|------|------|
| `diamondRadius` | 菱形整体大小 |
| `spriteContentScale` | 新贴图满画布时的有效内容缩放（默认 0.832） |
| `vertexSizeRatio` | 顶点圆直径（默认 0.20 × 边长） |
| `midpointSizeRatio` | 边中点（默认 0.09） |
| `edgeThicknessRatio` | 边条厚度（默认 0.05） |
| `edgeStripLength` | 四条边 Rect 长度；**0**=自动（radius×√2），**>0** 固定（如 450，勿只改子 Edge 的 width） |
| `inputMarkerSizeRatio` | 留痕相对顶点（默认 0.85） |
| `beatPulseSizeRatio` | 当前拍白点相对顶点 Rect（默认 1） |

改比例后 **Edit Mode 即刷新**（`OnValidate` → `ApplyLayout`）。留痕仅 Play 中按键生成。

## 推荐调参顺序

1. `diamondRadius` → 整体占比  
2. `spriteContentScale` → 贴图有效内容  
3. `edgeThicknessRatio` / `vertexSizeRatio` / `midpointSizeRatio` → 边与点相对关系  
4. `inputMarkerSizeRatio` / `beatPulseSizeRatio` → 留痕与当前拍  

## 验收

- Edit Mode：改参后边、顶点、4 中点立即重排，Console 无错。  
- Play：BeatPulse 仍四顶点跳；留痕连续；判定逻辑不变。  
- 目视：中央菱形裁切与 RefPhoto 比例满意。
