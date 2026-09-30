using System;
using System.IO;
using Polykov.Inventory;
using UnityEngine;

namespace Polykov.Lobby
{
    /// <summary>Saves the player's profile (inventory) as text in the persistent data folder.</summary>
    public static class ProfileStore
    {
        public static string Path => System.IO.Path.Combine(Application.persistentDataPath, "profile.txt");

        public static Profile Load(ItemDatabase db)
        {
            try
            {
                return File.Exists(Path) ? ProfileSerializer.Load(File.ReadAllText(Path), db) : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Could not read the profile, starting a new one: " + e.Message);
                return null;
            }
        }

        public static void Save(Profile profile)
        {
            try
            {
                string tmp = Path + ".tmp";
                File.WriteAllText(tmp, ProfileSerializer.Save(profile));
                if (File.Exists(Path)) File.Delete(Path);
                File.Move(tmp, Path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Could not save the profile: " + e.Message);
            }
        }

        public static void Delete()
        {
            if (File.Exists(Path)) File.Delete(Path);
        }
    }
}
