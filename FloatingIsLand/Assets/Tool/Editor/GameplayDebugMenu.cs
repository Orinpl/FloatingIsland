using System.Collections.Generic;
using System.Text;
using FloatingIsLand.App;
using FloatingIsLand.Domain.Build;
using FloatingIsLand.Domain.Map;
using FloatingIsLand.View;
using UnityEditor;
using UnityEngine;

namespace FloatingIsLand.EditorTools
{
    /// <summary>
    /// 局内调试菜单：不动鼠标也能把「开局 → 选组 → 选建筑 → 摆放 → 解锁下一组」跑一遍。
    ///
    /// 菜单项在 Play 模式下同样可用，所以它既是人工调试的快捷入口，
    /// 也是自动化验收建造链路的唯一手段——UI 点击没法脚本化，但这些入口可以。
    /// 只调 <see cref="GameSession"/> 的公开 API，不绕过任何领域校验。
    /// </summary>
    public static class GameplayDebugMenu
    {
        private const string Root = "Tools/调试/";

        [MenuItem(Root + "开始游戏（等同点主菜单开始）", false, 1)]
        public static void StartGame()
        {
            GameFlow flow = RequireFlow();
            if (flow == null)
            {
                return;
            }
            // StartGame 有状态守卫（只在 MainMenu 可用），Boot 还在加载配表时调用会被静默拒绝。
            // 不打出前后状态的话，看到的就是「调了但什么也没发生」，白白排查半天。
            GameStateId before = flow.CurrentStateId;
            flow.StartGame();
            if (flow.CurrentStateId == before && before != GameStateId.MainMenu)
            {
                Debug.LogWarning($"[调试] 开始游戏被拒绝：当前状态是 {before}，只有 MainMenu 能开局（Boot 阶段在加载配表，稍等再试）。");
                return;
            }
            Debug.Log($"[调试] 已请求开始游戏（{before} → {flow.CurrentStateId}）。");
        }

        [MenuItem(Root + "打印局内状态", false, 2)]
        public static void DumpState()
        {
            GameSession session = RequireSession();
            if (session == null)
            {
                return;
            }

            var text = new StringBuilder("[调试] 局内状态：\n");
            text.AppendLine($"  建造链路就绪：{session.IsBuildReady}");
            if (!session.IsBuildReady)
            {
                Debug.Log(text.ToString());
                return;
            }

            BuildRunState run = session.Run;
            text.AppendLine($"  等级 {run.Level}/{run.TotalLevels}  总分 {run.TotalScore}  金币 {run.Gold}");
            text.AppendLine($"  待选组 {run.Offers.Count} 个，手牌 {run.Hand.Count} 张，已建 {session.Board.Buildings.Count} 栋");
            text.AppendLine($"  地图 {session.Board.Map.Width}×{session.Board.Map.Length}，已刷 {session.Board.Map.PaintedCount} 格，元素 {session.Board.Elements.Count} 个");
            for (int i = 0; i < run.Hand.Count; i++)
            {
                BuildingBlueprint blueprint = session.Rules.GetBlueprintOrNull(run.Hand[i]);
                text.AppendLine($"    手牌[{i}] {run.Hand[i]}（{(blueprint != null ? blueprint.NameCn : "?")}）");
            }
            Debug.Log(text.ToString());
        }

        [MenuItem(Root + "选第一组建筑", false, 3)]
        public static void ChooseFirstOffer()
        {
            GameSession session = RequireSession();
            if (session == null)
            {
                return;
            }
            Debug.Log(session.ChooseOffer(0) ? "[调试] 已选第一组。" : "[调试] 当前不在选组阶段。");
        }

        [MenuItem(Root + "选中第一张手牌", false, 4)]
        public static void SelectFirstHandItem()
        {
            GameSession session = RequireSession();
            if (session == null)
            {
                return;
            }
            session.SelectHandItem(0);
            BuildingBlueprint blueprint = session.SelectedBlueprint;
            Debug.Log(blueprint != null
                ? $"[调试] 已选中 {blueprint.NameCn}（{blueprint.VariantId}）。"
                : "[调试] 手牌为空，没得选。");
        }

