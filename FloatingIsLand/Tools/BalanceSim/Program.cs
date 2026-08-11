using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FloatingIsLand.Config;
using FloatingIsLand.Domain.Build;
using FloatingIsLand.Domain.Map;
using FloatingIsLand.Domain.Wind;

// 积分曲线仿真：用贪心 AI 玩家把一整局（Stage 表里的全部关卡）连着打完，
// 看得分产出能不能跟上 Level.unlockScore 的组解锁门槛与 Stage.clearScore 的通关门槛。
//
// 判据（GAME_DESIGN §2.2 结束条件）：
//   - 太难 = 玩家在很靠前的关/组就卡住，3 关根本走不完；
//   - 太易 = 每组门槛都大幅溢出，门槛形同虚设。
// 目标手感：多数局能打穿 3 关，且中后期分数余量不宽裕（留一点决策压力）。
//
// 分数**跨关保留**：每关的门槛都以「进入本关时的累计总分」为基线做增量，
// 所以仿真必须一关接一关地连着跑，不能各关独立算。

const int DefaultRuns = 30;
const string TablesRelPath = "Assets/Resources/Tables";

string root = DetectRoot();
if (root == null)
{
    Console.Error.WriteLine("[错误] 未能定位 Unity 工程根（需同时含 Assets/ 与 Tools/）。");
    return 1;
}

int runs = DefaultRuns;
for (int i = 0; i < args.Length - 1; i++)
{
    if (string.Equals(args[i], "--runs", StringComparison.OrdinalIgnoreCase))
    {
        int.TryParse(args[i + 1], out runs);
    }
}
if (runs <= 0)
{
    runs = DefaultRuns;
}

// 免费解锁：跳过分数门槛强行把每关的组走满，用来量「不被卡住时的得分曲线」。
// 定价必须以这条曲线为基准，否则就是拿一个自己卡住自己的样本去定门槛。
bool freeUnlock = args.Any(a => string.Equals(a, "--free-unlock", StringComparison.OrdinalIgnoreCase));

TableLoader.LoadFromDirectory(Path.Combine(root, ToPath(TablesRelPath)));

BuildRuleSet rules = BuildRuleSetFactory.Create();
List<LevelDef> levels = BuildRuleSetFactory.CreateLevels();
List<GroupThemeDef> themes = BuildRuleSetFactory.CreateGroupThemes();
List<StageDef> stages = BuildRuleSetFactory.CreateStages();

var maps = new Dictionary<int, MapSnapshot>();
foreach (StageDef stage in stages)
{
    string mapPath = Path.Combine(root, ToPath($"Assets/Resources/Maps/stage_{stage.StageId}.json"));
    if (!File.Exists(mapPath))
    {
        Console.Error.WriteLine($"[错误] 找不到第 {stage.StageId} 关地图 {mapPath}。");
        Console.Error.WriteLine("       先在 Unity 里跑 Tools → 地图 → 按岛屿模型生成地图。");
        return 1;
    }
    maps[stage.StageId] = MapJson.Load($"stage_{stage.StageId}", File.ReadAllText(mapPath));
}

Console.WriteLine($"[仿真] 规则集：{rules.Blueprints.Count} 个建筑变体 / {rules.Elements.Count} 类元素 / {themes.Count} 个建筑组主题。");
foreach (StageDef stage in stages)
{
    MapSnapshot m = maps[stage.StageId];
    Console.WriteLine(
        $"[仿真] 第 {stage.StageId} 关「{stage.NameCn}」：{m.Width}×{m.Length} 已刷 {m.PaintedCount} 格，"
        + $"{stage.GroupCount} 组，通关分 +{stage.ClearScore}，门槛倍率 ×{stage.UnlockScoreMult}");
}
Console.WriteLine($"[仿真] 跑 {runs} 局{(freeUnlock ? "（免门槛，量得分曲线）" : "")}。");
Console.WriteLine();

var games = new List<GameOutcome>(runs);
for (int seed = 1; seed <= runs; seed++)
{
    games.Add(SimulateGame(maps, rules, levels, themes, stages, seed, freeUnlock));
}

Report(games, levels, stages);
return 0;

// ---------------------------------------------------------------- 一整局（连打全部关卡）

