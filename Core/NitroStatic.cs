using UnityEngine;
namespace NitroNetwork.Core
{
    [DefaultExecutionOrder(-101)]
    [RequireComponent(typeof(NitroIdentity))]
    public class NitroStatic : MonoBehaviour
    {
        void Awake()
        {
            var identity = GetComponent<NitroIdentity>();
            if (identity != null)
            {
                identity.IsStatic = true;
                return;
            }
        }
    }
}