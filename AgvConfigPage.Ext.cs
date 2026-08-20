using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace StudyDemo01
{
    public partial class AgvConfigPage : UserControl
    {
        private DispatcherTimer? _tipsTimer;

        private void AgvConfigPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (txtIconSize != null)
                txtIconSize.Text = ConfigHelper.AgvSettings.AgvIconSize.ToString();
            if (txtAngleOffset != null)
                txtAngleOffset.Text = ConfigHelper.AgvSettings.AgvAngleOffset.ToString();
        }

        private void BtnSaveRefreshV2_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtRefreshInterval.Text, out var val) && val > 0)
            {
                ConfigHelper.AgvSettings.MapRefreshInterval = val;
                ConfigHelper.SaveAgvConfig(ConfigHelper.AgvSettings);
                ShowTips("刷新间隔已保存");
            }
        }

        private void BtnSaveIconSize_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtIconSize.Text, out var val) && val > 0)
            {
                ConfigHelper.AgvSettings.AgvIconSize = val;
                ConfigHelper.SaveAgvConfig(ConfigHelper.AgvSettings);
                ShowTips("图标大小已保存");
            }
        }

        private void BtnSaveAngleOffset_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtAngleOffset.Text, out var val))
            {
                ConfigHelper.AgvSettings.AgvAngleOffset = val;
                ConfigHelper.SaveAgvConfig(ConfigHelper.AgvSettings);
                ShowTips("角度偏移已保存");
            }
        }

        private void BtnSaveColorsV2_Click(object sender, RoutedEventArgs e)
        {
            ConfigHelper.AgvSettings.AgvOnLineColor = txtOnColor.Text;
            ConfigHelper.AgvSettings.AgvOffLineColor = txtOffColor.Text;
            ConfigHelper.AgvSettings.AgvAlarmColor = txtAlarmColor.Text;
            ConfigHelper.SaveAgvConfig(ConfigHelper.AgvSettings);
            ShowTips("颜色配置已保存");
        }

        private void ShowTips(string msg)
        {
            tipsText.Text = msg;
            tipsToast.Visibility = Visibility.Visible;

            _tipsTimer?.Stop();
            _tipsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _tipsTimer.Tick += (s, ev) =>
            {
                _tipsTimer.Stop();
                tipsToast.Visibility = Visibility.Collapsed;
            };
            _tipsTimer.Start();
        }
    }
}
