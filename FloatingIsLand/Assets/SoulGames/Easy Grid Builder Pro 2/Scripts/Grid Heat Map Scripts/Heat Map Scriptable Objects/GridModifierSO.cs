using UnityEngine.InputSystem;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    [CreateAssetMenu(menuName = "SoulGames/Easy Grid Builder Pro 2/Grid Modifier SO", order = 300)]
    public class GridModifierSO : ScriptableObject
    {   
        public float minimumValue;
        public float maximumValue;
    }
}