static GameOutcome SimulateGame(
    Dictionary<int, MapSnapshot> maps, BuildRuleSet rules, List<LevelDef> levels,
    List<GroupThemeDef> themes, List<StageDef> stages, int seed, bool freeUnlock)
{
    var game = new GameOutcome { Seed = seed };
    int carry = 0;

    foreach (StageDef stage in stages)
    {
        StageOutcome stageOutcome = SimulateStage(
            maps[stage.StageId], rules, levels, themes, stage, seed, carry, freeUnlock);
        game.Stages.Add(stageOutcome);

        carry = stageOutcome.TotalScore;
        game.TotalScore = carry;
        game.StagesReached = stage.StageId;

        if (stageOutcome.Cleared)
        {
            game.StagesCleared++;
        }
        else if (!freeUnlock)
        {
            // 没达通关分就到此为止——后面的关根本进不去。
            // 但 --free-unlock 是用来量各关产能上限的，被通关门槛拦住就永远量不到后面的关，
            // 所以那个模式下照样往下跑。
            break;
        }
    }

    game.Completed = game.StagesCleared >= stages.Count;
    return game;
}

// ---------------------------------------------------------------- 一关

static StageOutcome SimulateStage(
    MapSnapshot map, BuildRuleSet rules, List<LevelDef> levels, List<GroupThemeDef> themes,
    StageDef stage, int seed, int carryScore, bool freeUnlock)
{
    // 每关一张新图、一套新风场；种子掺上关号，避免 3 关的随机序列完全一样
    int stageSeed = seed * 101 + stage.StageId * 7919;
    var board = new BuildBoard(map, rules);
    // 风场必须接：风帆的 windPath 建造限制、风力即时分、风车风力曲线全靠它。
    // 不接的话仿真会谎报「风帆一定放得下」且把它的风力分算成 0。
    var wind = new WindSystem(board, stageSeed);
    board.WindField = wind;
    var scoring = new ScoreEngine(board);
    var run = new BuildRunState(levels, themes, stage, stageSeed, carryScore);
    var random = new DeterministicRandom(stageSeed * 31 + 7);

    var outcome = new StageOutcome { StageId = stage.StageId, BaseScore = carryScore };
    run.Start();

    while (true)
    {
        // 二选一：挑「组内建筑当前都能放下」且预估分更高的那组
        if (run.Offers.Count > 0)
        {
            int best = 0;
            int bestScore = int.MinValue;
            for (int g = 0; g < run.Offers.Count; g++)
            {
                int score = EstimateGroup(board, scoring, rules, run.Offers[g], random);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = g;
                }
            }
            // 主题分布是分组设计的验收口径之一
            outcome.ThemeAtGroup[run.Level] = run.Offers[best].ThemeId;
            run.ChooseOffer(best);
        }

        // 逐栋摆放：每栋找当前最优落点
        int groupIncome = 0;
        int groupPlaced = 0;
        while (run.Hand.Count > 0)
        {
            BuildingBlueprint blueprint = rules.GetBlueprintOrNull(run.Hand[0]);
            if (blueprint == null)
            {
                run.ConsumeFromHand(0);
                continue;
            }

            Spot spot = FindBestSpot(board, scoring, blueprint, random);
            if (!spot.Found)
            {
                // 放不下就跳过并按 §4.3 扣分
                run.AddBuildScore(Tables.GameConfig.skipPenaltyScore);
                run.ConsumeFromHand(0);
                outcome.SkippedBuildings++;
                outcome.SkippedByVariant.TryGetValue(blueprint.VariantId, out int skipped);
                outcome.SkippedByVariant[blueprint.VariantId] = skipped + 1;
                continue;
            }

            board.Place(blueprint, spot.X, spot.Z, 0, spot.Rotation, spot.Score);
            if (WindSystem.AffectsWind(blueprint))
            {
                // 风帆转向 / 物流点延长风长会改写整张风场，后续落点的分数依赖新场
                wind.Recompute();
            }
            run.AddBuildScore(spot.Score);
            run.ConsumeFromHand(0);
            outcome.PlacedBuildings++;
            outcome.PlacedByVariant.TryGetValue(blueprint.VariantId, out int placed);
            outcome.PlacedByVariant[blueprint.VariantId] = placed + 1;
            groupIncome += Math.Max(0, spot.Score);
            groupPlaced++;
        }

        // 通关分是在第几组达成的——「达标就能走，不必建完全部组」这条要能量得出来
        if (outcome.ClearedAtGroup == 0 && run.IsStageCleared)
        {
            outcome.ClearedAtGroup = run.Level;
        }

        outcome.StageScoreAtGroup[run.Level] = run.StageScore;
        outcome.IncomeAtGroup[run.Level] = groupIncome;
        outcome.PlacedAtGroup[run.Level] = groupPlaced;

        if (run.IsLastGroup)
        {
            outcome.EndReason = $"本关 {run.TotalLevels} 组全部发完";
            break;
        }
        if (!freeUnlock && !run.CanAffordNextLevel())
        {
            outcome.EndReason = $"分数不足以解锁第 {run.Level + 1} 组（差 {run.NextUnlockScore - run.TotalScore}）";
            break;
        }

        if (freeUnlock)
        {
            run.AdvanceToNextLevel();
        }
        else
        {
            run.TryUnlockNextLevel();
        }
    }

    outcome.GroupsReached = run.Level;
    outcome.GroupTotal = run.TotalLevels;
    outcome.StageScore = run.StageScore;
    outcome.TotalScore = run.TotalScore;
    outcome.ClearScore = run.ClearScore;
    outcome.Cleared = run.IsStageCleared;
    return outcome;
}

