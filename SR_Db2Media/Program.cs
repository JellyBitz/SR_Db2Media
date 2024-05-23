using SR_Db2Media.Silkroad.Utils;
using SR_Db2Media.Utils.Database;

using Newtonsoft.Json;
using SRO.PK2;

using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Diagnostics;

namespace SR_Db2Media
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.Title = "SR_Db2Media v" + FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion + " - https://github.com/JellyBitz/SR_Db2Media";
            Console.WriteLine(Console.Title + Environment.NewLine);

            Pk2Stream pk2 = null;
            try
            {
                // Load settings
                Settings settings = LoadOrCreateFile("Settings.json");

                // Create output folder
                if(!string.IsNullOrEmpty(settings.OutputPath))
                    Directory.CreateDirectory(settings.OutputPath);

                // Create database handler
                SQLDataDriver sql = new SQLDataDriver(settings.SQLConnection.Host, settings.SQLConnection.Username, settings.SQLConnection.Password, settings.SQLConnection.Database);

                // Check Pk2 requirements to import into media
                if (settings.ImportToPk2.Enabled && File.Exists(settings.ImportToPk2.MediaPk2Path))
                {
                    // Try to initialize pk2
                    try
                    {
                        pk2 = new Pk2Stream(settings.ImportToPk2.MediaPk2Path, settings.ImportToPk2.BlowfishKey);
                    }
                    catch(Exception ex)
                    {
                        pk2 = null;
                        Console.WriteLine("PK2 Error: " + ex.Message);
                    }
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
                            foreach (var columns in rows)
                                sw.WriteLine(string.Join("\t",columns));
                        }
                        // Import file into media
                        if (pk2 != null)
                        {
                            if (pk2.AddFile(Path.Combine(settings.ImportToPk2.TextdataPath, Path.GetFileName(filePath)), File.ReadAllBytes(filePath)))
                                Console.WriteLine("Imported: " + filePath);
                            else
                                Console.WriteLine("Error importing \"" + filePath + "\"...");
                        }

                        // Encrypt skilldata
                        if (settings.UseSkillDataEncryptor && query2path.Path.ToLowerInvariant().StartsWith("skilldata_"))
                        {
                            var filePathEnc = Path.Combine(settings.OutputPath, Path.GetFileNameWithoutExtension(query2path.Path) + "enc.txt");
                            Console.WriteLine("Encrypting: " + filePath);
                            SkillDataEncryptor.EncryptFile(filePath, filePathEnc);
                            // Import file into media
                            if (pk2 != null)
                            {
                                if (pk2.AddFile(Path.Combine(settings.ImportToPk2.TextdataPath, Path.GetFileName(filePathEnc)), File.ReadAllBytes(filePathEnc)))
                                    Console.WriteLine("Imported: " + filePathEnc);
                                else
                                    Console.WriteLine("Error importing \"" + filePathEnc + "\"...");
                            }
                        }
                    }
                    else
                    {
                        // Delete file
                        if (File.Exists(filePath))
                            File.Delete(filePath);
                    }
                }
            }
            catch (Exception ex)
            {
                // User friendly
                Console.WriteLine("Error: " + ex.Message);
                Console.ReadKey();
            }
            finally
            {
                pk2?.Dispose();
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
        #endregion
    }
}
