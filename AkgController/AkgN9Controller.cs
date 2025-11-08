using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace AkgController;

/// <summary>
/// AKG N9 Hybrid 藍牙控制器
/// 使用 Airoha 晶片的 BLE GATT 協定
/// </summary>
public class AkgN9Controller : IDisposable
{
    // Airoha BLE UUIDs (來源：com/airoha/liblinker/constant/UuidTable.java)
    private static readonly Guid ServiceUuid = Guid.Parse("5052494D-2DAB-0341-6972-6F6861424C45");
    private static readonly Guid RxCharacteristicUuid = Guid.Parse("43484152-2DAB-3141-6972-6F6861424C45"); // App → 耳機
    private static readonly Guid TxCharacteristicUuid = Guid.Parse("43484152-2DAB-3241-6972-6F6861424C45"); // 耳機 → App
    private static readonly Guid CccdUuid = Guid.Parse("00002902-0000-1000-8000-00805F9B34FB");

    private const string DeviceName = "AKG N9 Hybrid";
    private const int ConnectionTimeout = 10000;  // 10 秒
    private const int MaxRetries = 3;  // GATT 服務發現最大重試次數

    private BluetoothLEDevice? _device;
    private GattSession? _gattSession;  // GATT 會話管理
    private GattCharacteristic? _rxCharacteristic;  // 寫入用
    private GattCharacteristic? _txCharacteristic;  // 通知用
    private TaskCompletionSource<bool>? _connectionTcs;  // 連接狀態等待

