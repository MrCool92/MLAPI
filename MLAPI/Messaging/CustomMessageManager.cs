using System;
using System.Collections.Generic;
using System.IO;
using MLAPI.Configuration;
using MLAPI.Logging;
using MLAPI.Security;
using MLAPI.Serialization;
using MLAPI.Serialization.Pooled;

namespace MLAPI.Messaging
{
    /// <summary>
    /// The manager class to manage custom messages, note that this is different from the NetworkingManager custom messages.
    /// These are named and are much easier to use.
    /// </summary>
    public static class CustomMessagingManager
    {
        #region Unnamed
        /// <summary>
        /// Delegate used for incoming unnamed messages
        /// </summary>
        /// <param name="clientId">The clientId that sent the message</param>
        /// <param name="stream">The stream containing the message data</param>
        public delegate void UnnamedMessageDelegate(ulong clientId, Stream stream);

        /// <summary>
        /// Event invoked when unnamed messages arrive
        /// </summary>
        public static event UnnamedMessageDelegate OnUnnamedMessage;

        internal static void InvokeUnnamedMessage(ulong clientId, Stream stream)
        {
            if (OnUnnamedMessage != null)
            {
                OnUnnamedMessage(clientId, stream);
            }

            NetworkingManager.Singleton.InvokeOnIncomingCustomMessage(clientId, stream);
        }


        /// <summary>
        /// Sends unnamed message to a list of clients
        /// </summary>
        /// <param name="clientIds">The clients to send to, sends to everyone if null</param>
        /// <param name="stream">The message stream containing the data</param>
        /// <param name="channel">The channel to send the data on</param>
        /// <param name="security">The security settings to apply to the message</param>
        public static void SendUnnamedMessage(List<ulong> clientIds, BitStream stream, string channel = null, SecuritySendFlags security = SecuritySendFlags.None)
        {
            if (!NetworkingManager.Singleton.IsServer)
            {
                if (LogHelper.CurrentLogLevel <= LogLevel.Error) LogHelper.LogWarning("Can not send unnamed messages to multiple users as a client");
                return;
            }

            if (clientIds == null)
            {
                for (int i = 0; i < NetworkingManager.Singleton.ConnectedClientsList.Count; i++)
                {
                    InternalMessageSender.Send(NetworkingManager.Singleton.ConnectedClientsList[i].ClientId, MLAPIConstants.MLAPI_UNNAMED_MESSAGE, string.IsNullOrEmpty(channel) ? "MLAPI_DEFAULT_MESSAGE" : channel, stream, security, null);
                }
            }
            else
            {
                for (int i = 0; i < clientIds.Count; i++)
                {
                    InternalMessageSender.Send(clientIds[i], MLAPIConstants.MLAPI_UNNAMED_MESSAGE, string.IsNullOrEmpty(channel) ? "MLAPI_DEFAULT_MESSAGE" : channel, stream, security, null);
                }
            }
        }

        /// <summary>
        /// Sends a unnamed message to a specific client
        /// </summary>
        /// <param name="clientId">The client to send the message to</param>
        /// <param name="stream">The message stream containing the data</param>
        /// <param name="channel">The channel tos end the data on</param>
        /// <param name="security">The security settings to apply to the message</param>
        public static void SendUnnamedMessage(ulong clientId, BitStream stream, string channel = null, SecuritySendFlags security = SecuritySendFlags.None)
        {
            InternalMessageSender.Send(clientId, MLAPIConstants.MLAPI_UNNAMED_MESSAGE, string.IsNullOrEmpty(channel) ? "MLAPI_DEFAULT_MESSAGE" : channel, stream, security, null);
        }
        #endregion
        #region Named

        /// <summary>
        /// Delegate used to handle named messages
        /// </summary>
        public delegate void HandleNamedMessageDelegate(ulong sender, Stream payload);

        private static readonly Dictionary<ulong, HandleNamedMessageDelegate> namedMessageHandlers = new Dictionary<ulong, HandleNamedMessageDelegate>();

        internal static void InvokeNamedMessage(ulong hash, ulong sender, Stream stream)
        {
            if (namedMessageHandlers.ContainsKey(hash))
            {
                namedMessageHandlers[hash](sender, stream);
            }
        }

        /// <summary>
        /// Registers a named message handler delegate.
        /// </summary>
        /// <param name="name">Name of the message.</param>
        /// <param name="callback">The callback to run when a named message is received.</param>
        public static void RegisterNamedMessageHandler(string name, HandleNamedMessageDelegate callback)
        {
            ulong hash = NetworkedBehaviour.HashMethodName(name);

            namedMessageHandlers[hash] = callback;
        }

        /// <summary>
        /// Sends a named message
        /// </summary>
        /// <param name="name">The message name to send</param>
        /// <param name="clientId">The client to send the message to</param>
        /// <param name="stream">The message stream containing the data</param>
        /// <param name="channel">The channel tos end the data on</param>
        /// <param name="security">The security settings to apply to the message</param>
        public static void SendNamedMessage(string name, ulong clientId, Stream stream, string channel = null, SecuritySendFlags security = SecuritySendFlags.None)
        {
            ulong hash = NetworkedBehaviour.HashMethodName(name);

            using (PooledBitStream messageStream = PooledBitStream.Get())
            {
                using (PooledBitWriter writer = PooledBitWriter.Get(messageStream))
                {
                    writer.WriteUInt64Packed(hash);
                }

                messageStream.CopyFrom(stream);

                InternalMessageSender.Send(clientId, MLAPIConstants.MLAPI_NAMED_MESSAGE, string.IsNullOrEmpty(channel) ? "MLAPI_DEFAULT_MESSAGE" : channel, messageStream, security, null);
            }
        }

        /// <summary>
        /// Sends the named message
        /// </summary>
        /// <param name="name">The message name to send</param>
        /// <param name="clientIds">The clients to send to, sends to everyone if null</param>
        /// <param name="stream">The message stream containing the data</param>
        /// <param name="channel">The channel to send the data on</param>
        /// <param name="security">The security settings to apply to the message</param>
        public static void SendNamedMessage(string name, List<ulong> clientIds, Stream stream, string channel = null, SecuritySendFlags security = SecuritySendFlags.None)
        {
            ulong hash = NetworkedBehaviour.HashMethodName(name);

            using (PooledBitStream messageStream = PooledBitStream.Get())
            {
                using (PooledBitWriter writer = PooledBitWriter.Get(messageStream))
                {
                    writer.WriteUInt64Packed(hash);
                }

                messageStream.CopyFrom(stream);

                if (!NetworkingManager.Singleton.IsServer)
                {
                    if (LogHelper.CurrentLogLevel <= LogLevel.Error) LogHelper.LogWarning("Can not send named messages to multiple users as a client");
                    return;
                }
                if (clientIds == null)
                {
                    for (int i = 0; i < NetworkingManager.Singleton.ConnectedClientsList.Count; i++)
                    {
                        InternalMessageSender.Send(NetworkingManager.Singleton.ConnectedClientsList[i].ClientId, MLAPIConstants.MLAPI_NAMED_MESSAGE, string.IsNullOrEmpty(channel) ? "MLAPI_DEFAULT_MESSAGE" : channel, messageStream, security, null);
                    }
                }
                else
                {
                    for (int i = 0; i < clientIds.Count; i++)
                    {
                        InternalMessageSender.Send(clientIds[i], MLAPIConstants.MLAPI_NAMED_MESSAGE, string.IsNullOrEmpty(channel) ? "MLAPI_DEFAULT_MESSAGE" : channel, messageStream, security, null);
                    }
                }
            }
        }
        #endregion
    }
}