/// <summary>估一组建筑当前能拿多少分（每栋各自找最优落点，不真的放）。</summary>
static int EstimateGroup(BuildBoard board, ScoreEngine scoring, BuildRuleSet rules, BuildingGroup group, DeterministicRandom random)
{
    int total = 0;
    for (int i = 0; i < group.VariantIds.Count; i++)
    {
        BuildingBlueprint blueprint = rules.GetBlueprintOrNull(group.VariantIds[i]);
        if (blueprint == null)
        {
            continue;
        }
        Spot spot = FindBestSpot(board, scoring, blueprint, random);
        total += spot.Found ? spot.Score : Tables.GameConfig.skipPenaltyScore;
    }
    return total;
}

/// <summary>
/// 找当前得分最高的合法落点。
/// 全图逐格试摆在 250×250 上太慢，改成随机采样候选格——贪心 AI 只是用来压曲线，
/// 不需要真最优，但采样必须确定性（同种子同结果），否则数值调整看不出因果。
/// </summary>
static Spot FindBestSpot(BuildBoard board, ScoreEngine scoring, BuildingBlueprint blueprint, DeterministicRandom random)
{
    const int Samples = 400;
    var best = new Spot();
    MapSnapshot map = board.Map;
    IReadOnlyList<MapCell> cells = map.Cells;
    if (cells.Count == 0)
    {
        return best;
    }

    for (int s = 0; s < Samples; s++)
    {
        MapCell cell = cells[random.NextInt(0, cells.Count)];
        var rotation = (Rotation)random.NextInt(0, 4);
        if (!board.CanPlace(blueprint, cell.X, cell.Z, 0, rotation).IsValid)
        {
            continue;
        }

        int score = scoring.Evaluate(blueprint, cell.X, cell.Z, 0, rotation).Total;
        if (!best.Found || score > best.Score)
        {
            best = new Spot { Found = true, X = cell.X, Z = cell.Z, Rotation = rotation, Score = score };
        }
    }
    return best;
}

// ---------------------------------------------------------------- 报告

