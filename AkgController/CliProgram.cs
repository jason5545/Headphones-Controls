using System;
using System.Threading.Tasks;

namespace AkgController;

/// <summary>
/// CLI 模式程式進入點
/// </summary>
public class CliProgram
{
    public static async Task<int> RunAsync(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("===========================================");
        Console.WriteLine("  AKG N9 Hybrid 降噪控制程式 v1.1");
        Console.WriteLine("  基於 Airoha RACE 協定開發");
        Console.WriteLine("===========================================\n");

        // 檢查參數
        if (args.Length == 0)
        {
            ShowUsage();
            return 1;
        }

        string command = args[0].ToLower();

        using var controller = new AkgN9Controller();

        // 連接到耳機
        Console.WriteLine("步驟 1: 連接到 AKG N9 Hybrid");
        Console.WriteLine("--------------------------------------------");

        if (!await controller.ConnectAsync())
        {
            Console.WriteLine("\n❌ 連接失敗！");
            Console.WriteLine("\n疑難排解：");
            Console.WriteLine("  1. 確認耳機已開啟且在藍牙配對模式");
            Console.WriteLine("  2. 確認 Windows 藍牙設定中已配對耳機");
            Console.WriteLine("  3. 確認耳機名稱為 \"AKG N9 Hybrid\"");
            Console.WriteLine("  4. 嘗試重新配對耳機");
            return 1;
        }

        Console.WriteLine("\n步驟 2: 執行指令");
        Console.WriteLine("--------------------------------------------");

        // 執行指令
        bool success = false;

        switch (command)
        {
            case "on":
                success = await controller.EnableAncAsync(RaceCommand.AncMode.Anc1);
                break;

            case "off":
                success = await controller.DisableAncAsync();
                break;

            case "toggle":
                success = await controller.ToggleAncAsync();
                break;

            case "passthrough":
            case "ambient":
                success = await controller.EnablePassThroughAsync(RaceCommand.AncMode.PassThrough1);
                break;

            case "anc1":
                success = await controller.EnableAncAsync(RaceCommand.AncMode.Anc1);
                break;

            case "anc2":
                success = await controller.EnableAncAsync(RaceCommand.AncMode.Anc2);
                break;

            case "passthrough1":
                success = await controller.EnablePassThroughAsync(RaceCommand.AncMode.PassThrough1);
                break;

            case "passthrough2":
                success = await controller.EnablePassThroughAsync(RaceCommand.AncMode.PassThrough2);
                break;

            default:
                Console.WriteLine($"未知的指令：{command}");
                ShowUsage();
                return 1;
        }

        // 等待回應
        Console.WriteLine("\n等待耳機回應...");
        await Task.Delay(1000);  // 給耳機時間處理指令

        Console.WriteLine("\n===========================================");
        if (success)
        {
            Console.WriteLine("✓ 操作完成");
            return 0;
        }
        else
        {
            Console.WriteLine("✗ 操作失敗");
            return 1;
        }
    }

    static void ShowUsage()
    {
        Console.WriteLine("\n使用方式：");
        Console.WriteLine("  AkgController.exe <指令>\n");

        Console.WriteLine("基本指令：");
        Console.WriteLine("  on              開啟降噪（ANC 模式 1）");
        Console.WriteLine("  off             關閉降噪");
        Console.WriteLine("  toggle          切換降噪狀態");
        Console.WriteLine("  passthrough     環境音模式（PassThrough 1）\n");

        Console.WriteLine("進階指令：");
        Console.WriteLine("  anc1            ANC 模式 1（標準降噪）");
        Console.WriteLine("  anc2            ANC 模式 2");
        Console.WriteLine("  passthrough1    環境音模式 1");
        Console.WriteLine("  passthrough2    環境音模式 2\n");

        Console.WriteLine("範例：");
        Console.WriteLine("  AkgController.exe on");
        Console.WriteLine("  AkgController.exe off");
        Console.WriteLine("  AkgController.exe passthrough\n");

        Console.WriteLine("注意事項：");
        Console.WriteLine("  • 執行前請確認耳機已在 Windows 藍牙設定中配對");
        Console.WriteLine("  • 耳機名稱必須為 \"AKG N9 Hybrid\"");
        Console.WriteLine("  • 需要 Windows 10/11 作業系統");
    }
}
