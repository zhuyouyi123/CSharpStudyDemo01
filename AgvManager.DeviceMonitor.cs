using System.Data;
using System.Text.Json;
using System.Windows;
using Microsoft.Data.SqlClient;

namespace StudyDemo01
{
    public partial class AgvManager : Window
    {
        private SqlConnection? _monitorConnection;
        private bool _monitorInitialized = false;

        private void NavDeviceMonitor_Click(object sender, RoutedEventArgs e)
        {
            if (!_monitorInitialized)
            {
                _monitorInitialized = true;
                navAgvList.Click += NavOther_Click;
                navQueueInfo.Click += NavOther_Click;
            }

            pageAgvList.Visibility = Visibility.Collapsed;
            pageQueueInfo.Visibility = Visibility.Collapsed;
            pageDeviceMonitor.Visibility = Visibility.Visible;
            txtTitle.Text = "设备监控";

            _ = EnsureMonitorConnectionAsync();
        }

        private void NavOther_Click(object sender, RoutedEventArgs e)
        {
            pageDeviceMonitor.Visibility = Visibility.Collapsed;
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

        private async System.Threading.Tasks.Task EnsureMonitorConnectionAsync()
        {
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
                    var config = ConfigHelper.DatabaseSettings;
                    var connStr = config.GetConnectionString(dbName);
                    _monitorConnection = new SqlConnection(connStr);
                    await _monitorConnection.OpenAsync();
                }

                await LoadDeviceMonitorDataAsync();
            }
            catch (Exception ex)
            {
                pageDeviceMonitor.ShowError("连接失败: " + ex.Message);
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

                        var statusText = connected ? "在线" : "离线";

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

            try
            {
                using var cmd = new SqlCommand(@"
                    SELECT station, x, y, frontsite, backsite, finalsite, groups
                    FROM agvstationinfo ORDER BY station", _monitorConnection);
                using var reader = await cmd.ExecuteReaderAsync();

                var stations = new List<StationInfo>();
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

                System.Diagnostics.Debug.WriteLine($"站点查询成功: {stations.Count} 个站点");
                pageDeviceMonitor.SetStations(stations);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("站点查询失败: " + ex.Message);
                System.Windows.MessageBox.Show("站点查询失败: " + ex.Message, "调试", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }
    }
}
