using System;

namespace AkgController;

/// <summary>
/// Airoha RACE 指令建構器
/// 基於反編譯的 Android APP (com/airoha/libbase/RaceCommand/packet/RacePacket.java)
/// </summary>
public static class RaceCommand
{
    // RACE 封包常數
    private const byte HEADER = 0x05;              // START_CHANNEL_BYTE
    private const byte TYPE_WITH_RESPONSE = 0x5A;  // 需要回應 (90)
    private const byte TYPE_RESPONSE = 0x5B;       // 回應封包 (91)

    // Race IDs (Little-Endian)
    private const ushort RACE_ID_ANC_CONTROL = 0x0E06;  // 3590 - ANC 控制
    private const ushort RACE_ID_GET_ANC_STATUS = 0x0901;  // 2305 - 取得 ANC 狀態

    // ANC 指令碼
    private const byte CMD_ANC_ON = 0x0A;   // ANC 開啟
    private const byte CMD_ANC_OFF = 0x0B;  // ANC 關閉
    private const byte CMD_SET_LEVEL = 0x1E;  // 設定強度

    /// <summary>
    /// ANC 模式 (Filter 值)
    /// 來源：SDKAncSettings.java
    /// </summary>
    public enum AncMode : byte
    {
        Off = 0,              // 關閉
        Anc1 = 1,             // ANC 模式 1（標準降噪）
        Anc2 = 2,             // ANC 模式 2
        Anc3 = 3,             // ANC 模式 3
        Anc4 = 4,             // ANC 模式 4
        PassThrough1 = 9,     // 環境音模式 1
        PassThrough2 = 10,    // 環境音模式 2
        PassThrough3 = 11     // 環境音模式 3
    }

    /// <summary>
    /// 建立 ANC 開啟指令
    /// </summary>
    /// <param name="mode">ANC 模式</param>
    /// <returns>RACE 指令 byte array</returns>
    public static byte[] CreateAncOnCommand(AncMode mode = AncMode.Anc1)
    {
        // Payload: [0x00, 0x0A, filter]
        byte[] payload = { 0x00, CMD_ANC_ON, (byte)mode };
        return BuildRacePacket(RACE_ID_ANC_CONTROL, payload);
    }

    /// <summary>
    /// 建立 ANC 關閉指令
    /// </summary>
    /// <returns>RACE 指令 byte array</returns>
    public static byte[] CreateAncOffCommand()
    {
        // Payload: [0x00, 0x0B]
        byte[] payload = { 0x00, CMD_ANC_OFF };
        return BuildRacePacket(RACE_ID_ANC_CONTROL, payload);
    }

    /// <summary>
    /// 建立環境音模式指令
    /// </summary>
    /// <param name="mode">環境音模式 (PassThrough1/2/3)</param>
    /// <returns>RACE 指令 byte array</returns>
    public static byte[] CreatePassThroughCommand(AncMode mode = AncMode.PassThrough1)
    {
        if (mode < AncMode.PassThrough1)
        {
            throw new ArgumentException("模式必須是 PassThrough1/2/3", nameof(mode));
        }
        return CreateAncOnCommand(mode);
    }

    /// <summary>
    /// 建立取得 ANC 狀態指令
    /// </summary>
    /// <returns>RACE 指令 byte array</returns>
    public static byte[] CreateGetAncStatusCommand()
    {
        byte[] payload = { 0x00 };
        return BuildRacePacket(RACE_ID_GET_ANC_STATUS, payload);
    }

    /// <summary>
    /// 建構 RACE 封包
    /// 格式：[Header] [Type] [Length_L] [Length_H] [RaceID_L] [RaceID_H] [Payload...]
    /// </summary>
    /// <param name="raceId">Race ID (little-endian)</param>
    /// <param name="payload">Payload bytes</param>
    /// <returns>完整的 RACE 封包</returns>
    private static byte[] BuildRacePacket(ushort raceId, byte[] payload)
    {
        // Length = Race ID (2 bytes) + Payload length
        ushort length = (ushort)(2 + payload.Length);

        // 建立封包
        byte[] packet = new byte[4 + length];  // Header(1) + Type(1) + Length(2) + (RaceID + Payload)

        packet[0] = HEADER;
        packet[1] = TYPE_WITH_RESPONSE;

        // Length (Little-Endian)
        packet[2] = (byte)(length & 0xFF);        // Low byte
        packet[3] = (byte)((length >> 8) & 0xFF); // High byte

        // Race ID (Little-Endian)
        packet[4] = (byte)(raceId & 0xFF);        // Low byte
        packet[5] = (byte)((raceId >> 8) & 0xFF); // High byte

        // Payload
        Array.Copy(payload, 0, packet, 6, payload.Length);

        return packet;
    }

    /// <summary>
    /// 將 byte array 轉換為 hex 字串（除錯用）
    /// </summary>
    public static string ToHexString(byte[] bytes)
    {
        return BitConverter.ToString(bytes).Replace("-", " ");
    }

    /// <summary>
    /// 檢查回應封包是否成功
    /// </summary>
    /// <param name="response">回應封包</param>
    /// <returns>是否成功</returns>
    public static bool IsResponseSuccess(byte[] response)
    {
        if (response == null || response.Length < 8)
            return false;

        // 檢查 Type = 0x5B (回應)
        if (response[1] != TYPE_RESPONSE)
            return false;

        // 檢查 Status byte（位置可能因封包而異）
        // 通常在 Payload 的某個位置為 0x00 表示成功
        // 這裡簡化處理，只要收到回應就視為成功
        return true;
    }
}