        /// <summary>
        /// 把当前选中的建筑摆到第一个合法落点上。扫描顺序固定，所以结果可复现。
        /// </summary>
        [MenuItem(Root + "把选中的建筑放到第一个合法位置", false, 5)]
        public static void PlaceSelectedAnywhere()
        {
            GameSession session = RequireSession();
            if (session == null || !session.IsBuildReady)
            {
                return;
            }

            BuildingBlueprint blueprint = session.SelectedBlueprint;
            if (blueprint == null)
            {
                Debug.LogWarning("[调试] 没有选中建筑，先跑「选中第一张手牌」。");
                return;
            }

            MapSnapshot map = session.Board.Map;
            for (int i = 0; i < map.Cells.Count; i++)
            {
                MapCell cell = map.Cells[i];
                for (int r = 0; r < 4; r++)
                {
                    var rotation = (Rotation)r;
                    PlacementCheck check;
                    ScoreBreakdown breakdown;
                    if (!session.TryPlaceSelected(cell.X, cell.Z, cell.Layer, rotation, out check, out breakdown))
                    {
                        continue;
                    }

                    var detail = new StringBuilder();
                    for (int line = 0; line < breakdown.Lines.Count; line++)
                    {
                        detail.Append(breakdown.Lines[line]).Append("  ");
                    }
                    Debug.Log($"[调试] {blueprint.NameCn} 已落地于 ({cell.X}, {cell.Z}) 朝向 {(int)rotation * 90}°，" +
                              $"得分 {breakdown.Total}：{detail}");
                    return;
                }
            }

            Debug.LogWarning($"[调试] 整张图都找不到 {blueprint.NameCn} 的合法落点。");
        }

        /// <summary>
        /// 把预览钉在「当前选中建筑的第一个合法落点」上，好让范围环 / 辉光 / 飘分三层表现显出来。
        ///
        /// 这三层只有光标悬在格子上才会出现，而鼠标位置没法脚本化——没有这个入口就只能靠人眼在
        /// Play 里凑。钉住之后 Game 视图截图里能直接看到效果。
        /// </summary>
        [MenuItem(Root + "钉住预览到第一个合法落点（看范围环与计分高亮）", false, 7)]
        public static void PinPreviewAtFirstValidCell()
        {
            GameSession session = RequireSession();
            if (session == null || !session.IsBuildReady)
            {
                return;
            }

            BuildingBlueprint blueprint = session.SelectedBlueprint;
            if (blueprint == null)
            {
                Debug.LogWarning("[调试] 没有选中建筑，先跑「选中第一张手牌」。");
                return;
            }

            var controller = Object.FindObjectOfType<BuildPlacementController>();
            if (controller == null)
            {
                Debug.LogWarning("[调试] 场景里没有 BuildPlacementController。");
                return;
            }

            // 从地图中心往外找，而不是按存储顺序取第一个：稀疏快照的头几格在岛缘的岩石堆里，
            // 环贴在地面上会被岩体整个埋掉，看着像"没生效"
            MapSnapshot map = session.Board.Map;
            var ordered = new List<MapCell>(map.Cells);
            int mapCenterX = map.Width / 2;
            int mapCenterZ = map.Length / 2;
            ordered.Sort((a, b) =>
            {
                int da = (a.X - mapCenterX) * (a.X - mapCenterX) + (a.Z - mapCenterZ) * (a.Z - mapCenterZ);
                int db = (b.X - mapCenterX) * (b.X - mapCenterX) + (b.Z - mapCenterZ) * (b.Z - mapCenterZ);
                return da != db ? da.CompareTo(db) : a.X != b.X ? a.X.CompareTo(b.X) : a.Z.CompareTo(b.Z);
            });

            for (int i = 0; i < ordered.Count; i++)
            {
                MapCell cell = ordered[i];
                if (!session.CheckSelectedPlacement(cell.X, cell.Z, cell.Layer, Rotation.Deg0).IsValid)
                {
                    continue;
                }

                // 钉的是**光标格**，控制器内部会自己换算回锚点格——跟真实鼠标走同一条路径，
                // 才能验到真实效果而不是另一条只在调试时成立的路。
                // 这里是 AnchorFromCenter 的逆运算（锚点 → 中心）。
                int centerX = cell.X + (blueprint.Footprint.SpanX(Rotation.Deg0) - 1) / 2;
                int centerZ = cell.Z + (blueprint.Footprint.SpanZ(Rotation.Deg0) - 1) / 2;
                controller.DebugPinPreview(centerX, centerZ, cell.Layer);

                ScoreBreakdown preview = session.PreviewSelectedScore(cell.X, cell.Z, cell.Layer, Rotation.Deg0);
                Debug.Log($"[调试] 预览已钉在 ({cell.X}, {cell.Z}) 层 {cell.Layer}：" +
                          $"{blueprint.NameCn} 半径 {blueprint.Radius} 格，预计得分 {(preview != null ? preview.Total : 0)}，" +
                          $"归因 {(preview != null ? preview.Attributions.Count : 0)} 条。" +
                          "看完跑「解除预览钉住」。");
                return;
            }

            Debug.LogWarning($"[调试] 整张图都找不到 {blueprint.NameCn} 的合法落点。");
        }

