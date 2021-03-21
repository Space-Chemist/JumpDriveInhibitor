using System;
using System.Xml.Serialization;
using Sandbox.ModAPI;
using VRage.Utils;

namespace JumpDriveInhibitor
{
	[XmlRoot("Beacon")]
    public class ConfigGeneral
    {
		public float MaxRadius { get; set; }
		public float MaxPowerDrainInKw { get; set; }

		public ConfigGeneral()
		{
			MaxRadius = 6000;
			MaxPowerDrainInKw = 20000;
		}

		public ConfigGeneral LoadSettings()
		{
			if (MyAPIGateway.Utilities.FileExistsInWorldStorage("BeaconSettings.xml", typeof(ConfigGeneral)) == true)
			{
				try
				{
					ConfigGeneral config = null;
					var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage("BeaconSettings.xml", typeof(ConfigGeneral));
					string configcontents = reader.ReadToEnd();
					config = MyAPIGateway.Utilities.SerializeFromXML<ConfigGeneral>(configcontents);
					//MyVisualScriptLogicProvider.SendChatMessage(config.ToString(), "config: ");
					MyLog.Default.WriteLine("config read");

					return config;
				}
				catch (Exception)
				{
					MyLog.Default.WriteLine("unable to read config");

					var defaultSettings = new ConfigGeneral();

					return defaultSettings;
				}
			}

			var settings = new ConfigGeneral();
			try
			{
				//MyVisualScriptLogicProvider.SendChatMessage("new config.", "Debug");
				using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage("BeaconSettings.xml", typeof(ConfigGeneral)))
				{
					MyLog.Default.WriteLine("making config");
					writer.Write(MyAPIGateway.Utilities.SerializeToXML(settings));
					//writer.Write("config");
					//MyVisualScriptLogicProvider.SendChatMessage(settings.ToString(), "Settings: ");
					//MyVisualScriptLogicProvider.SendChatMessage("new config made.", "Debug");

				}

			}
			catch (Exception)
			{
				MyLog.Default.WriteLine("could not write config");

			}

			return settings;
		}

		public string SaveSettings(ConfigGeneral settings)
		{
			try
			{
				using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage("BeaconSettings.xml", typeof(ConfigGeneral)))
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