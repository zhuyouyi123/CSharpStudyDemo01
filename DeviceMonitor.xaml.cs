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
        private List<StationLink> _linkList = new();
        private Dictionary<string, StationInfo> _stationMap = new();
        private bool _hasError = false;

        private static readonly Brush GridBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
        private static readonly Brush LinkBrush;
        private static readonly Brush StationFill = new SolidColorBrush(Color.FromRgb(199, 210, 254));
        private static readonly Brush StationStroke = new SolidColorBrush(Color.FromRgb(99, 102, 241));
        private static readonly Brush StationLabelBrush = new SolidColorBrush(Color.FromRgb(67, 56, 202));
        private static readonly Brush DefaultAgvOnBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        private static readonly Brush DefaultAgvAlarmBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
        private static readonly Brush DefaultAgvOffBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68));
        private static readonly Brush AxisLabelBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184));
        private static readonly Brush WhiteBrush = Brushes.White;

        private Brush AgvOnBrush;
        private Brush AgvAlarmBrush;
        private Brush AgvOffBrush;

        static DeviceMonitor()
        {
            GridBrush.Freeze();
            StationFill.Freeze();
            StationStroke.Freeze();
            StationLabelBrush.Freeze();
            DefaultAgvOnBrush.Freeze();
            DefaultAgvAlarmBrush.Freeze();
            DefaultAgvOffBrush.Freeze();
            AxisLabelBrush.Freeze();
            WhiteBrush.Freeze();

            var linkBrush = new SolidColorBrush(Color.FromRgb(100, 130, 180)) { Opacity = 0.5 };
            linkBrush.Freeze();
            LinkBrush = linkBrush;
        }

        public void ReloadColors()
        {
            var cfg = ConfigHelper.AgvSettings;
            AgvOnBrush = CreateFrozenBrush(cfg.AgvOnLineColor) ?? DefaultAgvOnBrush;
            AgvAlarmBrush = CreateFrozenBrush(cfg.AgvAlarmColor) ?? DefaultAgvAlarmBrush;
            AgvOffBrush = CreateFrozenBrush(cfg.AgvOffLineColor) ?? DefaultAgvOffBrush;
            HeaderOnBrush = AgvOnBrush;
            HeaderAlarmBrush = AgvAlarmBrush;
            HeaderOffBrush = AgvOffBrush;
            DrawMap();
        }

        public void ApplyStationFilter()
        {
            DrawMap();
        }

        private HashSet<string> GetLinkedStationIds()
        {
            var linked = new HashSet<string>();
            foreach (var link in _linkList)
            {
                linked.Add(link.FromStation);
                linked.Add(link.ToStation);
            }
            return linked;
        }

        private static Brush? CreateFrozenBrush(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return null;
            try
            {
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
                brush.Freeze();
                return brush;
            }
            catch { return null; }
        }

        private double _baseScale = 1.0;
        private double _avgX = 0, _avgY = 0;
        private double _dataMinX, _dataMaxX, _dataMinY, _dataMaxY;

        private double _zoomLevel = 1.0;
        private double _panX = 0, _panY = 0;
        private Point? _dragStart;
        private Point _dragPanStart;
        private const double ZoomStep = 0.2;
        private const double MinZoom = 0.3;
        private const double MaxZoom = 5.0;

        private readonly System.Windows.Threading.DispatcherTimer _refreshTimer;

        public DeviceMonitor()
        {
            InitializeComponent();
            AgvOnBrush = DefaultAgvOnBrush;
            AgvAlarmBrush = DefaultAgvAlarmBrush;
            AgvOffBrush = DefaultAgvOffBrush;
            HeaderOnBrush = AgvOnBrush;
            HeaderAlarmBrush = AgvAlarmBrush;
            HeaderOffBrush = AgvOffBrush;
            ReloadColors();
            _refreshTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(ConfigHelper.AgvSettings.MapRefreshInterval)
            };
            _refreshTimer.Tick += (s, e) => RefreshAgvData();

            Loaded += DeviceMonitor_Loaded;
            Unloaded += DeviceMonitor_Unloaded;
            mapCanvas.SizeChanged += (s, e) => DrawMap();

            mapCanvas.MouseWheel += MapCanvas_MouseWheel;
            mapCanvas.MouseLeftButtonDown += MapCanvas_MouseLeftButtonDown;
            mapCanvas.MouseLeftButtonUp += MapCanvas_MouseLeftButtonUp;
            mapCanvas.MouseMove += MapCanvas_MouseMove;
            mapCanvas.MouseLeave += MapCanvas_MouseLeave;
        }

        public void ApplyRefreshInterval()
        {
            _refreshTimer.Stop();
            _refreshTimer.Interval = TimeSpan.FromSeconds(ConfigHelper.AgvSettings.MapRefreshInterval);
            _refreshTimer.Start();
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

        #region Zoom / Pan

        private void MapCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var pos = e.GetPosition(mapCanvas);
            double oldZoom = _zoomLevel;

            if (e.Delta > 0)
                _zoomLevel = Math.Min(_zoomLevel + ZoomStep, MaxZoom);
            else
                _zoomLevel = Math.Max(_zoomLevel - ZoomStep, MinZoom);

            double ratio = _zoomLevel / oldZoom;
            _panX = pos.X - ratio * (pos.X - _panX);
            _panY = pos.Y - ratio * (pos.Y - _panY);

            UpdateZoomText();
            DrawMap();
        }

        private void MapCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Ellipse || e.OriginalSource is Polygon) return;

            _dragStart = e.GetPosition(this);
            _dragPanStart = new Point(_panX, _panY);
            mapCanvas.CaptureMouse();
            mapCanvas.Cursor = Cursors.SizeAll;
        }

        private void MapCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _dragStart = null;
            mapCanvas.ReleaseMouseCapture();
            mapCanvas.Cursor = Cursors.Arrow;
        }

        private void MapCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragStart == null) return;

            var current = e.GetPosition(this);
            _panX = _dragPanStart.X + (current.X - _dragStart.Value.X);
            _panY = _dragPanStart.Y + (current.Y - _dragStart.Value.Y);
            DrawMap();
        }

        private void MapCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            _dragStart = null;
            mapCanvas.ReleaseMouseCapture();
            mapCanvas.Cursor = Cursors.Arrow;
        }

        private void UpdateZoomText()
        {
            txtZoom.Text = $"缩放: {_zoomLevel * 100:F0}%";
        }

        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            double oldZoom = _zoomLevel;
            _zoomLevel = Math.Min(_zoomLevel + ZoomStep, MaxZoom);
            double cx = mapCanvas.ActualWidth / 2;
            double cy = mapCanvas.ActualHeight / 2;
            double ratio = _zoomLevel / oldZoom;
            _panX = cx - ratio * (cx - _panX);
            _panY = cy - ratio * (cy - _panY);
            UpdateZoomText();
            DrawMap();
        }

        private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            double oldZoom = _zoomLevel;
            _zoomLevel = Math.Max(_zoomLevel - ZoomStep, MinZoom);
            double cx = mapCanvas.ActualWidth / 2;
            double cy = mapCanvas.ActualHeight / 2;
            double ratio = _zoomLevel / oldZoom;
            _panX = cx - ratio * (cx - _panX);
            _panY = cy - ratio * (cy - _panY);
            UpdateZoomText();
            DrawMap();
        }

        private void BtnResetView_Click(object sender, RoutedEventArgs e)
        {
            _zoomLevel = 1.0;
            _panX = 0;
            _panY = 0;
            UpdateZoomText();
            DrawMap();
        }

        #endregion

        private void BtnCloseInfo_Click(object sender, RoutedEventArgs e)
        {
            infoPanel.Visibility = Visibility.Collapsed;
        }

        public void SetData(List<AgvCardInfo> agvList)
        {
            _hasError = false;
            _agvList = agvList;
            CacheBaseParams();
            DrawMap();
        }

        public void SetStations(List<StationInfo> stationList, List<StationLink>? links = null)
        {
            _stationList = stationList;
            _linkList = links ?? new();
            _stationMap.Clear();
            foreach (var s in _stationList)
                _stationMap[s.StationId] = s;
            CacheBaseParams();
            DrawMap();
        }

        public event EventHandler? RefreshRequested;

        private void RefreshAgvData()
        {
            RefreshRequested?.Invoke(this, EventArgs.Empty);
        }

        private void CacheBaseParams()
        {
            var allX = new List<double>();
            var allY = new List<double>();
            foreach (var a in _agvList) { allX.Add(ParseDouble(a.X)); allY.Add(ParseDouble(a.Y)); }
            foreach (var s in _stationList) { allX.Add(s.X); allY.Add(s.Y); }
            if (allX.Count == 0) return;

            _dataMinX = allX.Min(); _dataMaxX = allX.Max();
            _dataMinY = allY.Min(); _dataMaxY = allY.Max();
            _avgX = (_dataMinX + _dataMaxX) / 2;
            _avgY = (_dataMinY + _dataMaxY) / 2;

            double rangeX = _dataMaxX - _dataMinX; if (rangeX < 1) rangeX = 10;
            double rangeY = _dataMaxY - _dataMinY; if (rangeY < 1) rangeY = 10;

            double w = mapCanvas.ActualWidth; if (w < 10) w = 800;
            double h = mapCanvas.ActualHeight; if (h < 10) h = 600;
            double padding = 60;
            _baseScale = Math.Min((w - padding * 2) / rangeX, (h - padding * 2) / rangeY);
        }

        public void ShowError(string message)
        {
            _hasError = true;
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

        private double ScreenX(double worldX, double w)
            => w / 2 + (worldX - _avgX) * _baseScale * _zoomLevel + _panX;

        private double ScreenY(double worldY, double h)
            => h / 2 - (worldY - _avgY) * _baseScale * _zoomLevel + _panY;

        private void DrawMap()
        {
            if (_hasError) return;
            mapCanvas.Children.Clear();
            infoPanel.Visibility = Visibility.Collapsed;

            txtUpdateTime.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            if (_agvList.Count == 0 && _stationList.Count == 0) return;

            var w = mapCanvas.ActualWidth;
            var h = mapCanvas.ActualHeight;
            if (w < 10 || h < 10) return;

            DrawGrid();
            DrawAxisLabels(_dataMinX, _dataMaxX, _dataMinY, _dataMaxY);
            DrawLinks(w, h);

            var showUnlinked = ConfigHelper.AgvSettings.ShowUnlinkedStations;
            var linkedIds = showUnlinked ? null : GetLinkedStationIds();

            foreach (var station in _stationList)
            {
                if (linkedIds != null && !linkedIds.Contains(station.StationId))
                    continue;
                DrawStation(station, ScreenX(station.X, w), ScreenY(station.Y, h));
            }

            for (int i = 0; i < _agvList.Count; i++)
            {
                var agv = _agvList[i];
                DrawAgv(agv, ScreenX(ParseDouble(agv.X), w), ScreenY(ParseDouble(agv.Y), h));
            }
        }

        private void DrawGrid()
        {
            var w = mapCanvas.ActualWidth;
            var h = mapCanvas.ActualHeight;
            if (w < 10 || h < 10) return;

            double gridSize = 50 * _zoomLevel;
            double ox = _panX % gridSize;
            double oy = _panY % gridSize;

            for (double x = ox; x < w; x += gridSize)
                mapCanvas.Children.Add(new Line { X1 = x, Y1 = 0, X2 = x, Y2 = h, Stroke = GridBrush, StrokeThickness = 0.5 });

            for (double y = oy; y < h; y += gridSize)
                mapCanvas.Children.Add(new Line { X1 = 0, Y1 = y, X2 = w, Y2 = y, Stroke = GridBrush, StrokeThickness = 0.5 });
        }

        private void DrawAxisLabels(double minX, double maxX, double minY, double maxY)
        {
            var tb = new TextBlock
            {
                FontSize = 10,
                Foreground = AxisLabelBrush,
                Text = $"X: {minX:F1} ~ {maxX:F1}    Y: {minY:F1} ~ {maxY:F1}"
            };
            Canvas.SetLeft(tb, 12);
            Canvas.SetTop(tb, 6);
            mapCanvas.Children.Add(tb);
        }

        private void DrawLinks(double w, double h)
        {
            if (_linkList.Count == 0) return;

            foreach (var link in _linkList)
            {
                if (!_stationMap.TryGetValue(link.FromStation, out var from)) continue;
                if (!_stationMap.TryGetValue(link.ToStation, out var to)) continue;

                double x1 = ScreenX(from.X, w);
                double y1 = ScreenY(from.Y, h);
                double x2 = ScreenX(to.X, w);
                double y2 = ScreenY(to.Y, h);

                var line = new Line
                {
                    X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                    Stroke = LinkBrush,
                    StrokeThickness = 2,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                Canvas.SetZIndex(line, 1);
                mapCanvas.Children.Add(line);
            }
        }

        private void DrawStation(StationInfo station, double px, double py)
        {
            double size = 8;
            double half = size / 2;

            var diamond = new Polygon
            {
                Points = new PointCollection
                {
                    new Point(px, py - half),
                    new Point(px + half, py),
                    new Point(px, py + half),
                    new Point(px - half, py)
                },
                Fill = StationFill,
                Stroke = StationStroke,
                StrokeThickness = 1.5
            };
            Canvas.SetZIndex(diamond, 5);
            mapCanvas.Children.Add(diamond);

            if (ConfigHelper.AgvSettings.ShowStationTitles)
            {
                var label = new TextBlock
                {
                    Text = station.StationId,
                    FontSize = 8,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = StationLabelBrush,
                    TextAlignment = TextAlignment.Center,
                    Width = 24
                };
                Canvas.SetLeft(label, px - 12);
                Canvas.SetTop(label, py + half + 2);
                Canvas.SetZIndex(label, 6);
                mapCanvas.Children.Add(label);
            }

            var hitArea = new Ellipse
            {
                Width = size + 14, Height = size + 14,
                Fill = Brushes.Transparent, Cursor = Cursors.Hand
            };
            Canvas.SetLeft(hitArea, px - half - 7);
            Canvas.SetTop(hitArea, py - half - 7);
            Canvas.SetZIndex(hitArea, 10);

            var stRef = station;
            hitArea.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                ShowStationInfo(stRef);
            };
            mapCanvas.Children.Add(hitArea);
        }

        private void DrawAgv(AgvCardInfo agv, double px, double py)
        {
            double size = 22;
            double half = size / 2;

            Brush bgBrush;
            if (agv.StatusText == "在线") bgBrush = AgvOnBrush;
            else if (agv.StatusText == "报警") bgBrush = AgvAlarmBrush;
            else bgBrush = AgvOffBrush;

            double angle = ParseAngle(agv.AngleDisplay);
            double angleRad = angle * Math.PI / 180;
            double arrowLen = half + 12;
            double arrowEndX = px + Math.Sin(angleRad) * arrowLen;
            double arrowEndY = py - Math.Cos(angleRad) * arrowLen;

            var arrowLine = new Line
            {
                X1 = px, Y1 = py, X2 = arrowEndX, Y2 = arrowEndY,
                Stroke = bgBrush,
                StrokeThickness = 2.5,
                StrokeEndLineCap = PenLineCap.Round
            };
            Canvas.SetZIndex(arrowLine, 15);
            mapCanvas.Children.Add(arrowLine);

            var circle = new Ellipse
            {
                Width = size, Height = size,
                Fill = bgBrush,
                Stroke = WhiteBrush, StrokeThickness = 2
            };
            Canvas.SetLeft(circle, px - half);
            Canvas.SetTop(circle, py - half);
            Canvas.SetZIndex(circle, 16);
            mapCanvas.Children.Add(circle);

            var label = new TextBlock
            {
                Text = agv.DevId,
                FontSize = 9, FontWeight = FontWeights.Bold,
                Foreground = WhiteBrush,
                TextAlignment = TextAlignment.Center,
                Width = size
            };
            Canvas.SetLeft(label, px - half);
            Canvas.SetTop(label, py - 8);
            Canvas.SetZIndex(label, 17);
            mapCanvas.Children.Add(label);

            var hitArea = new Ellipse
            {
                Width = size + 10, Height = size + 10,
                Fill = Brushes.Transparent, Cursor = Cursors.Hand
            };
            Canvas.SetLeft(hitArea, px - half - 5);
            Canvas.SetTop(hitArea, py - half - 5);
            Canvas.SetZIndex(hitArea, 20);

            var agvRef = agv;
            hitArea.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                ShowAgvInfo(agvRef);
            };
            mapCanvas.Children.Add(hitArea);
        }

        #region Info Panel

        private Brush HeaderOnBrush;
        private Brush HeaderAlarmBrush;
        private Brush HeaderOffBrush;
        private static readonly Brush HeaderStationBrush = StationStroke;

        private void ShowAgvInfo(AgvCardInfo agv)
        {
            infoPanel.Visibility = Visibility.Visible;

            Brush headerBrush;
            if (agv.StatusText == "在线") headerBrush = HeaderOnBrush;
            else if (agv.StatusText == "报警") headerBrush = HeaderAlarmBrush;
            else headerBrush = HeaderOffBrush;

            infoHeader.Background = headerBrush;
            infoTitle.Text = agv.Name;
            infoContent.Text =
                $"状态: {agv.StatusText}\n" +
                $"电量: {agv.BatteryText}\n" +
                $"速度: {agv.SpeedDisplay}\n" +
                $"角度: {agv.AngleDisplay}\n" +
                $"X: {agv.X}    Y: {agv.Y}";
        }

        private void ShowStationInfo(StationInfo station)
        {
            infoPanel.Visibility = Visibility.Visible;

            infoHeader.Background = HeaderStationBrush;
            infoTitle.Text = "站点 " + station.StationId;
            infoContent.Text =
                $"X: {station.X:F2}\n" +
                $"Y: {station.Y:F2}\n" +
                $"前方: {(station.FrontSite > 0 ? station.FrontSite.ToString() : "-")}\n" +
                $"后方: {(station.BackSite > 0 ? station.BackSite.ToString() : "-")}\n" +
                $"终点: {(station.FinalSite > 0 ? station.FinalSite.ToString() : "-")}\n" +
                $"分组: {(station.Groups > 0 ? station.Groups.ToString() : "-")}";
        }

        #endregion
    }
}
