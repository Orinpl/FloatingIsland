using System.Collections.Generic;
using System.Text;
using FloatingIsLand.App;
using UnityEngine.UI;

namespace FloatingIsLand.UI
{
    /// <summary>
    /// 排行榜面板：从主界面进入，只读展示本地榜单（昵称 + 分数，通关的另行标注）。
    /// 榜单是这个游戏唯一持久化的东西——局内进度退出即销毁。
    /// </summary>
    public sealed class LeaderboardPanel : UIPanel
    {
        public Text titleText;
        public Text listText;
        public Button backButton;

        private readonly StringBuilder _builder = new StringBuilder();

        /// <summary>用当前本地榜单刷新列表。<paramref name="highlightRank"/> 是刚上榜的名次（0 = 不高亮）。</summary>
        public void Refresh(int highlightRank = 0)
        {
            IReadOnlyList<LeaderboardEntry> entries = Leaderboard.Load();

            if (titleText != null)
            {
                titleText.text = entries.Count > 0 ? $"排行榜（{entries.Count} 条记录）" : "排行榜";
            }
            if (listText == null)
            {
                return;
            }

            if (entries.Count == 0)
            {
                listText.text = "还没有记录。\n打完一局就会留下成绩。";
                return;
            }

            _builder.Length = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                LeaderboardEntry entry = entries[i];
                int rank = i + 1;
                string mark = entry.completed ? "通关" : $"第{entry.stageReached}关";
                // 刚打完那条加个箭头，玩家一眼能找到自己
                string cursor = rank == highlightRank ? "▶ " : "   ";
                _builder.AppendLine($"{cursor}{rank,2}.  {entry.name,-16} {entry.score,7}   {mark}");
            }
            listText.text = _builder.ToString();
        }
    }
}
