using System;
using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Save
{
    public enum SaveSlot
    {
        Autosave = 0,
        Slot1 = 1,
        Slot2 = 2,
        Slot3 = 3
    }

    [Serializable]
    public class SaveGameDto
    {
        public int schemaVersion = SaveService.CurrentSchemaVersion;
        public string savedUtc;
        public int seed;
        public float secondsPerDay = 0.35f;
        public bool autoRun = true;
        public WorldState world;
        public float fertilityBlessing = 1f;
        public float harvestBlessing = 1f;
        public float diseaseCurse = 1f;
        public float stabilityBlessing = 1f;
        public bool hasFocusRegion;
        public RegionId focusRegion;
    }

    [Serializable]
    public class SaveSlotInfo
    {
        public SaveSlot Slot;
        public bool Exists;
        public string WorldName;
        public int Year;
        public int TotalDays;
        public string SavedUtc;
    }
}
