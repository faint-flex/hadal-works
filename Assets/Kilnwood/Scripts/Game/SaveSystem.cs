using System;
using System.IO;
using Kilnwood.Sim;
using UnityEngine;

namespace Kilnwood.Game
{
    /// <summary>One save slot. Autosaves every 2 minutes, on quit, and from the menu.</summary>
    public static class SaveSystem
    {
        public static string PathOverride;
        public static string FilePath => PathOverride ?? Path.Combine(Application.persistentDataPath, "kilnwood-save.json");
        public static bool Exists => File.Exists(FilePath);

        public static void Save(World w)
        {
            try
            {
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(SaveData.From(w)));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e) { Debug.LogError("Kilnwood: save failed: " + e.Message); }
        }

        public static World Load(Balance b)
        {
            try
            {
                if (!Exists) return null;
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                return data?.ToWorld(b);
            }
            catch (Exception e)
            {
                Debug.LogError("Kilnwood: save unreadable: " + e.Message);
                return null;
            }
        }
    }
}
