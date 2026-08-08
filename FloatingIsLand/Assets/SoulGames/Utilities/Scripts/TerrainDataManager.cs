using System.Collections.Generic;
using UnityEngine;

namespace SoulGames.Utilities
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Utilities/Terrain Data Manager", 8)]
    public class TerrainDataManager : MonoBehaviour
    {
        // List to store all the terrain objects found in the scene
        [SerializeField] private List<Terrain> terrainList = new List<Terrain>();

        // Store data by TerrainData instead of Terrain component
        private static Dictionary<TerrainData, float[,]> savedHeights = new Dictionary<TerrainData, float[,]>();
        private static Dictionary<TerrainData, float[,,]> savedAlphamaps = new Dictionary<TerrainData, float[,,]>();
        private static Dictionary<TerrainData, Dictionary<int, int[,]>> savedDetailMaps = new Dictionary<TerrainData, Dictionary<int, int[,]>>();

        // Function to find all terrains in the scene and store them in the list
        public void FindAllTerrainsInScene()
        {
            // Clear the existing list to avoid duplicates
            terrainList.Clear();

            // Find all Terrain objects in the scene and add them to the list
            Terrain[] terrainsInScene = GameObject.FindObjectsByType<Terrain>(FindObjectsSortMode.None);

            // Add each terrain to the list
            foreach (Terrain terrain in terrainsInScene)
            {
                terrainList.Add(terrain);
            }

            Debug.Log($"Found & Added: {terrainList.Count}: terrains!");
        }

        // Save data when play mode starts
        private void OnEnable()
        {
            SaveAllTerrains(); // Save all terrain data at the start of play mode
        }
        
        // Restore data when exiting play mode or switching scenes
        private void OnDisable()
        {
            RestoreAllTerrains(); // Restore original terrain data when exiting play mode or switching scenes
        }

        // Save all terrains' data in the scene when entering play mode
        private void SaveAllTerrains()
        {
            Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            foreach (Terrain terrain in terrains)
            {
                SaveTerrainHeights(terrain);
                SaveTerrainAlphamaps(terrain);
                SaveTerrainDetails(terrain);
            }
        }

        // Restore all terrain data when exiting play mode or switching scenes
        private void RestoreAllTerrains()
        {
            foreach (var terrainData in savedHeights.Keys)
            {
                RestoreTerrainHeights(terrainData);
            }

            foreach (var terrainData in savedAlphamaps.Keys)
            {
                RestoreTerrainAlphamaps(terrainData);
            }

            foreach (var terrainData in savedDetailMaps.Keys)
            {
                RestoreTerrainDetails(terrainData);
            }

            // Clear saved data after restoring
            savedHeights.Clear();
            savedAlphamaps.Clear();
            savedDetailMaps.Clear();
        }

        // Save the heightmap of the terrain
        private static void SaveTerrainHeights(Terrain terrain)
        {
            if (terrain != null && !savedHeights.ContainsKey(terrain.terrainData))
            {
                TerrainData terrainData = terrain.terrainData;
                savedHeights[terrainData] = terrainData.GetHeights(0, 0, terrainData.heightmapResolution, terrainData.heightmapResolution);
            }
        }

        // Save the alphamaps (textures) of the terrain
        private static void SaveTerrainAlphamaps(Terrain terrain)
        {
            if (terrain != null && !savedAlphamaps.ContainsKey(terrain.terrainData))
            {
                TerrainData terrainData = terrain.terrainData;
                savedAlphamaps[terrainData] = terrainData.GetAlphamaps(0, 0, terrainData.alphamapWidth, terrainData.alphamapHeight);
            }
        }

        // Save the detail maps of all layers for the terrain
        private static void SaveTerrainDetails(Terrain terrain)
        {
            if (terrain != null)
            {
                TerrainData terrainData = terrain.terrainData;

                if (!savedDetailMaps.ContainsKey(terrainData))
                {
                    savedDetailMaps[terrainData] = new Dictionary<int, int[,]>();
                }

                // Loop through all the detail layers and save them
                for (int layer = 0; layer < terrainData.detailPrototypes.Length; layer++)
                {
                    savedDetailMaps[terrainData][layer] = terrainData.GetDetailLayer(0, 0, terrainData.detailWidth, terrainData.detailHeight, layer);
                }
            }
        }

        // Restore terrain heights
        private static void RestoreTerrainHeights(TerrainData terrainData)
        {
            if (savedHeights.ContainsKey(terrainData))
            {
                terrainData.SetHeights(0, 0, savedHeights[terrainData]);
            }
        }

        // Restore terrain alphamaps (textures)
        private static void RestoreTerrainAlphamaps(TerrainData terrainData)
        {
            if (savedAlphamaps.ContainsKey(terrainData))
            {
                terrainData.SetAlphamaps(0, 0, savedAlphamaps[terrainData]);
            }
        }

        // Restore the detail maps for all layers of the terrain
        private static void RestoreTerrainDetails(TerrainData terrainData)
        {
            if (savedDetailMaps.ContainsKey(terrainData))
            {
                // Loop through all the saved detail layers
                foreach (var layer in savedDetailMaps[terrainData].Keys)
                {
                    // Check if the saved detail data exists for this terrain layer
                    if (savedDetailMaps[terrainData].ContainsKey(layer))
                    {
                        // Restore the detail layer
                        terrainData.SetDetailLayer(0, 0, layer, savedDetailMaps[terrainData][layer]);
                    }
                }
            }
        }

        public List<Terrain> GetTerrainList() => terrainList;
    }
}