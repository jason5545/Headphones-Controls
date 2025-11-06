using System;
using System.Windows;

namespace AkgController;

/// <summary>
/// App.xaml 的互動邏輯
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 檢查是否有命令列參數
        if (e.Args.Length > 0)
        {
            // CLI 模式
            RunCliMode(e.Args);
            Shutdown();
        }
        // 否則啟動 GUI 模式（預設）
    }

    private async void RunCliMode(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        int exitCode = await CliProgram.RunAsync(args);
        Environment.Exit(exitCode);
    }
}
