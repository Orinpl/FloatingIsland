using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    [CreateAssetMenu(menuName = "SoulGames/Easy Grid Builder Pro 2/Buildable Free Object SO", order = 30)]
    public class BuildableFreeObjectSO : BuildableObjectSO
    {
        [Space]
        public bool faceCollidingSurfaceNormalDirection;
        public LayerMask surfaceNormalDirectionLayerMask;

        [Space]
        public BuildableFreeObjectCategorySO buildableFreeObjectCategorySO;

        [Space]
        public FreeObjectRotationType rotationType = FreeObjectRotationType.FourDirectionalRotation;
        public float freeRotationSpeed = 100f;
        [Space]
        public bool freezeRotation;
        public FourDirectionalRotation fourDirectionalRotation = FourDirectionalRotation.North;
        public EightDirectionalRotation eightDirectionalRotation = EightDirectionalRotation.North;
        public float freeRotation = default;

        [Space]
        public FreeObjectPlacementType placementType;
        public float intervalBetweenEachPlacement = 0;

        public float objectBaseSpacingAlongSpline = 1f;
        public float objectMinSpacingAlongSpline = 1f;
        public float objectMaxSpacingAlongSpline = 1f;
        public bool objectRotateToSplineDirection = false;
        public float spacingChangeSmoothness = 1f;
        public SplineTangetMode splineTangetMode = SplineTangetMode.AutoSmooth;
        public bool closedSpline = false;

        [Space]
        public bool affectByFreeObjectSnappers = true;

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