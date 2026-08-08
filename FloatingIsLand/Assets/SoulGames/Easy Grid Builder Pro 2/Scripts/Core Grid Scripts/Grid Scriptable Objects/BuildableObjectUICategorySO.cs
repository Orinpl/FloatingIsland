using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    [CreateAssetMenu(menuName = "SoulGames/Easy Grid Builder Pro 2/Buildable Object UI Category SO", order = 200)]
    public class BuildableObjectUICategorySO : ScriptableObject
    {
        public string categoryName;
        public Sprite categoryIcon;
    }
}