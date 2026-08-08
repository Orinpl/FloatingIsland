using System;
using UnityEngine;
using System.IO;

namespace SoulGames.EasyGridBuilderPro
{
    public class EasyGridBuilderProSaveSystem
    {
        private static SaveData saveData = new SaveData();

        [Serializable]
        public struct SaveData
        {
            public GridSystemsSaveData gridSystemsSaveData;
            public BuildableObjectsSaveData buildableObjectsSaveData;
        }

        public static string SaveFileName()
        {
            if (!GridManager.Instance.TryGetGridSaveAndLoadManager(out GridSaveAndLoadManager gridSaveAndLoadManager)) 
            {   
                return Application.dataPath + "/SoulGames/Easy Grid Builder Pro 2/EGB Pro 2 Local Saves" + "/EGB Pro 2 Save" + ".txt";
            }
            else return Application.dataPath + gridSaveAndLoadManager.GetLocalSavePath() + gridSaveAndLoadManager.GetSaveFileName() + gridSaveAndLoadManager.GetSaveExtention();
        }

        public static void Save()
        {
            HandleSaveData();
            File.WriteAllText(SaveFileName(), JsonUtility.ToJson(saveData, true));
        }

        private static void HandleSaveData()
        {
            if (!GridManager.Instance.TryGetGridSaveAndLoadManager(out GridSaveAndLoadManager gridSaveAndLoadManager)) return;
            gridSaveAndLoadManager.Save(ref saveData.gridSystemsSaveData, ref saveData.buildableObjectsSaveData);
        }

        public static void Load()
        {
            string saveFile = File.ReadAllText(SaveFileName());
            saveData = JsonUtility.FromJson<SaveData>(saveFile);
            HandleLoadData();
        }

        private static void HandleLoadData()
        {
            if (!GridManager.Instance.TryGetGridSaveAndLoadManager(out GridSaveAndLoadManager gridSaveAndLoadManager)) return;
            gridSaveAndLoadManager.Load(saveData.gridSystemsSaveData, saveData.buildableObjectsSaveData);
        }
    }
}