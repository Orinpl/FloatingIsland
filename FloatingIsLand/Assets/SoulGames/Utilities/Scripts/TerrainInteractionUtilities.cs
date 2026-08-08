using UnityEngine;

namespace SoulGames.Utilities
{
    public class TerrainInteractionUtilities : MonoBehaviour
    {
        public static void FlattenTerrain(Terrain terrain, Vector3 position, float flattenRadius)
        {
            TerrainData terrainData = terrain.terrainData;
            int heightmapResolution = terrainData.heightmapResolution;

            // Convert the world hit point to local terrain coordinates
            Vector3 terrainPosition = position - terrain.transform.position;

            // Normalize the coordinates based on the terrain size
            int x = Mathf.RoundToInt((terrainPosition.x / terrainData.size.x) * heightmapResolution);
            int y = Mathf.RoundToInt((terrainPosition.z / terrainData.size.z) * heightmapResolution);

            // Convert the hit point Y position from world space to local terrain height
            float localHeight = position.y - terrain.transform.position.y;

            // Normalize the height (localHeight / terrain height)
            float normalizedHeight = localHeight / terrainData.size.y;

            // Determine the area of the terrain to flatten
            int radius = Mathf.RoundToInt(flattenRadius / terrainData.size.x * heightmapResolution);

            // Set the new radius where the falloff will start
            float newFalloffRadius = 0.5f * flattenRadius; // You can change this to any value

            int innerRadius = Mathf.RoundToInt(newFalloffRadius / terrainData.size.x * heightmapResolution);

            // Calculate valid bounds to avoid out-of-bounds errors
            int startX = Mathf.Clamp(x - radius, 0, heightmapResolution - 1);
            int startY = Mathf.Clamp(y - radius, 0, heightmapResolution - 1);
            int endX = Mathf.Clamp(x + radius, 0, heightmapResolution - 1);
            int endY = Mathf.Clamp(y + radius, 0, heightmapResolution - 1);

            // Calculate the actual dimensions of the region
            int width = endX - startX;
            int height = endY - startY;

            // Validate the area to avoid unnecessary processing
            if (width <= 0 || height <= 0) return;

            // Get the heights of the terrain around the hit point
            float[,] heights = terrainData.GetHeights(startX, startY, width, height);

            // Directly set the heights in the area to the normalized height of the hit point
            for (int i = 0; i < heights.GetLength(0); i++)
            {
                for (int j = 0; j < heights.GetLength(1); j++)
                {
                    //heights[i, j] = normalizedHeight; // Set the height to the hit point's height directly

                    // Calculate the distance of this point from the center
                    float distance = Vector2.Distance(new Vector2(i, j), new Vector2(radius, radius));

                    if (distance < innerRadius)
                    {
                        // Inside the inner radius, the terrain is fully flattened
                        heights[i, j] = normalizedHeight;
                    }
                    else if (distance <= radius)
                    {
                        // Between the inner radius and the outer radius, apply falloff
                        float falloff = Mathf.Clamp01(1 - ((distance - innerRadius) / (radius - innerRadius)));

                        // Apply the falloff to smoothly blend the height towards the edges
                        heights[i, j] = Mathf.Lerp(heights[i, j], normalizedHeight, falloff);
                    }
                    // Outside the outer radius, the terrain remains unchanged
                }
            }

            // Apply the modified heights back to the terrain using clamped bounds
            terrainData.SetHeights(startX, startY, heights);
        }

        public static void ClearTerrainDetails(Terrain terrain, Vector3 position, float clearRadius)
        {
            TerrainData terrainData = terrain.terrainData;
            int detailResolution = terrainData.detailResolution;

            // Convert the world hit point to local terrain coordinates
            Vector3 terrainPosition = position - terrain.transform.position;

            // Normalize the coordinates based on the terrain size
            int x = Mathf.RoundToInt((terrainPosition.x / terrainData.size.x) * detailResolution);
            int y = Mathf.RoundToInt((terrainPosition.z / terrainData.size.z) * detailResolution);

            // Determine the area of the terrain to clear
            int radius = Mathf.RoundToInt(clearRadius / terrainData.size.x * detailResolution);

            // Set the new radius where the falloff will start (optional, for smoother edges)
            float newFalloffRadius = 0.5f * clearRadius; // You can change this to any value
            int innerRadius = Mathf.RoundToInt(newFalloffRadius / terrainData.size.x * detailResolution);

            // Loop through all the detail layers (vegetation)
            for (int layer = 0; layer < terrainData.detailPrototypes.Length; layer++)
            {
                int[,] details = terrainData.GetDetailLayer(x - radius, y - radius, radius * 2, radius * 2, layer);

                for (int i = 0; i < details.GetLength(0); i++)
                {
                    for (int j = 0; j < details.GetLength(1); j++)
                    {
                        // Calculate the distance of this point from the center
                        float distance = Vector2.Distance(new Vector2(i, j), new Vector2(radius, radius));

                        if (distance < innerRadius)
                        {
                            // Inside the inner radius, remove all vegetation
                            details[i, j] = 0;
                        }
                        else if (distance <= radius)
                        {
                            // Apply falloff for smoother edges
                            float falloff = Mathf.Clamp01(1 - ((distance - innerRadius) / (radius - innerRadius)));
                            details[i, j] = Mathf.RoundToInt(details[i, j] * (1 - falloff)); // Reduce vegetation gradually
                        }
                    }
                }

                // Apply the modified details back to the terrain
                terrainData.SetDetailLayer(x - radius, y - radius, layer, details);
            }
        }

