# 剧情环境替换

第一批已写入 Build Settings 中的三个剧情场景。朝歌的 29 段城墙、墙脚、门堡及扶壁采用带收分的夯土截面。中世纪换入 8 栋民居、8 张长椅、10 个扶壁和 10 根束柱，观测台的 24 段矮墙采用既有石砌材质。城门通道和教堂中轴的原有通行空间用于校验新模型。

| 本机生成 | 用途 | 原始三角形 | 近档 / 远档 |
| --- | --- | ---: | ---: |
| MedievalRidge | 替换中世纪原有的 12 个拉伸岩块 | 49,882 | 2,099 / 1,199 |
| GobiStrata | 戈壁东侧岩层与中世纪两处低丘 | 60,432 | 2,004 / 1,344 |
| GothicGableRelief | 中世纪主殿门洞两侧的嵌壁石雕 | 56,704 | 1,726 / 818 |
| ShangGateBronze | 朝歌两扇门上的铸铜饰板 | 32,652 | 2,798 / 1,158 |
| ShangPendulumBearing | 七座巨摆的铜制轴座 | 62,488 | 2,236 / 836 |
| ShangAltarStone | 宗庙祭台上的石制供桌 | 51,660 | 2,656 / 1,168 |
| ChaogeLoessRidge | 朝歌河谷的六处远山坡面 | 30,364 | 2,094 / 972 |

远景复用项目的岩面材质，模型关闭投影并省去碰撞。民居和外扶壁分别使用 4,000 面和 1,500 面的网格，长椅使用 6,000 / 1,500 面两档。原始 GLB、ComfyUI 环境和模型权重留在本机共享目录及缓存，游戏只引用处理后的网格。

第一批的两个 GLB 在本机生成。Unity 重新打开场景后检查了实际网格引用、朝歌城门和中世纪通道的中心线、戈壁岩层露出地形的高度，以及三轮太阳的无遮挡方向。macOS 开发包从 1,293,971,473 字节增加到 1,302,179,987 字节，增幅约 0.63%。本机画面及构建记录在 `Builds/ComfyEnvironment/`。

原生长夜回归通过 39 项检查，覆盖复苏、走回石殿、出殿观星和再次登录中世纪。Apple M5 在 1600×900、90 FPS 上限下采样三个场景共 9 个视角，最慢视角的 P95 帧时间为 11.58 ms。

## 门楼和主殿

朝歌门楼的三片屋盖换成分层苇顶和木檩椽，桥面换成拼板，门扇有背撑和铜饰。沿用桥面和门扇原有碰撞。中世纪主殿有十处侧窗洞、尖拱入口、玫瑰窗、后殿大窗和板岩屋面。门窗洞按尺寸制作，玻璃独立于墙体碰撞。四座火盆复用 1,700 面的现有铁盆模型，三张运行时贴图限 512 像素。

第二批的 macOS 开发包为 1,305,490,949 字节，较第一批增加约 3.16 MiB（0.25%）。火盆贴图限尺寸后，比使用 2,048 像素贴图的同场景构建减少约 8.63 MiB。画面和实测记录在 `Builds/ComfyArchitecture/`。

Unity 重载场景后通过 43 项检查。原生包用角色控制器实走约 62 米穿过朝歌门楼，68 米进主殿，70 米出主殿，并确认三日能自动记入观察记录。最终包复跑长夜流程通过 39 项检查。Apple M5 在 1600×900、90 FPS 上限下重新采样 9 个视角，最高 P95 帧时间为 11.57 ms。

## 巨摆、复苏池和宗庙

七座巨摆改为分层石墩、双层木梁和铜轴座，保留原有摆锤支点和摆幅。复苏池换成有拼缝的木沿、石基和可行走的两级踏步，池边棚改为通透柱廊。宗庙主殿和两座侧殿有实心墙、门洞和格栅窗，主殿和祭台均可从前庭走上去。二十段大道边沟、十三面织旗、七座火盆、渠桥板面和石殿天窗盖也已更新。

第三批的 macOS 开发包为 1,265,735,690 字节，较上一批减少 37.91 MiB（3.05%）。粮篮、储物陶罐、水井、木桥和铜火盆的运行时贴图采用 512 或 1,024 像素上限，复用原图。Unity 重载场景后通过 141 项检查。实机的 11 项建筑路线检查和 39 项长夜检查全部通过。Apple M5 在 1600×900、90 FPS 上限下采样 9 个视角，最高 P95 帧时间为 11.57 ms。构建、实机报告和取景在 `Builds/ComfyCapital/`。

