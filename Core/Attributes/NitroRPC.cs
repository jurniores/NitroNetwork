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
    /// ownership requirements, Target audience, delivery mode, and communication channel.
    /// </remarks>
    public class NitroRPC : Attribute
    {
        /// <summary>
        /// The type of NitroRPC, specifying the RPC behavior (e.g., Server or Client).
        /// </summary>
        public RPC Type;

        /// <summary>
        /// Indicates whether the RPC requires the caller to be the owner of the object.
        /// Default is <c>true</c>.
        /// </summary>
        public bool RequiresOwner = true;
        /// <summary>
        /// Indicates whether the RPC should be encrypted.
        /// Default is <c>false</c>.
        /// </summary>
        public bool Encrypt = false;

        /// <summary>
        /// Specifies the Target audience for the RPC, for Client RPC.
        /// Default is <see cref="Target.All"/>.
        /// </summary>
        public Target Target = Target.All;

        /// <summary>
        /// Defines the delivery mode for the RPC, such as reliable or unreliable.
        /// Default is <see cref="DeliveryMode.ReliableOrdered"/>.
        /// </summary>
        public DeliveryMode DeliveryMode = DeliveryMode.ReliableOrdered;

        /// <summary>
        /// Specifies the communication channel for the RPC.
        /// Default is <c>0</c>.
        /// </summary>
        public byte Channel = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="NitroRPC"/> class with the specified type.
        /// </summary>
        /// <param name="type">The type of NitroRPC (e.g., Server or Client).</param>
        public NitroRPC(RPC type)
        {
            this.Type = type;
        }
    }

    /// <summary>
    /// Enum representing the type of NitroRPC, specifying whether the RPC is for the server or client.
    /// </summary>
    public enum RPC
    {
        Server,
        Client
    }
}