using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows; // MessageBox를 위해 추가

namespace WorkPartner
{
    public static class DataManager
    {
        public static event Action SettingsUpdated;

        // 1. AppData 안에 우리 프로그램 전용 폴더 경로를 만듭니다.
        private static readonly string AppDataFolder;

        // 2. 각 파일의 전체 경로를 속성으로 만들어 쉽게 가져다 쓸 수 있게 합니다.
        public static string SettingsFilePath { get; }
        public static string TimeLogFilePath { get; }
        public static string TasksFilePath { get; }
        public static string TodosFilePath { get; }
        public static string MemosFilePath { get; }
        public static string ModelFilePath { get; }
        public static string ItemsDbFilePath { get; }

        // 프로그램이 시작될 때 단 한 번만 실행되는 생성자
        static DataManager()
        {
            try
            {
                AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WorkPartner");
                Directory.CreateDirectory(AppDataFolder); // 폴더가 없으면 생성

                SettingsFilePath = Path.Combine(AppDataFolder, "app_settings.json");
                TimeLogFilePath = Path.Combine(AppDataFolder, "timelogs.json");
                TasksFilePath = Path.Combine(AppDataFolder, "tasks.json");
                TodosFilePath = Path.Combine(AppDataFolder, "todos.json");
                MemosFilePath = Path.Combine(AppDataFolder, "memos.json");
                ModelFilePath = Path.Combine(AppDataFolder, "FocusPredictionModel.zip");
                // [수정] items_db.json은 AppData로 복사하여 사용하도록 변경
                ItemsDbFilePath = Path.Combine(AppDataFolder, "items_db.json");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"프로그램 초기화 중 오류가 발생했습니다. 프로그램을 재시작해주세요.\n\n오류: {ex.Message}", "초기화 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        // [수정] 제네릭 LoadData 메서드로 통합
        public static T LoadData<T>(string filePath) where T : new()
        {
            if (!File.Exists(filePath))
            {
                return new T();
            }

            try
            {
                var json = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new T(); // 파일이 비어있으면 기본 객체 반환
                }
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<T>(json, options) ?? new T();
            }
            catch (JsonException jsonEx)
            {
                // JSON 파싱 오류 처리
                MessageBox.Show($"'{Path.GetFileName(filePath)}' 파일을 불러오는 중 오류가 발생했습니다. 파일이 손상되었을 수 있습니다. 기본 설정으로 시작합니다.\n\n오류: {jsonEx.Message}", "데이터 로드 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                // 손상된 파일은 백업하고 새로 생성할 수 있습니다.
                try { File.Move(filePath, filePath + ".bak"); } catch { }
                return new T();
            }
            catch (Exception ex)
            {
                // 기타 파일 I/O 오류 처리
                MessageBox.Show($"'{Path.GetFileName(filePath)}' 파일을 불러오는 중 오류가 발생했습니다. 기본 설정으로 시작합니다.\n\n오류: {ex.Message}", "데이터 로드 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return new T();
            }
        }

        // [수정] 제네릭 SaveData 메서드로 통합
        public static void SaveData<T>(string filePath, T data)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                var json = JsonSerializer.Serialize(data, options);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"'{Path.GetFileName(filePath)}' 파일 저장 중 오류가 발생했습니다.\n\n오류: {ex.Message}", "데이터 저장 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static AppSettings LoadSettings()
        {
            return LoadData<AppSettings>(SettingsFilePath) ?? new AppSettings();
        }

        public static void SaveSettings(AppSettings settings)
        {
            SaveData(SettingsFilePath, settings);
        }

        public static void SaveSettingsAndNotify(AppSettings settings)
        {
            SaveSettings(settings);
            SettingsUpdated?.Invoke();
        }

        // AI 모델 파일과 같이, 처음에는 프로그램 폴더에 있다가
        // 수정이 필요할 때 AppData로 복사해야 하는 파일을 준비하는 메서드
        public static void PrepareFileForEditing(string sourceFileName)
        {
            try
            {
                string sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, sourceFileName);
                string destinationPath = Path.Combine(AppDataFolder, sourceFileName);

                // AppData에 파일이 없고, 원본 파일은 있을 때만 복사
                if (!File.Exists(destinationPath) && File.Exists(sourcePath))
                {
                    File.Copy(sourcePath, destinationPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"초기 설정 파일 복사 중 오류 발생: {sourceFileName}\n{ex.Message}");
            }
        }
    }
}
