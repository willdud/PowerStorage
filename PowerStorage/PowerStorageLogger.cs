using System;
using UnityEngine;

namespace PowerStorage
{
    public class PowerStorageLogger
    {
        public static void Log(string message, PowerStorageMessageType messageType)
        {
            if ((PowerStorage.ShownMessageTypes & messageType) == 0)
                return;

            Debug.Log(message);
        }
        public static void LogWarning(string message, PowerStorageMessageType messageType)
        {
            if ((PowerStorage.ShownMessageTypes & messageType) == 0)
                return;

            Debug.LogWarning(message);
        }
        public static void LogError(string message, PowerStorageMessageType messageType)
        {
            if ((PowerStorage.ShownMessageTypes & messageType) == 0)
                return;

            Debug.LogError(message);
        }
    }

    [Flags]
    public enum PowerStorageMessageType
    {
        None = 0,

        Charging = 1,
        Discharging = 2,
        Grid = Charging | Discharging,

        NetworkMapping = 4,
        NetworkMerging = 8,
        Network = NetworkMapping | NetworkMerging,
        
        Ui = 16,
        Saving = 32 ,
        Loading = 64,
        Simulation = 128,
        Misc = Ui | Saving | Loading | Simulation,

        All = Grid | Network | Misc,
    }
}
