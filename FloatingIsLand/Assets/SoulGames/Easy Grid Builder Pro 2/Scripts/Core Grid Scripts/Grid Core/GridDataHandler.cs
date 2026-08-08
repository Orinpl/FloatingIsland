using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    public class GridDataHandler : MonoBehaviour
    {
        private Dictionary<Vector2Int, GridCellData>[] gridCellDataDictionary;

        public void InitializeGridDataHandler(int verticalGridCount)
        {
            gridCellDataDictionary = new Dictionary<Vector2Int,GridCellData>[verticalGridCount];
            
            for (int i = 0; i < gridCellDataDictionary.Length; i++)
            {
                gridCellDataDictionary[i] = new Dictionary<Vector2Int,GridCellData>();
            }
        }

        public GridCellData GetCellData(int verticalGridIndex, Vector2Int cellPosition)
        {
            if (gridCellDataDictionary[verticalGridIndex].TryGetValue(cellPosition, out GridCellData gridCellData)) return gridCellData;
            else return gridCellDataDictionary[verticalGridIndex][cellPosition] = new GridCellData(null, null, null, null, null, null, null, null, null, null, null);
        }
        
        public void SetCellData(int verticalGridIndex, Vector2Int cellPosition, GridCellData gridCellData)
        {
            gridCellDataDictionary[verticalGridIndex][cellPosition] = gridCellData;
        }
    }
}