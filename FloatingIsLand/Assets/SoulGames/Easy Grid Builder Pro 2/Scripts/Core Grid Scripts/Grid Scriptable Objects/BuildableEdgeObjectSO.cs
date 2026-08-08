using System;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    [CreateAssetMenu(menuName = "SoulGames/Easy Grid Builder Pro 2/Buildable Edge Object SO", order = 10)]
    public class BuildableEdgeObjectSO : BuildableObjectSO
    {
        [Space]
        public BuildableEdgeObjectCategorySO buildableEdgeObjectCategorySO;

        [Space]
        public bool snapToTheClosestVerticalGridHeight;
        [Range(0, 100)] public float minimumHeightThresholdPercentage = 25f;
        
        [Space]
        public EdgeObjectPlacementType placementType = EdgeObjectPlacementType.FourDirectionWirePlacement;
        public float intervalBetweenEachPlacement = 0;
        
        [Space]
        public bool freezeRotation;
        public FourDirectionalRotation fourDirectionalRotation = FourDirectionalRotation.North;
        public bool isObjectFlipped = false;

        [Space]
        public bool mergeWithBuildableCornerObject;
        public BuildableCornerObjectSO buildableCornerObjectSO;
        public bool onlyUsedWithGhostObject;

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
            // Calculate the angle and subtract 90 (Edge object default direction is East instead of North), then use modulo to stay within the 0-360 range
            int angle = ((int)currentDirection * 90 - 90) % 360;
            // If the result is negative, add 360 to get the equivalent positive angle
            if (angle < 0) angle += 360;
            
            return angle;
        }

        public int GetObjectLengthRelativeToCellSize(float cellSize, BuildableObjectSO.RandomPrefabs activeBuildableObjectSORandomPrefab)
        {
            // Retrieve the prefab and its scale.
            BuildableEdgeObject objectPrefab = activeBuildableObjectSORandomPrefab.objectPrefab.GetComponent<BuildableEdgeObject>();
            Vector2 objectTrueScale = objectPrefab.GetObjectTrueScale();

            return Mathf.CeilToInt(objectTrueScale.x / cellSize);
        }
    }
}