石殿专项的 76 项检查通过，覆盖传奏、历法推演和进入复苏池阶段。旧的 `-traveller-entry-check` 在第二、第三批开发包中返回相同的失败报告。该用例在呈奏结束前检查天窗，并在七人复苏完成前请求记录。两份入口对照及专项结果位于 `Builds/ComfyCapital/Native/`。

## 墙角、远山和随身沙漏

四处城墙转角使用同一件 76 面的收分夯土构件，带实际碰撞。河谷远山在本机生成后修成封底坡面，原有地形承托模型底部。三个旧式沙漏换成五层木匣，分别放在戈壁石桌和文王背上。随身版本使用静态木匣和沙堆，王宫的五层沙漏继续由历法演示控制。

远景山峰、雅丹和三十株河岸树木改用已有中远档网格。构建使用清单已确认排除 `GobiMountainPeak_Ready_LOD0`、`GobiRidge_Ready_LOD0`、`YardangMesa_Ready_LOD0` 和 `RiverbankBroadleaf_LOD0`。第四批开发包为 1,244,717,992 字节，比第三批减少 20.05 MiB，比本轮工作开始前减少 46.97 MiB（3.81%）。Unity 场景检查通过 167 项。画面、网格清单和实机记录在 `Builds/ComfyTerrain/`。

标题到结尾的实机脚本回归通过 58 项检查，最终进入 `Summary` 并记录五条观察。旧入口测试现已等候呈奏、复苏和崩溃阶段，光斑检查允许角色站在推演位置。建筑通行的 11 项检查通过。Apple M5 在 1600×900、90 FPS 上限下采样三个场景的九个取景，最高 P95 帧时间为 11.79 ms。最终记录位于 `Builds/ComfyTerrain/NativeFinal/`。

## 复现本地生成

本机验证使用 ComfyUI `4ac1c17`、Comfy CLI 1.25.0 和 Apple MPS。工作流使用 [Hunyuan3D 2.0 官方单图模板](https://docs.comfy.org/tutorials/3d/hunyuan3D-2)的节点，权重来自 [Comfy-Org 重打包仓库](https://huggingface.co/Comfy-Org/hunyuan3D_2.0_repackaged/tree/15c2937a842a1f8cb5cedc840790ef973b583e5f/split_files)。

1. 在 ComfyUI 输入目录建立 `three-body-environment/`。将本文目录下的 `Ready/Previews/Ready_01.jpg` 和 `Ready/Previews/Ready_05.jpg` 分别存为 `rock-reference-sheet.jpg` 和 `gothic-reference-sheet.jpg`。将仓库中的 `ArtSource/Environment/ComfyReferences/RoyalReliefReference.jpg` 存为 `royal-relief-reference.jpg`。同目录的 `pendulum-bearing-reference-v2.jpg`、`ancestral-altar-reference.jpg` 和 `loess-ridge-reference-front.jpg` 保持文件名。
2. 使用 `comfy run --where local --workflow Tools/ComfyEnvironment/MedievalRidge.json --wait` 运行山脊。更换工作流文件名可生成其他模型。原始 GLB 位于输出目录的 `three-body-environment/`。
3. 用 `python3 Tools/prepare_comfy_environment_mesh.py <GLB> Assets/_Project/Art/GeneratedMeshes/<名称>` 生成对应模型的两档 OBJ。需要 `trimesh`、`numpy` 和 `fast-simplification`。岩层加 `--triangles 2200 1500 --seal`，石雕加 `--triangles 2600 1600 --seal`，铜饰加 `--triangles 3400 1400 --plaque-front positive`，轴座加 `--triangles 2400 1100 --seal`，祭石加 `--triangles 3000 1500 --seal`，河谷远山加 `--triangles 2400 1100 --terrain`。封补另需 `scipy`、`scikit-image`，铜饰和地形修整需 `scipy`。
4. 在 Unity 运行菜单 `Tools → Three Body Traveller → Assets → Apply Environment Models`。已有模型引用会随 OBJ 更新重新导入。

审阅范围覆盖 Build Settings 中的戈壁、朝歌和中世纪三个游戏场景。场景内的旧路标和行囊已停用，沿路使用既有三档模型。剩余简单网格用于道路、田畦、水面、冰面、地台、织物、沙流、火焰和巨摆悬索，其材质和尺寸直接服务于对应表面或效果。完整网格使用清单记录在 `Builds/ComfyTerrain/mesh-inventory.json`。
