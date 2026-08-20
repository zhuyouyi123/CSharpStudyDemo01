using System.Data;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Data.SqlClient;

namespace StudyDemo01
{
    public partial class AgvManager : Window
    {
        private SqlConnection? _monitorConnection;
        private bool _monitorInitialized = false;
        private bool _navHandlersWired = false;
        private AgvConfigPage? _agvConfigPage;
        private FencePage? _fencePage;
        private volatile bool _isRefreshing = false;
        private volatile bool _isConnecting = false;

        private void WireNavHandlers()
        {
            if (_navHandlersWired) return;
            _navHandlersWired = true;
            navAgvList.PreviewMouseLeftButtonDown += Nav_PreClick;
            navQueueInfo.PreviewMouseLeftButtonDown += Nav_PreClick;
            navTaskManage.PreviewMouseLeftButtonDown += Nav_PreClick;
            navFence.PreviewMouseLeftButtonDown += Nav_PreClick;
            navSettings.PreviewMouseLeftButtonDown += Nav_PreClick;
        }

        private void Window_Loaded2(object sender, RoutedEventArgs e)
        {
            WireNavHandlers();
            Closing += AgvManager_Closing;
        }

        private void Nav_PreClick(object sender, MouseButtonEventArgs e)
        {
            pageAgvList.Visibility = Visibility.Collapsed;
            pageQueueInfo.Visibility = Visibility.Collapsed;
            pageDeviceMonitor.Visibility = Visibility.Collapsed;
            if (_agvConfigPage != null) _agvConfigPage.Visibility = Visibility.Collapsed;
            if (_fencePage != null) _fencePage.Visibility = Visibility.Collapsed;

            if (sender == navAgvList)
            {
                pageAgvList.Visibility = Visibility.Visible;
                txtTitle.Text = "AGV列表";
                RestoreHeader();
            }
            else if (sender == navQueueInfo)
            {
                pageQueueInfo.Visibility = Visibility.Visible;
                txtTitle.Text = "队列信息";
                RestoreHeader();
            }
            else if (sender == navTaskManage)
            {
                pageDeviceMonitor.Visibility = Visibility.Visible;
                txtTitle.Text = "设备监控";
                RestoreHeader();
                _ = EnsureMonitorConnectionAsync();
            }
            else if (sender == navFence)
            {
                EnsureFencePage();
                _fencePage!.RefreshFenceTypes();
                _fencePage.Visibility = Visibility.Visible;
                txtTitle.Text = "围栏管理";
                RestoreHeader();
            }
            else if (sender == navSettings)
            {
                EnsureConfigPage();
                _agvConfigPage!.LoadCurrentConfig();
                _agvConfigPage.Visibility = Visibility.Visible;
                txtTitle.Text = "AGV功能配置";
                headerBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Transparent);
                headerBorder.Height = 0;
                headerBorder.BorderThickness = new Thickness(0);
            }
        }

        private void RestoreHeader()
        {
            headerBorder.Height = 64;
            headerBorder.BorderThickness = new Thickness(0, 0, 0, 1);
            headerBorder.Background = (System.Windows.Media.Brush)FindResource("CardBrush");
        }

        private void NavDeviceMonitor_Click(object sender, RoutedEventArgs e)
        {
            WireNavHandlers();

            if (!_monitorInitialized)
            {
                _monitorInitialized = true;
                pageDeviceMonitor.RefreshRequested += OnDeviceMonitorRefreshRequested;

                _agvConfigPage = new AgvConfigPage();
                _agvConfigPage.Visibility = Visibility.Collapsed;
                var parent = pageAgvList.Parent as Panel;
                parent?.Children.Add(_agvConfigPage);
            }

            _ = EnsureMonitorConnectionAsync();
        }

        public void ApplyAgvColors()
        {
            pageDeviceMonitor.ReloadColors();
        }

        public void ApplyStationFilter()
        {
            pageDeviceMonitor.ApplyStationFilter();
        }

        private void EnsureConfigPage()
        {
            if (_agvConfigPage != null) return;

            _agvConfigPage = new AgvConfigPage();
            _agvConfigPage.Visibility = Visibility.Collapsed;
            var parent = pageAgvList.Parent as Panel;
            parent?.Children.Add(_agvConfigPage);

            var row = Grid.GetRow(pageAgvList);
            Grid.SetRow(_agvConfigPage, row);
        }

        private void EnsureFencePage()
        {
            if (_fencePage != null) return;

            _fencePage = new FencePage();
            _fencePage.Visibility = Visibility.Collapsed;
            var parent = pageAgvList.Parent as Panel;
            parent?.Children.Add(_fencePage);

            var row = Grid.GetRow(pageAgvList);
            Grid.SetRow(_fencePage, row);
        }

