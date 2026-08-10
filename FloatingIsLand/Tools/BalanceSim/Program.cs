using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FloatingIsLand.Config;
using FloatingIsLand.Domain.Build;
using FloatingIsLand.Domain.Map;
using FloatingIsLand.Domain.Wind;

// 积分曲线仿真：用贪心 AI 玩家把一局跑完，看金币产出能不能跟上 Level.unlockCost 的解锁曲线。
//
// 判据（GAME_DESIGN §2 结束条件之三：建筑组处理完但金币不足解锁下一等级 → 本局结束）：
//   - 太难 = 玩家在很靠前的等级就卡住，20 级根本走不完；
//   - 太易 = 每级金币都大幅溢出，解锁费用形同虚设。
// 目标手感：能走到 20 级附近，且中后期金币余量不宽裕（留一点决策压力）。

const int DefaultRuns = 30;
const string MapRelPath = "Assets/Resources/Maps/stage_1.json";
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

// 免费解锁：跳过金币门槛强行跑满 20 级，用来量「不被卡住时的收入曲线」——
// 定价必须以这条曲线为基准，否则就是拿一个自己卡住自己的样本去调价。
bool freeUnlock = args.Any(a => string.Equals(a, "--free-unlock", StringComparison.OrdinalIgnoreCase));

TableLoader.LoadFromDirectory(Path.Combine(root, ToPath(TablesRelPath)));

string mapPath = Path.Combine(root, ToPath(MapRelPath));
if (!File.Exists(mapPath))
{
    Console.Error.WriteLine($"[错误] 找不到地图 {mapPath}。");
    Console.Error.WriteLine("       先在 Unity 里跑 Tools → 地图 → 按岛屿模型生成地图。");
    return 1;
}

MapSnapshot map = MapJson.Load("stage_1", File.ReadAllText(mapPath));
BuildRuleSet rules = BuildRuleSetFactory.Create();
List<LevelDef> levels = BuildRuleSetFactory.CreateLevels();
List<GroupThemeDef> themes = BuildRuleSetFactory.CreateGroupThemes();

Console.WriteLine($"[仿真] 地图 stage_1：{map.Width}×{map.Length}，已刷 {map.PaintedCount} 格，元素 {map.Elements.Count} 个。");
Console.WriteLine($"[仿真] 规则集：{rules.Blueprints.Count} 个建筑变体 / {rules.Elements.Count} 类元素；跑 {runs} 局。");
Console.WriteLine();

var results = new List<RunOutcome>(runs);
for (int seed = 1; seed <= runs; seed++)
{
    results.Add(Simulate(map, rules, levels, themes, seed, freeUnlock));
}

Report(results, levels);
return 0;

// ---------------------------------------------------------------- 仿真主体

