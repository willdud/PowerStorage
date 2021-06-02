using System;
using System.Collections;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using ICities;
using UnityEngine;

namespace PowerStorage
{
    public class GridSerializer : SerializableDataExtensionBase
    {
        const string GridKey = "PowerStorage|GridSerializer";
            
        public override void OnLoadData()
        {
            PowerStorageLogger.Log("Loading data. Time: " + Time.realtimeSinceStartup);
            var data = serializableDataManager.LoadData(GridKey);
            if (data == null)
            {
                PowerStorageLogger.Log("No data to load.");
                return;
            }

            var memStream = new MemoryStream();
            memStream.Write(data, 0, data.Length);
            memStream.Position = 0;

            var binaryFormatter = new BinaryFormatter();
            try
            {
                PowerStorageAi.BackupGrid = (Hashtable)binaryFormatter.Deserialize(memStream);
                PowerStorageLogger.Log("Finished loading data. Time: " + Time.realtimeSinceStartup);
            }
            catch (Exception e)
            {
                PowerStorageLogger.LogError("Unexpected " + e.GetType().Name + " loading data.");
            }
            finally
            {
                memStream.Close();
            }
        }

        public override void OnSaveData()
        {
            PowerStorageLogger.Log("Saving data");
            var binaryFormatter = new BinaryFormatter();
            var memStream = new MemoryStream();
            try
            {
                binaryFormatter.Serialize(memStream, PowerStorageAi.BackupGrid);
                serializableDataManager.SaveData(GridKey, memStream.ToArray());
                PowerStorageLogger.Log("Finished saving data");
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
}
