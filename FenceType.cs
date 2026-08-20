using System.Windows.Media;

namespace StudyDemo01
{
    public class FenceTypeInfo
    {
        public string Name { get; set; } = "";
        public string ColorHex { get; set; } = "#94A3B8";
    }

    public static class FenceTypeHelper
    {
        private static readonly List<FenceTypeInfo> _defaults = new()
        {
            new() { Name = "通用围栏", ColorHex = "#94A3B8" }
        };

        public static List<FenceTypeInfo> GetAll()
        {
            var list = ConfigHelper.AgvSettings.FenceTypes;
            if (list == null || list.Count == 0)
            {
                list = new List<FenceTypeInfo>(_defaults);
            }
            return list;
        }

        public static Color ParseHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return Color.FromRgb(148, 163, 184);
            hex = hex.Trim().TrimStart('#');
            if (hex.Length == 6 &&
                int.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out int r) &&
                int.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out int g) &&
                int.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out int b))
            {
                return Color.FromRgb((byte)r, (byte)g, (byte)b);
            }
            return Color.FromRgb(148, 163, 184);
        }

        public static Brush GetBrush(string hex)
        {
            var brush = new SolidColorBrush(ParseHex(hex));
            brush.Freeze();
            return brush;
        }

        public static Brush GetTypeBrush(string typeName)
        {
            var info = GetAll().FirstOrDefault(t => t.Name == typeName);
            return GetBrush(info?.ColorHex ?? "#94A3B8");
        }

        public static Color GetTypeColor(string typeName)
        {
            var info = GetAll().FirstOrDefault(t => t.Name == typeName);
            return ParseHex(info?.ColorHex ?? "#94A3B8");
        }

        public static string GetDefaultTypeName() => "通用围栏";
    }
}
