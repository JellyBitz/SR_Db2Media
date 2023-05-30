using Newtonsoft.Json;
using Pk2WriterAPI;
using SR_Db2Media.Silkroad.Utils;
using SR_Db2Media.Utils.Database;
using System;
using System.IO;
using System.Text;

namespace SR_Db2Media
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.Title = "SR_Db2Media - https://github.com/JellyBitz/SR_Db2Media";
            Console.WriteLine(Console.Title + Environment.NewLine);

            try
            {
                // Load settings
                Settings settings = LoadOrCreateFile("Settings.json");

                // Create output folder
                Directory.CreateDirectory(settings.OutputPath);

                // Create database handler
                SQLDataDriver sql = new SQLDataDriver(settings.Connection.Host, settings.Connection.Username, settings.Connection.Password, settings.Connection.Database);

                // Check Pk2 requirements to import
                bool canImport = File.Exists(settings.ImportToPk2.MediaPk2Path) && File.Exists(settings.ImportToPk2.GFXFileManagerDllPath);
                if (canImport)
                {
                    // Check files used to initialize
                    Pk2Writer.Initialize(settings.ImportToPk2.GFXFileManagerDllPath);
                }

                // Run setup
                foreach (var query2path in settings.Setup)
                {
                    var filePath = Path.Combine(settings.OutputPath, query2path.Path);
                    // Check if is enabled
                    if (query2path.Enabled)
                    {
                        var rows = sql.GetTableResult(query2path.Query);
                        // Overwrite file
                        Console.WriteLine("Creating: "+filePath);
                        using (StreamWriter sw = new StreamWriter(filePath, false, Encoding.Unicode))
                        {
                            foreach (var row in rows)
                                sw.WriteLine(string.Join("\t",row));
                        }
                        // Import file into media
                        if(canImport)
                            ImportFile(settings,filePath);

                        // Encrypt skilldata
                        if (settings.UseSkillDataEncryptor && query2path.Path.ToLowerInvariant().StartsWith("skilldata_"))
                        {
                            var filePathEnc = Path.Combine(settings.OutputPath, Path.GetFileNameWithoutExtension(query2path.Path) + "enc.txt");
                            Console.WriteLine("Encrypting: " + filePath);
                            SkillDataEncryptor.EncryptFile(filePath, filePathEnc);
                            // Import file into media
                            if (canImport)
                                ImportFile(settings, filePathEnc);
                        }
                    }
                    else
                    {
                        // Delete file
                        if (File.Exists(filePath))
                            File.Delete(filePath);
                    }
                }

                // Close media.pk2
                if (canImport)
                    Pk2Writer.Deinitialize();
            }
            catch (Exception ex)
            {
                // User friendly
                Console.WriteLine(ex.Message);
                Console.ReadKey();
            }
        }
        #region Private Helpers
        /// <summary>
        /// Creates a default settings if file doesn't exists
        /// </summary>
        private static Settings LoadOrCreateFile(string FilePath)
        {
            if (!File.Exists(FilePath))
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(new Settings(true), Formatting.Indented));
            return JsonConvert.DeserializeObject<Settings>(File.ReadAllText(FilePath));
        }
        /// <summary>
        /// Import file into media.pk2
        /// </summary>
        private static void ImportFile(Settings settings, string filePath)
        {
            // Open Pk2
            if (Pk2Writer.Open(settings.ImportToPk2.MediaPk2Path, settings.ImportToPk2.BlowfishKey))
            {
                if (Pk2Writer.ImportFile(Path.Combine(settings.ImportToPk2.TextdataPath, Path.GetFileName(filePath)), filePath))
                    Console.WriteLine("Imported: " + filePath);
                else
                    Console.WriteLine("Error importing \"" + filePath + "\"...");
                // Close Pk2
                Pk2Writer.Close();
            }
        }
        #endregion
    }
}
