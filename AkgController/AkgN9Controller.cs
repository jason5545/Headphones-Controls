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

    private BluetoothLEDevice? _device;
    private GattCharacteristic? _rxCharacteristic;  // 寫入用
    private GattCharacteristic? _txCharacteristic;  // 通知用

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
                return false;
            }

            Console.WriteLine($"找到 {devices.Count} 個裝置，嘗試連接第一個...");

            // 連接第一個找到的裝置
            _device = await BluetoothLEDevice.FromIdAsync(devices[0].Id);

            if (_device == null)
            {
                Console.WriteLine("無法建立裝置連接。");
                return false;
            }

            Console.WriteLine($"已連接到 {_device.Name} ({_device.BluetoothAddress:X})");

            // 取得 GATT 服務
            var servicesResult = await _device.GetGattServicesForUuidAsync(ServiceUuid);

            if (servicesResult.Status != GattCommunicationStatus.Success || servicesResult.Services.Count == 0)
            {
                Console.WriteLine($"找不到 Airoha GATT 服務 (UUID: {ServiceUuid})");
                return false;
            }

            var service = servicesResult.Services[0];
            Console.WriteLine($"已找到 Airoha GATT 服務");

            // 取得 RX Characteristic (寫入用)
            var rxResult = await service.GetCharacteristicsForUuidAsync(RxCharacteristicUuid);
            if (rxResult.Status != GattCommunicationStatus.Success || rxResult.Characteristics.Count == 0)
            {
                Console.WriteLine($"找不到 RX Characteristic (UUID: {RxCharacteristicUuid})");
                return false;
            }
            _rxCharacteristic = rxResult.Characteristics[0];

            // 取得 TX Characteristic (通知用)
            var txResult = await service.GetCharacteristicsForUuidAsync(TxCharacteristicUuid);
            if (txResult.Status != GattCommunicationStatus.Success || txResult.Characteristics.Count == 0)
            {
                Console.WriteLine($"找不到 TX Characteristic (UUID: {TxCharacteristicUuid})");
                return false;
            }
            _txCharacteristic = txResult.Characteristics[0];

            // 啟用 TX Notification
            var cccdResult = await _txCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify);

            if (cccdResult != GattCommunicationStatus.Success)
            {
                Console.WriteLine("無法啟用通知");
                return false;
            }

            // 訂閱通知事件（可選）
            _txCharacteristic.ValueChanged += OnNotificationReceived;

            Console.WriteLine("BLE GATT 初始化完成，準備發送指令。");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"連接失敗：{ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 發送 RACE 指令
    /// </summary>
    private async Task<bool> SendRaceCommandAsync(byte[] command)
    {
        if (_rxCharacteristic == null)
        {
            Console.WriteLine("尚未連接到裝置");
            return false;
        }

        try
        {
            Console.WriteLine($"發送指令: {RaceCommand.ToHexString(command)}");

            var writer = new DataWriter();
            writer.WriteBytes(command);

            var result = await _rxCharacteristic.WriteValueAsync(writer.DetachBuffer());

            if (result == GattCommunicationStatus.Success)
            {
                Console.WriteLine("指令已發送");
                return true;
            }
            else
            {
                Console.WriteLine($"發送失敗：{result}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"發送指令時發生錯誤：{ex.Message}");
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

        Console.WriteLine($"收到通知: {RaceCommand.ToHexString(data)}");

        // 檢查是否為成功回應
        if (RaceCommand.IsResponseSuccess(data))
        {
            Console.WriteLine("✓ 指令執行成功");
        }
    }

    /// <summary>
    /// 中斷連接
    /// </summary>
    public void Dispose()
    {
        if (_txCharacteristic != null)
        {
            _txCharacteristic.ValueChanged -= OnNotificationReceived;
        }

        _device?.Dispose();
        _device = null;
        _rxCharacteristic = null;
        _txCharacteristic = null;

        Console.WriteLine("已中斷連接");
    }
}