static void Report(List<GameOutcome> games, List<LevelDef> levels, List<StageDef> stages)
{
    Console.WriteLine("== 单局结果 ==");
    Console.WriteLine("种子   通关关数   最终总分   止步于");
    foreach (GameOutcome g in games.Take(10))
    {
        StageOutcome last = g.Stages[g.Stages.Count - 1];
        string where = g.Completed
            ? "打穿全部关卡"
            : $"第 {last.StageId} 关第 {last.GroupsReached}/{last.GroupTotal} 组：{last.EndReason}";
        Console.WriteLine($"{g.Seed,-6} {g.StagesCleared,-10} {g.TotalScore,-10} {where}");
    }
    if (games.Count > 10)
    {
        Console.WriteLine($"…（共 {games.Count} 局，上面只列前 10 局）");
    }
    Console.WriteLine();

    int completed = games.Count(g => g.Completed);
    Console.WriteLine("== 汇总 ==");
    Console.WriteLine($"平均通关关数：{games.Average(g => g.StagesCleared):0.00} / {stages.Count}");
    Console.WriteLine($"整局通关率：  {completed} / {games.Count}（{100.0 * completed / games.Count:0.0}%）");
    Console.WriteLine($"平均最终总分：{games.Average(g => g.TotalScore):0}");
    Console.WriteLine();

    Console.WriteLine("== 每关 ==");
    Console.WriteLine("关卡  样本   通关率     到达组数   达标于第N组   本关得分   通关门槛(增量)   已建   跳过");
    foreach (StageDef stage in stages)
    {
        var samples = games
            .SelectMany(g => g.Stages)
            .Where(s => s.StageId == stage.StageId)
            .ToList();
        if (samples.Count == 0)
        {
            continue;
        }
        double clearRate = 100.0 * samples.Count(s => s.Cleared) / samples.Count;
        var clearedSamples = samples.Where(s => s.ClearedAtGroup > 0).ToList();
        string clearedAt = clearedSamples.Count > 0
            ? $"{clearedSamples.Average(s => s.ClearedAtGroup):0.0} / {stage.GroupCount}"
            : "—";
        Console.WriteLine(
            $"{stage.StageId,-5} {samples.Count,-6} {clearRate,-10:0.0}% {samples.Average(s => s.GroupsReached),-10:0.0} "
            + $"{clearedAt,-13} {samples.Average(s => s.StageScore),-10:0} {stage.ClearScore,-16} "
            + $"{samples.Average(s => s.PlacedBuildings),-6:0.0} {samples.Average(s => s.SkippedBuildings),-6:0.0}");
    }
    Console.WriteLine();

    foreach (StageDef stage in stages)
    {
        ReportStageGroups(games, levels, stage);
    }

    ReportThemes(games, levels);
    ReportVariantMix(games);
}

/// <summary>一关内每组的收支：本组得分 vs 下一组门槛，覆盖率就是难易度。</summary>
static void ReportStageGroups(List<GameOutcome> games, List<LevelDef> levels, StageDef stage)
{
    var samples = games.SelectMany(g => g.Stages).Where(s => s.StageId == stage.StageId).ToList();
    if (samples.Count == 0)
    {
        return;
    }

    Console.WriteLine($"== 第 {stage.StageId} 关 每组收支（门槛已乘 ×{stage.UnlockScoreMult}） ==");
    Console.WriteLine("组    样本   本组得分   本关累计   下一组门槛   余量     判定");
    for (int i = 0; i < levels.Count && i < stage.GroupCount; i++)
    {
        LevelDef level = levels[i];
        var reached = samples.Where(s => s.IncomeAtGroup.ContainsKey(level.Level)).ToList();
        if (reached.Count == 0)
        {
            continue;
        }

        LevelDef next = i + 1 < levels.Count && i + 1 < stage.GroupCount ? levels[i + 1] : null;
        int need = next != null ? stage.ScaleUnlockScore(next.UnlockScore) : 0;
        double income = reached.Average(s => s.IncomeAtGroup[level.Level]);
        double cumulative = reached.Average(s => s.StageScoreAtGroup[level.Level]);
        double margin = cumulative - need;

        string verdict;
        if (next == null)
        {
            verdict = "—";
        }
        else if (margin < 0)
        {
            verdict = "卡住（平均都过不去）";
        }
        else if (margin < income * 0.35)
        {
            verdict = "吃紧（余量不到一组产出的三成）";
        }
        else if (margin > income * 2.5)
        {
            verdict = "偏松";
        }
        else
        {
            verdict = "合理";
        }

        Console.WriteLine(
            $"{level.Level,-5} {reached.Count,-6} {income,-10:0} {cumulative,-10:0} {need,-12} {margin,-8:0} {verdict}");
    }
    Console.WriteLine();
}

/// <summary>每组选中的主题分布——验收「前期生产 / 中期居住商业 / 后期物流港口」的节奏。</summary>
static void ReportThemes(List<GameOutcome> games, List<LevelDef> levels)
{
    Console.WriteLine("== 每组选中的主题分布（全部关卡合计） ==");
    Console.WriteLine("组    样本   主题（次数）");
    foreach (LevelDef level in levels)
    {
        var picks = games
            .SelectMany(g => g.Stages)
            .Where(s => s.ThemeAtGroup.ContainsKey(level.Level))
            .Select(s => s.ThemeAtGroup[level.Level])
            .ToList();
        if (picks.Count == 0)
        {
            continue;
        }
        string detail = string.Join("  ", picks
            .GroupBy(t => t)
            .OrderByDescending(x => x.Count())
            .Select(x => $"{x.Key}×{x.Count()}"));
        Console.WriteLine($"{level.Level,-5} {picks.Count,-6} {detail}");
    }
    Console.WriteLine();
}

