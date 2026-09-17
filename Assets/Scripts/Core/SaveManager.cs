using System.IO;
using UnityEngine;

namespace Core
{
    
    /// <summary>
    /// Handles reading and writing player save data (best lap times, settings).
    /// Uses versioned JSON files in Application.persistentDataPath.
    /// Phase 12 will flesh this out.
    /// </summary> 
    public class SaveManager : MonoBehaviour
    {
        private const string SaveFileName = "save_v1.json";
        private const int CurrentVersion = 1;

        private SaveData currentData;

        private string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        private void Awake()
        {
            Load();
        }

        #region Public API
        public float GetBestLapTime(string trackId)
        {
            if (currentData == null || !currentData.BestLapTimes.ContainsKey(trackId))
                return -1f; // No time recorded
            return currentData.BestLapTimes[trackId];
        }

        public void TrySetBestLapTime(string trackId, float time)
        {
            if (currentData == null) currentData = new() { Version = CurrentVersion };

            if (!currentData.BestLapTimes.ContainsKey(trackId) || time < currentData.BestLapTimes[trackId])
            {
                currentData.BestLapTimes[trackId] = time;
                Save();
            }
        }
        #endregion

        #region IO
        public void Save()
        {
            string json = JsonUtility.ToJson(currentData, prettyPrint: true);
            File.WriteAllText(SavePath, json);
            Debug.Log($"[SaveManager] Saved data to {SavePath}");
        }

        private void Load()
        {
            if (File.Exists(SavePath))
            {
                string json = File.ReadAllText(SavePath);
                currentData = JsonUtility.FromJson<SaveData>(json);
            }
            else
            {
                currentData = NewSave();
            }
        }

        private static SaveData NewSave() => new()
        {
            Version = CurrentVersion, 
            BestLapTimes = new  SerializableDictionary<string, float>()
        };
        #endregion
    }

    // Data model
    [System.Serializable]
    public class SaveData
    {
        public int Version;
        public SerializableDictionary<string, float> BestLapTimes;
    }

    /// <summary>
    /// JsonUtility-compatible dictionary wrapper
    /// </summary>
    [System.Serializable]
    public class SerializableDictionary<TKey, TValue>
    {
        public System.Collections.Generic.List<TKey> Keys = new();
        public System.Collections.Generic.List<TValue> Values = new();

        public bool TryGetValue(TKey key, out TValue value)
        {
            int index = Keys.IndexOf(key);
            if (index >= 0)
            {
                value = Values[index];
                return true;
            }
            value = default;
            return false;
        }

        public void Add(TKey key, TValue value)
        {
            Keys.Add(key);
            Values.Add(value);
        }

        public bool ContainsKey(TKey key) => Keys.Contains(key);

        public TValue this[TKey key]
        {
            get { int i = Keys.IndexOf(key); return i >= 0 ? Values[i] : default; }
            set
            {
                int i = Keys.IndexOf(key);
                if (i >= 0) Values[i] = value;
                else Add(key, value);
            }
        }
    }
}
