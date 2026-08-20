using System.Data;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Data.SqlClient;

namespace StudyDemo01
{
    public partial class FencePage : UserControl
    {
        private readonly List<StationInfo> _stationList = new();
        private readonly List<StationLink> _linkList = new();
        private readonly Dictionary<string, StationInfo> _stationMap = new();
        private readonly List<Fence> _fences = new();
        private Fence? _selectedFence;

        private readonly List<Point> _drawingPoints = new();
        private bool _isDrawing = false;
        private string _currentFenceTypeName = "通用围栏";

        private double _baseScale = 1.0;
        private double _avgX, _avgY;
        private double _dataMinX, _dataMaxX, _dataMinY, _dataMaxY;
        private double _zoomLevel = 1.0;
        private double _panX, _panY;
        private Point? _dragStart;
        private Point _dragPanStart;
        private bool _isMiddleDragging = false;
        private Point _middleDragStart;
        private Point _middleDragPanStart;
        private const double ZoomStep = 0.2;
        private const double MinZoom = 0.3;
        private const double MaxZoom = 5.0;

        public FencePage()
        {
            InitializeComponent();

            mapCanvas.SizeChanged += (s, e) => DrawMap();
            mapCanvas.MouseWheel += MapCanvas_MouseWheel;
            mapCanvas.MouseLeftButtonDown += MapCanvas_MouseLeftButtonDown;
            mapCanvas.MouseLeftButtonUp += MapCanvas_MouseLeftButtonUp;
            mapCanvas.MouseDown += MapCanvas_MouseDown;
            mapCanvas.MouseUp += MapCanvas_MouseUp;
            mapCanvas.MouseMove += MapCanvas_MouseMove;
            mapCanvas.MouseLeave += MapCanvas_MouseLeave;

            Loaded += FencePage_Loaded;
        }

        private void FencePage_Loaded(object sender, RoutedEventArgs e)
        {
            InitFenceTypeCombo();
            _ = LoadDataAsync();
        }

