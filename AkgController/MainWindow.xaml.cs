using System;
using System.Windows;
using System.Windows.Media;

namespace AkgController;

/// <summary>
/// MainWindow.xaml 的互動邏輯
/// </summary>
public partial class MainWindow : Window
{
    private AkgN9Controller? _controller;
    private bool _isConnected = false;

    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 連接按鈕點擊事件
    /// </summary>
    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isConnected)
        {
            // 已連接，執行中斷連接
            DisconnectDevice();
            return;
        }

        try
        {
            ConnectButton.IsEnabled = false;
            StatusText.Text = "正在連接...";

            _controller = new AkgN9Controller();
            bool success = await _controller.ConnectAsync();

            if (success)
            {
                _isConnected = true;
                StatusText.Text = "✓ 已連接到 AKG N9 Hybrid";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // 綠色
                ConnectButton.Content = "中斷連接";
                ConnectButton.Background = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // 紅色
                EnableControlButtons(true);
            }
            else
            {
                StatusText.Text = "✗ 連接失敗";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // 紅色
                MessageBox.Show(
                    "無法連接到耳機。\n\n請確認：\n" +
                    "1. 耳機已開啟且在範圍內\n" +
                    "2. 已在 Windows 藍牙設定中配對\n" +
                    "3. 耳機名稱為 \"AKG N9 Hybrid\"",
                    "連接失敗",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "✗ 連接錯誤";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // 紅色
            MessageBox.Show(
                $"連接時發生錯誤：\n{ex.Message}",
                "錯誤",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            ConnectButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// 中斷裝置連接
    /// </summary>
    private void DisconnectDevice()
    {
        _controller?.Dispose();
        _controller = null;
        _isConnected = false;

        StatusText.Text = "未連接";
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)); // 灰色
        ConnectButton.Content = "連接耳機";
        ConnectButton.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // 綠色
        EnableControlButtons(false);
    }

    /// <summary>
    /// 啟用/停用控制按鈕
    /// </summary>
    private void EnableControlButtons(bool enabled)
    {
        AncOnButton.IsEnabled = enabled;
        AncOffButton.IsEnabled = enabled;
        PassThroughButton.IsEnabled = enabled;
        Anc1Button.IsEnabled = enabled;
        Anc2Button.IsEnabled = enabled;
        PassThrough1Button.IsEnabled = enabled;
        PassThrough2Button.IsEnabled = enabled;
    }

    /// <summary>
    /// 開啟降噪按鈕
    /// </summary>
    private async void AncOnButton_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteCommand(async () =>
        {
            return await _controller!.EnableAncAsync(RaceCommand.AncMode.Anc1);
        }, "開啟降噪");
    }

    /// <summary>
    /// 關閉降噪按鈕
    /// </summary>
    private async void AncOffButton_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteCommand(async () =>
        {
            return await _controller!.DisableAncAsync();
        }, "關閉降噪");
    }

    /// <summary>
    /// 環境音模式按鈕
    /// </summary>
    private async void PassThroughButton_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteCommand(async () =>
        {
            return await _controller!.EnablePassThroughAsync(RaceCommand.AncMode.PassThrough1);
        }, "環境音模式");
    }

    /// <summary>
    /// ANC 模式 1 按鈕
    /// </summary>
    private async void Anc1Button_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteCommand(async () =>
        {
            return await _controller!.EnableAncAsync(RaceCommand.AncMode.Anc1);
        }, "ANC 模式 1");
    }

    /// <summary>
    /// ANC 模式 2 按鈕
    /// </summary>
    private async void Anc2Button_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteCommand(async () =>
        {
            return await _controller!.EnableAncAsync(RaceCommand.AncMode.Anc2);
        }, "ANC 模式 2");
    }

    /// <summary>
    /// 環境音模式 1 按鈕
    /// </summary>
    private async void PassThrough1Button_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteCommand(async () =>
        {
            return await _controller!.EnablePassThroughAsync(RaceCommand.AncMode.PassThrough1);
        }, "環境音模式 1");
    }

    /// <summary>
    /// 環境音模式 2 按鈕
    /// </summary>
    private async void PassThrough2Button_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteCommand(async () =>
        {
            return await _controller!.EnablePassThroughAsync(RaceCommand.AncMode.PassThrough2);
        }, "環境音模式 2");
    }

    /// <summary>
    /// 執行指令的通用方法
    /// </summary>
    private async System.Threading.Tasks.Task ExecuteCommand(Func<System.Threading.Tasks.Task<bool>> command, string commandName)
    {
        if (_controller == null || !_isConnected)
        {
            MessageBox.Show("請先連接耳機", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            EnableControlButtons(false);
            StatusText.Text = $"正在執行：{commandName}...";

            bool success = await command();

            if (success)
            {
                StatusText.Text = $"✓ {commandName} 完成";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // 綠色

                // 1 秒後恢復狀態文字
                await System.Threading.Tasks.Task.Delay(1000);
                StatusText.Text = "✓ 已連接到 AKG N9 Hybrid";
            }
            else
            {
                StatusText.Text = $"✗ {commandName} 失敗";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // 紅色
                MessageBox.Show(
                    $"執行 {commandName} 失敗。\n\n可能的原因：\n" +
                    "1. 藍牙連接不穩定\n" +
                    "2. 耳機已關閉或超出範圍\n" +
                    "3. 耳機正在與其他裝置連接",
                    "執行失敗",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"✗ {commandName} 錯誤";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // 紅色
            MessageBox.Show(
                $"執行時發生錯誤：\n{ex.Message}",
                "錯誤",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            EnableControlButtons(true);
        }
    }

    /// <summary>
    /// 視窗關閉時清理資源
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _controller?.Dispose();
    }
}
