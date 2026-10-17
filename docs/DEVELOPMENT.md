# 開發筆記

## 緣起

這個專案的起點不是「想做一個輸入法」，而是**打不到字**。

在中國大陸的網吧電腦上，系統多為網管統一部署的精簡版 / 無盤 Windows。
這些系統為了縮小體積與加快還原，常把整塊輸入法元件砍掉：

- 「設定 → 語言」沒有「新增輸入法」，或語言設定被完全移除
- 微軟速成叫不出來，連選項都不存在
- 小狼毫 (Rime) 安裝程式需要寫 `HKLM`、註冊 TSF、註冊 DLL → 必然失敗
- 找到 portable 版，也常因 TSF/CTF 元件缺失而「跑了但打不出字」

對習慣速成的人來說這是死局：英文鍵盤打不出中文，換拼音又不順手。
**速成的頭尾兩碼是肌肉記憶，不是換個輸入法就能重新適應的。**

試過各種現成方案都行不通之後，結論只有一個：
**不要跟輸入法框架搏鬥，直接繞開它。**

於是有了這個專案 —— 用鍵盤掛鉤自己組字，完全不碰 TSF/CTF。

> 這個背景很重要：它解釋了為什麼本專案**刻意不做**那些「正規」的事
> （不註冊 TIP、不裝驅動、不寫註冊表）。
> 那不是偷懶，而是唯一能在受限制環境中存活的做法。

---

## 架構

```
使用者按鍵
   │
   ▼
KeyboardHook (WH_KEYBOARD_LL)      ← 低階鍵盤掛鉤，全域攔鍵
   │  a-z / 數字 / 空白 / 翻頁 / 退格 / Esc
   ▼
Composer                            ← 純狀態機（無 UI、無 Win32 依賴）
   │  查字典 → 候選清單 → 選字
   ▼
Dict                                ← 702 bucket 索引，O(1) 查詢
   │  qdict.bin（內嵌或外部）
   ▼
CandidateForm                       ← 候選字視窗（WS_EX_NOACTIVATE）
   │
   ▼
Typist.SendInput (KEYEVENTF_UNICODE) ← 送出中文字
```

### 為什麼要這樣分層

`Composer` 完全不碰 UI 也不碰 Win32，所以可以用 `--selftest` 在無視窗環境下
跑 25 項測試。這是這個專案最重要的設計決策 —— 輸入法的邏輯錯誤（例如翻頁
off-by-one、連打吞字）光靠人手測試很難覆蓋。

---

## 三個踩過的坑（留給後人）

### 1. winexe 會因為 `Console.OutputEncoding` 崩潰

```csharp
// ❌ 在 /target:winexe 下，沒有主控台時會拋 IOException
Console.OutputEncoding = Encoding.UTF8;
```

症狀：程式啟動立即彈出錯誤框
`應用程式發生異常 未知的軟體異常 (0xe0434352)`。

`0xe0434352` 是 CLR 例外代碼（`0xE0434352` = `.NET` 的 COM 例外）。
修法：只在 `args.Length > 0`（有主控台）時才設定，並且包 try/catch。

### 2. 鍵盤掛鉤會攔到自己送出的字 → 無限回圈

`SendInput` 送出的按鍵**也會經過自己的 WH_KEYBOARD_LL 掛鉤**。
若不處理，送出的中文字會被當成新的輸入碼再處理一次。

修法：注入時在 `dwExtraInfo` 塞一個 magic 標記，掛鉤看到就放行。

```csharp
private static readonly IntPtr Magic = (IntPtr)0x514B494D;  // 'QKIM'

// 送出的時候
ki.dwExtraInfo = Magic;

// 掛鉤開頭
if (info.dwExtraInfo == Typist.Magic)
    return CallNextHookEx(...);   // 直接放行
```

> 注意：不要用「猜測是不是自己送的」這類 heuristic，`dwExtraInfo` 是唯一可靠的方式。

### 3. Shift 單按切換時吞掉 keyup → Shift 卡住

一開始在 keyup 時 `return (IntPtr)1`（吃掉事件）來避免系統切換輸入法，
結果造成 `Shift+字母` 打大寫時，系統收不到 Shift 放開，Shift 狀態卡住。

修法：**一定要放行 keyup**，只在內部切換自己的模式。

---

## 字典為什麼用 702 個 bucket

速成只有 1–2 碼，字母 a–z：

- 單碼：26 種
- 兩碼：26 × 26 = 676 種
- 合計：702 種

所以用一個 `string[702]` 就能 O(1) 查到所有候選，不需要 hash table 或排序。

索引計算：

```csharp
// 單碼
idx = c0 - 'a';                        // 0..25

// 兩碼
idx = 26 + (c0 - 'a') * 26 + (c1 - 'a');  // 26..701
```

---

## ⚠️ 不要加字頻排序

這是**刻意的產品決策**，不是沒做完。

速成使用者靠**肌肉記憶**打「第幾個位」。例如：

```
hi → 凡 么 丟 夙 舟 卵 我 私 的 待 禹 風
                            ↑        ↑
                         第7個「我」 第9個「的」
```

如果把「常用字」排到前面，`hi` 第 7 位會變成別的字 ——
使用者會突然一直打錯字，而且是**習慣被破壞**那種錯，非常惱人。

候選次序必須與 Windows 內建速成一致。

> 開發過程中曾試過用字頻表重排，被使用者明確否決，已全部移除。

---

## 測試

```powershell
# 25 項自我測試（字典完整性 + 組字流程 + 翻頁 + 連打 + 邊界）
.\dist\QuickIME.exe --selftest

# 候選窗截圖（不需要真人操作，方便檢查 UI 改動）
.\dist\QuickIME.exe --uishop shot.png
```

`SelfTest.cs` 涵蓋：

| 類別 | 項目 |
|------|------|
| 字典 | 25 個字根有字、`z` 無字、總字數 21701、已知碼驗證 |
| 基本 | 輸入、組字碼、首選、上屏、重置 |
| 空格 | 多頁翻頁、最後一頁上屏、單頁直接上屏 |
| 選字 | 數字鍵選第 N 個 |
| 翻頁 | PageMove 邊界、頁碼 |
| 連打 | 滿兩碼自動上屏並開新碼 |
| 邊界 | 退格、Esc、空碼查詢、不存在的碼 |

---

## 除錯

```powershell
.\dist\QuickIME.exe --diag
```

會在 exe 同目錄（或 `%APPDATA%`）寫 `QuickIME.log`，記錄：

```
hook: vk=73 composing=False cn=True      ← 收到按鍵
hook: injected vk=231 -> pass            ← 送出中文字（vk=231 是 Unicode 注入）
```

`--diag` 只影響日誌，不影響功能。正常使用時不寫日誌。

---

## 已知限制

| 限制 | 原因 | 可否解決 |
|------|------|----------|
| 管理員程式攔不到 | UIPI（使用者介面權限隔離） | 本程式也用管理員執行 |
| DirectX 獨佔全螢幕攔不到 | 該模式繞過一般訊息迴圈 | 改用視窗／無邊框 |
| 只能 Windows | 依賴 Win32 API | 需另寫平台層 |

### 為什麼不用 TSF

正規做法是寫 TSF TIP（`ITfTextInputProcessor` 等），但要：

1. 原生 DLL 匯出 `DllGetClassObject`
2. 註冊 `HKLM\SOFTWARE\Microsoft\CTF\TIP`
3. 依賴系統 CTF 元件完整

精簡版 Windows 常常缺這些元件，或封鎖安裝 —— 這正是使用者遇到的問題。
所以改用掛鉤方案，繞開整個框架。
