using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace FloatingIsLand.App
{
    /// <summary>排行榜的一条记录：昵称 + 分数，外加一个「是否通关」的标记。</summary>
    [Serializable]
    public sealed class LeaderboardEntry
    {
        /// <summary>玩家昵称。</summary>
        public string name = "";

        /// <summary>整局累计总分。</summary>
        public int score;

        /// <summary>是否打穿了全部关卡。</summary>
        public bool completed;

        /// <summary>止步/打穿在第几关（未通关时用来显示「第 N 关止步」）。</summary>
        public int stageReached;
    }

    /// <summary>
    /// 本地排行榜（<c>Application.persistentDataPath/leaderboard.json</c>）。
    ///
    /// 只有排行榜是持久的：局内进度**不做保存**，退出即销毁（用户要求）。
    /// 所以这里没有任何「续玩」概念，写进来的都是已经结束的整局成绩。
    /// 读盘失败一律当空榜处理并留一条警告——排行榜坏掉不该让玩家进不了游戏。
    /// </summary>
    public static class Leaderboard
    {
        /// <summary>榜单最多保留多少条（超出的低分丢弃）。</summary>
        public const int Capacity = 50;

        private const string FileName = "leaderboard.json";

        [Serializable]
        private sealed class Payload
        {
            public List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
        }

        private static List<LeaderboardEntry> _cache;

        private static string FilePath
        {
            get { return Path.Combine(Application.persistentDataPath, FileName); }
        }

        /// <summary>读全部记录，已按分数从高到低排好；没有记录返回空列表。</summary>
        public static IReadOnlyList<LeaderboardEntry> Load()
        {
            if (_cache != null)
            {
                return _cache;
            }

            _cache = new List<LeaderboardEntry>();
            try
            {
                if (File.Exists(FilePath))
                {
                    Payload payload = JsonUtility.FromJson<Payload>(File.ReadAllText(FilePath));
                    if (payload?.entries != null)
                    {
                        foreach (LeaderboardEntry entry in payload.entries)
                        {
                            if (entry != null)
                            {
                                _cache.Add(entry);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // 榜单坏了就当空榜，不能因此拦住玩家进游戏
                Debug.LogWarning($"[排行榜] 读取失败，按空榜处理：{e.Message}");
                _cache.Clear();
            }

            Sort(_cache);
            return _cache;
        }

        /// <summary>
        /// 提交一局成绩并落盘，返回它在榜上的名次（1 起）；没进前 <see cref="Capacity"/> 名返回 -1。
        /// </summary>
        public static int Submit(string playerName, RunResult result)
        {
            if (result == null)
            {
                return -1;
            }

            var entry = new LeaderboardEntry
            {
                name = Sanitize(playerName),
                score = result.TotalScore,
                completed = result.GameCompleted,
                stageReached = result.StageId,
            };

            var list = new List<LeaderboardEntry>(Load()) { entry };
            Sort(list);
            if (list.Count > Capacity)
            {
                list.RemoveRange(Capacity, list.Count - Capacity);
            }

            _cache = list;
            Save(list);

            int rank = list.IndexOf(entry);
            return rank >= 0 ? rank + 1 : -1;
        }

        /// <summary>清空榜单（调试 / 设置里的「重置记录」用）。</summary>
        public static void Clear()
        {
            _cache = new List<LeaderboardEntry>();
            Save(_cache);
        }

        /// <summary>通关的排在同分未通关的前面，其余按分数从高到低。</summary>
        private static void Sort(List<LeaderboardEntry> list)
        {
            list.Sort((a, b) =>
            {
                if (a.score != b.score)
                {
                    return b.score.CompareTo(a.score);
                }
                if (a.completed != b.completed)
                {
                    return b.completed.CompareTo(a.completed);
                }
                return b.stageReached.CompareTo(a.stageReached);
            });
        }

        private static void Save(List<LeaderboardEntry> list)
        {
            try
            {
                var payload = new Payload { entries = list };
                File.WriteAllText(FilePath, JsonUtility.ToJson(payload, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[排行榜] 写入失败，本次成绩没能保存：{e.Message}");
            }
        }

        /// <summary>昵称兜底：空的给个默认名，过长截断，去掉换行免得把榜单排版打乱。</summary>
        private static string Sanitize(string playerName)
        {
            string name = (playerName ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ').Trim();
            if (name.Length == 0)
            {
                name = "无名建造者";
            }
            return name.Length > 16 ? name.Substring(0, 16) : name;
        }
    }
}
