using System.Collections.Generic;
using UnityEngine;

namespace FloatingIsLand.View
{
    /// <summary>
    /// 领域实例 Id → 场景里那个模型。表现层要在「3 号居民区」头上打辉光飘数字，
    /// 就得能从归因里的 Id 找回 GameObject——靠名字反查太脆，Id 是领域层的真相。
    ///
    /// 建筑与元素**分两张表**：<see cref="Domain.Build.PlacedBuilding.Id"/> 与
    /// <see cref="Domain.Build.PlacedElement.Id"/> 是两条独立的 1 起序列，混在一张表里必然撞键。
    /// </summary>
    public sealed class WorldInstanceIndex
    {
        /// <summary>
        /// 一个已生成的世界实例。网格与包围盒在注册时算一次就存下来——
        /// 建筑落地后不会再动，每帧重新 GetComponentsInChildren 是纯浪费。
        /// </summary>
        public sealed class Entry
        {
            /// <summary>模型根（ModelSpawner 生成的包装根）。</summary>
            public GameObject Root { get; }

            /// <summary>所有网格 + 它们各自的世界矩阵，辉光用 Graphics.DrawMesh 逐个重画。</summary>
            public MeshFilter[] Meshes { get; }

            /// <summary>合并后的世界包围盒，用来算飘字锚点。</summary>
            public Bounds WorldBounds { get; }

            /// <summary>飘字挂点：包围盒顶面再抬一点。</summary>
            public Vector3 LabelAnchor
            {
                get { return WorldBounds.center + Vector3.up * (WorldBounds.extents.y + 0.6f); }
            }

            public Entry(GameObject root)
            {
                Root = root;
                Meshes = root != null ? root.GetComponentsInChildren<MeshFilter>(true) : new MeshFilter[0];

                var bounds = new Bounds(root != null ? root.transform.position : Vector3.zero, Vector3.zero);
                bool any = false;
                Renderer[] renderers = root != null ? root.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null)
                    {
                        continue;
                    }
                    if (!any)
                    {
                        bounds = renderers[i].bounds;
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderers[i].bounds);
                    }
                }
                WorldBounds = bounds;
            }
        }

        private readonly Dictionary<int, Entry> _buildings = new Dictionary<int, Entry>();
        private readonly Dictionary<int, Entry> _elements = new Dictionary<int, Entry>();

        public void RegisterBuilding(int id, GameObject root)
        {
            if (root != null)
            {
                _buildings[id] = new Entry(root);
            }
        }

        public void RegisterElement(int id, GameObject root)
        {
            if (root != null)
            {
                _elements[id] = new Entry(root);
            }
        }

        public bool TryGetBuilding(int id, out Entry entry)
        {
            return _buildings.TryGetValue(id, out entry) && entry.Root != null;
        }

        public bool TryGetElement(int id, out Entry entry)
        {
            return _elements.TryGetValue(id, out entry) && entry.Root != null;
        }

        /// <summary>换局时清空。不清的话上一局的死引用会一直留在表里。</summary>
        public void Clear()
        {
            _buildings.Clear();
            _elements.Clear();
        }
    }
}
