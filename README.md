# 速成輸入法 (Quick IME)

一個**免安裝、免管理員權限、單一執行檔**的速成（Quick / Cangjie 首尾碼）輸入法，專為**簡化版 / 精簡版 Windows**（網吧機、LTSC、被移除輸入法框架的系統）而做。

不需要安裝任何輸入法框架，雙擊即用。

```
QuickIME.exe  (127 KB，字典 + 聯想詞表已內嵌)
```

---

## ⬇️ 直接下載（唔使自己 build）

| 下載 | 檔案 | 說明 |
|------|------|------|
| **[QuickIME.exe](https://github.com/kay786-003/quick-ime-source/releases/download/v2.3/QuickIME.exe)** | 單一執行檔，雙擊即用（字典 + 聯想詞表已內嵌） |
| **[QuickIME-v2.3.zip](https://github.com/kay786-003/quick-ime-source/releases/download/v2.3/QuickIME-v2.3.zip)** | exe + 字典 + 使用說明 + 示意圖 |

> 也可以到 **[Releases](https://github.com/kay786-003/quick-ime-source/releases)** 頁面查看所有正式版本。

### 網吧環境下載建議

因為 GitHub 在中國大陸有時連線不穩，如果 `github.com` / `raw.githubusercontent.com` 很慢，
可以在網址前面加一個**鏡像前綴**。

> **用法**：把原本的 GitHub 網址**原封不動**貼在前綴後面，不用改任何字。
>
> 下面所有鏡像都在 **2026-10-19** 實測過：完整下載 `QuickIME.exe`（130,048 bytes）
> 與 `QuickIME-v2.3.zip`，SHA256 與 GitHub Release **完全一致**。

| 鏡像前綴 | 實測 | 備註 |
|----------|------|------|
| `https://gh-proxy.com/` | ✅ | 目前最穩 |
| `https://ghfast.top/` | ✅ | |
| `https://gh.llkk.cc/` | ✅ | |
| `https://gh-proxy.org/` | ✅ | |
| `https://gh.monlor.com/` | ✅ | |
| `https://ghproxy.imciel.com/` | ✅ | |
| `https://ghfile.geekertao.top/` | ✅ | |
| `https://gh.nxnow.top/` | ✅ | |
| `https://gh.sixyin.com/` | ✅ | |
| `https://cors.isteed.cc/` | ✅ | 通用 CORS 代理，非 GitHub 專用 |

⚠️ **`https://ghproxy.net/` 已失效**（整個站回 `502 Bad Gateway`，不只本專案），
舊版 README 曾推薦它，現已移除。

鏡像會隨時停站或限流，**一個不行就換下一個**；長期保存請直接下載到 USB 手指。

#### QuickIME.exe 直連（揀一個可以下載的，複製即可）

```
https://gh-proxy.com/https://github.com/kay786-003/quick-ime-source/releases/download/v2.3/QuickIME.exe
https://ghfast.top/https://github.com/kay786-003/quick-ime-source/releases/download/v2.3/QuickIME.exe
https://gh.llkk.cc/https://github.com/kay786-003/quick-ime-source/releases/download/v2.3/QuickIME.exe
https://gh-proxy.org/https://github.com/kay786-003/quick-ime-source/releases/download/v2.3/QuickIME.exe
https://gh.monlor.com/https://github.com/kay786-003/quick-ime-source/releases/download/v2.3/QuickIME.exe
https://ghproxy.imciel.com/https://github.com/kay786-003/quick-ime-source/releases/download/v2.3/QuickIME.exe
https://ghfile.geekertao.top/https://github.com/kay786-003/quick-ime-source/releases/download/v2.3/QuickIME.exe
https://gh.nxnow.top/https://github.com/kay786-003/quick-ime-source/releases/download/v2.3/QuickIME.exe
https://gh.sixyin.com/https://github.com/kay786-003/quick-ime-source/releases/download/v2.3/QuickIME.exe
https://cors.isteed.cc/https://github.com/kay786-003/quick-ime-source/releases/download/v2.3/QuickIME.exe
```

想下載 zip 版，把結尾的 `QuickIME.exe` 換成 `QuickIME-v2.3.zip` 就可以。

#### 也可以用 repo 內的備份 exe

`releases/QuickIME.exe` 已經提交在 repo 裡，不經 release asset：

```
https://raw.githubusercontent.com/kay786-003/quick-ime-source/main/releases/QuickIME.exe
```

同樣可以套用上面的鏡像前綴（例如
`https://gh-proxy.com/https://raw.githubusercontent.com/...`）。

#### 最後手段

1. **直接用 Releases 頁面** —— 見頁面頂部原連結，通常最穩
2. **先下載到 USB 手指**，之後去網吧直接插手指用，不必每次重下載

### 為什麼放現成 exe 在 repo 裡

因為**目標使用者正是在網吧、用著一台裝不到輸入法的電腦**。
他們需要的是「下載 → 雙擊 → 立刻打字」，而不是先裝 Git、裝 .NET SDK、跑建置腳本。

所以本專案把編譯好的 `QuickIME.exe` 直接提交在 [`releases/`](releases/) 資料夾，
用 GitHub 的 raw 連結就能下載，不需要 clone、不需要 build。

> `releases/` 裡的 exe 由 `build.ps1` 自動同步，
> GitHub Actions 也會在非 tag 推送時自動更新它，確保與原始碼一致。

### 檔案校驗

本專案 v2.3 的 Release 檔案 SHA256（**這是 Release 頁面實際檔案的值，可直接比對**）：

| 檔案 | 大小 | SHA256 |
|------|------|--------|
| `QuickIME.exe` | 134,144 bytes | `3c4bc23f1a4adf0a274fbb34e5210869f46ceace79d7ca1c7e2e7a886aeb925e` |
| `QuickIME-v2.3.zip` | 133,128 bytes | `d076cb5361b3f16227b77881d73e26153c9d7d0a7bea807c7adb5c990432b574` |

在 PowerShell 執行：

```powershell
Get-FileHash .\QuickIME.exe -Algorithm SHA256
.\QuickIME.exe --version      # 應顯示 2.3
.\QuickIME.exe --selftest     # 應顯示全部 148 項通過
```

> ⚠️ `releases/` 資料夾裡那份由 CI 自動重建，因為 .NET 每次會嵌入不同的 MVID，
> 所以它的 SHA256 **與 Release 頁面那份不同**，屬正常現象。
> 這種情況請改用 `--selftest` 驗證：能全部通過就代表字典與組字流程完好。

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
- ✅ **空格 = 下一頁**（微軟速成慣例；到尾頁再按會繞返第 1 頁，不會偷偷上屏）
- ✅ 數字鍵 1–9 選字
- ✅ 連打（打滿兩碼再打字母會自動上屏首選）
- ✅ **聯想字：打完一個字自動提示下一個字，會學習你的用字習慣**（見下節）
- ✅ **聯想字用 `Shift` + 數字鍵選字**（同 Windows 速成一樣；直接按 1–9 就打數字）
- ✅ **可設定左／右 Shift 單按切換中英**（見下節，打機長按 Shift 不會誤切換）
- ✅ **符號永遠半形**，可攔住 `Ctrl+空白`（不會誤轉全形）
- ✅ **系統匣圖示依模式變色**（藍＝中文／灰＝英文／深灰＝暫停）
- ✅ 系統匣圖示，可暫停／結束
- ✅ 免安裝、可放 USB 帶著走

---

## v2.1 更新重點：聯想字（會學習你）

打完一個中文字，自動列出「最可能接住落去」的字，按 **`Shift` + 數字鍵** 直接選：

```
聯想  你
 1   2   3   4   5   6   7   8   9
好  嗎  係  唔  的  有  會  想  了
```

> **v2.3 起跟足 Windows 速成：** 聯想字要**按住 `Shift`** 再按 `1`–`9` 才選得到；
> 直接按 `1`–`9` 就是打數字。這樣打完中文字想打「第3章」就不會被聯想窗吞掉。
> 想改回 v2.2 的直接數字選字：右鍵圖示 → **聯想字** → 取消勾選
> **「Shift + 數字鍵選聯想字」**。

### 同 Windows 內建聯想字的分別：**佢會學你**

Windows 的聯想字是固定的。本專案的聯想字會**記錄你的用字習慣**：

| 情況 | 結果 |
|------|------|
| 你打完「你」之後經常接「係」 | 打多幾次，「係」由第 3 位爬到第 1 位 |
| 之後改用「嗎」多啲 | 「嗎」又會爬到第一 |
| 你未用過的組合 | 保留出廠預設次序 |

排序規則：**學過的排前（次數越多越前），未學過的保留出廠次序。**

### 🆕 v2.2：記錄習慣可以隨時開關

聯想字和「記錄習慣」是**兩個獨立開關**：

| 聯想字 | 記錄用字習慣 | 效果 |
|--------|-------------|------|
| ✅ 開 | ✅ 開（預設） | 有聯想，而且越用越貼你的習慣 |
| ✅ 開 | ❌ 關 | **仍然有聯想，但只用出廠詞表**，不會再學新東西 |
| ❌ 關 | — | 完全不出聯想窗 |

右鍵系統匣 → **聯想字** → 勾選／取消 **「記錄用字習慣（常用的組合排更前）」**。

> 關掉學習**不會刪除**已學到的記憶 —— 之前學的仍然照舊排序，只是不再新增。
> 想真正清走就用「清空聯想字記憶」。
> 聯想字關掉時，「記錄習慣」和「清空」會變灰（因為已經沒有意義）。

### 聯想窗操作

| 按鍵 | 功能 |
|------|------|
| `Shift` + `1`–`9` | 選聯想字（選完繼續提示下一個字） |
| `1`–`9`（不按 Shift） | 直接打數字（聯想窗自動收起，不會吞掉你的數字） |
| `空白鍵` / `Esc` | 收起聯想窗（不上屏任何東西） |
| 直接打速成碼 | 照常打字（聯想窗自動收起） |

聯想窗 **6 秒**無操作會自動收起。只建議單一中文字；標點、英文、數字都不會學。

### 🧹 清空聯想字記憶

右鍵系統匣圖示 → **聯想字** → **清空聯想字記憶…**

- 會確認一次（無法復原）
- 清空後回復**出廠預設**聯想詞表，之後重新學習
- **速成候選字次序完全不受影響**

記憶存在 `%APPDATA%\QuickIME\assoc.txt`（純文字，可自行檢視或編輯）；
刪掉該檔等同清空。不想用聯想字：右鍵 → **聯想字** → 取消勾選即可。

---

## 🆕 v2.3 更新重點：更貼近 Windows 速成的鍵盤習慣

兩件「肌肉記憶」的事，改成跟 Windows 內建速成一致：

| 項目 | v2.2 | v2.3 |
|------|------|------|
| 聯想字選字 | 直接按 `1`–`9` | **`Shift` + `1`–`9`**（直接按 = 打數字） |
| 空白鍵到尾頁 | 再按 = 上屏第 1 頁第 1 個字 | **再按 = 繞返第 1 頁**，循環翻頁 |

### 為什麼空白鍵不再「到尾頁就上屏」

微軟速成用家掃候選字時，手指會一直按空白鍵。掃到尾頁那一下如果就上屏，
用家會在意想不到的情況下打出一個字（而且是錯的字）。
所以 v2.3 的空白鍵**只負責翻頁**：到尾頁再按就是繞返第 1 頁，永遠不會偷偷上屏。
要上屏就按數字鍵（或用 `Enter`／標點，維持原本的「先上屏首選」行為）。

順帶修正了一個舊 bug：v2.2 在尾頁按空白，上屏的是**第 1 頁**的第 1 個字，
而不是畫面正看著的那一頁的候選字。

### 聯想字 Shift+數字可關閉

右鍵系統匣 → **聯想字** → 勾選／取消 **「Shift + 數字鍵選聯想字」**。
取消之後就回復 v2.2 的直接數字選字。
（聯想字關掉時這一項會變灰，因為已經沒有意義。）

---

## v2.0 更新重點（打機黨）

v1.0 有一個對打機的人很致命的設計：**單按任何一邊 Shift 都會切換中英**。
長按左 Shift 跑步，一放開就變成英文，角色突然不受控。

v2.0 全部改掉：

| 問題 | v1.0 | v2.0 |
|------|------|------|
| 長按 Shift 跑步 | 放開就切英文 ❌ | 只有**右 Shift** 單按才切換 ✅ |
| Shift + WASD | 可能誤切換 ❌ | 期間按過其他鍵就**一律不切換** ✅ |
| 長按不放 | 仍會切換 ❌ | 超過 **0.7 秒**就當長按，不切換 ✅ |
| 長按期間打中文 | WASD 被吃掉 ❌ | **完全不組字**，鍵盤原樣給遊戲 ✅ |
| 誤按 `Ctrl+空白` | 可能轉全形 ❌ | **預設攔死**，完全冇反應 ✅ |
| 打機時睇狀態 | 要讀 tooltip ❌ | 圖示**變色**：藍＝中／灰＝英 ✅ |
| AppData 被鎖 | **整個程式開唔到** ❌ | 自動退回 exe 同目錄 ✅ |

### 打機用：左右 Shift 分開設定

原本「單按 Shift 就切換中英」對打機的人是個陷阱：
長按左 Shift 跑步，一放開就變成英文，角色突然不受控。

所以本專案：

- **預設只有「右 Shift」單按才切換中／英**，左 Shift 完全留給遊戲
- 按住 Shift 期間再按其他鍵（WASD／方向鍵）→ **一律當長按，不切換**
- 按住超過 **0.7 秒**才放開 → **一律當長按，不切換**
- 按住 Shift 期間**完全不組字**，鍵盤原樣傳給遊戲

想改的話：右鍵系統匣圖示 → **設定** →

```
中英切換鍵（單按 Shift）
  ☐ 左邊 Shift 單按切換中／英
  ☑ 右邊 Shift 單按切換中／英
──────────────────────
☑ 攔住 Ctrl+空白（唔會轉全形符號）
──────────────────────
  符號：永遠半形（唔會變全形）
```

兩邊都不勾 = 完全關閉 Shift 切換（只能按系統匣圖示切換）。
設定會存到 `%APPDATA%\QuickIME\settings.ini`，刪掉即回覆預設值。

### 符號與全形

本程式**只送 ASCII 半形符號**，不會變成全形（`，。！？`），
也不提供全形模式 —— 需要全形符號時請用微軟速成或其他輸入法。

`Ctrl+空白` 在微軟速成是「全形／半形」切換，打機緊張時很容易誤按。
本程式**預設把它完全攔住**（按了完全沒反應，連系統都收不到）；
可在選單取消勾選，恢復原本行為。

### 系統匣圖示會變色（一眼看狀態）

| 圖示 | 狀態 |
|------|------|
| 藍色「**速**」 | 中文模式（速成） |
| 灰色「**英**」 | 英文模式 |
| 深灰「**速**」 | 已暫停 |

打機時不用看 tooltip，用眼角瞄一眼顏色就知道現在會不會打出中文。

### 候選字視窗

![候選字視窗](docs/screenshot.png)

---

## 使用方式

1. 下載 `QuickIME.exe`
2. 雙擊執行 → 右下角時區旁邊出現藍色「**速**」字圖示
3. 在任何程式直接打字

| 按鍵 | 功能 |
|------|------|
| `空白鍵` | 下一頁；**到尾頁再按 = 繞返第 1 頁**（循環翻頁，永遠不會偷偷上屏） |
| `1`–`9` | 選目前頁第 1–9 個候選字 |
| `PageUp` / `-` | 上一頁 |
| `PageDown` / `+` | 下一頁 |
| `退格鍵` | 刪一個輸入碼 |
| `Esc` | 取消本次輸入 |
| `右 Shift`（單按） | 切換中／英（可在選單改成左 Shift／兩邊／關閉） |
| `Ctrl+空白` | 預設攔住，不會切換全形（可關閉攔截） |

> 長按 Shift（>0.7 秒）或按住期間按過其他鍵，都不會觸發切換。
> 按住 Shift 期間也完全不組字，鍵盤原樣傳給遊戲。
>
> **例外：聯想字窗開著時，`Shift` + `1`–`9` 會用來選聯想字**
> （跟 Windows 速成一樣）。放開 Shift 之後，長按不切換的規則完全不變。

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
>
> **聯想字不受此限**：聯想字是上屏之後另一個視窗的建議，
> 它學習用字習慣只改變「聯想窗」內的次序，速成候選字次序永遠不變。

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
│   ├── Assoc.cs              # 聯想字引擎（學習 + 出廠詞表 + 清空）
│   ├── assoc-seed.txt        # 聯想字出廠詞表（嵌入 exe）
│   ├── ToggleGate.cs         # 中英切換 / Ctrl+空白 判定（純邏輯，可單測）
│   ├── Keyboard.cs           # Win32 低階鍵盤掛鉤 + SendInput
│   ├── CandidateForm.cs      # 候選字 / 聯想字視窗（WS_EX_NOACTIVATE）
│   ├── Program.cs            # 主程式 / 系統匣 / 設定 / 說明
│   ├── SelfTest.cs           # 148 項自我測試
│   ├── GameTest.cs           # 打機情境測試（長按 Shift 不誤切換）
│   ├── IconShot.cs           # 系統匣圖示截圖
│   └── UiShot.cs             # 候選窗 / 聯想窗截圖（離線檢查 UI）
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
.\dist\QuickIME.exe --selftest          # 148 項自我測試
.\dist\QuickIME.exe --gametest          # 打機情境測試（長按 Shift + WASD 不誤切換）
.\dist\QuickIME.exe --uishop shot.png   # 產生候選窗截圖
.\dist\QuickIME.exe --iconshot .\icons  # 產生三種狀態的系統匣圖示
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

**不適合**：需要雲端詞庫、簡繁轉換、整句預測的使用者
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