        public static void PaintTerrainWithTexture(Terrain terrain, Vector3 position, float paintRadius, int textureIndex)
        {
            TerrainData terrainData = terrain.terrainData;
            int alphamapResolution = terrainData.alphamapResolution;

            // Convert the world position to local terrain coordinates
            Vector3 terrainPosition = position - terrain.transform.position;

            // Normalize the coordinates based on the terrain size
            int x = Mathf.RoundToInt((terrainPosition.x / terrainData.size.x) * alphamapResolution);
            int y = Mathf.RoundToInt((terrainPosition.z / terrainData.size.z) * alphamapResolution);

            // Calculate the radius in terms of the alphamap resolution
            int radius = Mathf.RoundToInt(paintRadius / terrainData.size.x * alphamapResolution);

            // Get the current alphamap data (splatmap) around the target area
            float[,,] alphamaps = terrainData.GetAlphamaps(x - radius, y - radius, radius * 2, radius * 2);

            // Loop through the area and paint the selected texture
            for (int i = 0; i < alphamaps.GetLength(0); i++)
            {
                for (int j = 0; j < alphamaps.GetLength(1); j++)
                {
                    // Calculate the distance of the current point from the center
                    float distance = Vector2.Distance(new Vector2(i, j), new Vector2(radius, radius));

                    // If the point is within the paint radius, modify the texture weights
                    if (distance <= radius)
                    {
                        // Normalize the distance to create a falloff effect (optional)
                        float falloff = Mathf.Clamp01(1 - (distance / radius));

                        // Set the selected texture weight (textureIndex)
                        alphamaps[i, j, textureIndex] = falloff;  // Paint with falloff effect

                        // Normalize the weights so they sum to 1
                        float totalWeight = 0f;
                        for (int k = 0; k < terrainData.alphamapLayers; k++)
                        {
                            totalWeight += alphamaps[i, j, k];
                        }

                        // Normalize each texture layer so that the sum equals 1
                        for (int k = 0; k < terrainData.alphamapLayers; k++)
                        {
                            alphamaps[i, j, k] /= totalWeight;
                        }
                    }
                }
            }

            // Apply the modified alphamap data back to the terrain
            terrainData.SetAlphamaps(x - radius, y - radius, alphamaps);
        }

        public static void PaintTerrainWithTexture(Terrain terrain, Vector3 position, float paintRadius, int textureIndex, float opacity = 1f, BrushType brushType = BrushType.Soft, float falloffStrength = 1f)
        {
            TerrainData terrainData = terrain.terrainData;
            int alphamapResolution = terrainData.alphamapResolution;

            // Convert the world position to local terrain coordinates
            Vector3 terrainPosition = position - terrain.transform.position;

            // Normalize the coordinates based on the terrain size
            int x = Mathf.RoundToInt((terrainPosition.x / terrainData.size.x) * alphamapResolution);
            int y = Mathf.RoundToInt((terrainPosition.z / terrainData.size.z) * alphamapResolution);

            // Calculate the radius in terms of the alphamap resolution
            int radius = Mathf.RoundToInt(paintRadius / terrainData.size.x * alphamapResolution);

            // Get the current alphamap data (splatmap) around the target area
            float[,,] alphamaps = terrainData.GetAlphamaps(x - radius, y - radius, radius * 2, radius * 2);

            // Loop through the area and paint the selected texture
            for (int i = 0; i < alphamaps.GetLength(0); i++)
            {
                for (int j = 0; j < alphamaps.GetLength(1); j++)
                {
                    // Calculate the distance of the current point from the center
                    float distance = Vector2.Distance(new Vector2(i, j), new Vector2(radius, radius));

                    // If the point is within the paint radius, modify the texture weights
                    if (distance <= radius)
                    {
                        // Get falloff value based on brush type and falloff strength
                        float falloff = GetBrushFalloff(distance, radius, falloffStrength);

                        // Apply opacity to control how strong the paint effect is
                        float paintStrength = opacity * falloff;

                        // Apply the hard brush logic
                        if (brushType == BrushType.Hard)
                        {
                            // Set selected texture to full strength (1) and others to 0
                            for (int k = 0; k < terrainData.alphamapLayers; k++)
                            {
                                alphamaps[i, j, k] = (k == textureIndex) ? 1f : 0f;
                            }
                        }
                        else
                        {
                            // Soft or blended brush: Blend the texture with the existing ones
                            alphamaps[i, j, textureIndex] = Mathf.Lerp(alphamaps[i, j, textureIndex], paintStrength, opacity);

                            // Normalize the weights so they sum to 1
                            float totalWeight = 0f;
                            for (int k = 0; k < terrainData.alphamapLayers; k++)
                            {
                                totalWeight += alphamaps[i, j, k];
                            }

                            // Normalize each texture layer so that the sum equals 1
                            for (int k = 0; k < terrainData.alphamapLayers; k++)
                            {
                                alphamaps[i, j, k] /= totalWeight;
                            }
                        }
                    }
                }
            }

            // Apply the modified alphamap data back to the terrain
            terrainData.SetAlphamaps(x - radius, y - radius, alphamaps);
        }

        private static float GetBrushFalloff(float distance, float radius, float falloffStrength)
        {
            // Calculate normalized distance from center (0 = center, 1 = edge)
            float normalizedDistance = Mathf.Clamp01(distance / radius);

            // Soft brush: Interpolate between a stronger and softer falloff
            return Mathf.Lerp(1f, 1f - Mathf.Pow(normalizedDistance, 2f), falloffStrength); // Smoother soft falloff
        }

        public enum BrushType
        {
            Soft,  // Smooth falloff
            Hard   // Hard edges
        }

    }
}