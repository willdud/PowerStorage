using UnityEngine;

namespace PowerStorage
{
    public class PowerStorageLogger
    {
        public static void Log(string message)
        {
            if (!PowerStorage.DebugLog)
                return;

            Debug.Log(message);
        }
        public static void LogError(string message)
        {
            if (!PowerStorage.DebugLog)
                return;

            Debug.LogError(message);
        }
    }
}
