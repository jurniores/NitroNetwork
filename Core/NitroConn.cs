using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;

namespace NitroNetwork.Core
{
    /// <summary>
    /// Representa uma conexão de rede (peer) no framework NitroNetwork.
    /// Armazena metadados da conexão, salas associadas, identidades de rede, dados customizados e chaves de criptografia.
    /// Fornece métodos para gerenciar salas e identidades vinculadas a esta conexão.
    /// </summary>
    public class NitroConn
    {
        /// <summary>
        /// Identificador único da conexão.
        /// </summary>
        public int Id;
        /// <summary>
        /// Message counter to validate disconnection via speedhack.
        /// </summary>
        public int countMsg = 0;
        /// <summary>
        /// Phase to check if the count is in sync with the server time.
        /// </summary>
        public ulong fase = 0;
        /// <summary>
        /// Ping of the connection.
        /// </summary>
        public int Ping => NitroManager.GetMyPing(this);

        /// <summary>
        /// Endpoint IP associado a esta conexão.
        /// </summary>
        public IPEndPoint iPEndPoint;

        /// <summary>
        /// Salas associadas a esta conexão.
        /// </summary>
        public Dictionary<string, NitroRoom> rooms = new();

        /// <summary>
        /// Identidades de rede a serem destruídas quando a conexão for encerrada.
        /// </summary>
        public Dictionary<int, NitroIdentity> identities = new();

        /// <summary>
        /// Dados customizados associados a esta conexão.
        /// </summary>
        public Dictionary<object, object> customData = new();

        /// <summary>
        /// Chave AES para comunicação criptografada.
        /// </summary>
        public byte[] keyAes;

        /// <summary>
        /// Adiciona uma identidade à lista de identidades associadas a esta conexão.
        /// </summary>
        /// <param name="identity">A identidade a ser adicionada.</param>
        public void AddIdentity(NitroIdentity identity)
        {
            identities.Add(identity.Id, identity);
        }

        /// <summary>
        /// Remove uma identidade da lista de identidades associadas a esta conexão.
        /// </summary>
        /// <param name="identity">A identidade a ser removida.</param>
        public void RemoveIdentity(NitroIdentity identity)
        {
            identities.Remove(identity.Id);
        }

        /// <summary>
        /// Adiciona uma sala à lista de salas associadas a esta conexão.
        /// </summary>
        /// <param name="room">A sala a ser adicionada.</param>
        /// <returns>True se a sala foi adicionada com sucesso; caso contrário, false.</returns>
        internal bool AddRoom(NitroRoom room)
        {
            if (rooms.TryAdd(room.Name, room))
            {
                return true;
            }
            else
            {
                NitroLogs.LogWarning($"Falha ao adicionar sala {room.Name} ao peer {Id}");
            }
            return false;
        }

        /// <summary>
        /// Remove todas as salas associadas a esta conexão.
        /// </summary>
        internal void LeaveAllRooms()
        {
            for (int i = rooms.Values.Count - 1; i >= 0; i--)
            {
                var room = new List<NitroRoom>(rooms.Values)[i];
                room.RemoveConn(this);
            }
            rooms.Clear();
        }

        /// <summary>
        /// Remove uma sala específica da lista de salas associadas a esta conexão.
        /// </summary>
        /// <param name="room">A sala a ser removida.</param>
        /// <returns>True se a sala foi removida com sucesso; caso contrário, false.</returns>
        internal bool RemoveRoom(NitroRoom room)
        {
            if (rooms.Remove(room.Name))
            {
                return true;
            }
            NitroLogs.LogWarning($"Falha ao remover sala {room.Name} do peer {Id}");
            return false;
        }

        /// <summary>
        /// Destroi todas as identidades associadas a esta conexão e limpa os dados customizados.
        /// </summary>
        internal void DestroyAllIdentities()
        {
            foreach (var identity in identities)
            {
                identity.Value.Destroy();
            }
            identities.Clear();
            customData.Clear();
        }
    }
}