    /// <summary>
    /// 搜尋並連接到 AKG N9 耳機
    /// </summary>
    public async Task<bool> ConnectAsync()
    {
        try
        {
            Console.WriteLine($"正在搜尋 {DeviceName}...");

            // 搜尋藍牙裝置
            var selector = BluetoothLEDevice.GetDeviceSelectorFromDeviceName(DeviceName);
            var devices = await DeviceInformation.FindAllAsync(selector);

            if (devices.Count == 0)
            {
                Console.WriteLine($"找不到 {DeviceName}，請確認耳機已開啟且在範圍內。");
                Console.WriteLine("\n正在列出所有已配對的藍牙 LE 裝置...");
                await ListAllBluetoothDevicesAsync();
                return false;
            }

            Console.WriteLine($"找到 {devices.Count} 個裝置，嘗試連接第一個...");

            // 建立裝置物件
            _device = await BluetoothLEDevice.FromIdAsync(devices[0].Id);

            if (_device == null)
            {
                Console.WriteLine("❌ 無法建立裝置物件。");
                return false;
            }

            Console.WriteLine($"✓ 裝置物件已建立: {_device.Name} ({_device.BluetoothAddress:X})");

            // 訂閱連接狀態變更事件
            _device.ConnectionStatusChanged += OnConnectionStatusChanged;

            // 建立並配置 GATT Session（關鍵：維持連接穩定性）
            Console.WriteLine("正在建立 GATT Session...");
            _gattSession = await GattSession.FromDeviceIdAsync(_device.BluetoothDeviceId);

            if (_gattSession == null)
            {
                Console.WriteLine("❌ 無法建立 GATT Session。");
                return false;
            }

            // 設定維持連接（這是確保連接穩定的關鍵）
            _gattSession.MaintainConnection = true;
            Console.WriteLine($"✓ GATT Session 已建立 (MaxPduSize: {_gattSession.MaxPduSize})");

            // 檢查並等待連接建立
            if (_device.ConnectionStatus != BluetoothConnectionStatus.Connected)
            {
                Console.WriteLine("等待裝置連接中...");
                _connectionTcs = new TaskCompletionSource<bool>();

                // 等待最多 10 秒讓連接建立
                var timeoutTask = Task.Delay(ConnectionTimeout);
                var completedTask = await Task.WhenAny(_connectionTcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    Console.WriteLine("❌ 連接超時（10 秒內未建立連接）。");
                    return false;
                }

                if (!_connectionTcs.Task.Result)
                {
                    Console.WriteLine("❌ 連接失敗。");
                    return false;
                }
            }

            Console.WriteLine($"✓ 已連接到 {_device.Name}，連接狀態: {_device.ConnectionStatus}");

            // 使用重試機制取得 GATT 服務（解決首次發現可能失敗的問題）
            GattDeviceServicesResult? servicesResult = null;
            int retryCount = 0;

            while (retryCount < MaxRetries)
            {
                Console.WriteLine($"正在發現 GATT 服務... (嘗試 {retryCount + 1}/{MaxRetries})");

                // 使用 Uncached 模式強制重新發現，避免使用過時的快取資料
                servicesResult = await _device.GetGattServicesForUuidAsync(
                    ServiceUuid,
                    BluetoothCacheMode.Uncached);

                if (servicesResult.Status == GattCommunicationStatus.Success &&
                    servicesResult.Services.Count > 0)
                {
                    Console.WriteLine("✓ GATT 服務發現成功");
                    break;
                }

                retryCount++;
                if (retryCount < MaxRetries)
                {
                    Console.WriteLine($"⚠ GATT 服務發現失敗 (狀態: {servicesResult.Status})，1 秒後重試...");
                    await Task.Delay(1000);
                }
            }

            if (servicesResult?.Status != GattCommunicationStatus.Success ||
                servicesResult.Services.Count == 0)
            {
                Console.WriteLine($"❌ 找不到 Airoha GATT 服務 (UUID: {ServiceUuid})");
                Console.WriteLine($"   狀態: {servicesResult?.Status}");
                if (servicesResult?.ProtocolError.HasValue == true)
                {
                    Console.WriteLine($"   協定錯誤: {servicesResult.ProtocolError.Value}");
                }
                return false;
            }

            var service = servicesResult.Services[0];
            Console.WriteLine($"✓ 已找到 Airoha GATT 服務");

            // 取得 RX Characteristic (寫入用)
            Console.WriteLine("正在取得 RX Characteristic...");
            var rxResult = await service.GetCharacteristicsForUuidAsync(RxCharacteristicUuid);
            if (rxResult.Status != GattCommunicationStatus.Success || rxResult.Characteristics.Count == 0)
            {
                Console.WriteLine($"❌ 找不到 RX Characteristic (UUID: {RxCharacteristicUuid})");
                Console.WriteLine($"   狀態: {rxResult.Status}");
                return false;
            }
            _rxCharacteristic = rxResult.Characteristics[0];
            Console.WriteLine($"✓ RX Characteristic 已取得 (屬性: {_rxCharacteristic.CharacteristicProperties})");

            // 取得 TX Characteristic (通知用)
            Console.WriteLine("正在取得 TX Characteristic...");
            var txResult = await service.GetCharacteristicsForUuidAsync(TxCharacteristicUuid);
            if (txResult.Status != GattCommunicationStatus.Success || txResult.Characteristics.Count == 0)
            {
                Console.WriteLine($"❌ 找不到 TX Characteristic (UUID: {TxCharacteristicUuid})");
                Console.WriteLine($"   狀態: {txResult.Status}");
                return false;
            }
            _txCharacteristic = txResult.Characteristics[0];
            Console.WriteLine($"✓ TX Characteristic 已取得 (屬性: {_txCharacteristic.CharacteristicProperties})");

            // 啟用 TX Notification
            Console.WriteLine("正在啟用通知...");
            var cccdResult = await _txCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify);

            if (cccdResult != GattCommunicationStatus.Success)
            {
                Console.WriteLine($"❌ 無法啟用通知 (狀態: {cccdResult})");
                return false;
            }

            // 訂閱通知事件
            _txCharacteristic.ValueChanged += OnNotificationReceived;
            Console.WriteLine("✓ 通知已啟用");

            Console.WriteLine("\n========================================");
            Console.WriteLine("✓ BLE GATT 初始化完成，準備發送指令");
            Console.WriteLine("========================================\n");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 連接失敗：{ex.Message}");
            Console.WriteLine($"   堆疊追蹤：{ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// 檢查連接是否仍然有效
    /// </summary>
    public bool IsConnected()
    {
        return _device != null &&
               _device.ConnectionStatus == BluetoothConnectionStatus.Connected &&
               _gattSession != null &&
               _rxCharacteristic != null;
    }

    /// <summary>
    /// 連接狀態變更事件處理器
    /// </summary>
    private void OnConnectionStatusChanged(BluetoothLEDevice sender, object args)
    {
        var status = sender.ConnectionStatus;
        Console.WriteLine($"📡 連接狀態變更: {status}");

        if (status == BluetoothConnectionStatus.Connected)
        {
            Console.WriteLine("✓ 裝置已連接");
            _connectionTcs?.TrySetResult(true);
        }
        else if (status == BluetoothConnectionStatus.Disconnected)
        {
            Console.WriteLine("⚠ 裝置已中斷連接");
            _connectionTcs?.TrySetResult(false);
        }
    }

    /// <summary>
    /// 發送 RACE 指令
    /// </summary>
    private async Task<bool> SendRaceCommandAsync(byte[] command)
    {
        // 檢查連接狀態
        if (!IsConnected())
        {
            Console.WriteLine("❌ 裝置未連接，無法發送指令");
            Console.WriteLine($"   裝置狀態: {_device?.ConnectionStatus.ToString() ?? "null"}");
            return false;
        }

        if (_rxCharacteristic == null)
        {
            Console.WriteLine("❌ RX Characteristic 未初始化");
            return false;
        }

        try
        {
            Console.WriteLine($"📤 發送指令: {RaceCommand.ToHexString(command)}");

            var writer = new DataWriter();
            writer.WriteBytes(command);

            // 使用 WriteValueWithResultAsync 以獲得更詳細的錯誤資訊
            var result = await _rxCharacteristic.WriteValueWithResultAsync(writer.DetachBuffer());

            if (result.Status == GattCommunicationStatus.Success)
            {
                Console.WriteLine("✓ 指令已成功發送");
                return true;
            }
            else
            {
                Console.WriteLine($"❌ 發送失敗：{result.Status}");
                if (result.ProtocolError.HasValue)
                {
                    Console.WriteLine($"   協定錯誤碼: 0x{result.ProtocolError.Value:X2}");
                }
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 發送指令時發生錯誤：{ex.Message}");
            Console.WriteLine($"   例外類型: {ex.GetType().Name}");
            return false;
        }
    }

    /// <summary>
    /// 開啟 ANC（主動降噪）
    /// </summary>
    public async Task<bool> EnableAncAsync(RaceCommand.AncMode mode = RaceCommand.AncMode.Anc1)
    {
        Console.WriteLine($"啟用 ANC 模式：{mode}");
        var command = RaceCommand.CreateAncOnCommand(mode);
        return await SendRaceCommandAsync(command);
    }

    /// <summary>
    /// 關閉 ANC
    /// </summary>
    public async Task<bool> DisableAncAsync()
    {
        Console.WriteLine("關閉 ANC");
        var command = RaceCommand.CreateAncOffCommand();
        return await SendRaceCommandAsync(command);
    }

    /// <summary>
    /// 啟用環境音模式
    /// </summary>
    public async Task<bool> EnablePassThroughAsync(RaceCommand.AncMode mode = RaceCommand.AncMode.PassThrough1)
    {
        Console.WriteLine($"啟用環境音模式：{mode}");
        var command = RaceCommand.CreatePassThroughCommand(mode);
        return await SendRaceCommandAsync(command);
    }

    /// <summary>
    /// 切換 ANC 狀態（簡化版：Off → ANC1 → PassThrough1 → Off）
    /// </summary>
    public async Task<bool> ToggleAncAsync()
    {
        // 這裡簡化處理，實際應該先查詢當前狀態
        // 目前直接啟用 ANC1
        Console.WriteLine("切換 ANC（啟用 ANC1）");
        return await EnableAncAsync(RaceCommand.AncMode.Anc1);
    }

    /// <summary>
    /// 通知接收處理
    /// </summary>
    private void OnNotificationReceived(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        var reader = DataReader.FromBuffer(args.CharacteristicValue);
        var data = new byte[reader.UnconsumedBufferLength];
        reader.ReadBytes(data);

        Console.WriteLine($"📥 收到通知: {RaceCommand.ToHexString(data)} (長度: {data.Length} bytes)");

        // 檢查是否為成功回應
        if (RaceCommand.IsResponseSuccess(data))
        {
            Console.WriteLine("✓ 耳機回應：指令執行成功");
        }
        else
        {
            Console.WriteLine("⚠ 耳機回應：未知或失敗");
        }
    }

    /// <summary>
    /// 列出所有已配對的藍牙 LE 裝置（診斷用）
    /// </summary>
    private async Task ListAllBluetoothDevicesAsync()
    {
        try
        {
            var selector = BluetoothLEDevice.GetDeviceSelector();
            var allDevices = await DeviceInformation.FindAllAsync(selector);

            if (allDevices.Count == 0)
            {
                Console.WriteLine("沒有找到任何藍牙 LE 裝置");
                return;
            }

            Console.WriteLine($"找到 {allDevices.Count} 個藍牙 LE 裝置：");
            foreach (var device in allDevices)
            {
                Console.WriteLine($"  - 名稱: {device.Name ?? "(未命名)"}");
                Console.WriteLine($"    ID: {device.Id}");
                Console.WriteLine($"    已配對: {device.Pairing.IsPaired}");
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"列出裝置時發生錯誤：{ex.Message}");
        }
    }

    /// <summary>
    /// 中斷連接並清理資源
    /// </summary>
    public void Dispose()
    {
        Console.WriteLine("正在清理連接資源...");

        // 取消訂閱通知事件
        if (_txCharacteristic != null)
        {
            _txCharacteristic.ValueChanged -= OnNotificationReceived;
            Console.WriteLine("✓ 已取消訂閱 TX 通知");
        }

        // 取消訂閱連接狀態變更事件
        if (_device != null)
        {
            _device.ConnectionStatusChanged -= OnConnectionStatusChanged;
            Console.WriteLine("✓ 已取消訂閱連接狀態事件");
        }

        // 清理 GATT Session（重要：釋放連接）
        if (_gattSession != null)
        {
            _gattSession.MaintainConnection = false;
            _gattSession.Dispose();
            _gattSession = null;
            Console.WriteLine("✓ GATT Session 已釋放");
        }

        // 清理裝置物件
        _device?.Dispose();
        _device = null;

        // 清理 Characteristics
        _rxCharacteristic = null;
        _txCharacteristic = null;

        // 清理連接等待任務
        _connectionTcs = null;

        Console.WriteLine("✓ 已中斷連接並清理所有資源");
    }
}
