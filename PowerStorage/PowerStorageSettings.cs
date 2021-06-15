using System;
using UnityEngine;

namespace PowerStorage
{
    [Serializable]
    public class PowerStorageSettings
    {
        [SerializeField]
        public float LossRatio = 0.2f;
        [SerializeField]
        public int SafetyKwIntake = 2000;
        [SerializeField]
        public int SafetyKwDischarge = 2000;
        
        [SerializeField]
        public bool? Chirp = true;
        [SerializeField]
        public bool? Debug = false;
        [SerializeField]
        public bool? Profile = true;
    }
}
