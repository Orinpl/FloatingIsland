using System;
using FloatingIsLand.Domain.Map;
using UnityEngine;

namespace FloatingIsLand.Config
{
    /// <summary>
    /// Unity 薄适配器：从 Resources/{folder} 读关卡地图 TextAsset 交给 <see cref="MapJson"/>。
    /// 与 <see cref="UnityTableLoader"/> 同口径（本文件是地图读取链路中唯一 using UnityEngine 的地方）。
    /// 地图按关卡分文件：Resources/Maps/stage_1.json、stage_2.json …
    /// </summary>
    public static class UnityMapLoader
    {
        public const string DefaultFolder = "Maps";

        /// <summary>Resources 相对路径（不含 .json 扩展名），存读两端共用，避免命名漂移。</summary>
        public static string ResourcePath(int stageId, string folder = DefaultFolder)
        {
            return folder + "/stage_" + stageId;
        }

        /// <summary>加载关卡地图；文件缺失或内容非法抛带关卡号的异常。</summary>
        public static MapSnapshot LoadFromResources(int stageId, string folder = DefaultFolder)
        {
            string path = ResourcePath(stageId, folder);
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"关卡 {stageId} 的地图缺失：Resources/{path}.json 不存在（还没刷过这一关？用 Tools → 地图 → 地形刷子 刷一张并保存）。");
            }
            return MapJson.Load("stage_" + stageId, asset.text);
        }

        /// <summary>尝试加载关卡地图；文件不存在返回 false。内容存在但非法仍然抛异常——坏数据不该被静默当成"没有地图"。</summary>
        public static bool TryLoadFromResources(int stageId, out MapSnapshot snapshot, string folder = DefaultFolder)
        {
            var asset = Resources.Load<TextAsset>(ResourcePath(stageId, folder));
            if (asset == null)
            {
                snapshot = null;
                return false;
            }
            snapshot = MapJson.Load("stage_" + stageId, asset.text);
            return true;
        }
    }
}
