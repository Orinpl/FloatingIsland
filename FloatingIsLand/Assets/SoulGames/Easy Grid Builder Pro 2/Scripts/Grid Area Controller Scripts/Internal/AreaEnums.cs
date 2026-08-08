using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    public enum AreaUpdateMode
    {
        Initialize,
        OnPositionChanged,
        Continuous,
    }

    public enum AreaShape
    {
        Rectangle,
        Circle,
    }

    public enum BasicGridAreaTriggerInvokerType
    {
        GridObject,
        EdgeObject,
        CornerObject,
        FreeObject,
    }
}