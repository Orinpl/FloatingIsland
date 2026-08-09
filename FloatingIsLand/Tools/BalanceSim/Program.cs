using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using FloatingIsLand.Config;
using FloatingIsLand.Domain.Build;
using FloatingIsLand.Domain.Map;

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

Console.WriteLine($"[仿真] 地图 stage_1：{map.Width}×{map.Length}，已刷 {map.PaintedCount} 格，元素 {map.Elements.Count} 个。");
Console.WriteLine($"[仿真] 规则集：{rules.Blueprints.Count} 个建筑变体 / {rules.Elements.Count} 类元素；跑 {runs} 局。");
Console.WriteLine();

var results = new List<RunOutcome>(runs);
for (int seed = 1; seed <= runs; seed++)
{
    results.Add(Simulate(map, rules, levels, seed));
}

Report(results, levels);
return 0;

// ---------------------------------------------------------------- 仿真主体

static RunOutcome Simulate(MapSnapshot map, BuildRuleSet rules, List<LevelDef> levels, int seed)
{
    var board = new BuildBoard(map, rules);
    var scoring = new ScoreEngine(board);
    var run = new BuildRunState(levels, rules, seed);
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
            run.ChooseOffer(best);
        }

        // 逐栋摆放：每栋找当前最优落点
        bool placedAny = false;
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
                continue;
            }

            board.Place(blueprint, spot.X, spot.Z, 0, spot.Rotation, spot.Score);
            run.AddBuildScore(spot.Score);
            run.ConsumeFromHand(0);
            outcome.PlacedBuildings++;
            placedAny = true;
        }

        outcome.GoldAtLevel[run.Level] = run.Gold;
        outcome.ScoreAtLevel[run.Level] = run.TotalScore;

        if (run.Level >= run.TotalLevels)
        {
            outcome.EndReason = "走完 20 级";
            break;
        }
        if (!run.CanAffordNextLevel())
        {
            outcome.EndReason = "金币不足以解锁下一级";
            break;
        }
        if (!placedAny && run.Hand.Count == 0 && run.Offers.Count == 0 && outcome.PlacedBuildings == 0)
        {
            outcome.EndReason = "无处可建";
            break;
        }

        run.TryUnlockNextLevel();
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

    Console.WriteLine("== 每级金币余量 vs 解锁费用 ==");
    Console.WriteLine("等级  解锁下一级需要   本级结束时平均金币   余量      判定");
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
        double avgGold = samples.Average(r => r.GoldAtLevel[level.Level]);
        double margin = avgGold - need;

        string verdict;
        if (next == null)
        {
            verdict = "—";
        }
        else if (margin < 0)
        {
            verdict = "偏难（多数局卡在这里）";
        }
        else if (need > 0 && margin > need * 1.5)
        {
            verdict = "偏易（费用形同虚设）";
        }
        else
        {
            verdict = "合理";
        }

        Console.WriteLine($"{level.Level,-5} {need,-16} {avgGold,-20:0} {margin,-9:0} {verdict}");
    }
    Console.WriteLine();

    Console.WriteLine("== 结束原因分布 ==");
    foreach (var group in results.GroupBy(r => r.EndReason).OrderByDescending(g => g.Count()))
    {
        Console.WriteLine($"  {group.Key}：{group.Count()} 局");
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
    public readonly Dictionary<int, int> GoldAtLevel = new Dictionary<int, int>();
    public readonly Dictionary<int, int> ScoreAtLevel = new Dictionary<int, int>();
}
