# 中世纪环境模型（2026-11-06）

使用 Comfy Cloud 的腾讯混元 3D 3.1 节点生成 PBR 静态建筑，再以 gltfpack 减面，转换为 Unity 可直接使用的 OBJ、Standard 材质和三级 LOD。原档保留在 ArtSource，运行时只引用处理后的网格和缩小贴图。

实际采用尖拱门廊、哥特束柱、石砌山墙民居三件，分别用于 1 处入口、10 根大厅柱、4 栋民居；木构民居继续参与街景组合。商人民居候选的 UV 显示异常，未采用，原件及无用工作流已清理，保留提示词和哈希以说明筛选结果。

[生产清单](production.json)包含实际提示词对应的工作流、云端任务 ID、原档哈希、尺寸与 LOD 面数。[场景检查](scene-checks.json)覆盖中央通道、两名卫兵出门寻路和三日视线。编辑器回归 141 项通过。建筑审阅截图临时隐藏人物。

- [大厅](Previews/Nave.jpg)
- [入口](Previews/Portal.jpg)
- [街景](Previews/Street.jpg)

复现时用 `comfy run --where cloud --workflow Tools/MedievalProduction/<模型>.json --allow-spend` 提交。该命令会消耗账户额度；通常直接使用已归档原件即可，无需再次生成。使用记录中的 gltfpack 1.3 生成三级 GLB，再运行 `Tools/MedievalProduction/prepare_model.py`；门廊近档以 0.4 比例及 0.005 误差、束柱近档以 0.28 比例及 0.005 误差减面；民居目标为 10000 / 3500 / 1000 面。具体输出面数及纹理尺寸见清单。

Unity 导入及预览入口为 `ThreeBodyProblem.Editor.MedievalAssetProduction.PrepareAndCapture`；场景放置和导航更新入口为 `ThreeBodyProblem.Editor.MedievalSceneProduction.ApplyAndReview`。在隔离工程中执行，核对结果后保存场景。混元原件适用平台内容条件；网格和生成纹理不按项目中 CC0 摄影素材的标识归类。

[Windows 构建体积检查](resource-build.json)：正式配置构建成功，零错误、零警告；新增场景资源后增加 14,308,886 字节（1.529%）。这项是新版美术的构建检查；先前 R2 ZIP 的真实模型全剧情回归另见 `Documentation/WindowsStoryQA/verified-20261106.json`，R2 ZIP 尚未包含本轮新模型。
