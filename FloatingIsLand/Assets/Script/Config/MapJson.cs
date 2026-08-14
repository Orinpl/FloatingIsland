using System;
using System.Collections.Generic;
using FloatingIsLand.Domain.Map;
using Newtonsoft.Json;

namespace FloatingIsLand.Config
{
    /// <summary>
    /// 地图地形层 JSON ↔ <see cref="MapSnapshot"/>。机制与 <see cref="TableJson"/> 一致（Newtonsoft），
    /// 但地图不是配表：正本是 Resources/Maps/*.json 本身，由地图编辑器
    /// （Assets/Script/ViewEGB/Editor/MapEditorWindow）写出，不经 Excel 转表。
    ///
    /// 格式（稀疏，只列刷过的格子）：
    /// <code>
    /// { "stageId": 1, "width": 32, "length": 32, "layerCount": 1,
    ///   "cells":    [ { "x": 12, "z": 30, "layer": 0, "e": "greenField" } ],
    ///   "elements": [ { "x": 14, "z": 16, "layer": 0, "e": "anchor", "r": 0 },
    ///                 { "x": 20, "z": 8, "layer": 0, "e": "windSource", "r": 0,
    ///                   "d": 2, "f": 3, "len": 18 } ] }
    /// </code>
    /// cells 是逐格的地形层，elements 是带占地形状的地图元素（巨型风车/锚点/矿藏/风源），
    /// 两者分开存：地形是区域属性，元素要有个体身份才能做锚点归属与收益递减（§12.8）。
    /// elements 缺失按空处理，老地图文件可以直接读。
    ///
    /// d/f/len 是风源的手工风参数（d=Dir8 风向 0~7，f=风力，len=风长），只有地图编辑器
    /// 授权过的风源才有（f&gt;0 即授权标志）；缺失 = 运行时按局种子随机（老地图/自动散布的口径）。
    ///
    /// 存盘时按 (layer, z, x) 排序，保证同一张图两次保存字节一致——否则 git diff 全是噪音。
    /// </summary>
    public static class MapJson
    {
        /// <summary>反序列化地图 JSON；内容为空、解析失败或数据非法时抛带地图名的异常。</summary>
        public static MapSnapshot Load(string mapName, string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException(
                    $"地图 [{mapName}] 的 JSON 内容为 null/空（文件缺失或为空？用 Tools → 地图 → 地图编辑器 刷一张并保存）。");
            }

            MapDto dto;
            try
            {
                dto = JsonConvert.DeserializeObject<MapDto>(json);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(
                    $"地图 [{mapName}] JSON 反序列化失败：{e.Message}", e);
            }

            if (dto == null)
            {
                throw new InvalidOperationException(
                    $"地图 [{mapName}] JSON 反序列化结果为 null（文件内容是字面量 null？地图应为 JSON 对象 {{...}}）。");
            }

            CellDto[] cellDtos = dto.cells ?? Array.Empty<CellDto>();
            var cells = new List<MapCell>(cellDtos.Length);
            for (int i = 0; i < cellDtos.Length; i++)
            {
                CellDto c = cellDtos[i];
                if (c == null)
                {
                    throw new InvalidOperationException($"地图 [{mapName}] 第 {i} 个地块是 null。");
                }
                cells.Add(new MapCell(c.x, c.z, c.layer, c.e));
            }

            ElementDto[] elementDtos = dto.elements ?? Array.Empty<ElementDto>();
            var elements = new List<MapElementPlacement>(elementDtos.Length);
            for (int i = 0; i < elementDtos.Length; i++)
            {
                ElementDto e = elementDtos[i];
                if (e == null)
                {
                    throw new InvalidOperationException($"地图 [{mapName}] 第 {i} 个元素是 null。");
                }
                if (e.r < 0 || e.r > 3)
                {
                    throw new InvalidOperationException(
                        $"地图 [{mapName}] 第 {i} 个元素 [{e.e}] 的朝向 {e.r} 非法（应为 0~3，即 0°/90°/180°/270°）。");
                }
                if (e.f < 0)
                {
                    throw new InvalidOperationException(
                        $"地图 [{mapName}] 第 {i} 个元素 [{e.e}] 的风力 {e.f} 非法（应 ≥ 0；0 = 无手工风参数）。");
                }
                if (e.f > 0)
                {
                    if (e.d < 0 || e.d > 7)
                    {
                        throw new InvalidOperationException(
                            $"地图 [{mapName}] 第 {i} 个元素 [{e.e}] 的风向 {e.d} 非法（应为 0~7，Dir8 八向）。");
                    }
                    if (e.len <= 0)
                    {
                        throw new InvalidOperationException(
                            $"地图 [{mapName}] 第 {i} 个元素 [{e.e}] 带手工风力 {e.f} 却没有正的风长（len={e.len}）。");
                    }
                }
                elements.Add(new MapElementPlacement(e.e, e.x, e.z, e.layer, (Rotation)e.r, e.d, e.f, e.len));
            }

