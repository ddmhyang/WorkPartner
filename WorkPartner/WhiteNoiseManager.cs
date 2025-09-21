using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace WorkPartner
{
    public class DataManager
    {
        private static readonly string dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        private static readonly string todosFilePath = Path.Combine(dataPath, "todos.json");
        private static readonly string logsFilePath = Path.Combine(dataPath, "logs.json");
        private static readonly string memoFilePath = Path.Combine(dataPath, "memo.txt");
        private static readonly string characterDataFilePath = Path.Combine(dataPath, "character.json");
        private static readonly string shopItemsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "items_db.json");
        private static readonly string soundSettingsFilePath = Path.Combine(dataPath, "sound_settings.json");
        private static readonly string appSettingsFilePath = Path.Combine(dataPath, "settings.json"); // 설정 파일 경로 추가

        private static DataManager _instance;
        public static DataManager Instance => _instance ?? (_instance = new DataManager());

        private List<TodoItem> _todos;
        private Dictionary<DateTime, List<TimeLogEntry>> _logs;
        private CharacterData _characterData;
        private List<ShopItem> _shopItems;
        private Dictionary<string, double> _soundSettings;
        private AppSettings _appSettings; // 설정 객체 추가

        public event Action SettingsChanged; // 설정 변경 알림 이벤트

        private DataManager()
        {
            Directory.CreateDirectory(dataPath);
            LoadTodos();
            LoadLogs();
            LoadCharacterData();
            LoadShopItems();
            LoadSoundSettings();
            LoadSettings(); // 설정 로드
        }

        // To-Do
        public List<TodoItem> GetTodos() => _todos;
        public void AddTodo(TodoItem todo) { _todos.Add(todo); SaveTodos(); }
        public void RemoveTodo(TodoItem todo) { _todos.Remove(todo); SaveTodos(); }
        public void SaveTodos() => File.WriteAllText(todosFilePath, JsonConvert.SerializeObject(_todos, Formatting.Indented));
        private void LoadTodos() => _todos = File.Exists(todosFilePath) ? JsonConvert.DeserializeObject<List<TodoItem>>(File.ReadAllText(todosFilePath)) : new List<TodoItem>();

        // Logs
        public List<TimeLogEntry> GetLogs(DateTime date) => _logs.ContainsKey(date.Date) ? _logs[date.Date] : new List<TimeLogEntry>();
        private void LoadLogs() => _logs = File.Exists(logsFilePath) ? JsonConvert.DeserializeObject<Dictionary<DateTime, List<TimeLogEntry>>>(File.ReadAllText(logsFilePath)) : new Dictionary<DateTime, List<TimeLogEntry>>();

        // Memo
        public string GetMemo() => File.Exists(memoFilePath) ? File.ReadAllText(memoFilePath) : "";
        public void SaveMemo(string content) => File.WriteAllText(memoFilePath, content);

        // Character Data
        public CharacterData GetCharacterData() => _characterData;
        public void SaveCharacterData(CharacterData data) { _characterData = data; File.WriteAllText(characterDataFilePath, JsonConvert.SerializeObject(_characterData, Formatting.Indented)); }
        private void LoadCharacterData() => _characterData = File.Exists(characterDataFilePath) ? JsonConvert.DeserializeObject<CharacterData>(File.ReadAllText(characterDataFilePath)) : new CharacterData();

        // Shop Items
        public List<ShopItem> GetAllShopItems() => _shopItems;
        private void LoadShopItems() => _shopItems = File.Exists(shopItemsFilePath) ? JsonConvert.DeserializeObject<List<ShopItem>>(File.ReadAllText(shopItemsFilePath)) : new List<ShopItem>();

        // Sound Settings
        public Dictionary<string, double> GetSoundSettings() => _soundSettings;
        public void SaveSoundSetting(string soundName, double volume) { _soundSettings[soundName] = volume; File.WriteAllText(soundSettingsFilePath, JsonConvert.SerializeObject(_soundSettings, Formatting.Indented)); }
        private void LoadSoundSettings() => _soundSettings = File.Exists(soundSettingsFilePath) ? JsonConvert.DeserializeObject<Dictionary<string, double>>(File.ReadAllText(soundSettingsFilePath)) : new Dictionary<string, double>();

        // App Settings
        public AppSettings GetSettings() => _appSettings;
        public void SaveSettingsAndNotify(AppSettings settings)
        {
            _appSettings = settings;
            File.WriteAllText(appSettingsFilePath, JsonConvert.SerializeObject(_appSettings, Formatting.Indented));
            SettingsChanged?.Invoke(); // 변경 사항 알림
        }
        private void LoadSettings() => _appSettings = File.Exists(appSettingsFilePath) ? JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(appSettingsFilePath)) : new AppSettings();

        // 파일 경로 접근 (레거시 코드 호환용)
        public string GetLogsFilePath() => logsFilePath;
        public string GetModelFilePath() => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "model.zip");
    }
}

