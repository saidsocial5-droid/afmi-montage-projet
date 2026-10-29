using System;
using System.IO;

namespace AFMI_Montage_projet_2.Services
{
    public static class DataMigrationService
    {
        public static void MigrateLegacyData()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            
            // New unified paths
            string newRoamingFolder = Path.Combine(roamingAppData, "AFMI_Montage_Projet");
            string newLocalFolder = Path.Combine(localAppData, "AFMI_Montage_Projet");
            
            if (!Directory.Exists(newRoamingFolder)) Directory.CreateDirectory(newRoamingFolder);
            if (!Directory.Exists(newLocalFolder)) Directory.CreateDirectory(newLocalFolder);

            // 1. Settings Migration
            string oldSettingsFolder = Path.Combine(roamingAppData, "AFMIV3");
            string oldSettingsFile = Path.Combine(oldSettingsFolder, "settings.json");
            string newSettingsFile = Path.Combine(newRoamingFolder, "settings.json");
            
            if (File.Exists(oldSettingsFile) && !File.Exists(newSettingsFile))
            {
                File.Copy(oldSettingsFile, newSettingsFile, true);
            }

            // 2. Google Auth Migration
            string oldAuthFolder = Path.Combine(roamingAppData, "AFMI_ProjectManager", "GoogleAuth");
            string newAuthFolder = Path.Combine(newRoamingFolder, "GoogleAuth");
            
            if (Directory.Exists(oldAuthFolder) && !Directory.Exists(newAuthFolder))
            {
                Directory.CreateDirectory(newAuthFolder);
                foreach (var file in Directory.GetFiles(oldAuthFolder))
                {
                    string dest = Path.Combine(newAuthFolder, Path.GetFileName(file));
                    File.Copy(file, dest, true);
                }
            }
            
            // 3. Database config is currently in LocalAppData. Let's move it to Roaming
            string oldDbConfig = Path.Combine(newLocalFolder, "dbconfig.json");
            string newDbConfig = Path.Combine(newRoamingFolder, "dbconfig.json");
            
            if (File.Exists(oldDbConfig) && !File.Exists(newDbConfig))
            {
                File.Copy(oldDbConfig, newDbConfig, true);
            }
            
            // DB stays in newLocalFolder (which is the same as old db folder, so no move needed for projects.db)
        }
    }
}
