using System;
using System.IO;
using ColossalFramework.IO;
using ICities;
using UnityEngine;

namespace PowerStorage
{
    public class Grid : SerializableDataExtensionBase
    {
        const string GridKey = "PowerStorage|GridSerializer";
        public const uint DataVersion = 0;

        private static volatile ConcurrentMap _backupGrid;
        public static ConcurrentMap BackupGrid 
        { 
            get 
            {          
                _backupGrid = _backupGrid ?? new ConcurrentMap();                
                return _backupGrid ;
            }
            set
            {
                PowerStorageLogger.Log("I've been set to: " + value + " : " + (value?.Map?.Count.ToString() ?? "NULL"), PowerStorageMessageType.Grid);
                _backupGrid = value; 
            }
        }

        public override void OnCreated(ISerializableData serializableData)
        {
            base.OnCreated(serializableData);  
        }

        public override void OnLoadData()
        {            
            base.OnLoadData();

            PowerStorageLogger.Log("Loading data. Time: " + Time.realtimeSinceStartup, PowerStorageMessageType.Loading);
            var data = serializableDataManager.LoadData(GridKey);
            if (data == null)
            {
                PowerStorageLogger.Log("No data to load.", PowerStorageMessageType.Loading);
                return;
            }
           
            try
            {
                using (var stream = new MemoryStream(data))
                {
                    stream.Position = 0;
                    BackupGrid = DataSerializer.Deserialize<ConcurrentMap>(stream, DataSerializer.Mode.Memory);
                }

                PowerStorageLogger.Log($"Finished loading data. Time: {Time.realtimeSinceStartup} - {BackupGrid.Map.Count} Buildings. Bytes: {data.Length}", PowerStorageMessageType.Loading);
            }
            catch (Exception e)
            {
                PowerStorageLogger.LogError("Unexpected " + e.GetType().Name + " loading data.", PowerStorageMessageType.All);
                PowerStorageLogger.LogError(e.Message, PowerStorageMessageType.All);
                PowerStorageLogger.LogError(e.StackTrace, PowerStorageMessageType.All);
                BackupGrid = new ConcurrentMap();
            }
        }

        public override void OnSaveData()
        {            
            base.OnSaveData();

            PowerStorageLogger.Log($"Saving data - {BackupGrid.Map.Count} Buildings", PowerStorageMessageType.Saving);
            using(var memStream = new MemoryStream())
            {
                try
                {
                    DataSerializer.Serialize(memStream, DataSerializer.Mode.Memory, DataVersion, BackupGrid);
                    var bytes = memStream.ToArray();
                    serializableDataManager.SaveData(GridKey, bytes);
                    PowerStorageLogger.Log($"Finished saving data. Bytes: {bytes.Length}", PowerStorageMessageType.Saving);
                }
                catch (Exception e)
                {
                    PowerStorageLogger.LogError("Unexpected " + e.GetType().Name + " saving data.", PowerStorageMessageType.Saving);
                    PowerStorageLogger.LogError(e.Message, PowerStorageMessageType.Saving);
                    PowerStorageLogger.LogError(e.StackTrace, PowerStorageMessageType.Saving);
                }
                finally
                {
                    memStream.Close();
                }
            }
        }

        public override void OnReleased()
        {
            base.OnReleased();
            BackupGrid = null;
        }
    }
}
