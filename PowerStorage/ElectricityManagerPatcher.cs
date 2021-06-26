using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace PowerStorage
{
    public class ElectricityManagerPatcher
    {
        private static bool _patched = false;
        private const string HarmonyId = "willdud.PowerStorage";

        public static void DoPatching()
        {
            if (_patched) 
                return;

            _patched = true;
            PowerStorageLogger.Log("Harmony Patching...", PowerStorageMessageType.All);
            var harmony = CreateHarmony();
            
            harmony.PatchAll(typeof(PowerStorage).Assembly);
        }

        public static void UnPatchAll()
        {
            if (!_patched) 
                return;

            var harmony = CreateHarmony();
            harmony.UnpatchAll(HarmonyId);

            PowerStorageLogger.Log("Harmony Patches Reverted", PowerStorageMessageType.All);
            _patched = false;
        }

        private static Harmony CreateHarmony()
        {
            return new Harmony(HarmonyId);
        }
    }

    [HarmonyPatch(typeof(ElectricityManager), nameof(ElectricityManager.TryFetchElectricity))]
    [HarmonyPatch(MethodType.Normal, typeof(Vector3), typeof(int), typeof(int))]
    class PatchTryFetchElectricity
    {
        static void Prefix(Vector3 pos, int rate, int max)
        {
            GridsBuildingsRollup.AddConsumption(pos, rate.ToKw());
        }
    }

    [HarmonyPatch(typeof(ElectricityManager), nameof(ElectricityManager.TryDumpElectricity))]
    [HarmonyPatch(MethodType.Normal, typeof(Vector3), typeof(int), typeof(int))]
    class PatchTryDumpElectricity
    {
        static void Prefix(Vector3 pos, int rate, int max)
        {
            GridsBuildingsRollup.AddCapacity(pos, rate.ToKw());
        }
    }

    [HarmonyPatch(typeof(ElectricityManager), nameof(ElectricityManager.UpdateGrid))]
    [HarmonyPatch(MethodType.Normal, typeof(float), typeof(float), typeof(float), typeof(float))]
    class PatchUpdateGrid
    {
        static void Prefix(float minX, float minZ, float maxX, float maxZ)
        {
            GridsBuildingsRollup.UpdateGrid();
        }
    }

    [HarmonyPatch(typeof(BuildingAI), nameof(BuildingAI.CreateBuilding))]
    class PatchCreateBuilding
    {
        static void Prefix(ushort buildingID, ref Building data)
        {
            PowerStorage.RegisterBuilding(buildingID, data);
        }
    }

    [HarmonyPatch(typeof(BuildingAI), nameof(BuildingAI.ReleaseBuilding))]
    class PatchReleaseBuilding
    {
        static void Prefix(ushort buildingID, ref Building data)
        {
            var pair = GridsBuildingsRollup.MasterBuildingList.FirstOrDefault(p => p.Index == buildingID);
            if (pair != null)
            {
                Object.Destroy(pair.GridGameObject);
                GridsBuildingsRollup.MasterBuildingList.Remove(pair);
            }
        }
    }

    [HarmonyPatch(typeof(BuildingAI), nameof(BuildingAI.BeginRelocating))]
    class PatchBeginRelocating
    {
        static void Prefix(ushort buildingID, ref Building data)
        {
            var pair = GridsBuildingsRollup.MasterBuildingList.FirstOrDefault(p => p.Index == buildingID);
            if (pair != null)
            {
                Object.Destroy(pair.GridGameObject);
                GridsBuildingsRollup.MasterBuildingList.Remove(pair);
            }
        }
    }

    [HarmonyPatch(typeof(BuildingAI), nameof(BuildingAI.EndRelocating))]
    class PatchEndRelocating
    {
        static void Prefix(ushort buildingID, ref Building data)
        {
            PowerStorage.RegisterBuilding(buildingID, data);
        }
    }
}
