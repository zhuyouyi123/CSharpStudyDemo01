using System.IO;
using System.Text.Json;
using System.Windows;

namespace StudyDemo01
{
    public static class FenceManager
    {
        private static readonly string FencesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fences");

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            Converters = { new PointJsonConverter() }
        };

        static FenceManager()
        {
            if (!Directory.Exists(FencesDir))
                Directory.CreateDirectory(FencesDir);
        }

        public static List<Fence> LoadAll()
        {
            var list = new List<Fence>();
            if (!Directory.Exists(FencesDir)) return list;

            foreach (var file in Directory.GetFiles(FencesDir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var fence = JsonSerializer.Deserialize<Fence>(json, JsonOpts);
                    if (fence != null) list.Add(fence);
                }
                catch { }
            }
            return list;
        }

        public static void Save(Fence fence)
        {
            var json = JsonSerializer.Serialize(fence, JsonOpts);
            var path = Path.Combine(FencesDir, fence.Id + ".json");
            File.WriteAllText(path, json);
        }

        public static void Delete(string fenceId)
        {
            var path = Path.Combine(FencesDir, fenceId + ".json");
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public class PointJsonConverter : System.Text.Json.Serialization.JsonConverter<Point>
    {
        public override Point Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            var x = root.GetProperty("X").GetDouble();
            var y = root.GetProperty("Y").GetDouble();
            return new Point(x, y);
        }

        public override void Write(Utf8JsonWriter writer, Point value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("X", value.X);
            writer.WriteNumber("Y", value.Y);
            writer.WriteEndObject();
        }
    }
}