        public void RefreshFenceTypes()
        {
            var prev = cmbFenceType.SelectedItem as ComboBoxItem;
            var prevName = prev?.Tag as string;
            InitFenceTypeCombo();
            if (prevName != null)
            {
                foreach (ComboBoxItem item in cmbFenceType.Items)
                {
                    if (item.Tag as string == prevName)
                    {
                        cmbFenceType.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private void InitFenceTypeCombo()
        {
            cmbFenceType.Items.Clear();
            foreach (var info in FenceTypeHelper.GetAll())
            {
                cmbFenceType.Items.Add(new ComboBoxItem
                {
                    Content = info.Name,
                    Tag = info.Name
                });
            }
            if (cmbFenceType.Items.Count > 0)
                cmbFenceType.SelectedIndex = 0;
        }

        private void CmbFenceType_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (cmbFenceType.SelectedItem is ComboBoxItem item && item.Tag is string name)
                _currentFenceTypeName = name;
        }

        #region Data Loading

        private async Task LoadDataAsync()
        {
            await LoadStationsAsync();
            _fences.Clear();
            _fences.AddRange(FenceManager.LoadAll());
            BuildFenceList();
            CacheBaseParams();
            DrawMap();
        }

        private async Task LoadStationsAsync()
        {
            try
            {
                var config = ConfigHelper.DatabaseSettings;
                var dbName = GetDbName();
                if (string.IsNullOrEmpty(dbName)) return;

                using var conn = new SqlConnection(config.GetConnectionString(dbName));
                await conn.OpenAsync();

                using (var cmd = new SqlCommand(@"
                    SELECT station, x, y, frontsite, backsite, finalsite, groups
                    FROM agvstationinfo ORDER BY station", conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    _stationList.Clear();
                    while (await reader.ReadAsync())
                    {
                        _stationList.Add(new StationInfo
                        {
                            StationId = reader.IsDBNull(0) ? "" : reader[0].ToString() ?? "",
                            X = SafeToDouble(reader[1]),
                            Y = SafeToDouble(reader[2]),
                            FrontSite = SafeToInt(reader[3]),
                            BackSite = SafeToInt(reader[4]),
                            FinalSite = SafeToInt(reader[5]),
                            Groups = SafeToInt(reader[6])
                        });
                    }
                }

                _linkList.Clear();
                _stationMap.Clear();
                foreach (var s in _stationList)
                    _stationMap[s.StationId] = s;

                try
                {
                    using var linkCmd = new SqlCommand(@"
                        SELECT station0, station1 FROM agvstationnetinfo", conn);
                    using var linkReader = await linkCmd.ExecuteReaderAsync();
                    var seen = new HashSet<string>();
                    while (await linkReader.ReadAsync())
                    {
                        var s0 = linkReader.IsDBNull(0) ? "" : linkReader.GetValue(0).ToString() ?? "";
                        var s1 = linkReader.IsDBNull(1) ? "" : linkReader.GetValue(1).ToString() ?? "";
                        if (string.IsNullOrEmpty(s0) || string.IsNullOrEmpty(s1)) continue;
                        var key = string.Compare(s0, s1, StringComparison.Ordinal) < 0
                            ? $"{s0}->{s1}" : $"{s1}->{s0}";
                        if (seen.Add(key))
                            _linkList.Add(new StationLink { FromStation = s0, ToStation = s1 });
                    }
                }
                catch { }
            }
            catch { }
        }

        private string GetDbName()
        {
            var parent = this.Parent as FrameworkElement;
            while (parent != null)
            {
                if (parent is Window window)
                {
                    var title = window.Title;
                    var idx = title.IndexOf(" - ");
                    if (idx >= 0 && idx + 3 < title.Length)
                        return title.Substring(idx + 3).Trim();
                    break;
                }
                parent = parent.Parent as FrameworkElement;
            }
            return "";
        }

        private static int SafeToInt(object value)
        {
            if (value == null || value == DBNull.Value) return 0;
            var s = value.ToString() ?? "0";
            if (s.Contains(';')) s = s.Split(';')[0];
            return int.TryParse(s, out var v) ? v : 0;
        }

        private static double SafeToDouble(object value)
        {
            if (value == null || value == DBNull.Value) return 0.0;
            var s = value.ToString() ?? "0";
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0.0;
        }

        #endregion

        #region Coordinate System

        private void CacheBaseParams()
        {
            var allX = new List<double>();
            var allY = new List<double>();
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

        private double ScreenX(double worldX, double w)
            => w / 2 + (worldX - _avgX) * _baseScale * _zoomLevel + _panX;

        private double ScreenY(double worldY, double h)
            => h / 2 - (worldY - _avgY) * _baseScale * _zoomLevel + _panY;

        private double WorldX(double screenX, double w)
            => (screenX - w / 2 - _panX) / (_baseScale * _zoomLevel) + _avgX;

        private double WorldY(double screenY, double h)
            => -(screenY - h / 2 - _panY) / (_baseScale * _zoomLevel) + _avgY;

        #endregion

        #region Zoom / Pan

        private void MapCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var pos = e.GetPosition(mapCanvas);
            double oldZoom = _zoomLevel;
            _zoomLevel = e.Delta > 0
                ? Math.Min(_zoomLevel + ZoomStep, MaxZoom)
                : Math.Max(_zoomLevel - ZoomStep, MinZoom);
            double ratio = _zoomLevel / oldZoom;
            _panX = pos.X - ratio * (pos.X - _panX);
            _panY = pos.Y - ratio * (pos.Y - _panY);
            txtZoom.Text = $"缩放: {_zoomLevel:F1}x";
            DrawMap();
        }

        private void MapCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isDrawing)
            {
                var pos = e.GetPosition(mapCanvas);
                var w = mapCanvas.ActualWidth;
                var h = mapCanvas.ActualHeight;
                var worldPt = new Point(WorldX(pos.X, w), WorldY(pos.Y, h));
                _drawingPoints.Add(worldPt);
                DrawMap();
                return;
            }

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
            if (!_isDrawing) mapCanvas.Cursor = Cursors.Arrow;
        }

        private void MapCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_dragStart != null && !_isDrawing)
            {
                var current = e.GetPosition(this);
                _panX = _dragPanStart.X + (current.X - _dragStart.Value.X);
                _panY = _dragPanStart.Y + (current.Y - _dragStart.Value.Y);
                DrawMap();
            }
            else if (_isMiddleDragging)
            {
                var current = e.GetPosition(this);
                _panX = _middleDragPanStart.X + (current.X - _middleDragStart.X);
                _panY = _middleDragPanStart.Y + (current.Y - _middleDragStart.Y);
                DrawMap();
            }
        }

        private void MapCanvas_MouseLeave(object sender, MouseEventArgs e)
        {
            _dragStart = null;
            _isMiddleDragging = false;
            mapCanvas.ReleaseMouseCapture();
            if (!_isDrawing) mapCanvas.Cursor = Cursors.Arrow;
        }

        private void MapCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                _isMiddleDragging = true;
                _middleDragStart = e.GetPosition(this);
                _middleDragPanStart = new Point(_panX, _panY);
                mapCanvas.CaptureMouse();
                mapCanvas.Cursor = Cursors.SizeAll;
            }
        }

