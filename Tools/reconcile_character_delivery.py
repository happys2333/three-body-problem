"""Reconcile actual Unity-validated files with the character production targets."""
import csv
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DOC = ROOT / "Documentation/CharacterProduction"


def read(name):
    return json.loads((DOC / name).read_text())


def write(name, data):
    (DOC / name).write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n")


def main():
    imported = read("unity-character-validation.json")
    clips = {c["name"]: c for c in imported["clips"]}
    sources = read("mixamo-assets.json")
    paths = {c["id"]: c["unityFile"] for c in sources["clips"]}
    for name in ("LookSky", "WalkBackward", "PutDown", "Shiver"):
        paths["MX_" + name] = f"Assets/_Project/Characters/Mixamo/Derived/MX_{name}.anim"
    old = read("hunyuan-production-manifest.json")
    integration_path = DOC / "story-character-integration.json"
    integration = json.loads(integration_path.read_text()) if integration_path.exists() else {}
    integrated = integration.get("status") == "integrated_runtime_verified"
    crowd_path = DOC / "full-cast-integration.json"
    crowd = read("full-cast-integration.json") if crowd_path.exists() else {}
    crowd_integrated = crowd.get("status") == "all_scene_characters_runtime_verified"
    crowd_pairs = read("crowd-motion-validation.json")["combinations"] if crowd_integrated else []
    crowd_clips = {p["clip"] for p in crowd_pairs if p["passed"]}
    paths.update({c["id"]: c["unityRuntimeFile"] for c in old["generatedSharedAnimations"]})
    assert set(paths) == set(clips), "Import the complete library before reconciling."
    report_names = [f"unity-motion-retarget--{i}.json" for i in (0, 20, 40, 60, 80)]
    report_names += [f"unity-motion-retarget-{i}-0.json" for i in ("MX_Fear", "MX_Bow")
                     if (DOC / f"unity-motion-retarget-{i}-0.json").exists()]
    checks = {}
    for name in report_names:
        for result in read(name)["combinations"]:
            if result["clip"] in clips:
                checks[result["role"], result["clip"]] = result
    roles = [c["id"] for c in old["characters"] if c.get("prefabFile")]
    assert set(checks) == {(role, clip) for role in roles for clip in clips}, "Missing retarget evidence."
    assert all(c["passed"] for c in checks.values()), "Retarget checks failed."
    for source in sources["clips"]:
        for field in ("rawFile", "unityFile"):
            assert hashlib.sha256((ROOT / source[field]).read_bytes()).hexdigest() == source["sha256"]
        source.update(state="unity_humanoid_retarget_verified", validation={
            "humanoid": clips[source["id"]]["humanoid"], "seconds": clips[source["id"]]["seconds"],
            "looping": clips[source["id"]]["looping"], "verifiedRoles": roles})
    write("mixamo-assets.json", sources)

    # A clip can cover gross body movement while its story-specific acting/contact remains unfinished.
    coverage = {}
    def add(keys, ids, note="", partial=False):
        for key in keys.split():
            coverage[key] = (ids.split(), note, partial)
    add("IdleNeutral IdleBreathing Idle WaitForPlayer", "MX_Idle MX_WeightShift", "等候触发与看向玩家由剧情逻辑控制。")
    add("IdleGuard", "MX_WeightShift", "卫兵保持站岗朝向，循环重心变化；持矛与盾使用骨骼挂接。")
    add("WalkForward Walk LeadWalk", "MX_Walking", "路程与步速由移动逻辑控制。")
    add("WalkBackward Retreat", "MX_WalkBackward", "由前向行走反向派生；需要移动逻辑匹配。")
    add("StrafeLeft", "MX_LeftStrafeWalking MX_LeftStrafe")
    add("StrafeRight", "MX_RightStrafeWalking MX_RightStrafe")
    add("TurnLeft90", "MX_LeftTurn90", "玩家骨盆转角实测约 -89.9°；仍需角色朝向控制。")
    add("TurnRight90", "MX_RightTurn90", "玩家骨盆转角实测约 +103.2°，须调校后才能用于精确 90° 转向。", True)
    add("Turn180 Turn", "MX_LeftTurn MX_RightTurn", "玩家骨盆实测约 -174.5° / +173.5°；需逻辑校准方向。")
    add("Run", "MX_Running")
    add("Jog", "MX_Running", "只有奔跑模板；慢跑节奏与步幅仍需另调。", True)
    add("StopWalk", "MX_AdventureRunToStop", "这是奔跑急停，专用行走停止过渡仍未制作。", True)
    add("TalkCalm", "MX_TalkCalm")
    add("TalkExplain Explain", "MX_TalkCalm MX_HappyHandGesture", "通用交谈和手势；尚非按台词制作的讲解。", True)
    add("TalkAngry Anger", "MX_TalkArgue MX_AngryGesture")
    add("Listen", "MX_WeightShift MX_HeadNodYes", "中性站姿和点头可作倾听基础；专用倾听表演仍需调整。", True)
    add("Nod", "MX_HeadNodYes MX_LengthyHeadNod")
    add("ExcitedNod", "MX_HardHeadNod", "已有用力点头，兴奋程度需导演调整。", True)
    add("ShakeHead Reject", "MX_ShakingHeadNo MX_AnnoyedHeadShake")
    add("PointForward PointRoute", "MX_PointForward", "无实际路线目标约束；接入时需定位指向。")
    add("LookSky LookAtSky", "MX_LookSky", "只保留头颈观天，身体与手臂替换为自然待机；无持火把姿势。")
    add("Think Doubt", "MX_Think MX_ThoughtfulHeadShake")
    add("Question Debate SternSpeech", "MX_TalkArgue", "争辩模板；需按台词剪辑与调整强度。", True)
    add("Command Accuse", "MX_PointForward MX_AngryGesture", "指引/愤怒手势可作基础；专用命令或指控表演仍需制作。", True)
    add("Dismiss", "MX_DismissingGesture")
    if "MX_Bow" in clips:
        add("Bow", "MX_Bow", "通用正式鞠躬；尚未制作商朝或中世纪特定礼仪。", True)
    add("Kneel", "MX_KneelDown", "站立至下跪；地面高度需要在场景中对齐。")
    add("Pray", "MX_PrayStanding", "站姿祈祷，不是跪姿祈祷。")
    add("Blessing", "MX_PrayStanding MX_HappyHandGesture", "只有站姿祈祷和通用手势，专用赐福动作仍未制作。", True)
    add("CrouchStart CrouchIdle CrouchWalk Crouch StandFromCrouch", "MX_AdventureStandToCover MX_AdventureCrouchedSneakingLeft MX_AdventureCrouchedSneakingRight MX_AdventureCoverToStand", "掩体进入/退出及侧向蹲行；不能代表完整自由蹲伏移动组。", True)
    add("Jump", "MX_Jump")
    add("Land", "MX_AdventureHardLanding MX_AdventureFallingToRoll", "重落地与翻滚；起跳和碰地时机由游戏控制。")
    add("ReachInteract Interact", "MX_PickUp", "只有拾取身体模板；实际交互目标和手指接触未完成。", True)
    add("PickUp", "MX_PickUp", "身体动作已验证，道具位置与手指接触待制作。", True)
    add("PutDown PutDownBox", "MX_PutDown", "由单手拾取反向派生；双手放箱及物体接触需另做。", True)
    add("SitDown", "MX_SitDown", "需要座椅高度与位置对齐。")
    add("SitIdle", "MX_SitIdle", "需要座椅高度与位置对齐。")
    add("StandFromSeat StandUp", "MX_StandFromSeat", "坐姿至站起，需要座椅对齐。")
    add("CarryCalendarBox", "MX_CarryBoxIdle MX_CarryBoxWalk", "双手持箱身体模板；实际历法箱与手掌接触尚未接入。", True)
    add("OpenBox", "MX_OpenBox", "只含身体动作；实际箱盖铰链、手掌接触与时序待制作。", True)
    add("Write WriteJournal Sketch", "MX_WriteSeated", "坐姿书写身体模板；笔、纸、指骨和笔尖轨迹仍需制作。", True)
    add("Shiver", "MX_Shiver", "项目以待机叠加胸肩头颈微振动制作的 3 秒循环，并非 Mixamo 下载的发抖。")
    add("ShieldEyes", "MX_ShieldEyes")
    add("HeatExhaustion Exhaustion", "MX_RunToExhaustion MX_InjuredHurtingIdle", "奔跑疲惫/疼痛模板；专门高温中暑表演仍需制作。", True)
    add("Stagger", "MX_InjuredStumbleIdle MX_InjuredWalk", "受伤踉跄模板，可作身体失衡基础。")
    add("Collapse", "FallForward", "只有向前跌倒校正版；不能代表全部昏厥和倒地方向。", True)
    add("GetUp", "MX_GetUpProne", "只含趴倒后起身，尚无仰面起身。")
    add("Shock Worry", "MX_Fear", "恐惧模板，惊讶/忧虑的表演强度需调整。", True)
    add("Fear", "MX_Fear")
    add("Despair", "Despair")
    missing = {
        "StartWalk": "缺专用起步过渡；可用行走循环淡入，但尚未制作独立片段。",
        "PointSky": "缺空手举臂指天；空手观天仅含头颈动作，不能代替指天。",
        "PointAtSky": "缺空手举臂指天；观天片段不能代替指天。",
        "ReadBook": "未取得直接读书片段；持书、翻页与手指接触待制作。",
        "ReadJournal": "未取得直接阅读观察簿片段；持书与翻页待制作。",
        "ReadCalendar": "未取得专用读历法片段；历法道具和手部接触待制作。",
        "FlipHourglass": "翻沙漏需匹配真实道具、双手接触与指骨；当前骨架没有独立手指。",
        "PresentCalendar": "献历法的持物与递交表演尚未制作。",
        "CountThreeSuns": "精确手指计数尚未制作，当前骨架没有独立手指。",
        "InspectDevice": "检视仪器的目标约束、持物和精细动作尚未制作。",
    }
    rows = list(csv.DictReader((DOC / "ACTION_COVERAGE.csv").open(encoding="utf-8-sig")))
    fields = list(rows[0])
    support = {"商朝卫兵", "中世纪卫兵", "复苏平民男", "复苏平民女"}
    for row in rows:
        if row["角色或范围"] == "全部六位主要角色":
            continue
        target = row["动作需求"]
        if target in coverage:
            ids, note, partial = coverage[target]
            assert all(i in clips for i in ids)
            row.update({"实际状态": "部分覆盖／需剧情定制" if partial else "共享身体动作已验证",
                        "可用片段": ";".join(ids), "Unity文件": ";".join(paths[i] for i in ids),
                        "说明": "六位主要角色重定向通过；尚未接入主线。" + note})
            if integrated and row["角色或范围"] not in support:
                role_map = {"玩家／海人／哥白尼": "PlayerTraveller", "周文王": "WenWang", "纣王": "KingZhou", "教皇": "Pope", "学者": "Scholar", "达芬奇": "DaVinci"}
                role = role_map.get(row["角色或范围"])
                wired = integration["triggeredClipsByRole"]
                used = any(i in wired.get(role, []) for i in ids) if role else any(i in values for values in wired.values() for i in ids)
                row["说明"] = ("已有剧情触发；" if used else "资源可用，当前剧情未配置此动作触发；") + note
        else:
            row.update({"实际状态": "待定制", "可用片段": "", "Unity文件": "",
                        "说明": missing.get(target, "未取得符合该需求的片段。")})
        if row["角色或范围"] in support:
            if crowd_integrated:
                ids, note, partial = coverage.get(target, ([], missing.get(target, "未取得符合需求的专用片段。"), True))
                validated = [i for i in ids if i in crowd_clips]
                support_role = {"商朝卫兵":"ShangGuard", "中世纪卫兵":"ChurchGuard", "复苏平民男":"CitizenMale", "复苏平民女":"CitizenFemale"}[row["角色或范围"]]
                used = any(i in crowd["triggeredClipsByRole"].get(support_role, []) for i in ids)
                row["实际状态"] = "模型已入场；" + ("常用身体动作已验证" if validated else "共享模板待配角验证" if ids else "专用动作待制作")
                row["说明"] = ("已有运行时触发；" if used else "当前剧情未触发此动作；") + ("配角验证片段：" + ";".join(validated) + "。" if validated else "") + note
            else:
                row["实际状态"] = "角色待制作；" + row["实际状态"]
                row["说明"] += "支持角色模型尚未生成，未验证其骨架。"
    with (DOC / "ACTION_COVERAGE.csv").open("w", encoding="utf-8-sig", newline="") as file:
        writer = csv.DictWriter(file, fields); writer.writeheader(); writer.writerows(rows)
    plan = read("mixamo-collection-plan.json")
    plan.update(state="collection_completed_with_documented_custom_gaps",
                observedUI="User completed Adobe login; Mixamo showed a signed-in account. Downloaded through Chrome UI.",
                deliveredSourceClips=len(sources["clips"]), deliveredDerivedClips=4,
                sourceManifest="Documentation/CharacterProduction/mixamo-assets.json")
    for target in plan["targets"]:
        ids, note, partial = coverage.get(target["id"], ([], missing.get(target["id"], "未取得符合需求的片段。"), True))
        target.update(state=("partial_body_template_custom_work_pending" if partial else "body_motion_retarget_verified") if ids else "custom_motion_pending",
                      actualClips=ids, note=note)
    write("mixamo-collection-plan.json", plan)
    old.update(status="all_scene_characters_runtime_verified" if crowd_integrated else "six_models_integrated_first_person_player_runtime_verified" if integrated else "six_models_and_expanded_shared_motion_library_verified_story_integration_pending",
               continuationBlocker=None,
               continuationResult="User authorized Mixamo and completed Adobe login. The acquired library supplements the exhausted Hunyuan quota.",
               supplementaryMotionManifest="Documentation/CharacterProduction/mixamo-assets.json",
               motionLimitations=["Counts describe named clips/variants, not unique acting categories. See ACTION_COVERAGE.csv for exact and partial coverage.",
                                  "No individual finger or facial bones; prop contact, page turning, hourglass handling and lip sync remain unfinished.",
                                  "Pope robe/cape stretches and clips; skin-weight/cloth cleanup is required.",
                                  "Root motion is disabled; movement, heading, chairs and props require game/scene alignment.",
                                  "Story scenes retain their original placeholder actors; the library is usable in the separate preview scene."])
    if integrated:
        old["sceneIntegration"] = "Six main character models integrated into the three story scenes; the player uses a stable first-person camera and a headless visible body. Guards and citizens retain placeholders."
        old["motionLimitations"][-1] = "Only authored story cues are connected; the full 99-clip library is separately previewable. Guards/citizens, precise prop handling and facial performance remain unfinished."
        old["storyIntegrationReport"] = "Documentation/CharacterProduction/story-character-integration.json"
    if crowd_integrated:
        old["sceneIntegration"] = "All 36 character instances across three story scenes use textured Humanoid models. The 27 supporting actors use nine variants from four source bodies; the first-person player is preserved."
        old["motionLimitations"][-1] = "Only authored cues are connected; 45 supporting variant/motion pairs cover five runtime motions. Special guard salutes/arrest, precise prop contact and facial performance remain unfinished."
        old["crowdIntegrationReport"] = "Documentation/CharacterProduction/full-cast-integration.json"
        variants = read("crowd-library-validation.json")["variants"]
        model_paths = {s["id"]:s["unityFile"] for s in read("crowd-model-sources.json")["models"]}
        model_paths["PlayerTraveller"] = "Assets/_Project/Characters/Hunyuan/PlayerTraveller/PlayerTraveller_Rig.fbx"
        for actor in old["characters"]:
            matches = [v for v in variants if v["role"] == actor["id"]]
            if not matches:
                continue
            actor.update(state="textured_humanoid_integrated_runtime_verified", prefabFiles=[v["prefab"] for v in matches],
                         modelFiles=sorted({model_paths[v["source"]] for v in matches}),
                         animationFiles=[paths[i] for i in sorted(crowd_clips)],
                         sourceManifest="Documentation/CharacterProduction/crowd-model-sources.json",
                         retargetVerifiedClips=sorted(crowd_clips),
                         note="Shared source bodies with equipment, cloth-palette and height variations; not newly generated unique Hunyuan characters. Special action wishlist remains a wishlist.")
        old["completedAssets"] += [v["prefab"] for v in variants]
    old["validation"].update(report="Documentation/CharacterProduction/character-motion-validation-summary.json",
                              combinations=len(checks), passed=sum(c["passed"] for c in checks.values()))
    for actor in old["characters"]:
        if actor.get("prefabFile"):
            actor["retargetVerifiedClips"] = sorted(clips)
    old["completedAssets"] = list(dict.fromkeys(old["completedAssets"] + list(paths.values()) +
        ["Assets/_Project/Scenes/CharacterPreview/CharacterMotionGallery.unity"]))
    write("hunyuan-production-manifest.json", old)
    summary = {
        "date": "2026-10-27", "models": len(roles), "roles": roles,
        "hunyuanSourceClips": 6, "mixamoSourceClips": len(sources["clips"]), "mixamoDerivedClips": 4,
        "namedClipsAndVariants": len(clips), "uniqueActingCategories": "Not asserted; packs contain variants of the same action.",
        "verifiedCombinations": len(checks), "passedCombinations": sum(c["passed"] for c in checks.values()),
        "sampledPosesPerCombination": 5, "reportFiles": report_names,
        "scope": "Humanoid bone motion, baked skin deformation, finite vertices and bounds; corrected Hunyuan fall ends within 15cm of ground.",
        "limitations": old["motionLimitations"], "sceneIntegration": old["sceneIntegration"],
        "derived": {
            "MX_LookSky": "Head/neck curves from MX_LookUpTorchBase plus neutral MX_Idle body and arms.",
            "MX_WalkBackward": "MX_Walking curves reversed in time.",
            "MX_PutDown": "MX_PickUp curves reversed in time; prop/finger contact unfinished.",
            "MX_Shiver": "Project-authored chest/shoulder/neck micro-oscillations over MX_Idle; not a downloaded shiver clip."
        },
        "turnMeasurementsDegrees": {"MX_LeftTurn90": -89.89, "MX_RightTurn90": 103.19,
                                   "MX_LeftTurn": -174.51, "MX_RightTurn": 173.50},
        "previewScene": "Assets/_Project/Scenes/CharacterPreview/CharacterMotionGallery.unity",
    }
    previous = read("character-motion-validation-summary.json") if (DOC / "character-motion-validation-summary.json").exists() else {}
    for key in ("runtimeGalleryQA", "editorRestoration", "evidence"):
        if key in previous:
            summary[key] = previous[key]
    if integrated:
        summary["storyIntegrationReport"] = "Documentation/CharacterProduction/story-character-integration.json"
        summary["editorRestoration"] = {"currentScene": "Assets/_Project/Scenes/Traveller/01_GobiJourney.unity",
                                        "isPlaying": False, "isDirty": False,
                                        "storyScenesSavedWithNewCharacters": True}
        summary["evidence"] = list(dict.fromkeys(summary.get("evidence", []) + integration["evidence"]))
    if crowd_integrated:
        summary["supportingCast"] = {"instances":27, "variants":9, "sourceBodies":4,
                                     "verifiedVariantMotionPairs":len(crowd_pairs),
                                     "report":"Documentation/CharacterProduction/full-cast-integration.json"}
    write("character-motion-validation-summary.json", summary)
    with (DOC / "MOTION_CATALOG.csv").open("w", encoding="utf-8-sig", newline="") as file:
        writer = csv.writer(file); writer.writerow(["动作编号", "来源", "秒", "循环", "Unity文件", "六位角色验证"])
        for name, clip in sorted(clips.items()):
            source = "项目派生" if name in ("MX_LookSky", "MX_WalkBackward", "MX_PutDown", "MX_Shiver") else "Mixamo" if name.startswith("MX_") else "混元"
            writer.writerow([name, source, round(clip["seconds"], 4), clip["looping"], paths[name], "通过"])
    print(json.dumps({"models": len(roles), "clips": len(clips), "checkedPairs": len(checks), "coverageRows": len(rows)}))


if __name__ == "__main__":
    main()
