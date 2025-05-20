using System;
using UnityEngine;

namespace NitroNetwork.Core
{
    /// <summary>
    /// Represents a NitroRPC attribute used to define remote procedure call (RPC) settings
    /// for networked communication in the NitroNetwork framework.
    /// </summary>
    /// <remarks>
    /// This attribute is used to configure the behavior of RPCs, including the type of RPC,
    /// ownership requirements, target audience, delivery mode, and communication channel.
    /// </remarks>
    public class NitroRPC : Attribute
    {
        /// <summary>
        /// The type of NitroRPC, specifying the RPC behavior (e.g., Server or Client).
        /// </summary>
        public NitroType type;

        /// <summary>
        /// Indicates whether the RPC requires the caller to be the owner of the object.
        /// Default is <c>true</c>.
        /// </summary>
        public bool requiresOwner = true;
        /// <summary>
        /// Indicates whether the RPC should be encrypted.
        /// Default is <c>false</c>.
        /// </summary>
        public bool criptograde = false;

        /// <summary>
        /// Specifies the target audience for the RPC, for Client RPC.
        /// Default is <see cref="Target.All"/>.
        /// </summary>
        public Target target = Target.All;

        /// <summary>
        /// Defines the delivery mode for the RPC, such as reliable or unreliable.
        /// Default is <see cref="DeliveryMode.ReliableOrdered"/>.
        /// </summary>
        public DeliveryMode deliveryMode = DeliveryMode.ReliableOrdered;

        /// <summary>
        /// Specifies the communication channel for the RPC.
        /// Default is <c>0</c>.
        /// </summary>
        public byte channel = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="NitroRPC"/> class with the specified type.
        /// </summary>
        /// <param name="type">The type of NitroRPC (e.g., Server or Client).</param>
        public NitroRPC(NitroType type)
        {
            this.type = type;
        }
    }

    /// <summary>
    /// Enum representing the type of NitroRPC, specifying whether the RPC is for the server or client.
    /// </summary>
    public enum NitroType
    {
        Server,
        Client
    }
}