        private void MapCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                _isMiddleDragging = false;
                mapCanvas.ReleaseMouseCapture();
                if (!_isDrawing) mapCanvas.Cursor = Cursors.Arrow;
                else mapCanvas.Cursor = Cursors.Cross;
            }
        }

        private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
        {
            _zoomLevel = Math.Min(_zoomLevel + ZoomStep, MaxZoom);
            txtZoom.Text = $"缩放: {_zoomLevel:F1}x";
            DrawMap();
        }

        private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
        {
            _zoomLevel = Math.Max(_zoomLevel - ZoomStep, MinZoom);
            txtZoom.Text = $"缩放: {_zoomLevel:F1}x";
            DrawMap();
        }

        private void BtnResetView_Click(object sender, RoutedEventArgs e)
        {
            _zoomLevel = 1.0; _panX = 0; _panY = 0;
            CacheBaseParams();
            txtZoom.Text = $"缩放: {_zoomLevel:F1}x";
            DrawMap();
        }

        #endregion

        #region Drawing

        private void DrawMap()
        {
            mapCanvas.Children.Clear();
            if (_stationList.Count == 0) return;

            var w = mapCanvas.ActualWidth;
            var h = mapCanvas.ActualHeight;
            if (w < 10 || h < 10) return;

            DrawGrid(w, h);
            DrawLinks(w, h);

            var showUnlinked = ConfigHelper.AgvSettings.ShowUnlinkedStations;
            var linkedIds = showUnlinked ? null : GetLinkedStationIds();
            var showTitles = ConfigHelper.AgvSettings.ShowStationTitles;

            foreach (var s in _stationList)
            {
                if (linkedIds != null && !linkedIds.Contains(s.StationId))
                    continue;

                var px = ScreenX(s.X, w);
                var py = ScreenY(s.Y, h);

                var diamond = new Polygon
                {
                    Points = new PointCollection
                    {
                        new Point(px, py - 4),
                        new Point(px + 4, py),
                        new Point(px, py + 4),
                        new Point(px - 4, py)
                    },
                    Fill = new SolidColorBrush(Color.FromRgb(199, 210, 254)),
                    Stroke = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                    StrokeThickness = 1.5
                };
                Canvas.SetZIndex(diamond, 5);
                mapCanvas.Children.Add(diamond);

                if (showTitles)
                {
                    var label = new TextBlock
                    {
                        Text = s.StationId,
                        FontSize = 8,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(67, 56, 202)),
                        TextAlignment = TextAlignment.Center,
                        Width = 24
                    };
                    Canvas.SetLeft(label, px - 12);
                    Canvas.SetTop(label, py + 6);
                    Canvas.SetZIndex(label, 6);
                    mapCanvas.Children.Add(label);
                }
            }

            DrawFences(w, h);
            DrawDrawingPreview(w, h);
        }

        private void DrawGrid(double w, double h)
        {
            var gridBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)) { Opacity = 0.5 };
            gridBrush.Freeze();
            double step = 50 * _baseScale * _zoomLevel;
            if (step < 20) step = 20;

            for (double x = _panX % step; x < w; x += step)
            {
                var line = new Line { X1 = x, Y1 = 0, X2 = x, Y2 = h, Stroke = gridBrush, StrokeThickness = 0.5 };
                Canvas.SetZIndex(line, 0);
                mapCanvas.Children.Add(line);
            }
            for (double y = _panY % step; y < h; y += step)
            {
                var line = new Line { X1 = 0, Y1 = y, X2 = w, Y2 = y, Stroke = gridBrush, StrokeThickness = 0.5 };
                Canvas.SetZIndex(line, 0);
                mapCanvas.Children.Add(line);
            }
        }

        private void DrawLinks(double w, double h)
        {
            if (_linkList.Count == 0) return;

            var linkBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184)) { Opacity = 0.5 };
            linkBrush.Freeze();

            foreach (var link in _linkList)
            {
                if (!_stationMap.TryGetValue(link.FromStation, out var from)) continue;
                if (!_stationMap.TryGetValue(link.ToStation, out var to)) continue;

                var line = new Line
                {
                    X1 = ScreenX(from.X, w), Y1 = ScreenY(from.Y, h),
                    X2 = ScreenX(to.X, w), Y2 = ScreenY(to.Y, h),
                    Stroke = linkBrush, StrokeThickness = 1.5,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                Canvas.SetZIndex(line, 1);
                mapCanvas.Children.Add(line);
            }
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

        private void DrawFences(double w, double h)
        {
            foreach (var fence in _fences)
            {
                if (fence.Points.Count < 3) continue;

                var brush = FenceTypeHelper.GetTypeBrush(fence.TypeName);
                var fillBrush = brush.Clone();
                fillBrush.Opacity = 0.15;
                fillBrush.Freeze();

                var points = new PointCollection();
                foreach (var pt in fence.Points)
                    points.Add(new Point(ScreenX(pt.X, w), ScreenY(pt.Y, h)));

                var polygon = new Polygon
                {
                    Points = points,
                    Fill = fillBrush,
                    Stroke = brush,
                    StrokeThickness = fence == _selectedFence ? 3 : 1.5,
                    StrokeDashArray = fence == _selectedFence ? null : new DoubleCollection(),
                    Tag = fence
                };
                Canvas.SetZIndex(polygon, 8);
                polygon.MouseLeftButtonDown += FencePolygon_MouseLeftButtonDown;
                mapCanvas.Children.Add(polygon);

                var cx = fence.Points.Average(p => p.X);
                var cy = fence.Points.Average(p => p.Y);
                var nameBox = new TextBlock
                {
                    Text = fence.Name,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = brush,
                    TextAlignment = TextAlignment.Center
                };
                Canvas.SetLeft(nameBox, ScreenX(cx, w) - 30);
                Canvas.SetTop(nameBox, ScreenY(cy, h) - 8);
                Canvas.SetZIndex(nameBox, 9);
                mapCanvas.Children.Add(nameBox);
            }
        }

        private void DrawDrawingPreview(double w, double h)
        {
            if (!_isDrawing || _drawingPoints.Count == 0) return;

            var brush = FenceTypeHelper.GetTypeBrush(_currentFenceTypeName);
            var fillBrush = brush.Clone();
            fillBrush.Opacity = 0.1;
            fillBrush.Freeze();

            if (_drawingPoints.Count >= 2)
            {
                var points = new PointCollection();
                foreach (var pt in _drawingPoints)
                    points.Add(new Point(ScreenX(pt.X, w), ScreenY(pt.Y, h)));

                var polygon = new Polygon
                {
                    Points = points,
                    Fill = fillBrush,
                    Stroke = brush,
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 4, 2 }
                };
                Canvas.SetZIndex(polygon, 15);
                mapCanvas.Children.Add(polygon);
            }

            foreach (var pt in _drawingPoints)
            {
                var ellipse = new Ellipse
                {
                    Width = 8, Height = 8,
                    Fill = brush,
                    Stroke = Brushes.White,
                    StrokeThickness = 2
                };
                Canvas.SetLeft(ellipse, ScreenX(pt.X, w) - 4);
                Canvas.SetTop(ellipse, ScreenY(pt.Y, h) - 4);
                Canvas.SetZIndex(ellipse, 16);
                mapCanvas.Children.Add(ellipse);
            }
        }

        private void FencePolygon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_isDrawing) return;
            if (sender is Polygon polygon && polygon.Tag is Fence fence)
            {
                _selectedFence = fence;
                btnDelete.Visibility = Visibility.Visible;
                DrawMap();
                e.Handled = true;
            }
        }

        #endregion

        #region Fence Operations

        private void BtnDraw_Click(object sender, RoutedEventArgs e)
        {
            _isDrawing = true;
            _drawingPoints.Clear();
            btnDraw.Visibility = Visibility.Collapsed;
            btnFinish.Visibility = Visibility.Visible;
            btnCancel.Visibility = Visibility.Visible;
            fenceTypePanel.Visibility = Visibility.Visible;
            fenceListSidebar.Visibility = Visibility.Collapsed;
            sidebarColumn.Width = new GridLength(0);
            txtDrawHint.Text = "左键添加顶点，右键拖动地图，点击完成绘制结束";
            mapCanvas.Cursor = Cursors.Cross;
        }

        private void BtnFinish_Click(object sender, RoutedEventArgs e)
        {
            if (_drawingPoints.Count < 3)
            {
                ShowTips("至少需要3个顶点才能形成围栏");
                return;
            }
            ShowNameInputDialog();
        }

        private void FinishDrawing(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                name = _currentFenceTypeName;

            var fence = new Fence
            {
                Name = name,
                TypeName = _currentFenceTypeName,
                Points = new List<Point>(_drawingPoints)
            };

            FenceManager.Save(fence);
            _fences.Add(fence);
            BuildFenceList();

            _isDrawing = false;
            _drawingPoints.Clear();
            btnDraw.Visibility = Visibility.Visible;
            btnFinish.Visibility = Visibility.Collapsed;
            btnCancel.Visibility = Visibility.Collapsed;
            fenceTypePanel.Visibility = Visibility.Collapsed;
            fenceListSidebar.Visibility = Visibility.Visible;
            sidebarColumn.Width = new GridLength(190);
            txtDrawHint.Text = "";
            mapCanvas.Cursor = Cursors.Arrow;
            DrawMap();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _isDrawing = false;
            _drawingPoints.Clear();
            btnDraw.Visibility = Visibility.Visible;
            btnFinish.Visibility = Visibility.Collapsed;
            btnCancel.Visibility = Visibility.Collapsed;
            fenceTypePanel.Visibility = Visibility.Collapsed;
            fenceListSidebar.Visibility = Visibility.Visible;
            sidebarColumn.Width = new GridLength(190);
            txtDrawHint.Text = "";
            mapCanvas.Cursor = Cursors.Arrow;
            DrawMap();
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFence == null) return;

            var result = MessageBox.Show($"确定删除围栏「{_selectedFence.Name}」吗？",
                "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            FenceManager.Delete(_selectedFence.Id);
            _fences.Remove(_selectedFence);
            _selectedFence = null;
            btnDelete.Visibility = Visibility.Collapsed;
            BuildFenceList();
            DrawMap();
        }

        #endregion

        #region Tips & Name Input

        private System.Windows.Threading.DispatcherTimer? _tipsTimer;

        private void ShowTips(string message)
        {
            tipsText.Text = message;
            tipsPopup.IsOpen = true;

            _tipsTimer?.Stop();
            _tipsTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _tipsTimer.Tick += (s, e) =>
            {
                tipsPopup.IsOpen = false;
                _tipsTimer.Stop();
            };
            _tipsTimer.Start();
        }

        private void ShowNameInputDialog()
        {
            popupNameInput.IsOpen = true;
            txtInputFenceName.Text = "";
            txtNameInputError.Text = "";
            Dispatcher.BeginInvoke(new Action(() =>
            {
                txtInputFenceName.Focus();
                Keyboard.Focus(txtInputFenceName);
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void BtnNameInputOk_Click(object sender, RoutedEventArgs e)
        {
            var name = txtInputFenceName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                txtNameInputError.Text = "请输入围栏名称";
                return;
            }

            var allTypes = FenceTypeHelper.GetAll();
            if (_fences.Any(f => f.Name == name))
            {
                txtNameInputError.Text = "该围栏名称已存在";
                return;
            }

            popupNameInput.IsOpen = false;
            FinishDrawing(name);
        }

        private void BtnNameInputCancel_Click(object sender, RoutedEventArgs e)
        {
            popupNameInput.IsOpen = false;
        }

        private void PopupNameInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                BtnNameInputOk_Click(sender, e);
            else if (e.Key == Key.Escape)
                popupNameInput.IsOpen = false;
        }

        private void TxtInputFenceName_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnNameInputOk_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                popupNameInput.IsOpen = false;
                e.Handled = true;
            }
        }

        private void PopupBorder_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        #endregion

        #region Fence List

        private void BuildFenceList()
        {
            fenceListPanel.Children.Clear();

            if (_fences.Count == 0)
            {
                fenceListPanel.Children.Add(new TextBlock
                {
                    Text = "暂无围栏，点击「绘制围栏」添加",
                    FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    Margin = new Thickness(2, 8, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                });
                return;
            }

            foreach (var fence in _fences)
            {
                var color = FenceTypeHelper.GetTypeColor(fence.TypeName);
                var border = new Border
                {
                    Background = fence == _selectedFence
                        ? new SolidColorBrush(Color.FromArgb(30, color.R, color.G, color.B))
                        : Brushes.Transparent,
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 4, 6, 4),
                    Margin = new Thickness(0, 0, 0, 1),
                    Cursor = Cursors.Hand,
                    Tag = fence
                };
                border.MouseLeftButtonDown += FenceListItem_Click;

                var stack = new StackPanel();
                var titleRow = new DockPanel();

                var colorDot = new Border
                {
                    Width = 6, Height = 6, CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(color),
                    Margin = new Thickness(0, 0, 5, 0)
                };
                DockPanel.SetDock(colorDot, Dock.Left);

                var nameText = new TextBlock
                {
                    Text = fence.Name,
                    FontSize = 12, FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                    VerticalAlignment = VerticalAlignment.Center
                };

                titleRow.Children.Add(colorDot);
                titleRow.Children.Add(nameText);

                var typeText = new TextBlock
                {
                    Text = fence.TypeName + $" · {fence.Points.Count}个顶点",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    Margin = new Thickness(11, 1, 0, 0)
                };

                stack.Children.Add(titleRow);
                stack.Children.Add(typeText);
                border.Child = stack;
                fenceListPanel.Children.Add(border);
            }
        }

        private void FenceListItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is Fence fence)
            {
                if (_selectedFence == fence)
                {
                    _selectedFence = null;
                    btnDelete.Visibility = Visibility.Collapsed;
                }
                else
                {
                    _selectedFence = fence;
                    btnDelete.Visibility = Visibility.Visible;
                }
                BuildFenceList();
                DrawMap();
            }
        }

        #endregion
    }
}
