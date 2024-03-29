﻿using System;
using System.IO;
using CitiesHarmony.API;
using ColossalFramework.UI;
using ICities;
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
        public static PowerStorageMessageType ShownMessageTypes { get; set; } = PowerStorageMessageType.None;


        public string Name => "Power Storage";
        public string Description => "A portion of power generated is put aside to be drawn from when the grid is low.";

        private string FullSavePath => Path.Combine(Application.dataPath, ConfigFile);
        private const string ConfigFile = "PowerStorage.Config.json";

        public void OnEnabled() 
        {
            HarmonyHelper.EnsureHarmonyInstalled();
        }

        public void OnSettingsUI(UIHelperBase helper)
        {
            LoadSettings();

            UISlider sliderObj1 = null;
            UISlider sliderObj2 = null;
            UISlider sliderObj3 = null;
            UICheckBox checkBox1 = null;
            UICheckBox checkBox2 = null;
            UIDropDown dd = null;
            var group = helper.AddGroup("Power Storage Settings");
            sliderObj1 = (UISlider)group.AddSlider($"Power loss on conversion", 0, 1, 0.1f, LossRatio, (value) =>
            {
                if (sliderObj1 != null)
                {
                    sliderObj1.tooltip = $"{LossRatio*100f}%";
                    sliderObj1.RefreshTooltip();
                }
                    
                LossRatio = value;
            });
            sliderObj2 = (UISlider)group.AddSlider("Safety When Charging (KW)", 0, 10000, 500, SafetyKwIntake, (value) =>
            {
                if (sliderObj2 != null)
                {
                    sliderObj2.tooltip = $"{SafetyKwIntake}KW";
                    sliderObj2.RefreshTooltip();
                }
                SafetyKwIntake = Convert.ToInt32(value);
            });
            group.AddSpace(20);
            sliderObj3 = (UISlider)group.AddSlider("Safety When Discharging (KW)", 0, 10000, 500, SafetyKwDischarge, (value) =>
            {
                if (sliderObj3 != null)
                {
                    sliderObj3.tooltip = $"{SafetyKwDischarge}KW";
                    sliderObj3.RefreshTooltip();
                }
                SafetyKwDischarge = Convert.ToInt32(value);
            });
            group.AddSpace(20);
            checkBox1 = (UICheckBox)group.AddCheckbox("Chrip About Low Power", Chirp, isChecked =>
            {
                Chirp = isChecked;
            });
            
            dd = (UIDropDown)group.AddDropdown("Logging Type", Enum.GetNames(typeof(PowerStorageMessageType)), (int)ShownMessageTypes, (value) =>
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
                    ShownMessageTypes = psSettings.ShownMessageTypes;
                    if(psSettings.Chirp.HasValue)
                        Chirp = psSettings.Chirp.Value;
                    if(psSettings.Debug.HasValue)
                        DebugLog = psSettings.Debug.Value;
                }
            } 
            catch (Exception e) 
            {
                PowerStorageLogger.Log("Using default settings: " + e, PowerStorageMessageType.All);
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
                    ShownMessageTypes = ShownMessageTypes
                };
                File.WriteAllText(FullSavePath, JsonUtility.ToJson(psSettings));
            } 
            catch (Exception e) 
            {
                PowerStorageLogger.LogError(e.Message, PowerStorageMessageType.All);
                PowerStorageLogger.Log("Couldn't save settings", PowerStorageMessageType.All);
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
    }
}
