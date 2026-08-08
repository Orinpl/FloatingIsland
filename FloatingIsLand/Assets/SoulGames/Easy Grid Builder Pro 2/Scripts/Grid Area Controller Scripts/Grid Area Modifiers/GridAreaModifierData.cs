using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    public class GridAreaModifierData
    {
        public Dictionary<GridArea, GridAreaData> GridAreaDataDictionary;

        public GridModifierSO gridModifierSO;
        public float modifierValueChangeAmount;
        public ValueChangeType valueChangeType;
        public bool enableValueFallOff;
        public bool resetValuesOnDestroy;

        public GridAreaModifierData()
        {
            this.GridAreaDataDictionary = new Dictionary<GridArea, GridAreaData>();

            this.gridModifierSO = default;
            this.modifierValueChangeAmount = 1f;
            this.valueChangeType = ValueChangeType.Fixed;
            this.enableValueFallOff = false;
            this.resetValuesOnDestroy = true;
        }

        public GridAreaModifierData(Dictionary<GridArea, GridAreaData> GridAreaDataDictionary,
                                    GridModifierSO gridModifierSO, float modifierValueChangeAmount, ValueChangeType valueChangeType, bool enableValueFallOff, bool resetValuesOnDestroy)
        {
            this.GridAreaDataDictionary = GridAreaDataDictionary ?? new Dictionary<GridArea, GridAreaData>();

            this.gridModifierSO = gridModifierSO;
            this.modifierValueChangeAmount = modifierValueChangeAmount;
            this.valueChangeType = valueChangeType;
            this.enableValueFallOff = enableValueFallOff;
            this.resetValuesOnDestroy = resetValuesOnDestroy;
        }
    }
}