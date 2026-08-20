using System.Windows;

namespace StudyDemo01
{
    public class Fence
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "";
        public string TypeName { get; set; } = "通用围栏";
        public List<Point> Points { get; set; } = new();
    }
}
