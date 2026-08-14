using FloatingIsLand.Config;
using FloatingIsLand.View;
using SoulGames.EasyGridBuilderPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FloatingIsLand.ViewEGB.EditorTools
{
    /// <summary>
    /// 独立的地图编辑场景（Assets/Scenes/MapEditor.unity）：只放地图编辑需要的最小组件——
    /// 相机 + 灯 + EGB 网格 + TerrainOverlayRenderer，**没有任何游戏运行组件**
    /// （MapBootstrap / WorldRenderer / 摆放控制都不进来），Main 只作为游戏加载的场景。
    ///
    /// 网格默认按 Stage 表第 1 关的尺寸（配表没加载上时退 250×250）；换关编辑时
    /// <see cref="MapEditorWindow"/> 里有「把场景网格调成 W×H」按钮，不必重生成场景。
    /// 网格覆盖参数与 Main 共用 <see cref="EGBSceneSetup.ConfigureGrid"/>，两边不会漂移。
    /// </summary>
    public static class MapEditorSceneSetup
    {
        internal const string ScenePath = "Assets/Scenes/MapEditor.unity";
        private const string RootName = "GridSystems";

        private const int FallbackWidth = 250;
        private const int FallbackLength = 250;
        private const int LayerCount = 1;

        [MenuItem("Tools/地图/打开地图编辑场景", false, 0)]
        public static void OpenScene()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                CreateScene();
            }
            else
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return;
                }
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            if (System.IO.File.Exists(ScenePath))
            {
                MapEditorWindow.Open();
            }
        }

        [MenuItem("Tools/地图/生成地图编辑场景", false, 10)]
        public static void CreateScene()
        {
            var managersPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EGBSceneSetup.ManagersPrefabPath);
            var gridPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EGBSceneSetup.GridPrefabPath);
            if (managersPrefab == null || gridPrefab == null)
            {
                EditorUtility.DisplayDialog("地图编辑场景",
                    "找不到插件成品 prefab：\n" + EGBSceneSetup.ManagersPrefabPath + "\n" +
                    EGBSceneSetup.GridPrefabPath + "\n（EGB 插件被移动或删除？）", "知道了");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            int width = FallbackWidth;
            int length = FallbackLength;
            try
            {
                if (!TableLoader.IsLoaded)
                {
                    UnityTableLoader.LoadFromResources();
                }
                StageRow stage1 = Tables.Stage.GetOrNull(1);
                if (stage1 != null && stage1.mapWidth > 0 && stage1.mapHeight > 0)
                {
                    width = stage1.mapWidth;
                    length = stage1.mapHeight;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[地图编辑场景] 配表加载失败，网格尺寸退 {FallbackWidth}×{FallbackLength}：{e.Message}");
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 网格骨架：与 Main 相同的成品 prefab 模板 + 同一份覆盖参数
            var root = new GameObject(RootName);
            var managers = (GameObject)PrefabUtility.InstantiatePrefab(managersPrefab, scene);
            managers.transform.SetParent(root.transform, false);

            EGBSceneSetup.EnsureGridLayerNamed();
            var gridManager = managers.GetComponentInChildren<GridManager>();
            if (gridManager != null)
            {
                EGBSceneSetup.SetSerializedInt(gridManager, "gridSystemLayerMask", 1 << EGBSceneSetup.GridSystemLayer);
            }

            var gridGo = (GameObject)PrefabUtility.InstantiatePrefab(gridPrefab, scene);
            gridGo.transform.SetParent(root.transform, false);
            var grid = gridGo.GetComponentInChildren<EasyGridBuilderPro>();
            if (grid == null)
            {
                Debug.LogError("[地图编辑场景] " + EGBSceneSetup.GridPrefabPath + " 上找不到 EasyGridBuilderPro 组件（插件版本变了？）。", gridGo);
                return;
            }
            EGBSceneSetup.ConfigureGrid(grid, width, length, LayerCount);

            var overlayGo = new GameObject("TerrainOverlay");
            overlayGo.transform.SetParent(root.transform, false);
            overlayGo.AddComponent<TerrainOverlayRenderer>();

            // 相机与灯：场景不进 Play，只为让 Scene/Game 视图有个能看的默认机位
            var geometry = new GridGeometry(
                grid.transform.position, width, length, grid.GetCellSize(),
                grid.GetVerticalGridHeight(), grid.GetGridOriginType() == GridOrigin.Center);
            Vector3 center = geometry.IsValid
                ? geometry.CellCenter(width / 2, length / 2, 0)
                : Vector3.zero;

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.farClipPlane = 2000f;
            float pullBack = geometry.IsValid ? geometry.CellSize * Mathf.Max(width, length) * 0.35f : 150f;
            cameraGo.transform.position = center + new Vector3(0f, pullBack * 1.2f, -pullBack);
            cameraGo.transform.LookAt(center);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[地图编辑场景] 已生成 {ScenePath}：网格 {width}×{length}×{LayerCount} 层 + TerrainOverlay + 相机/灯。" +
                      "这是纯编辑场景（无游戏组件），用 Tools → 地图 → 地图编辑器 开始编辑。");
        }
    }
}
