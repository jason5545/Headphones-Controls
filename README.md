# AKG N9 Hybrid 降噪控制程式

**Windows 圖形化/命令列工具 - 透過藍牙控制 AKG N9 Hybrid 耳機的主動降噪功能**

---

## 專案簡介

這是一個透過分析 AKG Headphones Android App 藍牙通訊協定開發的 Windows 工具，可以直接控制 AKG N9 Hybrid 耳機的降噪功能，提供圖形化介面和命令列兩種操作方式。

### 技術特點

- ✅ 完整實作 Airoha RACE 協定
- ✅ 直接透過 BLE GATT 通訊
- ✅ 支援所有 ANC 模式和環境音模式
- ✅ 提供 GUI 圖形介面和 CLI 命令列雙模式
- ✅ 獨立執行檔，無需 .NET Runtime
- ✅ 開源且易於擴充

---

## 系統需求

- **作業系統**：Windows 10/11（需支援藍牙 LE）
- **耳機型號**：AKG N9 Hybrid
- **藍牙版本**：Bluetooth 4.0 或更新

---

## 快速開始

### 1. 下載程式

前往 [Releases](https://github.com/yourusername/Headphones-Controls/releases) 下載最新版本的 `AkgController.exe`。

### 2. 配對耳機

在執行程式前，請先在 Windows 藍牙設定中配對您的 AKG N9 Hybrid 耳機：

1. 開啟 Windows 設定 → 藍牙與裝置
2. 開啟耳機並進入配對模式
3. 在 Windows 中新增裝置並配對

### 3. 使用程式

#### GUI 模式（圖形介面）

直接雙擊執行 `AkgController.exe` 即可開啟圖形化介面，透過按鈕控制耳機。

#### CLI 模式（命令列）

開啟命令提示字元（CMD）或 PowerShell，使用參數執行：

```cmd
# 開啟降噪
AkgController.exe on

# 關閉降噪
AkgController.exe off

# 環境音模式
AkgController.exe passthrough
```

---

## 指令說明

### 基本指令

| 指令 | 功能 | 說明 |
|------|------|------|
| `on` | 開啟降噪 | 啟用 ANC 模式 1（標準降噪）|
| `off` | 關閉降噪 | 完全關閉主動降噪功能 |
| `toggle` | 切換狀態 | 切換降噪開關（目前為開啟 ANC1）|
| `passthrough` | 環境音模式 | 啟用環境音模式 1 |

### 進階指令

| 指令 | 功能 |
|------|------|
| `anc1` | ANC 模式 1（標準降噪）|
| `anc2` | ANC 模式 2 |
| `passthrough1` | 環境音模式 1 |
| `passthrough2` | 環境音模式 2 |

### 使用範例

```cmd
# 上班通勤時開啟降噪
AkgController.exe on

# 需要聽周圍聲音時切換到環境音
AkgController.exe passthrough

# 回家後關閉降噪
AkgController.exe off
```

---

## 進階用法

### 建立快捷鍵

您可以透過 Windows 快捷方式設定全域熱鍵：

1. 對 `AkgController.exe` 建立捷徑
2. 在捷徑屬性中設定「目標」：
   ```
   "C:\Path\To\AkgController.exe" on
   ```
3. 設定「快速鍵」（如 Ctrl+Alt+A）

### 整合到 AutoHotkey

```autohotkey
; Ctrl+Alt+A: 開啟降噪
^!a::
    Run, C:\Path\To\AkgController.exe on
return

; Ctrl+Alt+O: 關閉降噪
^!o::
    Run, C:\Path\To\AkgController.exe off
return

; Ctrl+Alt+P: 環境音模式
^!p::
    Run, C:\Path\To\AkgController.exe passthrough
return
```

---

## 從原始碼建置

### 需求

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10 SDK (10.0.22621.0 或更新)

### 建置步驟

```cmd
# 複製專案
cd AkgController

# 建置 Release 版本（單一執行檔）
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# 輸出位置
# .\bin\Release\net8.0-windows10.0.22621.0\win-x64\publish\AkgController.exe
```

---

## 技術細節

### BLE UUID

| 用途 | UUID |
|------|------|
| Airoha GATT Service | `5052494D-2DAB-0341-6972-6F6861424C45` |
| RX Characteristic（寫入）| `43484152-2DAB-3141-6972-6F6861424C45` |
| TX Characteristic（通知）| `43484152-2DAB-3241-6972-6F6861424C45` |

### RACE 指令格式

```
[0] = 0x05 (Header)
[1] = 0x5A (Type: 需要回應)
[2-3] = Length (Little-Endian)
[4-5] = Race ID (0x0E06, Little-Endian)
[6...] = Payload

範例（開啟 ANC）:
05 5A 05 00 06 0E 00 0A 01
```

---

## 疑難排解

### 找不到裝置

**問題**：執行時顯示「找不到 AKG N9 Hybrid」

**解決方案**：
1. 確認耳機已在 Windows 藍牙設定中配對
2. 確認耳機已開啟且在範圍內
3. 嘗試移除並重新配對耳機
4. 檢查耳機名稱是否為 "AKG N9 Hybrid"（某些區域可能不同）

### 無法連接 GATT 服務

**問題**：顯示「找不到 Airoha GATT 服務」

**解決方案**：
1. 重新啟動耳機
2. 在 Windows 藍牙設定中「移除裝置」後重新配對
3. 確認 Windows 藍牙驅動程式已更新
4. 嘗試在 Windows 設定中「連接」耳機後再執行程式

### 指令發送失敗

**問題**：顯示「發送失敗」或「指令已發送」但耳機沒反應

**解決方案**：
1. 檢查耳機是否正在與手機連接（請中斷手機連接）
2. 確認耳機韌體版本（過舊或過新可能不相容）
3. 執行時查看輸出的 HEX 指令是否正確
4. 嘗試重新啟動電腦藍牙服務

---

## 專案結構

```
Headphones-Controls/
├── AkgController/              # C# 專案
│   ├── App.xaml                # WPF 應用程式進入點
│   ├── App.xaml.cs             # 應用程式邏輯
│   ├── MainWindow.xaml         # GUI 主視窗介面
│   ├── MainWindow.xaml.cs      # GUI 視窗邏輯
│   ├── CliProgram.cs           # CLI 命令列程式
│   ├── AkgN9Controller.cs      # BLE 控制器
│   ├── RaceCommand.cs          # RACE 指令建構器
│   └── AkgController.csproj    # 專案檔
└── README.md                   # 本檔案
```

---

## 授權與免責聲明

### 授權

本專案採用 MIT 授權條款，詳見 [LICENSE](LICENSE) 檔案。

### 免責聲明

- 本專案純粹用於教育和個人使用目的
- 協定分析結果僅用於相容性和互通性研究
- 使用本程式造成的任何損壞或保固失效，開發者不負任何責任
- AKG 和 Harman 是其各自所有者的註冊商標

---

## 貢獻

歡迎提交 Issue 和 Pull Request！

如果您有其他 AKG 耳機型號並希望新增支援，請提供：
1. 耳機型號
2. 執行 `AkgController.exe` 的錯誤訊息
3. 如可能，提供 HCI snoop log

---

## 致謝

- 感謝 [JADX](https://github.com/skylot/jadx) 提供優秀的開發工具
- 感謝 Airoha（MediaTek）和 Harman 開發的優秀音訊技術
- 感謝開源技術社群的知識分享

---

## 作者

由 AI 助手（Claude）協助協定分析和開發

**版本**：1.1.0（新增 GUI 介面）
**最後更新**：2025-11-06
