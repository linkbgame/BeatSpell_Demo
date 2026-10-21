# Cursor 约束：第4轮魔法完整补完 plan 只读

请将下列内容复制为 `.cursor/rules/round4-spell-plan-frozen.mdc`（`alwaysApply: true`），或在 Cursor Rules 中手动添加等价规则。

---

```markdown
---
description: 第4轮魔法完整补完 plan 为只读基准，禁止 Agent 修改
alwaysApply: true
---

# 第4轮魔法完整补完 plan（只读）

`Assets/Docs/第4轮魔法完整补完plan.md` 为固定基准，**禁止**任何编辑、追加验收记录或更新 frontmatter todo。

- 开发前只**阅读**该文件，按「小步 1～6」推进。
- 进度与 MCP 验收写入 `Assets/Docs/项目跟进plan.md`。
- 用户要求改「第4轮 plan」时，改跟进文档，不改冻结文件。
```
