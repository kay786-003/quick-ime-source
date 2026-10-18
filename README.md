# 速成輸入法 (Quick IME)

一個**免安裝、免管理員權限、單一執行檔**的速成（Quick / Cangjie 首尾碼）輸入法，專為**簡化版 / 精簡版 Windows**（網吧機、LTSC、被移除輸入法框架的系統）而做。

不需要安裝任何輸入法框架，雙擊即用。

```
QuickIME.exe  (82 KB，字典已內嵌)
```

---

## ⬇️ 直接下載（唔使自己 build）

| 下載 | 檔案 | 說明 |
|------|------|------|
| **[QuickIME.exe](https://github.com/kay786-003/quick-ime-source/raw/main/releases/QuickIME.exe)** | 82 KB | 單一執行檔，雙擊即用（字典已內嵌） |
| **[QuickIME-v1.0.zip](https://github.com/kay786-003/quick-ime-source/raw/main/releases/QuickIME-v1.0.zip)** | 65 KB | exe + 使用說明 + 示意圖 |

> 也可以到 **[Releases](../../releases)** 頁面下載，那裡有標記版本號的正式版本。

### 網吧環境下載建議

因為 GitHub 在中國大陸有時連線不穩，如果 `raw.githubusercontent.com` 很慢：

1. **用 Releases 頁面** —— `github.com` 的 release 下載通常比 raw 穩定
2. **用鏡像加速**，例如在連結前加上 `https://ghproxy.net/`：
   ```
   https://ghproxy.net/https://github.com/kay786-003/quick-ime-source/raw/main/releases/QuickIME.exe
   ```
3. **先下載到 USB 手指**，之後去網吧直接插手指用，不必每次重下載

### 為什麼放現成 exe 在 repo 裡

因為**目標使用者正是在網吧、用著一台裝不到輸入法的電腦**。
他們需要的是「下載 → 雙擊 → 立刻打字」，而不是先裝 Git、裝 .NET SDK、跑建置腳本。

所以本專案把編譯好的 `QuickIME.exe` 直接提交在 [`releases/`](releases/) 資料夾，
用 GitHub 的 raw 連結就能下載，不需要 clone、不需要 build。

> `releases/` 裡的 exe 由 `build.ps1` 自動同步，
> GitHub Actions 也會在非 tag 推送時自動更新它，確保與原始碼一致。

### 檔案校驗

```
SHA256 (QuickIME.exe) = B9A49041CB66BECD0D229653F467AA1A51AA43C21A3128DE110DB547011567B0
```

若你下載後想確認檔案未被篡改，可在 PowerShell 執行：

```powershell
Get-FileHash .\QuickIME.exe -Algorithm SHA256
```

---

## 為什麼要做這個專案

**起因很簡單：在中國大陸的網吧電腦上，裝不到速成輸入法。**

網吧的機器通常是網管統一部署的精簡版 / 网维大师 / 無盤系統 Windows，
這些系統為了縮小體積、加快還原、方便管理，往往把整塊輸入法相關元件都砍掉：

- 「設定 → 語言」裡**沒有「新增輸入法」**，或整個語言設定被閹掉
- **微軟速成根本叫不出來**，連「速成」這個選項都不存在
- **小狼毫 (Rime) 裝不上** —— 安裝程式需要寫 `HKLM`、註冊 TSF、註冊 DLL，
  在網吧的權限與還原卡環境下必然失敗
- 就算找到 portable 版，也常因缺少 TSF/CTF 元件而**跑了但打不出字**

而對於習慣速成的人來說，這是致命的：英文鍵盤打不出中文，
但換成拼音又完全不順手 —— 速成的「頭尾兩碼」是**肌肉記憶**，
不是換個輸入法就能適應的東西。

我自己就是這樣，試過各種方法都行不通，
**最後決定自己寫一個**。

所以這個專案的目標很明確：

> **不依賴任何 Windows 輸入法框架，在任何一台 Windows 上雙擊就能打速成。**

---

## 為什麼現有方案都不行

| 方案 | 需要什麼 | 網吧／精簡版能用嗎 |
|------|----------|--------------------|
| 微軟速成（內建） | 系統 IME 框架 + `HKLM` 註冊的 TIP | ❌ 常被整個移除 |
| 微軟速成（重裝） | 管理員權限 + TSF/CTF 元件完整 | ❌ 網吧不給、還原卡會蓋掉 |
| Rime 小狼毫 | 安裝程式 + 多個 DLL + 寫註冊表 | ❌ 安裝即失敗 |
| Rime portable | 仍需要 TSF/CTF 存在 | ❌ 元件缺失時打不出字 |
| **本專案** | **.NET Framework 4.x（Windows 內建）** | ✅ |

關鍵差別：本專案**完全繞開輸入法框架**。

作法是：**Win32 低階鍵盤掛鉤攔鍵 → 自行組字 → `SendInput` 送出 Unicode 字元**。

- 不寫註冊表
- 不裝驅動
- 不改系統設定
- 不經剪貼簿（不會污染你的剪貼簿）
- 單一 `.exe`，可放 USB 帶著走

---

## 功能

- ✅ 速成（首碼 + 尾碼）查字，**21,701 字**，含香港增補字符集
- ✅ 單碼輸入（`a` → 日、`o` → 人）
- ✅ 候選字視窗，跟隨文字插入點
- ✅ **空格 = 下一頁**（微軟速成慣例）
- ✅ 數字鍵 1–9 選字
- ✅ 連打（打滿兩碼再打字母會自動上屏首選）
- ✅ Shift 單按切換中／英
- ✅ 系統匣圖示，可暫停／結束
- ✅ 免安裝、可放 USB 帶著走

### 候選字視窗

![候選字視窗](docs/screenshot.png)

---

## 使用方式

1. 下載 `QuickIME.exe`
2. 雙擊執行 → 右下角時區旁邊出現藍色「**速**」字圖示
3. 在任何程式直接打字

| 按鍵 | 功能 |
|------|------|
| `空白鍵` | 下一頁（最後一頁再按 = 選第 1 個字） |
| `1`–`9` | 選第 1–9 個候選字 |
| `PageUp` / `-` | 上一頁 |
| `PageDown` / `+` | 下一頁 |
| `退格鍵` | 刪一個輸入碼 |
| `Esc` | 取消本次輸入 |
| `Shift`（單按） | 切換中／英 |

### 速成規則

速成 = 倉頡取「**頭碼**」＋「**尾碼**」兩個字根。

| 鍵 | 字根 | 鍵 | 字根 | 鍵 | 字根 |
|----|------|----|------|----|------|
| a | 日 | b | 月 | c | 金 |
| d | 木 | e | 水 | f | 火 |
| g | 土 | h | 竹 | i | 戈 |
| j | 十 | k | 大 | l | 中 |
| m | 一 | n | 弓 | o | 人 |
| p | 心 | q | 手 | r | 口 |
| s | 尸 | t | 廿 | u | 山 |
| v | 女 | w | 田 | x | 難 |
| y | 卜 | | | | |

| 打 | 出 | 說明 |
|----|----|------|
| `a` | 日 | 單碼 |
| `ab` | 明 | 日 + 月 |
| `of` | 伙 **你** 係 … | 「你」是第 2 個候選 |
| `vd` | **好** 她 妹 … | |
| `ha` | 白 昏 徇 **香** … | 「香」是第 4 個 |
| `eu` | 氾 汕 沈 沉 … **港** | 「港」是第 16 個 |

---

## 字序說明（重要）

候選字次序**完全依照 Windows 內建速成的排位**（原表 `sort: original`），
**沒有做任何字頻重排**。

原因：速成使用者靠記憶「第幾個位」打字，一旦改動次序就會打錯字。

> 這是刻意的設計決策。若你 fork 本專案，**請勿**加入字頻排序。

---

## 建置

只需要 Windows + .NET Framework 4.x（Windows 10/11 內建，無需安裝 SDK）。

```powershell
# 完整建置（重建字典 + 編譯 + 測試）
powershell -ExecutionPolicy Bypass -File .\build.ps1 -RunTests

# 只編譯（用現成字典）
powershell -ExecutionPolicy Bypass -File .\build.ps1 -SkipDict -RunTests
```

產出：

```
dist\QuickIME.exe    # 單一執行檔（字典內嵌）
dist\qdict.bin       # 字典（可選，方便自行替換）
```

### 專案結構

```
.
├── build.ps1                 # 一鍵建置
├── releases/                 # ⭐ 現成 exe（提交進 repo，方便直接下載）
│   ├── QuickIME.exe
│   ├── 速成輸入法_v1.0.zip
│   ├── 使用說明.txt
│   └── 候選字視窗示意圖.png
├── src/
│   ├── Dict.cs               # 字典引擎（702 bucket 索引）
│   ├── Composer.cs           # 組字狀態機（與 UI/掛鉤解耦，可單測）
│   ├── Keyboard.cs           # Win32 低階鍵盤掛鉤 + SendInput
│   ├── CandidateForm.cs      # 候選字視窗（WS_EX_NOACTIVATE）
│   ├── Program.cs            # 主程式 / 系統匣 / 說明
│   ├── SelfTest.cs           # 25 項自我測試
│   └── UiShot.cs             # 候選窗截圖（離線檢查 UI）
├── tools/
│   ├── gen-dict.ps1          # 由 yaml 產生 qdict.bin
│   └── package-repo.ps1      # 打包成可上傳 GitHub 的 zip
├── data/
│   └── ms_quick.dict.yaml    # 字表來源
└── docs/
    ├── screenshot.png
    ├── DEVELOPMENT.md
    └── 使用說明.txt
```

> `build.ps1` 建置完成後，會自動把 `QuickIME.exe` 同步到 `releases/`，
> 所以發新版本時只要重新跑一次建置，再 commit 即可。

### 執行測試

```powershell
.\dist\QuickIME.exe --selftest          # 25 項自我測試
.\dist\QuickIME.exe --uishop shot.png   # 產生候選窗截圖
.\dist\QuickIME.exe --help
```

---

## 字典格式

`qdict.bin`（小端序）：

| 偏移 | 型別 | 內容 |
|------|------|------|
| 0 | `int32` | magic `0x514B494D` (`'QKIM'`) |
| 4 | `int32` | 版本 = `2` |
| 8 | `int32` | bucket 數 = `702` |
| 12 | `int32` | 總字數 |
| 16 | `int32[702]` | 每個 bucket 的字數 |
| … | `uint16[]` | 字元（UTF-16 code unit） |

Bucket 索引：

```
0..25   → 單碼 'a'..'z'
26..701 → 兩碼 (c1-'a')*26 + (c2-'a')
```

---

## 適合誰用

- ✅ **在網吧／公司／學校電腦上，只有英文鍵盤但想打中文的人**
- ✅ 用慣**速成**，換拼音會手指打結的人
- ✅ 系統是精簡版 / LTSC / 無盤系統，**裝不到微軟速成或小狼毫**
- ✅ 需要**放 USB 帶著走**、用完不留痕跡的人
- ✅ 想學速成，需要一個不干擾系統的練習工具

**不適合**：需要雲端詞庫、聯想詞、簡繁轉換、個人化學習的使用者
（本專案刻意保持精簡，只做「速成查字」這一件事。）

---

## 已知限制

| 情況 | 說明 |
|------|------|
| 管理員權限程式 | 若目標程式以管理員身分執行，本程式也需以管理員身分執行（UIPI 限制） |
| DirectX 獨佔全螢幕 | 可能攔不到鍵盤，請改用視窗／無邊框模式 |
| 防毒軟體 | 因使用鍵盤掛鉤，可能出現提示，需允許 |
| 僅 Windows | 依賴 Win32 API |
| 網吧還原卡 | 關機後檔案會被還原，請放 USB 或雲端硬碟 |

---

## 授權與致謝

本專案的**程式碼**以 [MIT License](LICENSE) 釋出。

**字典資料並非本專案原創**，來自以下專案，需遵守其授權：

| 來源 | 內容 | 授權 |
|------|------|------|
| [philipposkhos/rime-ms-quick](https://github.com/philipposkhos/rime-ms-quick) | `ms_quick.dict.yaml`（Windows 10 速成排位） | 見上游 |
| [rime/rime-quick](https://github.com/rime/rime-quick) | 速成方案概念 | LGPL-3.0 |
| 《五倉世紀》[chinesecj.com](http://www.chinesecj.com) | 單字碼表（經 rime-cangjie） | GPL |
| [rime/rime-cangjie](https://github.com/rime/rime-cangjie) | 倉頡碼表整理（佛振修訂） | GPL |

> ⚠️ **重要**：因為字典源自 GPL 授權的碼表，若你要**連同字典一起散布**本專案，
> 整體應以 **GPL-3.0** 相容方式釋出。
> 若只散布純程式碼（不含 `qdict.bin`、不含 `data/ms_quick.dict.yaml`），則可用 MIT。
>
> 建議：本專案整體採用 **GPL-3.0** 最安全。詳見 [LICENSE](LICENSE) 與 [NOTICE](NOTICE)。

---

## 致謝

- 鍵盤掛鉤與送入架構參考 Win32 / MSDN 標準做法
- 字表整理：Kho (rime-ms-quick)、佛振 (rime-cangjie)