            try
            {
                // MapSnapshot 构造函数负责越界/重复/空 id 的校验，坏地图在这里炸
                return new MapSnapshot(dto.stageId, dto.width, dto.length, dto.layerCount <= 0 ? 1 : dto.layerCount, cells, elements);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException($"地图 [{mapName}] 数据非法：{e.Message}", e);
            }
        }

        /// <summary>序列化为 JSON（缩进 + 稳定排序）。编辑器刷子保存时用。</summary>
        public static string Save(MapSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var sorted = new List<MapCell>(snapshot.Cells);
            sorted.Sort(CompareCells);

            var sortedElements = new List<MapElementPlacement>(snapshot.Elements);
            sortedElements.Sort(CompareElements);

            var dto = new MapDto
            {
                stageId = snapshot.StageId,
                width = snapshot.Width,
                length = snapshot.Length,
                layerCount = snapshot.LayerCount,
                cells = new CellDto[sorted.Count],
                elements = new ElementDto[sortedElements.Count],
            };
            for (int i = 0; i < sorted.Count; i++)
            {
                MapCell cell = sorted[i];
                dto.cells[i] = new CellDto { x = cell.X, z = cell.Z, layer = cell.Layer, e = cell.ElementId };
            }
            for (int i = 0; i < sortedElements.Count; i++)
            {
                MapElementPlacement element = sortedElements[i];
                dto.elements[i] = new ElementDto
                {
                    x = element.X,
                    z = element.Z,
                    layer = element.Layer,
                    e = element.ElementId,
                    r = (int)element.Rotation,
                    d = element.WindDir,
                    f = element.WindForce,
                    len = element.WindLength,
                };
            }

            return JsonConvert.SerializeObject(dto, Formatting.Indented);
        }

        private static int CompareCells(MapCell a, MapCell b)
        {
            if (a.Layer != b.Layer) return a.Layer.CompareTo(b.Layer);
            if (a.Z != b.Z) return a.Z.CompareTo(b.Z);
            return a.X.CompareTo(b.X);
        }

        private static int CompareElements(MapElementPlacement a, MapElementPlacement b)
        {
            if (a.Layer != b.Layer) return a.Layer.CompareTo(b.Layer);
            if (a.Z != b.Z) return a.Z.CompareTo(b.Z);
            if (a.X != b.X) return a.X.CompareTo(b.X);
            return string.CompareOrdinal(a.ElementId, b.ElementId);
        }

        // ---------- 序列化 DTO（MapSnapshot 无空构造、MapCell 是 readonly struct，不能让 Newtonsoft 直接映射） ----------

        [Serializable]
        private sealed class MapDto
        {
            public int stageId;
            public int width;
            public int length;
            public int layerCount;
            public CellDto[] cells;
            public ElementDto[] elements;
        }

        [Serializable]
        private sealed class CellDto
        {
            public int x;
            public int z;
            public int layer;
            /// <summary>elementId。字段名压到 "e"：250×250 的图逐格重复长键名，体积和可读性都划不来。</summary>
            public string e;
        }

        [Serializable]
        private sealed class ElementDto
        {
            public int x;
            public int z;
            public int layer;
            /// <summary>elementId，与 <see cref="CellDto.e"/> 同口径。</summary>
            public string e;
            /// <summary>朝向 0~3（0°/90°/180°/270°）。</summary>
            public int r;

            // ---- 风源手工参数（DefaultValueHandling.Ignore：0 不落盘，非风源元素的 JSON 保持干净） ----

            /// <summary>手工风向 0~7（Dir8）。仅 f &gt; 0 时有意义。</summary>
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public int d;

            /// <summary>手工风力；&gt; 0 = 已授权，0 = 运行时随机。</summary>
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public int f;

            /// <summary>手工风长（格）。仅 f &gt; 0 时有意义。</summary>
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public int len;
        }
    }
}
