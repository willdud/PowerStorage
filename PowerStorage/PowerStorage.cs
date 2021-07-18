using System;
using System.IO;
using CitiesHarmony.API;
using ColossalFramework.UI;
using ICities;
using PowerStorage.Supporting;
using UnityEngine;

namespace PowerStorage
{
    public class PowerStorage : IUserMod, ISerializableDataExtension
    {
        public static float LossRatio { get; set; } = 0.2f;
        public static int SafetyKwIntake { get; set; } = 2000;
        public static int SafetyKwDischarge { get; set; } = 2000;
        public static bool Chirp { get; set; } = true;
        public static bool DebugLog { get; set; } = false;
        public static bool Profile { get; set; } = false;
        public static bool DebugRenders { get; set; } = false;
        public static PowerStorageMessageType ShownMessageTypes { get; set; } = PowerStorageMessageType.None;

        public string Name => "Power Storage";
        public string Description => "A portion of power generated is put aside to be drawn from when the grid is low.";

        private string FullSavePath => Path.Combine(Application.dataPath, ConfigFile);
        private const string ConfigFile = "PowerStorage.Config.json";


        public void OnSettingsUI(UIHelperBase helper)
        {
            LoadSettings();

            UISlider sliderPowerLoss = null;
            UISlider sliderChargeSafety = null;
            UISlider sliderDischargeSafety = null;

            var group = helper.AddGroup("Power Storage Settings");
            sliderPowerLoss = (UISlider)group.AddSlider("Power loss on conversion", 0, 1, 0.1f, LossRatio, (value) =>
            {
                if (sliderPowerLoss != null)
                {
                    sliderPowerLoss.tooltip = $"{LossRatio*100f}%";
                    sliderPowerLoss.RefreshTooltip();
                }
                    
                LossRatio = value;
            });

            sliderChargeSafety = (UISlider)group.AddSlider("Safety When Charging (KW)", 0, 10000, 500, SafetyKwIntake, (value) =>
            {
                if (sliderChargeSafety != null)
                {
                    sliderChargeSafety.tooltip = $"{SafetyKwIntake}KW";
                    sliderChargeSafety.RefreshTooltip();
                }
                SafetyKwIntake = Convert.ToInt32(value);
            });

            group.AddSpace(20);

            sliderDischargeSafety = (UISlider)group.AddSlider("Safety When Discharging (KW)", 0, 10000, 500, SafetyKwDischarge, (value) =>
            {
                if (sliderDischargeSafety != null)
                {
                    sliderDischargeSafety.tooltip = $"{SafetyKwDischarge}KW";
                    sliderDischargeSafety.RefreshTooltip();
                }
                SafetyKwDischarge = Convert.ToInt32(value);
            });

            group.AddSpace(20);
            
            group.AddCheckbox("Chrip About Low Power", Chirp, isChecked =>
            {
                Chirp = isChecked;
            });
            
            group.AddSpace(20);

            group.AddCheckbox("Profile Logging", Profile, isChecked =>
            {
                Profile = isChecked;
            });
            
            group.AddSpace(20);

            group.AddCheckbox("Debug Rendering", DebugRenders, isChecked =>
            {
                DebugRenders = isChecked;
            });

            group.AddSpace(20);

            group.AddDropdown("Logging Type", Enum.GetNames(typeof(PowerStorageMessageType)), (int)ShownMessageTypes, (value) =>
            {
                switch (value)
                {
                    case 0:
                        DebugLog = false;
                        ShownMessageTypes = PowerStorageMessageType.None;
                        break;
                    case 1:
                        DebugLog = true;
                        ShownMessageTypes = PowerStorageMessageType.Charging;
                        break;
                    case 2:
                        DebugLog = true;
                        ShownMessageTypes = PowerStorageMessageType.Discharging;
                        break;
                    case 3:
                        DebugLog = true;
                        ShownMessageTypes = PowerStorageMessageType.Grid;
                        break;
                    case 4:
                        DebugLog = true;
                        ShownMessageTypes = PowerStorageMessageType.NetworkMapping;
                        break;
                    case 5:
                        DebugLog = true;
                        ShownMessageTypes = PowerStorageMessageType.NetworkMerging;
                        break;
                    case 6:
                        DebugLog = true;
                        ShownMessageTypes = PowerStorageMessageType.Network;
                        break;
                    case 7:
                        DebugLog = true;
                        ShownMessageTypes = PowerStorageMessageType.Ui;
                        break;
                    case 8:
                        DebugLog = true;
                        ShownMessageTypes = PowerStorageMessageType.Saving;
                        break;
                    case 9:
                        DebugLog = true;
                        ShownMessageTypes = PowerStorageMessageType.Loading;
                        break;
                    case 10:
                        DebugLog = true;
                        ShownMessageTypes = PowerStorageMessageType.Simulation;
                        break;
                    case 11:
                        DebugLog = true;
                        ShownMessageTypes = PowerStorageMessageType.Misc;
                        break;
                    case 12:
                        DebugLog = true;
                        ShownMessageTypes = PowerStorageMessageType.All;
                        break;
                }
            });
        }
        
        public void LoadSettings()
        {
            try 
            {
                if (File.Exists(FullSavePath))
                {
                    var fileContent = File.ReadAllText(FullSavePath);
                    var psSettings = JsonUtility.FromJson<PowerStorageSettings>(fileContent);

                    LossRatio = psSettings.LossRatio;
                    SafetyKwIntake = psSettings.SafetyKwIntake;
                    SafetyKwDischarge = psSettings.SafetyKwDischarge;
                    if(psSettings.Chirp.HasValue)
                        Chirp = psSettings.Chirp.Value;
                    if(psSettings.Debug.HasValue)
                        DebugLog = psSettings.Debug.Value;
                    if(psSettings.Profile.HasValue)
                        Profile = psSettings.Profile.Value;
                }
            } 
            catch (Exception e) 
            {
                PowerStorageLogger.LogError(e.Message, PowerStorageMessageType.Loading);
                PowerStorageLogger.Log("Using default settings: " + e, PowerStorageMessageType.Loading);
            }
        }
        public void SaveSettings()
        {
            try
            {
                PowerStorageLogger.Log("Full Save Path: " + FullSavePath, PowerStorageMessageType.Saving);
                var psSettings = new PowerStorageSettings
                {
                    LossRatio = LossRatio,
                    SafetyKwIntake = SafetyKwIntake,
                    SafetyKwDischarge = SafetyKwDischarge,
                    Chirp = Chirp,
                    Debug = DebugLog,
                    Profile = Profile,
                    DebugRenders = DebugRenders
                };
                File.WriteAllText(FullSavePath, JsonUtility.ToJson(psSettings));
            } 
            catch (Exception e) 
            {
                PowerStorageLogger.LogError(e.Message, PowerStorageMessageType.Saving);
                PowerStorageLogger.Log("Couldn't save settings", PowerStorageMessageType.Saving);
            }
        }
        
        public void OnCreated(ISerializableData serializedData)
        { }

        public void OnReleased()
        { }

        public void OnLoadData()
        {
            LoadSettings();
        }

        public void OnSaveData()
        {
            SaveSettings();
        }

        public void OnEnabled()
        {
            HarmonyHelper.DoOnHarmonyReady(Patcher.DoPatching);
        }
        
        public void OnDisabled() 
        {
            if (HarmonyHelper.IsHarmonyInstalled) 
                Patcher.UnPatchAll();
        }
    }
}
