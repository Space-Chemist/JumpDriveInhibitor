using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace JumpDriveInhibitor
{
    public class ConnectionHelper
    {
        #region fields

        public static List<byte> ClientMessageCache = new List<byte>();
        public static Dictionary<ulong, List<byte>> ServerMessageCache = new Dictionary<ulong, List<byte>>();

        #endregion

        #region connections to server

        /// <summary>
        /// Creates and sends an entity with the given information for the server. Never call this on DS instance!
        /// </summary>

        public static void SendMessageToServer(MessageBase message)
        {
            message.Side = MessageSide.ServerSide;
            if (MyAPIGateway.Multiplayer.IsServer)
                message.SenderSteamId = MyAPIGateway.Multiplayer.ServerId;
            if (MyAPIGateway.Session.Player != null)
            {
                message.SenderSteamId = MyAPIGateway.Session.Player.SteamUserId;
                message.SenderDisplayName = MyAPIGateway.Session.Player.DisplayName;
            }
            message.SenderLanguage = (int)MyAPIGateway.Session.Config.Language;
            try
            {
                byte[] byteData = MyAPIGateway.Utilities.SerializeToBinary(message);
                MyAPIGateway.Multiplayer.SendMessageToServer(SpeedConsts.ConnectionId, byteData);
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine($" error in jump inhibitor {ex}");
            }
        }

        #endregion

        #region connections to all

        /// <summary>
        /// Creates and sends an entity with the given information for the server and all players.
        /// </summary>
        /// <param name="message"></param>
        public static void SendMessageToAll(MessageBase message)
        {
            if (MyAPIGateway.Multiplayer.IsServer)
                message.SenderSteamId = MyAPIGateway.Multiplayer.ServerId;
            if (MyAPIGateway.Session.Player != null)
            {
                message.SenderSteamId = MyAPIGateway.Session.Player.SteamUserId;
                message.SenderDisplayName = MyAPIGateway.Session.Player.DisplayName;
            }
            message.SenderLanguage = (int)MyAPIGateway.Session.Config.Language;

            if (!MyAPIGateway.Multiplayer.IsServer)
                SendMessageToServer(message);
            SendMessageToAllPlayers(message);
        }

        #endregion

        #region connections to clients

        public static void SendMessageToPlayer(ulong steamId, MessageBase message)
        {
            
            message.Side = MessageSide.ClientSide;
            try
            {
                byte[] byteData = MyAPIGateway.Utilities.SerializeToBinary(message);
                MyAPIGateway.Multiplayer.SendMessageTo(SpeedConsts.ConnectionId, byteData, steamId);
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine($" error in jump inhibitor {ex}");
            }
        }

        public static void SendMessageToAllPlayers(MessageBase messageContainer)
        {
            //MyAPIGateway.Multiplayer.SendMessageToOthers(StandardClientId, System.Text.Encoding.Unicode.GetBytes(ConvertData(content))); <- does not work as expected ... so it doesn't work at all?
            List<IMyPlayer> players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players, p => p != null && !p.IsBot);
            foreach (IMyPlayer player in players)
                SendMessageToPlayer(player.SteamUserId, messageContainer);
        }

        #endregion

        #region processing

        /// <summary>
        /// Server side execution of the actions defined in the data.
        /// </summary>
        /// <param name="rawData"></param>
        public static void ProcessData(byte[] rawData)
        {
            MessageBase message;

            try
            {
                message = MyAPIGateway.Utilities.SerializeFromBinary<MessageBase>(rawData);
            }
            catch (Exception ex)
            {
                return;
            }

            if (message != null)
            {
                try
                {
                    message.InvokeProcessing();
                }
                catch (Exception ex)
                {
                    MyLog.Default.WriteLine($" error in jump inhibitor {ex}");
                }
            }
        }

        #endregion
    }
}