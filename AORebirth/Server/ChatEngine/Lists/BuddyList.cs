#region License

// Copyright (c) 2005-2014, CellAO Team
//
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
//
//     * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//     * Neither the name of the CellAO Team nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//

#endregion

namespace ChatEngine.Lists
{
    #region Usings ...

    using System;
    using System.Collections.Generic;

    using AORebirth.Database.Dao;
    using AORebirth.Database.Entities;

    using ChatEngine.CoreClient;
    using ChatEngine.CoreServer;
    using ChatEngine.Packets;

    #endregion

    /// <summary>
    /// Buddy List
    /// </summary>
    public static class BuddyList
    {
        private static readonly byte[] DefaultStatusBytes = { 0x00, 0x01, 0x00 };

        #region Public Methods and Operators

        /// <summary>
        /// The load buddy list.
        /// </summary>
        /// <param name="charId">
        /// The char Id.
        /// </param>
        /// <returns>
        /// Buddy list
        /// </returns>
        public static List<int> LoadBuddyList(int charId)
        {
            DBCharacter character = CharacterDao.Instance.Get(charId);
            if (character == null)
            {
                return new List<int>();
            }

            return character.GetBuddiesIds();
        }

        /// <summary>
        /// Push stored buddy online statuses to a newly authenticated chat client.
        /// </summary>
        public static void SendBuddyStatuses(Client client)
        {
            if (client == null || client.Character == null || client.Character.CharacterId == 0)
            {
                return;
            }

            List<int> buddyIds;
            try
            {
                buddyIds = LoadBuddyList((int)client.Character.CharacterId);
            }
            catch (Exception)
            {
                return;
            }

            foreach (int buddyId in buddyIds)
            {
                if (buddyId <= 0)
                {
                    continue;
                }

                SendBuddyStatus(client, (uint)buddyId, ResolveOnline(buddyId));
            }
        }

        /// <summary>
        /// Tell every connected client that has <paramref name="characterId"/> on their buddy list
        /// whether that character is online.
        /// </summary>
        public static void AnnounceStatusToWatchers(ChatServer server, uint characterId, uint online)
        {
            if (server == null || characterId == 0)
            {
                return;
            }

            foreach (Client watcher in server.ConnectedClients.Values)
            {
                if (watcher == null || watcher.Character == null || watcher.Character.CharacterId == characterId)
                {
                    continue;
                }

                List<int> buddyIds;
                try
                {
                    buddyIds = LoadBuddyList((int)watcher.Character.CharacterId);
                }
                catch (Exception)
                {
                    continue;
                }

                if (!buddyIds.Contains((int)characterId))
                {
                    continue;
                }

                SendBuddyStatus(watcher, characterId, online);
            }
        }

        /// <summary>
        /// Persist add and confirm to the requester with current online status.
        /// </summary>
        public static void AddBuddy(Client client, uint buddyId)
        {
            if (client == null || client.Character == null || client.Character.CharacterId == 0 || buddyId == 0)
            {
                return;
            }

            if (buddyId == client.Character.CharacterId)
            {
                return;
            }

            CharacterDao.Instance.AddBuddy((int)client.Character.CharacterId, (int)buddyId);
            SendBuddyStatus(client, buddyId, ResolveOnline((int)buddyId));
        }

        /// <summary>
        /// Persist remove and confirm to the requester.
        /// </summary>
        public static void RemoveBuddy(Client client, uint buddyId)
        {
            if (client == null || client.Character == null || client.Character.CharacterId == 0 || buddyId == 0)
            {
                return;
            }

            CharacterDao.Instance.RemoveBuddy((int)client.Character.CharacterId, (int)buddyId);
            client.Send(BuddyRemoved.Create(buddyId));
        }

        // LoadRecentMsgsList unused?

        /// <summary>
        /// The load recent msgs list.
        /// </summary>
        /// <param name="charId">
        /// The char Id.
        /// </param>
        /// <returns>
        /// List of received messages
        /// </returns>
        public static IEnumerable<DBReceivedMessages> LoadRecentMsgsList(uint charId)
        {
            return ReceivedMessagesDao.Instance.LoadRecentMessageses((int)charId);
        }

        #endregion

        #region Methods

        private static uint ResolveOnline(int characterId)
        {
            try
            {
                return (uint)CharacterDao.Instance.IsOnline(characterId);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static void SendBuddyStatus(Client client, uint buddyId, uint online)
        {
            if (client == null || buddyId == 0)
            {
                return;
            }

            if (!client.KnownClients.Contains(buddyId))
            {
                string name = ResolveName(buddyId);
                if (!string.IsNullOrEmpty(name))
                {
                    client.Send(PlayerName.Create(buddyId, name));
                }

                client.KnownClients.Add(buddyId);
            }

            client.Send(BuddyOnlineStatus.Create(buddyId, online, DefaultStatusBytes));
        }

        private static string ResolveName(uint characterId)
        {
            try
            {
                DBCharacter character = CharacterDao.Instance.Get((int)characterId);
                return character != null ? character.Name : string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        #endregion
    }
}
