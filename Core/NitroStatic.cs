using UnityEngine;

[DefaultExecutionOrder(-101)]
public class NitroStatic : MonoBehaviour
{
    void Awake()
    {
        var identity = GetComponent<NitroIdentity>();
        if(identity != null)
        {
            identity.IsStatic = true;
            return;
        }
    }
}
