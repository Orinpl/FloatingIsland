using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    public enum PaintingMethod
    {
        OnClick,
        OnClickHold,
    }

    public enum ValueChangeType
    {
        Fixed,
        Addition,
        Multiplication,
    }

    public enum HeatMapValueReadMethod
    {
        ReadAllValues,
        ReadSpecificValue,
        Both,
    }
}