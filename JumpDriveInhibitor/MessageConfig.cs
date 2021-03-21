using ProtoBuf;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using System.Globalization;
using System.Text;

namespace JumpDriveInhibitor
{
    [ProtoContract]
    public class MessageConfig : MessageBase
    {
        #region properties

        /// <summary>
        /// The key config item to set.
        /// </summary>
        [ProtoMember(201)]
        public string ConfigName;

        /// <summary>
        /// The value to set the config item to.
        /// </summary>
        [ProtoMember(202)]
        public string Value;

        #endregion

        public static void SendMessage(string configName, string value)
        {
            ConnectionHelper.SendMessageToServer(new MessageConfig { ConfigName = configName.ToLower(), Value = value });
        }

        public override void ProcessClient()
        {
            // never processed on client
        }

        public override void ProcessServer()
        {
            var player = MyAPIGateway.Players.FindPlayerBySteamId(SenderSteamId);

            if (player == null)
                return;

            // Only Admin can change config.
            if (!player.IsAdmin())
            {
                
                return;
            }

            // These will match with names defined in the RegEx patterm <ConfigurableSpeedComponentLogic.ConfigSpeedPattern>
            switch (ConfigName)
            {
                #region reset all settings to stock.

                case "resetall":
                    {
                        JumpDriveInhibitorBlock.Instance.ConfigGeneralComponent.MaxRadius = JumpDriveInhibitorBlock.Instance.DefaultDefinitionValues.MaxRadius;
                        JumpDriveInhibitorBlock.Instance.ConfigGeneralComponent.MaxPowerDrain = JumpDriveInhibitorBlock.Instance.DefaultDefinitionValues.MaxPowerDrain;
                        JumpDriveInhibitorBlock.Instance.IsModified = true;

                        var msg = new StringBuilder();
                        msg.AppendFormat("All settings have been reset to stock standard game settings.");
                        msg.AppendLine();
                        msg.AppendLine("Once you have finished your changes, you must save the game and then restart it immediately for it to take effect.");
                        msg.AppendLine();
                        msg.AppendLine("If you only save the game and do not restart, any player that connects will experience issues.");
                        MessageClientDialogMessage.SendMessage(SenderSteamId, "ConfigSpeed", " ", msg.ToString());
                    }
                    break;

                #endregion

                #region LargeShipMaxSpeed

                case "large":
                case "largeship":
                case "largeshipmaxspeed":
                case "largeshipspeed":
                    if (!string.IsNullOrEmpty(Value))
                    {
                        int decimalTest;
                        if (int.TryParse(Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimalTest))
                        {
                            if (decimalTest > 0)
                            {
                                JumpDriveInhibitorBlock.Instance.ConfigGeneralComponent.MaxRadius = decimalTest;
                                JumpDriveInhibitorBlock.Instance.IsModified = true;

                                var msg = new StringBuilder();
                                msg.AppendFormat("LargeShipMaxSpeed updated to: {0:N0} m/s\r\n", JumpDriveInhibitorBlock.Instance.ConfigGeneralComponent.MaxRadius);
                                msg.AppendFormat("SmallShipMaxSpeed is: {0:N0} m/s\r\n", JumpDriveInhibitorBlock.Instance.ConfigGeneralComponent.MaxPowerDrain);
                                msg.AppendLine();
                                msg.AppendLine();
                                msg.AppendLine("Once you have finished your changes, you must save the game and then restart it immediately for it to take effect.");
                                msg.AppendLine();
                                msg.AppendLine("If you only save the game and do not restart, any player that connects will experience issues.");
                                MessageClientDialogMessage.SendMessage(SenderSteamId, "ConfigSpeed", " ", msg.ToString());

                                // Default values. Not whitelisted:
                                //VRage.Game.MyObjectBuilder_EnvironmentDefinition.Defaults.LargeShipMaxSpeed
                                //VRage.Game.MyObjectBuilder_EnvironmentDefinition.Defaults.SmallShipMaxSpeed
                                return;
                            }
                        }
                    }

                    MessageClientTextMessage.SendMessage(SenderSteamId, "ConfigSpeed", "The new maximum ship speed limit can only be between {0:N0}");
                    break;

                #endregion

                #region SmallShipMaxSpeed

                case "small":
                case "smallship":
                case "smallshipmaxspeed":
                case "smallshipspeed":
                    if (!string.IsNullOrEmpty(Value))
                    {
                        int decimalTest;
                        if (int.TryParse(Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimalTest))
                        {
                            if (decimalTest > 0)
                            {
                                JumpDriveInhibitorBlock.Instance.ConfigGeneralComponent.MaxPowerDrain = decimalTest;
                                JumpDriveInhibitorBlock.Instance.IsModified = true;

                                var msg = new StringBuilder();
                                msg.AppendFormat("SmallShipMaxSpeed updated to: {0:N0} m/s\r\n", JumpDriveInhibitorBlock.Instance.ConfigGeneralComponent.MaxPowerDrain);
                                msg.AppendFormat("LargeShipMaxSpeed is: {0:N0} m/s\r\n", JumpDriveInhibitorBlock.Instance.ConfigGeneralComponent.MaxRadius);
                                msg.AppendLine();
                                msg.AppendLine();
                                msg.AppendLine("Once you have finished your changes, you must save the game and then restart it immediately for it to take effect.");
                                msg.AppendLine();
                                msg.AppendLine("If you only save the game and do not restart, any player that connects will experience issues.");
                                MessageClientDialogMessage.SendMessage(SenderSteamId, "ConfigSpeed", " ", msg.ToString());
                                return;
                            }
                        }
                    }

                    MessageClientTextMessage.SendMessage(SenderSteamId, "ConfigSpeed", "The new maximum ship speed limit can only be between {0:N0}");
                    break;

                #endregion
            }
        }
    }
}