using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Windows;
using Microsoft.ML;

namespace WorkPartner
{
    public static class DataManager
    {
        private static readonly string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WorkPartner");
        public static readonly string SettingsFilePath = Path.Combine(AppDataFolder, "app_settings.json");
        public static readonly string TasksFilePath = Path.Combine(AppDataFolder, "tasks.json");
        public static readonly string TodosFilePath = Path.Combine(AppDataFolder, "todos.json");
        public static readonly string TimeLogFilePath = Path.Combine(AppDataFolder, "time_log.json");
        public static readonly string MemosFilePath = Path.Combine(AppDataFolder, "memos.json");
        public static readonly string ItemsDbFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "items_db.json");
        public static readonly string ModelFilePath = Path.Combine(AppDataFolder, "FocusPredictionModel.zip");

        public static event Action SettingsUpdated;

        static DataManager()
        {
            Directory.CreateDirectory(AppDataFolder);
        }

        public static AppSettings LoadSettings()
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                try
                {
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                catch
                {
                    return new AppSettings(); // Return default if file is corrupt
                }
            }
            return new AppSettings();
        }

        public static void SaveSettingsAndNotify(AppSettings settings)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };
            var json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(SettingsFilePath, json);
            SettingsUpdated?.Invoke();
        }

        public static void PrepareFileForEditing(string resourceFileName)
        {
            string destinationPath = Path.Combine(AppDataFolder, resourceFileName);
            if (!File.Exists(destinationPath))
            {
                try
                {
                    string sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, resourceFileName);
                    if (File.Exists(sourcePath))
                    {
                        File.Copy(sourcePath, destinationPath);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error preparing resource file: {ex.Message}");
                }
            }
        }

        public static void DeleteAllData()
        {
            var filesToDelete = new string[]
            {
                SettingsFilePath,
                TimeLogFilePath,
                TasksFilePath,
                TodosFilePath,
                MemosFilePath,
                ModelFilePath
            };

            foreach (var filePath in filesToDelete)
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
        }
    }
}
