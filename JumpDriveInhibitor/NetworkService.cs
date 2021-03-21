using System;
using System.Text;
using Sandbox.ModAPI;
using VRage.Utils;

namespace JumpDriveInhibitor
{
    public class NetworkService
    {
        public static void NetworkInit()
        {
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(42, HandleIncomingPacket);
        }

        private static void HandleIncomingPacket(ushort comId, byte[] msg, ulong id, bool relible)
        {
            try
            {
                var message = Encoding.ASCII.GetString(msg);
                if (message.Equals("clear")) return;
               
            }
            catch (Exception error)
            {
                MyLog.Default.WriteLine($" error in Jump Inhibitor network{error}");
            }
        }


        public static void SendPacket(string data)
        {
            try
            {
                var bytes = Encoding.ASCII.GetBytes(data);
                MyAPIGateway.Multiplayer.SendMessageToServer(42, bytes);
            }
            catch (Exception error)
            {
                MyLog.Default.WriteLine($" error in Jump Inhibitor network{error}");
            }
        }
    }
}