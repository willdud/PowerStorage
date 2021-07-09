using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace PowerStorage.Supporting
{
    public class PowerStorageProfiler
    {
        public static Stopwatch Start(string message)
        {
            if (!PowerStorage.Profile)
                return null;
            
            Debug.Log($"[0ms] - START - {message}");
            var watch = new Stopwatch();
            watch.Start();
            return watch;
        }
        public static void Lap(string message, Stopwatch watch)
        {
            if (!PowerStorage.Profile || watch == null)
                return;

            Debug.Log($"[{watch.ElapsedMilliseconds}ms] - LAP - {message}");
        }
        public static void Stop(string message, Stopwatch watch)
        {
            if (!PowerStorage.Profile || watch == null)
                return;

            watch.Stop();
            Debug.Log($"[{watch.ElapsedMilliseconds}ms] - END - {message}");
        }
    }
}
