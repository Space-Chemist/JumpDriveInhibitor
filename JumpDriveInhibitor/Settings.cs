namespace JumpDriveInhibitor
{
    class Settings
    {
        public static ConfigGeneral General = new ConfigGeneral();

        public static void LoadSettings()
        {
            General = General.LoadSettings();
        }
    }
}