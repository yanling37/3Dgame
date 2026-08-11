using System;
using System.IO;
using UnityEngine;

namespace DivineWorld.Simulation.Save
{
    /// <summary>
    /// Local JSON save/load under Application.persistentDataPath.
    /// </summary>
    public static class SaveService
    {
        public const int CurrentSchemaVersion = 1;

        public static string SaveRoot =>
            Path.Combine(Application.persistentDataPath, "DivineWorld", "saves");

        public static string GetPath(SaveSlot slot)
        {
            string fileName = slot switch
            {
                SaveSlot.Autosave => "autosave.json",
                SaveSlot.Slot1 => "slot1.json",
                SaveSlot.Slot2 => "slot2.json",
                SaveSlot.Slot3 => "slot3.json",
                _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null)
            };
            return Path.Combine(SaveRoot, fileName);
        }

        public static bool TrySave(SaveSlot slot, SaveGameDto dto, out string error)
        {
            error = null;
            if (dto == null)
            {
                error = "存档数据为空";
                return false;
            }

            try
            {
                Directory.CreateDirectory(SaveRoot);
                dto.schemaVersion = CurrentSchemaVersion;
                dto.savedUtc = DateTime.UtcNow.ToString("o");
                string json = JsonUtility.ToJson(dto, true);
                File.WriteAllText(GetPath(slot), json);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Debug.LogError($"[SaveService] Save failed ({slot}): {ex}");
                return false;
            }
        }

        public static bool TryLoad(SaveSlot slot, out SaveGameDto dto, out string error)
        {
            dto = null;
            error = null;
            string path = GetPath(slot);
            if (!File.Exists(path))
            {
                error = "该槽位没有存档";
                return false;
            }

            try
            {
                string json = File.ReadAllText(path);
                dto = JsonUtility.FromJson<SaveGameDto>(json);
                if (dto == null)
                {
                    error = "存档解析失败";
                    return false;
                }

                if (dto.schemaVersion != CurrentSchemaVersion)
                {
                    error = $"存档版本不兼容（文件 v{dto.schemaVersion}，当前 v{CurrentSchemaVersion}）";
                    dto = null;
                    return false;
                }

                if (dto.world == null)
                {
                    error = "存档缺少世界数据";
                    dto = null;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Debug.LogError($"[SaveService] Load failed ({slot}): {ex}");
                return false;
            }
        }

        public static bool TryGetSlotInfo(SaveSlot slot, out SaveSlotInfo info)
        {
            info = new SaveSlotInfo { Slot = slot, Exists = false };
            if (!TryLoad(slot, out var dto, out _))
            {
                return false;
            }

            info.Exists = true;
            info.WorldName = dto.world != null ? dto.world.WorldName : "";
            info.Year = dto.world != null ? dto.world.Year : 0;
            info.TotalDays = dto.world != null ? dto.world.TotalDays : 0;
            info.SavedUtc = dto.savedUtc ?? "";
            return true;
        }

        public static string SlotLabel(SaveSlot slot) =>
            slot == SaveSlot.Autosave ? "自动" : $"槽{(int)slot}";
    }
}
