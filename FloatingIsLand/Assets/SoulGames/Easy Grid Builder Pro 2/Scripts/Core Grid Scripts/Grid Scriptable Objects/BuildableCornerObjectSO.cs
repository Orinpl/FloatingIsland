using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    [CreateAssetMenu(menuName = "SoulGames/Easy Grid Builder Pro 2/Buildable Corner Object SO", order = 20)]
    public class BuildableCornerObjectSO : BuildableObjectSO
    {
        [Space]
        public BuildableCornerObjectCategorySO buildableCornerObjectCategorySO;

        [Space]
        public bool snapToTheClosestVerticalGridHeight;
        [Range(0, 100)] public float minimumHeightThresholdPercentage = 25f;

        [Space]
        public CornerObjectRotationType rotationType = CornerObjectRotationType.FourDirectionalRotation;
        public float freeRotationSpeed = 100f;
        
        [Space]
        public bool freezeRotation;
        public FourDirectionalRotation fourDirectionalRotation = FourDirectionalRotation.North;
        public EightDirectionalRotation eightDirectionalRotation = EightDirectionalRotation.North;
        public float freeRotation = default;

        [Space]
        public CornerObjectPlacementType placementType = CornerObjectPlacementType.SinglePlacement;
        public float intervalBetweenEachPlacement = 0f;

        [Space]
        public bool spawnOnlyAtEndPoints;

        public FourDirectionalRotation GetNextFourDirectionalDirectionClockwise(FourDirectionalRotation currentDirection)
        {
            return (FourDirectionalRotation)(((int)currentDirection + 1) % 4);
        }

        public EightDirectionalRotation GetNextEightDirectionalDirectionClockwise(EightDirectionalRotation currentDirection)
        {
            return (EightDirectionalRotation)(((int)currentDirection + 1) % 8);
        }

        public FourDirectionalRotation GetNextFourDirectionalDirectionCounterClockwise(FourDirectionalRotation currentDirection)
        {
            return (FourDirectionalRotation)(((int)currentDirection + 3) % 4);
        }

        public EightDirectionalRotation GetNextEightDirectionalDirectionCounterClockwise(EightDirectionalRotation currentDirection)
        {
            return (EightDirectionalRotation)(((int)currentDirection + 7) % 8);
        }

        public int GetFourDirectionalRotationAngle(FourDirectionalRotation currentDirection)
        {
            // Each direction is 90 degrees apart, calculate based on enum ordinal value.
            return ((int)currentDirection * 90) % 360;
        }

        public int GetEightDirectionalRotationAngle(EightDirectionalRotation currentDirection)
        {
            // Each direction is 90 degrees apart, calculate based on enum ordinal value.
            return ((int)currentDirection * 45) % 360;
        }
    }
}