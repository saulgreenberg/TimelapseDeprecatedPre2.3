using Newtonsoft.Json;
using System.IO;
using Timelapse.Database;

namespace Timelapse.Detection
{
    public static class ImportDetectionData
    {
        // THIS CLASS IS DEFUNCT
        public static bool DetectionFromJson(string path, SQLiteWrapper database)
        {
            if (File.Exists(path) == false)
            {
                return false;
            }

            try{
                Detector detector = JsonConvert.DeserializeObject<Detector>(File.ReadAllText(path));
                //DetectionDatabases.PopulateTables(detector, database);
                return true;
            }
            catch 
            {
                return false;
            }
        }
    }
}
