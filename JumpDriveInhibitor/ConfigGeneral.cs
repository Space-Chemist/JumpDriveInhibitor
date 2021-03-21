using System;
using System.Globalization;
using System.Xml.Serialization;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Utils;

namespace JumpDriveInhibitor
{
	[ProtoContract]
	[Serializable]
	[XmlRoot("JumpInhibitor")]
    public class ConfigGeneral
    {
		[ProtoMember(1)]
        public float MaxRadius { get; set; }

        [ProtoMember(2)]
        public float MaxPowerDrain { get; set; }

        /// <summary>
        /// Make a copy of all values within <see cref="ConfigGeneral"/>.
        /// </summary>
        public ConfigGeneral()
        {
	        MaxRadius = 6000;
	        MaxPowerDrain = 200000;
        }
        
        public ConfigGeneral LoadSettings()
        {
	        if (MyAPIGateway.Utilities.FileExistsInLocalStorage("PrizeSettings.xml", typeof(ConfigGeneral)) == true)
	        {
		        try
		        {
			        ConfigGeneral config = null;
			        var reader = MyAPIGateway.Utilities.ReadFileInLocalStorage("PrizeSettings.xml", typeof(ConfigGeneral));
			        string configcontents = reader.ReadToEnd();
			        config = MyAPIGateway.Utilities.SerializeFromXML<ConfigGeneral>(configcontents);
			        //MyVisualScriptLogicProvider.SendChatMessage(config.ToString(), "config: ");
			        if (!MyAPIGateway.Session.IsServer) return config;
			        var msg = $"{config.MaxRadius.ToString(CultureInfo.InvariantCulture)}-{config.MaxPowerDrain.ToString(CultureInfo.InvariantCulture)}";
			        NetworkService.SendPacket(msg);
			        return config;
		        }
		        catch (Exception)
		        {

			        var defaultSettings = new ConfigGeneral();

			        return defaultSettings;
		        }
	        }

	        var settings = new ConfigGeneral();
	        try
	        {
		        //MyVisualScriptLogicProvider.SendChatMessage("new config.", "Debug");
		        using (var writer = MyAPIGateway.Utilities.WriteFileInLocalStorage("PrizeSettings.xml", typeof(ConfigGeneral)))
		        {
			        writer.Write(MyAPIGateway.Utilities.SerializeToXML(settings));
			        //writer.Write("config");
			        //MyVisualScriptLogicProvider.SendChatMessage(settings.ToString(), "Settings: ");
			        //MyVisualScriptLogicProvider.SendChatMessage("new config made.", "Debug");

		        }

	        }
	        catch (Exception)
	        {

	        }

	        return settings;
        }

        public string SaveSettings(ConfigGeneral settings)
        {
	        try
	        {
		        using (var writer = MyAPIGateway.Utilities.WriteFileInLocalStorage("PrizeSettings.xml", typeof(ConfigGeneral)))
		        {

			        writer.Write(MyAPIGateway.Utilities.SerializeToXML(settings));

		        }



		        return "Settings Updated Successfully.";
	        }
	        catch (Exception)
	        {

	        }

	        return "Settings Changed, But Could Not Be Saved To XML. Changes May Be Lost On Session Reload.";
        }
    }
	
}