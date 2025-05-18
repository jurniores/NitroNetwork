using UnityEngine;

public class HideIfAttribute : PropertyAttribute
{
    public string boolFieldName;
    public bool hideIfTrue;

    public HideIfAttribute(string boolFieldName, bool hideIfTrue = true)
    {
        this.boolFieldName = boolFieldName;
        this.hideIfTrue = hideIfTrue;
    }
}