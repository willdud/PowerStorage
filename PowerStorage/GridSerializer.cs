using System;
using System.Collections;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using ICities;
using PowerStorage.Supporting;
using PowerStorage.Unity;
using UnityEngine;

namespace PowerStorage
{
    public class GridSerializer : SerializableDataExtensionBase
    {
        const string GridKey = "PowerStorage|GridSerializer";
            
        public override void OnLoadData()
        {
            PowerStorageLogger.Log("Loading data. Time: " + Time.realtimeSinceStartup, PowerStorageMessageType.Loading);
            var data = serializableDataManager.LoadData(GridKey);
            if (data == null)
            {
                PowerStorageLogger.Log("No data to load.", PowerStorageMessageType.Loading);
                return;
            }

            var memStream = new MemoryStream();
            memStream.Write(data, 0, data.Length);
            memStream.Position = 0;

            var binaryFormatter = new BinaryFormatter();
            try
            {
                PowerStorageAi.BackupGrid = (Hashtable)binaryFormatter.Deserialize(memStream);
                PowerStorageLogger.Log("Finished loading data. Time: " + Time.realtimeSinceStartup, PowerStorageMessageType.Loading);
            }
            catch (Exception e)
            {
                PowerStorageLogger.LogError("Unexpected " + e.GetType().Name + " loading data.", PowerStorageMessageType.Loading);
                PowerStorageLogger.LogError(e.Message, PowerStorageMessageType.Loading);
                PowerStorageLogger.LogError(e.StackTrace, PowerStorageMessageType.Loading);
            }
            finally
            {
                memStream.Close();
            }
        }

        public override void OnSaveData()
        {
            PowerStorageLogger.Log("Saving data", PowerStorageMessageType.Saving);
            var binaryFormatter = new BinaryFormatter();
            var memStream = new MemoryStream();
            try
            {
                binaryFormatter.Serialize(memStream, PowerStorageAi.BackupGrid);
                serializableDataManager.SaveData(GridKey, memStream.ToArray());
                PowerStorageLogger.Log("Finished saving data", PowerStorageMessageType.Saving);
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
}