        [MenuItem(Root + "解除预览钉住", false, 8)]
        public static void UnpinPreview()
        {
            var controller = Object.FindObjectOfType<BuildPlacementController>();
            if (controller != null)
            {
                controller.DebugUnpinPreview();
                Debug.Log("[调试] 预览已交还给鼠标。");
            }
        }

        /// <summary>把当前手牌全部摆掉，再解锁下一组——一键推进一整级。</summary>
        [MenuItem(Root + "一键推进一级（摆完手牌 + 解锁下一组）", false, 6)]
        public static void AdvanceOneLevel()
        {
            GameSession session = RequireSession();
            if (session == null || !session.IsBuildReady)
            {
                return;
            }

            if (session.HasPendingOffers)
            {
                session.ChooseOffer(0);
            }

            int guard = 64; // 防手牌消耗不掉时死循环
            while (session.Run.Hand.Count > 0 && guard-- > 0)
            {
                session.SelectHandItem(0);
                int before = session.Run.Hand.Count;
                PlaceSelectedAnywhere();
                if (session.Run.Hand.Count == before)
                {
                    // 放不下就丢掉这张，避免卡住整个推进
                    session.Run.ConsumeFromHand(0);
                }
            }

            bool advanced = session.RequestNextGroup();
            Debug.Log($"[调试] 一级推进完成：等级 {session.Run.Level}，总分 {session.Run.TotalScore}，" +
                      $"金币 {session.Run.Gold}，已建 {session.Board.Buildings.Count} 栋，" +
                      $"{(advanced ? "已进入下一级并抽出新组" : "没有下一级了")}。");
        }

        private static GameFlow RequireFlow()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[调试] 需要先进入 Play 模式。");
                return null;
            }
            GameFlow flow = GameFlow.Instance;
            if (flow == null)
            {
                Debug.LogWarning("[调试] 找不到 GameFlow（当前是不是没从 Boot 场景启动？）。");
            }
            return flow;
        }

        private static GameSession RequireSession()
        {
            GameFlow flow = RequireFlow();
            if (flow == null)
            {
                return null;
            }
            if (flow.CurrentSession == null)
            {
                Debug.LogWarning("[调试] 当前没有局内会话，先跑「开始游戏」并等加载完成。");
                return null;
            }
            return flow.CurrentSession;
        }
    }
}
