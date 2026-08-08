using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    [CreateAssetMenu(menuName = "SoulGames/Easy Grid Builder Pro 2/Buildable Grid Object SO", order = 0)]
    public class BuildableGridObjectSO : BuildableObjectSO
    {
        [Space]
        public BuildableGridObjectCategorySO buildableGridObjectCategorySO;

        [Space]
        public bool snapToTheClosestVerticalGridHeight;
        [Range(0, 100)] public float minimumHeightThresholdPercentage = 25f;

        [Space]
        public bool freezeRotation;
        public FourDirectionalRotation fourDirectionalRotation = FourDirectionalRotation.North;

        [Space]
        public GridObjectPlacementType placementType = GridObjectPlacementType.SinglePlacement;
        public float intervalBetweenEachPlacement = 0;
        
        [Space]
        public bool spawnOnlyAtEndPoints;

        /// <summary>
        /// Gets the next clockwise direction relative to the current direction.
        /// </summary>
        /// <param name="currentDirection">The current direction.</param>
        /// <returns>The direction to the right (clockwise) of the current direction.</returns>
        public FourDirectionalRotation GetNextDirectionClockwise(FourDirectionalRotation currentDirection)
        {
            // Increment the direction enum, wrap around using modulo if it exceeds the enum length.
            return (FourDirectionalRotation)(((int)currentDirection + 1) % 4);
        }

        /// <summary>
        /// Gets the next direction counterclockwise relative to the current direction.
        /// </summary>
        /// <param name="currentDirection">The current direction.</param>
        /// <returns>The direction to the left (counterclockwise) of the current direction.</returns>
        public FourDirectionalRotation GetNextDirectionCounterClockwise(FourDirectionalRotation currentDirection)
        {
            // Using modulo to wrap around the enum for counterclockwise rotation.
            // Subtracts 1 to move counterclockwise, adds 4 before modulo to ensure positive result, 3 is used instead of -1 to avoid negative values.
            return (FourDirectionalRotation)(((int)currentDirection + 3) % 4);
        }

        /// <summary>
        /// Gets the rotation angle corresponding to the given cardinal direction.
        /// </summary>
        /// <param name="currentDirection">The current cardinal direction.</param>
        /// <returns>The rotation angle in degrees for the given direction.</returns>
        public int GetRotationAngle(FourDirectionalRotation currentDirection)
        {
            // Each direction is 90 degrees apart, calculate based on enum ordinal value.
            return ((int)currentDirection * 90) % 360;
        }

        /// <summary>
        /// Calculates the offset for placement based on the object's rotation and size.
        /// </summary>
        /// <param name="currentDirection">The direction the object is facing.</param>
        /// <param name="cellSize">The size of each grid cell.</param>
        /// <param name="activeBuildableObjectSORandomPrefab">The random prefab chosen from the array.</param>
        /// <returns>The offset in grid units to properly position the object.</returns>
        public Vector2Int GetObjectRotationOffset(FourDirectionalRotation currentDirection, float cellSize, BuildableObjectSO.RandomPrefabs activeBuildableObjectSORandomPrefab)
        {
            // Calculate the object's size to get the space it will occupy.
            Vector2Int objectSize = GetObjectSizeRelativeToCellSize(cellSize, activeBuildableObjectSORandomPrefab);

            // Set the offset based on the object's current directionalRotation.
            // This offset ensures the object aligns correctly in the grid cells.
            switch (currentDirection)
            {
                case FourDirectionalRotation.North:   return Vector2Int.zero;
                case FourDirectionalRotation.East:    return new Vector2Int(0, objectSize.x);
                case FourDirectionalRotation.South:   return objectSize;
                case FourDirectionalRotation.West:    return new Vector2Int(objectSize.y, 0);
                default:                return Vector2Int.zero; // Fallback for undefined directions.
            }
        }

        /// <summary>
        /// Calculates the grid size of the placed object considering the cell size.
        /// </summary>
        /// <param name="cellSize">The size of each grid cell.</param>
        /// <param name="activeBuildableObjectSORandomPrefab">The random prefab chosen from the array.</param>
        /// <returns>The size of the object in grid units.</returns>
        public Vector2Int GetObjectSizeRelativeToCellSize(float cellSize, BuildableObjectSO.RandomPrefabs activeBuildableObjectSORandomPrefab)
        {
            // Retrieve the prefab and its scale.
            BuildableGridObject objectPrefab = activeBuildableObjectSORandomPrefab.objectPrefab.GetComponent<BuildableGridObject>();
            Vector2 objectTrueScale = objectPrefab.GetObjectTrueScale();

            // Calculate the object size in grid units, rounding up to ensure it fits within the cells.
            int width = Mathf.CeilToInt(objectTrueScale.x / cellSize);
            int height = Mathf.CeilToInt(objectTrueScale.y / cellSize);

            return new Vector2Int(width, height);
        }

        /// <summary>
        /// Gets the list of cell positions occupied by an object on the grid.
        /// </summary>
        /// <param name="objectStartingCellPosition">The starting cell position of the object.</param>
        /// <param name="currentDirection">The direction the object is facing.</param>
        /// <param name="cellSize">The size of each cell in the grid.</param>
        /// <param name="activeBuildableObjectSORandomPrefab">Random prefab chosen from the array to calculate the size.</param>
        /// <returns>A list of cell positions occupied by the object.</returns>
        public List<Vector2Int> GetObjectCellPositionsList(Vector2Int objectStartingCellPosition, FourDirectionalRotation currentDirection, float cellSize, BuildableObjectSO.RandomPrefabs activeBuildableObjectSORandomPrefab)
        {
            Vector2Int objectSize = GetObjectSizeRelativeToCellSize(cellSize, activeBuildableObjectSORandomPrefab);
            List<Vector2Int> cellPositionList = new List<Vector2Int>();

            // Determine how to iterate based on the object's orientation.
            // For North and South directions, the object's size directly maps to the grid.
            // For East and West directions, the object's dimensions are swapped due to rotation.
            int width = currentDirection == FourDirectionalRotation.East || currentDirection == FourDirectionalRotation.West ? objectSize.y : objectSize.x;
            int height = currentDirection == FourDirectionalRotation.East || currentDirection == FourDirectionalRotation.West ? objectSize.x : objectSize.y;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    cellPositionList.Add(objectStartingCellPosition + new Vector2Int(x, y));
                }
            }

            return cellPositionList;
        }
    }
}