/// <summary>各建筑变体的落地/跳过统计——验收「核心建筑多、增幅建筑少」和放不下的风险。</summary>
static void ReportVariantMix(List<GameOutcome> games)
{
    var placed = new Dictionary<string, int>();
    var skipped = new Dictionary<string, int>();
    foreach (StageOutcome s in games.SelectMany(g => g.Stages))
    {
        foreach (var kv in s.PlacedByVariant)
        {
            placed.TryGetValue(kv.Key, out int n);
            placed[kv.Key] = n + kv.Value;
        }
        foreach (var kv in s.SkippedByVariant)
        {
            skipped.TryGetValue(kv.Key, out int n);
            skipped[kv.Key] = n + kv.Value;
        }
    }

    Console.WriteLine("== 建筑出现量（全部局全部关卡合计） ==");
    Console.WriteLine("变体              落地    跳过    每局落地");
    foreach (var kv in placed.Concat(skipped.Where(s => !placed.ContainsKey(s.Key)))
                 .OrderByDescending(kv => kv.Value))
    {
        placed.TryGetValue(kv.Key, out int ok);
        skipped.TryGetValue(kv.Key, out int no);
        Console.WriteLine($"{kv.Key,-17} {ok,-7} {no,-7} {(double)ok / games.Count,-8:0.0}");
    }
}

// ---------------------------------------------------------------- 辅助

static string DetectRoot()
{
    foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Assets"))
                && Directory.Exists(Path.Combine(dir.FullName, "Tools")))
            {
                return dir.FullName;
            }
        }
    }
    return null;
}

static string ToPath(string relative)
{
    return relative.Replace('/', Path.DirectorySeparatorChar);
}

internal struct Spot
{
    public bool Found;
    public int X;
    public int Z;
    public Rotation Rotation;
    public int Score;
}

/// <summary>一整局（连打全部关卡）的结果。</summary>
internal sealed class GameOutcome
{
    public int Seed;
    /// <summary>打穿了几关。</summary>
    public int StagesCleared;
    /// <summary>走到了第几关（含没通关的那一关）。</summary>
    public int StagesReached;
    /// <summary>最终累计总分（排行榜记的就是它）。</summary>
    public int TotalScore;
    public bool Completed;
    public readonly List<StageOutcome> Stages = new List<StageOutcome>();
}

/// <summary>一关的结果。</summary>
internal sealed class StageOutcome
{
    public int StageId;
    /// <summary>进入本关时的累计总分（本关一切门槛的基线）。</summary>
    public int BaseScore;
    public int GroupsReached;
    public int GroupTotal;
    /// <summary>本关内得到的分。</summary>
    public int StageScore;
    /// <summary>本关结束时的累计总分。</summary>
    public int TotalScore;
    /// <summary>通关门槛（累计分口径）。</summary>
    public int ClearScore;
    public bool Cleared;
    /// <summary>通关分是在本关第几组达成的；0 = 整关都没达标。</summary>
    public int ClearedAtGroup;
    public int PlacedBuildings;
    public int SkippedBuildings;
    public string EndReason = "";

    /// <summary>本关第 N 组选中的主题。</summary>
    public readonly Dictionary<int, string> ThemeAtGroup = new Dictionary<int, string>();
    /// <summary>本关第 N 组的正分收入。</summary>
    public readonly Dictionary<int, int> IncomeAtGroup = new Dictionary<int, int>();
    /// <summary>本关第 N 组结束时的本关累计分。</summary>
    public readonly Dictionary<int, int> StageScoreAtGroup = new Dictionary<int, int>();
    /// <summary>本关第 N 组落地的建筑数。</summary>
    public readonly Dictionary<int, int> PlacedAtGroup = new Dictionary<int, int>();
    public readonly Dictionary<string, int> PlacedByVariant = new Dictionary<string, int>();
    public readonly Dictionary<string, int> SkippedByVariant = new Dictionary<string, int>();
}
