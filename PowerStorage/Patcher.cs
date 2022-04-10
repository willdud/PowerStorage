using HarmonyLib;

namespace PowerStorage
{
    public class Patcher
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
    
    //[HarmonyPatch(typeof(ElectricityManager), nameof(ElectricityManager.UpdateGrid))]
    //[HarmonyPatch(MethodType.Normal, typeof(float), typeof(float), typeof(float), typeof(float))]
    //class PatchUpdateGrid
    //{
    //    static void Prefix(float minX, float minZ, float maxX, float maxZ)
    //    {
            
    //    }
    //}
}