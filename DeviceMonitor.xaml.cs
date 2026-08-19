using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace StudyDemo01
{
    public partial class DeviceMonitor : UserControl
    {
        private List<AgvCardInfo> _agvList = new();
        private List<StationInfo> _stationList = new();
        private readonly System.Windows.Threading.DispatcherTimer _refreshTimer;

        // 缓存坐标映射，供站点绘制复用
        private double _minX, _maxX, _minY, _maxY, _scale, _centerX, _centerY, _avgX, _avgY;

        public DeviceMonitor()
        {
            InitializeComponent();
            _refreshTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _refreshTimer.Tick += (s, e) => LoadData();
            Loaded += DeviceMonitor_Loaded;
            Unloaded += DeviceMonitor_Unloaded;
            mapCanvas.SizeChanged += (s, e) => DrawMap();
        }

        private void DeviceMonitor_Loaded(object sender, RoutedEventArgs e)
        {
            LoadData();
            _refreshTimer.Start();
        }

        private void DeviceMonitor_Unloaded(object sender, RoutedEventArgs e)
        {
            _refreshTimer.Stop();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void BtnCloseCard_Click(object sender, RoutedEventArgs e)
        {
            agvInfoCard.Visibility = Visibility.Collapsed;
        }

        public void SetData(List<AgvCardInfo> agvList)
        {
            _agvList = agvList;
            DrawMap();
        }

        public void SetStations(List<StationInfo> stationList)
        {
            _stationList = stationList;
            DrawMap();
        }

        public void ShowError(string message)
        {
            txtMapInfo.Text = message;
            txtOnlineMap.Text = "在线: 0";
            txtOfflineMap.Text = "离线: 0";
            txtAlarmMap.Text = "报警: 0";
            mapCanvas.Children.Clear();
            DrawGrid();

            var tb = new TextBlock
            {
                Text = message,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                TextAlignment = TextAlignment.Center
            };
            Canvas.SetLeft(tb, mapCanvas.ActualWidth / 2 - 100);
            Canvas.SetTop(tb, mapCanvas.ActualHeight / 2 - 12);
            mapCanvas.Children.Add(tb);
        }

        public void LoadData()
        {
            if (_agvList.Count == 0 && _stationList.Count == 0) return;

            txtOnlineMap.Text = "在线: " + _agvList.Count(a => a.StatusText == "在线");
            txtOfflineMap.Text = "离线: " + _agvList.Count(a => a.StatusText == "离线");
            txtAlarmMap.Text = "报警: " + _agvList.Count(a => a.StatusText == "报警");
            txtMapInfo.Text = $"共 {_agvList.Count} 台设备 · {_stationList.Count} 个站点";

            DrawMap();
        }

        private static double ParseDouble(string s)
        {
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return v;
            return 0;
        }

        private static double ParseAngle(string s)
        {
            var cleaned = s.Replace("°", "").Trim();
            return ParseDouble(cleaned);
        }

        private void DrawMap()
        {
            mapCanvas.Children.Clear();
            DrawGrid();

            if (_agvList.Count == 0 && _stationList.Count == 0) return;

            var width = mapCanvas.ActualWidth;
            var height = mapCanvas.ActualHeight;
            if (width < 10 || height < 10) return;

            var allX = new List<double>();
            var allY = new List<double>();

            foreach (var a in _agvList)
            {
                allX.Add(ParseDouble(a.X));
                allY.Add(ParseDouble(a.Y));
            }
            foreach (var s in _stationList)
            {
                allX.Add(s.X);
                allY.Add(s.Y);
            }

            _minX = allX.Min();
            _maxX = allX.Max();
            _minY = allY.Min();
            _maxY = allY.Max();

            double rangeX = _maxX - _minX;
            double rangeY = _maxY - _minY;
            if (rangeX < 1) rangeX = 10;
            if (rangeY < 1) rangeY = 10;

            double padding = 60;
            double drawWidth = width - padding * 2;
            double drawHeight = height - padding * 2;

            double scaleX = drawWidth / rangeX;
            double scaleY = drawHeight / rangeY;
            _scale = Math.Min(scaleX, scaleY);

            _centerX = width / 2;
            _centerY = height / 2;
            _avgX = (_minX + _maxX) / 2;
            _avgY = (_minY + _maxY) / 2;

            DrawAxisLabels(_minX, _maxX, _minY, _maxY);

            // 先画站点（底层）
            foreach (var station in _stationList)
            {
                double sx = _centerX + (station.X - _avgX) * _scale;
                double sy = _centerY - (station.Y - _avgY) * _scale;
                DrawStation(station, sx, sy);
            }

            // 再画AGV（上层）
            for (int i = 0; i < _agvList.Count; i++)
            {
                var agv = _agvList[i];
                double px = _centerX + (ParseDouble(agv.X) - _avgX) * _scale;
                double py = _centerY - (ParseDouble(agv.Y) - _avgY) * _scale;
                DrawAgv(agv, px, py);
            }
        }

        private void DrawGrid()
        {
            var width = mapCanvas.ActualWidth;
            var height = mapCanvas.ActualHeight;
            if (width < 10 || height < 10) return;

            double gridSize = 50;
            var gridBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));

            for (double x = gridSize; x < width; x += gridSize)
            {
                mapCanvas.Children.Add(new Line
                {
                    X1 = x, Y1 = 0, X2 = x, Y2 = height,
                    Stroke = gridBrush, StrokeThickness = 0.5
                });
            }

            for (double y = gridSize; y < height; y += gridSize)
            {
                mapCanvas.Children.Add(new Line
                {
                    X1 = 0, Y1 = y, X2 = width, Y2 = y,
                    Stroke = gridBrush, StrokeThickness = 0.5
                });
            }
        }

        private void DrawAxisLabels(double minX, double maxX, double minY, double maxY)
        {
            var tb = new TextBlock
            {
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                Text = $"X: {minX:F1} ~ {maxX:F1}    Y: {minY:F1} ~ {maxY:F1}"
            };
            Canvas.SetLeft(tb, 12);
            Canvas.SetTop(tb, 6);
            mapCanvas.Children.Add(tb);
        }

        private void DrawStation(StationInfo station, double px, double py)
        {
            double size = 14;
            double half = size / 2;

            var stationBrush = new SolidColorBrush(Color.FromRgb(99, 102, 241)); // indigo
            var lightBrush = new SolidColorBrush(Color.FromRgb(199, 210, 254));   // light indigo

            // 菱形（旋转45度的正方形）
            var diamond = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(px, py - half),       // top
                    new Point(px + half, py),        // right
                    new Point(px, py + half),        // bottom
                    new Point(px - half, py)         // left
                },
                Fill = lightBrush,
                Stroke = stationBrush,
                StrokeThickness = 1.5
            };
            mapCanvas.Children.Add(diamond);

            // 站点编号标签
            var label = new TextBlock
            {
                Text = station.StationId,
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(67, 56, 202)),
                TextAlignment = TextAlignment.Center,
                Width = 28
            };
            Canvas.SetLeft(label, px - 14);
            Canvas.SetTop(label, py + half + 2);
            mapCanvas.Children.Add(label);

            // 点击区域
            var hitArea = new Ellipse
            {
                Width = size + 12, Height = size + 12,
                Fill = Brushes.Transparent, Cursor = Cursors.Hand
            };
            Canvas.SetLeft(hitArea, px - half - 6);
            Canvas.SetTop(hitArea, py - half - 6);

            var stRef = station;
            var pxRef = px;
            var pyRef = py;
            hitArea.MouseLeftButtonDown += (s, e) => ShowStationCard(stRef, pxRef, pyRef);
            mapCanvas.Children.Add(hitArea);
        }

        private void DrawAgv(AgvCardInfo agv, double px, double py)
        {
            double size = 36;
            double half = size / 2;

            Color bgColor;
            if (agv.StatusText == "在线")
                bgColor = Color.FromRgb(16, 185, 129);
            else if (agv.StatusText == "报警")
                bgColor = Color.FromRgb(245, 158, 11);
            else
                bgColor = Color.FromRgb(239, 68, 68);

            double angle = ParseAngle(agv.AngleDisplay);
            double angleRad = angle * Math.PI / 180;
            double arrowLen = half + 12;
            double arrowEndX = px + Math.Sin(angleRad) * arrowLen;
            double arrowEndY = py - Math.Cos(angleRad) * arrowLen;

            mapCanvas.Children.Add(new Line
            {
                X1 = px, Y1 = py, X2 = arrowEndX, Y2 = arrowEndY,
                Stroke = new SolidColorBrush(bgColor),
                StrokeThickness = 2.5,
                StrokeEndLineCap = PenLineCap.Round
            });

            var circle = new Ellipse
            {
                Width = size, Height = size,
                Fill = new SolidColorBrush(bgColor),
                Stroke = Brushes.White, StrokeThickness = 2
            };
            Canvas.SetLeft(circle, px - half);
            Canvas.SetTop(circle, py - half);
            mapCanvas.Children.Add(circle);

            var label = new TextBlock
            {
                Text = agv.DevId,
                FontSize = 11, FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center,
                Width = size
            };
            Canvas.SetLeft(label, px - half);
            Canvas.SetTop(label, py - 8);
            mapCanvas.Children.Add(label);

            var hitArea = new Ellipse
            {
                Width = size + 10, Height = size + 10,
                Fill = Brushes.Transparent, Cursor = Cursors.Hand
            };
            Canvas.SetLeft(hitArea, px - half - 5);
            Canvas.SetTop(hitArea, py - half - 5);

            var agvRef = agv;
            var pxRef = px;
            var pyRef = py;
            hitArea.MouseLeftButtonDown += (s, e) => ShowInfoCard(agvRef, pxRef, pyRef);
            mapCanvas.Children.Add(hitArea);
        }

        private void ShowInfoCard(AgvCardInfo agv, double px, double py)
        {
            agvInfoCard.Visibility = Visibility.Visible;

            tipName.Text = agv.Name;
            tipBattery.Text = agv.BatteryText;
            tipSpeed.Text = agv.SpeedDisplay;
            tipAngle.Text = agv.AngleDisplay;
            tipStatus.Text = agv.StatusText;
            tipX.Text = agv.X;
            tipY.Text = agv.Y;

            Color headerColor;
            Color statusColor;
            if (agv.StatusText == "在线")
            {
                headerColor = Color.FromRgb(16, 185, 129);
                statusColor = Color.FromRgb(16, 185, 129);
            }
            else if (agv.StatusText == "报警")
            {
                headerColor = Color.FromRgb(245, 158, 11);
                statusColor = Color.FromRgb(245, 158, 11);
            }
            else
            {
                headerColor = Color.FromRgb(239, 68, 68);
                statusColor = Color.FromRgb(239, 68, 68);
            }

            cardHeader.Background = new SolidColorBrush(headerColor);
            statusDot.Background = new SolidColorBrush(statusColor);
            statusEllipse.Fill = new SolidColorBrush(statusColor);
            tipStatus.Foreground = new SolidColorBrush(statusColor);
            statusBg.Background = new SolidColorBrush(Color.FromRgb(
                headerColor.R, headerColor.G, headerColor.B));
            statusBg.Opacity = 0.15;

            PositionCard(px, py);
        }

        private void ShowStationCard(StationInfo station, double px, double py)
        {
            agvInfoCard.Visibility = Visibility.Visible;

            tipName.Text = "站点 " + station.StationId;
            tipBattery.Text = "-";
            tipSpeed.Text = "-";
            tipAngle.Text = "-";
            tipStatus.Text = "站点";
            tipX.Text = station.X.ToString("F2");
            tipY.Text = station.Y.ToString("F2");

            var headerColor = Color.FromRgb(99, 102, 241);
            var statusColor = Color.FromRgb(99, 102, 241);

            cardHeader.Background = new SolidColorBrush(headerColor);
            statusDot.Background = new SolidColorBrush(statusColor);
            statusEllipse.Fill = new SolidColorBrush(statusColor);
            tipStatus.Foreground = new SolidColorBrush(statusColor);
            statusBg.Background = new SolidColorBrush(Color.FromRgb(
                headerColor.R, headerColor.G, headerColor.B));
            statusBg.Opacity = 0.15;

            PositionCard(px, py);
        }

        private void PositionCard(double px, double py)
        {
            double cardLeft = px + 28;
            double cardTop = py - 60;
            if (cardLeft + 270 > mapCanvas.ActualWidth)
                cardLeft = px - 290;
            if (cardTop < 0)
                cardTop = py + 28;
            if (cardTop + 240 > mapCanvas.ActualHeight)
                cardTop = mapCanvas.ActualHeight - 250;

            Canvas.SetLeft(agvInfoCard, cardLeft);
            Canvas.SetTop(agvInfoCard, cardTop);
        }
    }
}