        private void OnDeviceMonitorRefreshRequested(object? sender, EventArgs e)
        {
            _ = RefreshAgvDataAsync();
        }

        private string GetDbName()
        {
            var name = txtDbName.Text?.Trim();
            if (!string.IsNullOrEmpty(name)) return name;

            var title = this.Title;
            var idx = title.IndexOf(" - ");
            if (idx >= 0 && idx + 3 < title.Length)
                return title.Substring(idx + 3).Trim();

            return "";
        }

        private void DisposeConnection()
        {
            if (_monitorConnection != null)
            {
                try { _monitorConnection.Close(); } catch { }
                try { _monitorConnection.Dispose(); } catch { }
                _monitorConnection = null;
            }
        }

        private void AgvManager_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            DisposeConnection();
        }

        private async System.Threading.Tasks.Task EnsureMonitorConnectionAsync()
        {
            if (_isConnecting) return;
            _isConnecting = true;
            try
            {
                var dbName = GetDbName();
                if (string.IsNullOrEmpty(dbName))
                {
                    pageDeviceMonitor.ShowError("无法获取数据库名称");
                    return;
                }

                if (_monitorConnection == null || _monitorConnection.State != ConnectionState.Open)
                {
                    DisposeConnection();
                    var config = ConfigHelper.DatabaseSettings;
                    var connStr = config.GetConnectionString(dbName);
                    _monitorConnection = new SqlConnection(connStr);
                    await _monitorConnection.OpenAsync();
                }

                await LoadDeviceMonitorDataAsync();
            }
            catch (Exception ex)
            {
                DisposeConnection();
                pageDeviceMonitor.ShowError("连接失败: " + ex.Message);
            }
            finally
            {
                _isConnecting = false;
            }
        }

        private async System.Threading.Tasks.Task LoadDeviceMonitorDataAsync()
        {
            if (_monitorConnection == null || _monitorConnection.State != ConnectionState.Open) return;

            try
            {
                var list = new List<AgvCardInfo>();
                using (var cmd = new SqlCommand(@"
                    SELECT devid, isenable, remark FROM agvinfo ORDER BY devid", _monitorConnection))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var devid = reader.IsDBNull(0) ? "0" : reader[0].ToString();
                        var isenable = reader.IsDBNull(1) ? true : Convert.ToBoolean(reader[1]);
                        var remark = reader.IsDBNull(2) ? "" : reader[2].ToString() ?? "";

                        var name = "AGV " + devid;

                        double x = 0, y = 0, speed = 0, angle = 0;
                        int battery = 0;
                        bool alarm = false, connected = false;

                        if (!string.IsNullOrWhiteSpace(remark))
                        {
                            try
                            {
                                using var doc = JsonDocument.Parse(remark);
                                var root = doc.RootElement;

                                if (root.TryGetProperty("X", out var xP)) x = xP.GetDouble();
                                if (root.TryGetProperty("Y", out var yP)) y = yP.GetDouble();
                                if (root.TryGetProperty("AGVSpeed", out var sP)) speed = sP.GetDouble();
                                if (root.TryGetProperty("Angle", out var aP)) angle = aP.GetDouble();
                                if (root.TryGetProperty("BatteryPower", out var bP)) battery = bP.GetInt32();
                                if (root.TryGetProperty("Alarm", out var alP)) alarm = alP.GetBoolean();
                                if (root.TryGetProperty("Connected_Info", out var cP)) connected = cP.GetBoolean();
                            }
                            catch { }
                        }

                        var statusText = alarm ? "报警" : (connected ? "在线" : "离线");

                        list.Add(new AgvCardInfo
                        {
                            DevId = devid ?? "0",
                            Name = name,
                            X = x.ToString("F2"),
                            Y = y.ToString("F2"),
                            SpeedDisplay = speed.ToString("F2"),
                            AngleDisplay = angle.ToString("F2") + "°",
                            StatusText = statusText,
                            BatteryText = battery + "%"
                        });
                    }
                }

                pageDeviceMonitor.SetData(list);

                await LoadStationDataAsync();
            }
            catch (Exception ex)
            {
                pageDeviceMonitor.ShowError("查询失败: " + ex.Message);
            }
        }

        private static int SafeToInt(object value)
        {
            if (value == null || value == DBNull.Value) return 0;
            var s = value.ToString() ?? "0";
            if (s.Contains(';')) s = s.Split(';')[0];
            if (s.Contains(' ')) s = s.Split(' ')[0];
            return int.TryParse(s, out var v) ? v : 0;
        }

        private static double SafeToDouble(object value)
        {
            if (value == null || value == DBNull.Value) return 0.0;
            var s = value.ToString() ?? "0";
            return double.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0.0;
        }

        private async System.Threading.Tasks.Task LoadStationDataAsync()
        {
            if (_monitorConnection == null || _monitorConnection.State != ConnectionState.Open) return;

            var stations = new List<StationInfo>();
            var links = new List<StationLink>();

            try
            {
                using (var cmd = new SqlCommand(@"
                    SELECT station, x, y, frontsite, backsite, finalsite, groups
                    FROM agvstationinfo ORDER BY station", _monitorConnection))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var station = reader.IsDBNull(0) ? "" : reader[0].ToString() ?? "";
                        stations.Add(new StationInfo
                        {
                            StationId = station,
                            X = SafeToDouble(reader[1]),
                            Y = SafeToDouble(reader[2]),
                            FrontSite = SafeToInt(reader[3]),
                            BackSite = SafeToInt(reader[4]),
                            FinalSite = SafeToInt(reader[5]),
                            Groups = SafeToInt(reader[6])
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("站点查询失败: " + ex.Message);
            }

            try
            {
                using (var linkCmd = new SqlCommand(@"
                    SELECT station0, station1 FROM agvstationnetinfo", _monitorConnection))
                using (var linkReader = await linkCmd.ExecuteReaderAsync())
                {
                    var seen = new HashSet<string>();
                    while (await linkReader.ReadAsync())
                    {
                        var s0 = linkReader.IsDBNull(0) ? "" : linkReader.GetValue(0).ToString() ?? "";
                        var s1 = linkReader.IsDBNull(1) ? "" : linkReader.GetValue(1).ToString() ?? "";
                        if (string.IsNullOrEmpty(s0) || string.IsNullOrEmpty(s1)) continue;
                        var key = string.Compare(s0, s1, StringComparison.Ordinal) < 0
                            ? $"{s0}->{s1}" : $"{s1}->{s0}";
                        if (seen.Add(key))
                            links.Add(new StationLink { FromStation = s0, ToStation = s1 });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("连线查询失败: " + ex.Message);
            }

            pageDeviceMonitor.SetStations(stations, links);
        }

        private async System.Threading.Tasks.Task RefreshAgvDataAsync()
        {
            if (_isRefreshing) return;
            _isRefreshing = true;
            try
            {
                if (_monitorConnection == null || _monitorConnection.State != ConnectionState.Open)
                    return;

                var list = new List<AgvCardInfo>();
                using (var cmd = new SqlCommand(@"
                    SELECT devid, isenable, remark FROM agvinfo ORDER BY devid", _monitorConnection))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var devid = reader.IsDBNull(0) ? "0" : reader[0].ToString();
                        var remark = reader.IsDBNull(2) ? "" : reader[2].ToString() ?? "";

                        var name = "AGV " + devid;
                        double x = 0, y = 0, speed = 0, angle = 0;
                        int battery = 0;
                        bool alarm = false, connected = false;

                        if (!string.IsNullOrWhiteSpace(remark))
                        {
                            try
                            {
                                using var doc = JsonDocument.Parse(remark);
                                var root = doc.RootElement;
                                if (root.TryGetProperty("X", out var xP)) x = xP.GetDouble();
                                if (root.TryGetProperty("Y", out var yP)) y = yP.GetDouble();
                                if (root.TryGetProperty("AGVSpeed", out var sP)) speed = sP.GetDouble();
                                if (root.TryGetProperty("Angle", out var aP)) angle = aP.GetDouble();
                                if (root.TryGetProperty("BatteryPower", out var bP)) battery = bP.GetInt32();
                                if (root.TryGetProperty("Alarm", out var alP)) alarm = alP.GetBoolean();
                                if (root.TryGetProperty("Connected_Info", out var cP)) connected = cP.GetBoolean();
                            }
                            catch { }
                        }

                        list.Add(new AgvCardInfo
                        {
                            DevId = devid ?? "0",
                            Name = name,
                            X = x.ToString("F2"),
                            Y = y.ToString("F2"),
                            SpeedDisplay = speed.ToString("F2"),
                            AngleDisplay = angle.ToString("F2") + "°",
                            StatusText = alarm ? "报警" : (connected ? "在线" : "离线"),
                            BatteryText = battery + "%"
                        });
                    }
                }

                pageDeviceMonitor.SetData(list);
            }
            catch { }
            finally
            {
                _isRefreshing = false;
            }
        }
    }
}