static RunOutcome Simulate(
    MapSnapshot map, BuildRuleSet rules, List<LevelDef> levels, List<GroupThemeDef> themes, int seed, bool freeUnlock)
{
    var board = new BuildBoard(map, rules);
    // 风场必须接：风帆的 windPath 建造限制、风力即时分、风车风力曲线全靠它。
    // 不接的话仿真会谎报「风帆一定放得下」且把它的风力分算成 0。
    var wind = new WindSystem(board, seed);
    board.WindField = wind;
    var scoring = new ScoreEngine(board);
    var run = new BuildRunState(levels, themes, rules, seed);
    var random = new DeterministicRandom(seed * 31 + 7);

    var outcome = new RunOutcome { Seed = seed };
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
            // 主题分布是本次改版的验收口径之一：前期该只出生产线，后期才该出物流/港口
            outcome.ThemeAtLevel[run.Level] = run.Offers[best].ThemeId;
            run.ChooseOffer(best);
        }

        // 逐栋摆放：每栋找当前最优落点
        bool placedAny = false;
        int levelIncome = 0;
        int levelPlaced = 0;
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
            levelIncome += Math.Max(0, spot.Score);
            levelPlaced++;
            placedAny = true;
        }

        outcome.GoldAtLevel[run.Level] = run.Gold;
        outcome.ScoreAtLevel[run.Level] = run.TotalScore;
        outcome.IncomeAtLevel[run.Level] = levelIncome;
        outcome.PlacedAtLevel[run.Level] = levelPlaced;

        if (run.Level >= run.TotalLevels)
        {
            outcome.EndReason = "走完 20 级";
            break;
        }
        if (!freeUnlock && !run.CanAffordNextLevel())
        {
            outcome.EndReason = "金币不足以解锁下一级";
            break;
        }
        if (!placedAny && run.Hand.Count == 0 && run.Offers.Count == 0 && outcome.PlacedBuildings == 0)
        {
            outcome.EndReason = "无处可建";
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

    outcome.FinalLevel = run.Level;
    outcome.TotalScore = run.TotalScore;
    outcome.FinalGold = run.Gold;
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

static void Report(List<RunOutcome> results, List<LevelDef> levels)
{
    Console.WriteLine("== 单局结果 ==");
    Console.WriteLine("种子   到达等级   总分      末金币   已建   跳过   结束原因");
    foreach (RunOutcome r in results.Take(10))
    {
        Console.WriteLine(
            $"{r.Seed,-6} {r.FinalLevel,-10} {r.TotalScore,-9} {r.FinalGold,-8} {r.PlacedBuildings,-6} {r.SkippedBuildings,-6} {r.EndReason}");
    }
    if (results.Count > 10)
    {
        Console.WriteLine($"…（共 {results.Count} 局，上面只列前 10 局）");
    }
    Console.WriteLine();

    double avgLevel = results.Average(r => r.FinalLevel);
    double avgScore = results.Average(r => r.TotalScore);
    int finished = results.Count(r => r.FinalLevel >= levels.Count);
    Console.WriteLine("== 汇总 ==");
    Console.WriteLine($"平均到达等级：{avgLevel:0.0} / {levels.Count}");
    Console.WriteLine($"平均总分：    {avgScore:0}");
    Console.WriteLine($"通关率：      {finished} / {results.Count}（{100.0 * finished / results.Count:0.0}%）");
    Console.WriteLine();

    Console.WriteLine("== 每级建造收入（定价基准） ==");
    Console.WriteLine("等级  本级平均收入   累计收入   本级建筑数   单栋均分   建议解锁价(累计60%)");
    double cumulative = 0;
    for (int i = 0; i < levels.Count; i++)
    {
        LevelDef level = levels[i];
        var samples = results.Where(r => r.IncomeAtLevel.ContainsKey(level.Level)).ToList();
        if (samples.Count == 0)
        {
            continue;
        }
        double income = samples.Average(r => r.IncomeAtLevel[level.Level]);
        double placed = samples.Average(r => r.PlacedAtLevel[level.Level]);
        cumulative += income;
        double perBuilding = placed > 0 ? income / placed : 0;
        // 建议价 = “走到本级为止的累计收入”的 60%，再减去前面已收的费用，
        // 即把总支出控制在总收入的 60% 上下，留 40% 作为容错与决策空间。
        double suggested = i + 1 < levels.Count ? cumulative * 0.6 : 0;
        Console.WriteLine($"{level.Level,-5} {income,-13:0} {cumulative,-11:0} {placed,-12:0.0} {perBuilding,-11:0} {suggested,-10:0}");
    }
    Console.WriteLine();

    // 判据用「本级收入 / 本级解锁价」的覆盖率，而不是拿累计金币比单级费用：
    // 金币只进不出，累计额必然远大于单级价，用它判难易永远得出“偏易”。
    Console.WriteLine("== 每级收支平衡 ==");
    Console.WriteLine("等级  本级收入   解锁下一级   覆盖率   结束时结余   判定");
    for (int i = 0; i < levels.Count; i++)
    {
        LevelDef level = levels[i];
        var samples = results.Where(r => r.GoldAtLevel.ContainsKey(level.Level)).ToList();
        if (samples.Count == 0)
        {
            continue;
        }

        LevelDef next = i + 1 < levels.Count ? levels[i + 1] : null;
        int need = next != null ? next.UnlockCost : 0;
        double income = samples.Average(r => r.IncomeAtLevel[level.Level]);
        double gold = samples.Average(r => r.GoldAtLevel[level.Level]);
        double coverage = need > 0 ? income / need : 0;

        string verdict;
        if (next == null)
        {
            verdict = "—";
        }
        else if (gold < need)
        {
            verdict = "卡住（结余不够解锁）";
        }
        else if (coverage < 1.0)
        {
            verdict = "吃老本（本级收入盖不住本级费用）";
        }
        else if (coverage > 1.8)
        {
            verdict = "偏松";
        }
        else
        {
            verdict = "合理";
        }

        Console.WriteLine($"{level.Level,-5} {income,-10:0} {need,-12} {coverage,-9:0.00} {gold,-11:0} {verdict}");
    }

    double totalIncome = 0;
    double totalCost = 0;
    for (int i = 0; i < levels.Count; i++)
    {
        var samples = results.Where(r => r.IncomeAtLevel.ContainsKey(levels[i].Level)).ToList();
        if (samples.Count > 0)
        {
            totalIncome += samples.Average(r => r.IncomeAtLevel[levels[i].Level]);
        }
        if (i > 0)
        {
            totalCost += levels[i].UnlockCost;
        }
    }
    Console.WriteLine($"总收入 {totalIncome:0}，总解锁支出 {totalCost:0}，支出占比 {100 * totalCost / Math.Max(1, totalIncome):0.#}%");
    Console.WriteLine();

    Console.WriteLine("== 结束原因分布 ==");
    foreach (var group in results.GroupBy(r => r.EndReason).OrderByDescending(g => g.Count()))
    {
        Console.WriteLine($"  {group.Key}：{group.Count()} 局");
    }
    Console.WriteLine();

    ReportThemes(results, levels);
    ReportVariantMix(results);
}

/// <summary>每级选中的主题分布——用来验收「前期生产 / 中期居住商业 / 后期物流港口」的节奏。</summary>
static void ReportThemes(List<RunOutcome> results, List<LevelDef> levels)
{
    Console.WriteLine("== 每级选中的主题分布 ==");
    Console.WriteLine("等级  样本   主题（次数）");
    foreach (LevelDef level in levels)
    {
        var picks = results
            .Where(r => r.ThemeAtLevel.ContainsKey(level.Level))
            .Select(r => r.ThemeAtLevel[level.Level])
            .ToList();
        if (picks.Count == 0)
        {
            continue;
        }
        string detail = string.Join("  ", picks
            .GroupBy(t => t)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key}×{g.Count()}"));
        Console.WriteLine($"{level.Level,-5} {picks.Count,-6} {detail}");
    }
    Console.WriteLine();
}

/// <summary>各建筑变体的落地/跳过统计——用来验收「核心建筑多、增幅建筑少」和放不下的风险。</summary>
static void ReportVariantMix(List<RunOutcome> results)
{
    var placed = new Dictionary<string, int>();
    var skipped = new Dictionary<string, int>();
    foreach (RunOutcome r in results)
    {
        foreach (var kv in r.PlacedByVariant)
        {
            placed.TryGetValue(kv.Key, out int n);
            placed[kv.Key] = n + kv.Value;
        }
        foreach (var kv in r.SkippedByVariant)
        {
            skipped.TryGetValue(kv.Key, out int n);
            skipped[kv.Key] = n + kv.Value;
        }
    }

    Console.WriteLine("== 建筑出现量（全部局合计） ==");
    Console.WriteLine("变体              落地    跳过    每局落地");
    foreach (var kv in placed.Concat(skipped.Where(s => !placed.ContainsKey(s.Key)))
                 .OrderByDescending(kv => kv.Value))
    {
        placed.TryGetValue(kv.Key, out int ok);
        skipped.TryGetValue(kv.Key, out int no);
        Console.WriteLine($"{kv.Key,-17} {ok,-7} {no,-7} {(double)ok / results.Count,-8:0.0}");
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

internal sealed class RunOutcome
{
    public int Seed;
    public int FinalLevel;
    public int TotalScore;
    public int FinalGold;
    public int PlacedBuildings;
    public int SkippedBuildings;
    public string EndReason = "";
    /// <summary>本级选中的那组来自哪个主题（验收分组节奏用）。</summary>
    public readonly Dictionary<int, string> ThemeAtLevel = new Dictionary<int, string>();
    /// <summary>各变体成功落地次数（验收组内配比用）。</summary>
    public readonly Dictionary<string, int> PlacedByVariant = new Dictionary<string, int>();
    /// <summary>各变体因放不下被跳过的次数（前期就给受地形限制的建筑，风险全在这一栏）。</summary>
    public readonly Dictionary<string, int> SkippedByVariant = new Dictionary<string, int>();
    public readonly Dictionary<int, int> GoldAtLevel = new Dictionary<int, int>();
    public readonly Dictionary<int, int> ScoreAtLevel = new Dictionary<int, int>();
    /// <summary>本级内的正分收入（=可转金币部分）。</summary>
    public readonly Dictionary<int, int> IncomeAtLevel = new Dictionary<int, int>();
    /// <summary>本级内成功落地的建筑数。</summary>
    public readonly Dictionary<int, int> PlacedAtLevel = new Dictionary<int, int>();
}
