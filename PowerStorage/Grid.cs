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
            
        public volatile static ConcurrentMap BackupGrid;      

        public override void OnCreated(ISerializableData serializableData)
        {
            base.OnCreated(serializableData);            
            BackupGrid = new ConcurrentMap();
        }

        public override void OnLoadData()
        {            
            base.OnLoadData();

            PowerStorageLogger.Log("Loading data. Time: " + Time.realtimeSinceStartup);
            var data = serializableDataManager.LoadData(GridKey);
            if (data == null)
            {
                PowerStorageLogger.Log("No data to load.");
                return;
            }
           
            try
            {
                using (var stream = new MemoryStream(data))
                {
                    stream.Position = 0;
                    BackupGrid = DataSerializer.Deserialize<ConcurrentMap>(stream, DataSerializer.Mode.Memory);
                }

                PowerStorageLogger.Log($"Finished loading data. Time: {Time.realtimeSinceStartup} - {BackupGrid.Map.Count} Buildings. Bytes: {data.Length}");
            }
            catch (Exception e)
            {
                PowerStorageLogger.LogError("Unexpected " + e.GetType().Name + " loading data.");
                PowerStorageLogger.LogError(e.Message);
                PowerStorageLogger.LogError(e.StackTrace);
            }
        }

        public override void OnSaveData()
        {            
            base.OnSaveData();

            PowerStorageLogger.Log($"Saving data - {BackupGrid.Map.Count} Buildings");
            using(var memStream = new MemoryStream())
            {
                try
                {
                    DataSerializer.Serialize(memStream, DataSerializer.Mode.Memory, DataVersion, BackupGrid);
                    var bytes = memStream.ToArray();
                    serializableDataManager.SaveData(GridKey, bytes);
                    PowerStorageLogger.Log($"Finished saving data. Bytes: {bytes.Length}");
                }
                catch (Exception e)
                {
                    PowerStorageLogger.LogError("Unexpected " + e.GetType().Name + " saving data.");
                    PowerStorageLogger.LogError(e.Message);
                    PowerStorageLogger.LogError(e.StackTrace);
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
