# 当前进度：新增点击肚子收手护肚

- 沿用确认的正面姿势稿，新增内置 imagegen 透明动作素材，位于 `assets/reference/belly-click/production-sheet-v1.png`。
- 新动作 `belly`：78 帧、30 fps、2.6 秒；抬手收拢、贴腹停留、放手回正。
- 点击白肚皮触发护肚；头部仍触发摸头，手臂仍触发挥手。护肚重复点击继续当前播放，不重置、不排队；拖动沿用原有手势判定。
- 右键「播放动作」新增「收手护肚」。
- 通过手掌与手指对应点配准、局部光流插帧，固定正面脸部和脚底，首尾采用同一待机图。护肚每帧选用一张配准表面，减少手掌叠影。
- 预览：`assets/reference/belly-click/收手护肚.gif`；关键帧：`artifacts/belly/contact-sheet.png`。
- 验证通过：48 项行为检查、466 项素材交付检查、43 项发布版真实窗口检查。护肚脚底基线固定为 y=469，包含重复点击、贴腹停留、回待机与右键菜单验证。
- 验证记录：`artifacts/delivery-validation.json`、`artifacts/published-smoke/smoke-test.json`；真实窗口预览：`artifacts/published-smoke/belly-window-preview.png`。

# 上一轮进度：按原 GIF 校准造型并复现动作

已完成三张基础校准造型接入、冷眼和转身中间姿态制作、其他四动作体型同步、程序更新与 Windows x64 发布验证。

- 摸头：174 帧、30 fps、5800 ms；300 ms 伸手 + 原 4700 ms 反应 + 800 ms 放手回正。
- 动作：被摸头、冷眼、向画面右侧转身、收手、侧身停留、回到正面。
- 连续性：统一 420 像素角色尺度、脚底锚点 (256,470)；实际 174 帧脚底基线 469–471；首尾与待机一致。
- 已移除闭眼享受和下半身像素锁定；身体、腿脚随转身变化。
- 保留头部触发、重复点击继续、身体挥手与拖动。摸头期间点击区域跟随角色，伸入的手不成为独立点击目标。
- 验证通过：38 项行为检查、225 项素材交付检查、35 项发布版真实窗口检查，包含 5.6 秒仍在回正、5.8 秒后恢复待机。
- 更新 EXE、便携包、源码素材包、生成提示词、GIF 和原帧对照图。源码包含预览与验证记录。

主预览：`assets/reference/pet/摸头-连贯全身.gif`。
原帧比较：`artifacts/pet/original-comparison.png`。
生成提示词：`assets/reference/calibrated/prompts.json`（内置 imagegen）。
发布记录：`artifacts/published-smoke/smoke-test.json`。
独立素材检查：`artifacts/delivery-validation.json`。

还原范围：造型按原 GIF 可见区域校准，角色重建与姿态插值仍可能与原电影模型存在轮廓、材质和运动细节差异；未展示的腿脚为补画，不能宣称全身逐像素复刻。当前已验证单显示器 96 DPI 环境；多屏混合缩放尚未实机验收。
