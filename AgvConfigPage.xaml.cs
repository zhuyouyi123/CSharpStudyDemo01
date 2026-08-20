using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace StudyDemo01
{
    public partial class AgvConfigPage : UserControl
    {
        public AgvConfigPage()
        {
            InitializeComponent();
            txtRefreshInterval.PreviewTextInput += TxtRefreshInterval_PreviewTextInput;
            txtRefreshInterval.PreviewKeyDown += TxtRefreshInterval_PreviewKeyDown;
            txtOnColor.TextChanged += TxtColor_TextChanged;
            txtOffColor.TextChanged += TxtColor_TextChanged;
            txtAlarmColor.TextChanged += TxtColor_TextChanged;
            txtOnColor.PreviewTextInput += TxtHex_PreviewTextInput;
            txtOffColor.PreviewTextInput += TxtHex_PreviewTextInput;
            txtAlarmColor.PreviewTextInput += TxtHex_PreviewTextInput;
            LoadCurrentConfig();
        }

        private void TxtRefreshInterval_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]$");
        }

        private void TxtRefreshInterval_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space) e.Handled = true;
        }

        private void TxtHex_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "[0-9A-Fa-f]");
        }

        private void TxtColor_TextChanged(object sender, TextChangedEventArgs e)
        {
            var txt = sender as TextBox;
            if (txt == null) return;

            var color = txt.Text?.Trim();
            if (color != null && !color.StartsWith("#")) color = "#" + color;

            var preview = txt.Name switch
            {
                "txtOnColor" => borderOnColorPreview,
                "txtOffColor" => borderOffColorPreview,
                "txtAlarmColor" => borderAlarmColorPreview,
                _ => null
            };

            if (preview != null && TryParseColor(color, out _))
            {
                preview.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            }
        }

        private void OnColorPreview_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string targetName)
            {
                var popup = targetName switch
                {
                    "txtOnColor" => popupOnColor,
                    "txtOffColor" => popupOffColor,
                    "txtAlarmColor" => popupAlarmColor,
                    _ => null
                };
                if (popup != null) popup.IsOpen = true;
            }
        }

        private void ColorSwatch_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border swatch && swatch.Tag is string tag)
            {
                var parts = tag.Split(':');
                if (parts.Length != 2) return;

                var type = parts[0];
                var hex = parts[1];

                TextBox? txt = type switch
                {
                    "On" => txtOnColor,
                    "Off" => txtOffColor,
                    "Alarm" => txtAlarmColor,
                    _ => null
                };

                Border? preview = type switch
                {
                    "On" => borderOnColorPreview,
                    "Off" => borderOffColorPreview,
                    "Alarm" => borderAlarmColorPreview,
                    _ => null
                };

                if (txt != null)
                {
                    txt.Text = hex;
                    if (preview != null && TryParseColor(hex, out var c))
                        preview.Background = new SolidColorBrush(c);
                }

                var popup = type switch
                {
                    "On" => popupOnColor,
                    "Off" => popupOffColor,
                    "Alarm" => popupAlarmColor,
                    _ => null
                };
                if (popup != null) popup.IsOpen = false;
            }
        }

        private void PopupBorder_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border)
            {
                var parent = VisualTreeHelper.GetParent(border);
                while (parent != null && parent is not Popup)
                    parent = VisualTreeHelper.GetParent(parent);
                if (parent is Popup popup)
                    popup.IsOpen = false;
            }
        }

        private bool _showUnlinked = true;
        private bool _showStationTitle = true;

        private void ToggleUnlinked_Click(object sender, MouseButtonEventArgs e)
        {
            _showUnlinked = !_showUnlinked;
            UpdateToggleVisual();
        }

        private void UpdateToggleVisual()
        {
            if (toggleUnlinked != null && toggleUnlinkedThumb != null)
            {
                toggleUnlinked.Background = new SolidColorBrush(_showUnlinked ? Color.FromRgb(59, 130, 246) : Color.FromRgb(203, 213, 225));
                toggleUnlinkedThumb.HorizontalAlignment = _showUnlinked ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                toggleUnlinkedThumb.Margin = _showUnlinked ? new Thickness(0, 0, 3, 0) : new Thickness(3, 0, 0, 0);
            }
            if (toggleStationTitle != null && toggleStationTitleThumb != null)
            {
                toggleStationTitle.Background = new SolidColorBrush(_showStationTitle ? Color.FromRgb(59, 130, 246) : Color.FromRgb(203, 213, 225));
                toggleStationTitleThumb.HorizontalAlignment = _showStationTitle ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                toggleStationTitleThumb.Margin = _showStationTitle ? new Thickness(0, 0, 3, 0) : new Thickness(3, 0, 0, 0);
            }
        }

        private void BtnSaveUnlinked_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = ConfigHelper.AgvSettings;
                config.ShowUnlinkedStations = _showUnlinked;
                ConfigHelper.SaveAgvConfig(config);

                var window = Window.GetWindow(this);
                if (window is AgvManager manager)
                    manager.ApplyStationFilter();

                MessageBox.Show("配置已保存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ToggleStationTitle_Click(object sender, MouseButtonEventArgs e)
        {
            _showStationTitle = !_showStationTitle;
            UpdateToggleVisual();
        }

        private void BtnSaveStationTitle_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = ConfigHelper.AgvSettings;
                config.ShowStationTitles = _showStationTitle;
                ConfigHelper.SaveAgvConfig(config);

                var window = Window.GetWindow(this);
                if (window is AgvManager manager)
                    manager.ApplyStationFilter();

                MessageBox.Show("配置已保存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void LoadCurrentConfig()
        {
            var config = ConfigHelper.AgvSettings;
            txtRefreshInterval.Text = config.MapRefreshInterval.ToString();
            SetColorInput(txtOnColor, borderOnColorPreview, config.AgvOnLineColor);
            SetColorInput(txtOffColor, borderOffColorPreview, config.AgvOffLineColor);
            SetColorInput(txtAlarmColor, borderAlarmColorPreview, config.AgvAlarmColor);
            _showUnlinked = config.ShowUnlinkedStations;
            _showStationTitle = config.ShowStationTitles;
            UpdateToggleVisual();
        }

        private static void SetColorInput(TextBox txt, Border preview, string hex)
        {
            txt.Text = hex;
            if (TryParseColor(hex, out var color))
                preview.Background = new SolidColorBrush(color);
        }

        private static bool TryParseColor(string? hex, out Color color)
        {
            color = Colors.Gray;
            if (string.IsNullOrWhiteSpace(hex)) return false;
            try
            {
                color = (Color)ColorConverter.ConvertFromString(hex);
                return true;
            }
            catch { return false; }
        }

        private void BtnSaveRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(txtRefreshInterval.Text, out var interval) || interval < 1 || interval > 60)
            {
                MessageBox.Show("请输入 1~60 的整数", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var config = ConfigHelper.AgvSettings;
                config.MapRefreshInterval = interval;
                ConfigHelper.SaveAgvConfig(config);
                MessageBox.Show("刷新间隔已保存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSaveColors_Click(object sender, RoutedEventArgs e)
        {
            var onColor = txtOnColor.Text?.Trim() ?? "#3B82F6";
            var offColor = txtOffColor.Text?.Trim() ?? "#94A3B8";
            var alarmColor = txtAlarmColor.Text?.Trim() ?? "#EF4444";

            if (!onColor.StartsWith("#")) onColor = "#" + onColor;
            if (!offColor.StartsWith("#")) offColor = "#" + offColor;
            if (!alarmColor.StartsWith("#")) alarmColor = "#" + alarmColor;

            try
            {
                var config = ConfigHelper.AgvSettings;
                config.AgvOnLineColor = onColor;
                config.AgvOffLineColor = offColor;
                config.AgvAlarmColor = alarmColor;
                ConfigHelper.SaveAgvConfig(config);

                var window = Window.GetWindow(this);
                if (window is AgvManager manager)
                {
                    manager.ApplyAgvColors();
                }

                MessageBox.Show("颜色配置已保存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
