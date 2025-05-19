using UnityEngine;
namespace NitroNetwork.Core
{
    /// <summary>
    /// Component that marks a GameObject as static in the NitroNetwork system.
    /// Ensures the associated NitroIdentity is set as static on Awake.
    /// </summary>
    [DefaultExecutionOrder(-101)]
    [RequireComponent(typeof(NitroIdentity))]
    public class NitroStatic : MonoBehaviour
    {
        /// <summary>
        /// Sets the NitroIdentity's IsStatic property to true when the object awakens.
        /// </summary>
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