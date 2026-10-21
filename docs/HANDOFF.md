# 交接文檔 — v2.5 已 commit、未 push

> 寫於 2026-10-21。上一個 session 完成了 v2.5 的程式碼修復（當時未 commit）；
> 本 session 已把修復 **commit 進本機 `main`（`2ef1d73` + 文件 commit）**，
> 但**仍然未 push、未打 tag、未發 Release**。
> 這份文件說明「做了什麼、還剩什麼、怎樣接手」。

---

## 一、當前狀態（一句話）

**修復已在本機 `main` 上，`origin/main` 仍停在 v2.4 的 `7ff93c2`，本地比遠端多兩個 commit，全部未推送。**

```
origin/main 7ff93c2  docs: 校驗值改用 v2.4 Release 實際檔案…（＝v2.4）
本地 main   2ef1d73  fix(v2.5): 修正六個會打錯字的 bug、重構掛鉤接線使可測
             之後還有一個 docs commit（對齊 v2.5 說明與交接文件）
狀態        已 commit、未 push、未 tag
```

驗證指令：

```powershell
cd E:\WSL_Projects\quick-ime-source
git status --short
git log -1 --oneline
git rev-parse HEAD; git rev-parse origin/main
```

---

## 二、這次 commit 包含什麼

第一個 commit `2ef1d73`（程式碼 + 建置產物）：

| 檔案 | 性質 | 說明 |
|------|------|------|
| `src/Composer.cs` | **實質修改** | z 次序、空白鍵規則 |
| `src/Keyboard.cs` | **實質修改** | HookResult、Ctrl+Shift、Gate 失步、接線重構 |
| `src/HookTest.cs` | **實質修改** | 46 → 177 項測試 |
| `src/SelfTest.cs` | **實質修改** | 147 → 152 項（空白鍵規則） |
| `VERSION` | 設定 | `2.4` → `2.5` |
| `src/VersionInfo.cs` | **自動產生** | build.ps1 從 VERSION 生成 |
| `releases/QuickIME.exe` | **建置產物** | v2.5 的 exe（已提交） |
| `releases/速成輸入法_v2.5.zip` | **建置產物** | v2.5 的 zip（已提交，新增） |

第二個 commit（文件）：`AGENTS.md`、`README.md`、`docs/DEVELOPMENT.md`、
`docs/使用說明.txt`、`docs/HANDOFF.md`、`docs/HANDOFF-PROMPT.md`。

✅ 建置產物已按 repo 慣例一併提交（`.gitignore` 明寫 `releases/` 係故意提交嘅，
CI 的 `build.yml` 也會把 prebuilt exe commit 回 `releases/`）。

---

## 三、v2.5 修了什麼（9 項）

全部都有回歸測試守住，並經過 **10 個 mutation 驗證**（改壞實作 → 測試必須 FAIL）。

### 會直接打錯字的 bug

| # | 問題 | 症狀 | 修正位置 |
|---|------|------|----------|
| 1 | **`z` 當第三碼留死碼** | `ab` 後按 `z`，下個字變 `za`，打出「　」 | `Composer.TypeLetter`：把「滿兩碼上屏」搬到 `z` 檢查之前 |
| 2 | **按數字選字時數字漏進文件** | `hi` 按 `2` → 文件出現「么2」 | `KeyboardHook.ShouldSwallowKey`：`CommitPending` 也要吞鍵 |
| 3 | **單頁多候選時空白鍵上屏** | `a`（日/曰）按空白 → 直接打出「日」 | `Composer.PressSpace`：改成只有 `Count <= 1` 才上屏 |
| 4 | **`Ctrl+Shift+.` 變「。」** | 想打 `>` 卻出全形句號 | `FullWidthPunct`：移除 Shift 表 |
| 5 | **組字中 `Ctrl+Shift+符號` 殘留組字** | 上屏後碼還在 | 符號鍵例外：先上屏再放行（`Ctrl+C` 仍不上屏） |
| 6 | **Gate 失步時 `Ctrl+Shift+.` 仍出「。」** | 暫停期間按住 Shift 後恢復 | `_gate.IsShiftHeld \|\| k.ShiftPhysicallyDown` |

### 新增能力

| # | 內容 |
|---|------|
| 7 | `HookResult.CommitPendingPass = 3`：「送字但放行」（`ab` 按 `z` 用） |
| 8 | 英文模式下 Ctrl+符號 完全透明 |
| 9 | `ShouldFlush` / `ShouldSwallowKey` / `CallbackCore` / `OnHookMessage` / `InvokeHookCallback` 重構，讓掛鉤接線可測 |

---

## 四、測試現狀

```
--selftest   152 項
--hooktest   177 項
--gametest   全部通過（6 項，但程式不印數字）
```

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -SkipDict -RunTests
```

**這一節是整個交接最重要的部分。**

### 為什麼要重構測試

v2.4 有 148 項測試全過，卻有 4 個會打錯字的 bug。根因：`HookTest` 的 `Sim` 輔助類別
**自己實作**了「HookResult 2/3 就 Flush」和吞鍵規則，所以 `HookCallback` 真正的接線
（回傳值 → 送字 → 吞鍵）**完全沒被行使**。把它改回錯版，全套測試依然全過。

五輪獨立審查逐步揭穿：

1. 「契約測試」只驗純函式，不驗呼叫點
2. 抽成 `CallbackCore` 後，`HookCallback` 仍可自己寫、繞過它
3. 「`Ctrl+Shift+C` 不上屏」的測試**沒按住 Shift**，走錯分支，斷言空轉
4. `ProcessKeyDownAndApply` 是死碼（已刪）
5. `HookCallback` 外殼零覆蓋（改成永遠放行，175 項全過）

### 最終架構

```
HookCallback (外殼，只做委派)
  └→ OnHookMessage (本體，經兩個可替換接縫碰 Win32)
       ├─ Win.ReadKeyEvent / IsDown / ShiftActuallyDown   ← 測試換成假的
       ├─ CallNext                                         ← 測試換成假的
       └─ ConvertCoreResult(CallbackCore(input))
            └→ ProcessKeyDown → ApplyHookResult → ShouldSwallowKey
```

測試用 `InvokeHookCallback` 驅動真正的外殼，**每個情境各開新 hook**（避免序列相依）。

### Mutation 驗證結果（接手後請重跑確認）

| Mutation | 改壞內容 | FAIL |
|---|---|---|
| M1 | `HookCallback` 外殼 → 永遠放行 | 13 |
| M2 | `OnHookMessage` 尾行 → v2.4 自寫版 | 6 |
| M3 | 吞鍵只認 `Consumed` | 7 |
| M4 | `ConvertCoreResult` 兩邊倒轉 | 8 |
| M5 | `PressSpace` → `TotalPages<=1` | 5 |
| M6 | z 檢查搬回 `len>=2` 之前 | 7 |
| M7 | 刪 `!k.Ctrl \|\| isPunctKey` | 10 |
| M8 | 刪 `ChineseMode &&` | 2 |
| M9 | `Sim.Press` 自己 Flush | 1 |
| M10 | 刪 `ApplyHookResult` | 24 |

**注意**：`Keyboard.cs` / `Composer.cs` 是 **LF**，`HookTest.cs` 是 **CRLF**。
寫 mutation 腳本時要用 `[System.IO.File]::ReadAllText` + `.Replace()` 精確字串替換，
不要用 regex 拼 `\n`。

---

## 五、接手後建議做的事（按優先次序）

### P0 — 已完成

1. ✅ **重跑全套測試**確認基準沒漂移（`152 / 177 / 打機情境全部通過`）。
2. ✅ **建置產物已提交**（`releases/QuickIME.exe` 與 `速成輸入法_v2.5.zip`）。
   `README.md` 的下載連結**仍然刻意指向 v2.4 的 Release** —— v2.5 Release 未存在，
   改了會變死連結。要等 tag 發佈成功之後才改。

### P1 — 剩下的發佈工作（**需要使用者明確同意**）

3. ✅ **commit** 已完成（`2ef1d73` + 文件 commit，都在本機 `main`）。
4. ⬜ **push** 未做。push 後 CI 會自動建置。注意 CI 的 `build.yml` 有一步會讀
   `releases/QuickIME.exe` 的 `FileVersionInfo` 判斷版本 —— v2.5 已在 `releases/`
   裡，所以那一步會略過更新（不會洗版），這是預期行為。
5. ⬜ **打 tag `v2.5`** 才會發 Release。發佈後才可以把 README 的下載連結從 v2.4 改成 v2.5。

### P2 — 已知但未修的問題（**不要自己動手，先問使用者**）

| 問題 | 說明 |
|------|------|
| **聯想窗開著時 `Shift+1` 打不出 `!`** | AGENTS.md 已列為「仍未修」。鍵位本身無法區分「選聯想字」和「打驚嘆號」，需要先確認微軟速成的實際行為 |
| **NumPad 數字鍵未接** | 使用者說「我也沒有右手邊的 Numpad」，已明確跳過 |
| **組字中 Tab/Delete/Home/F5 會先上屏首選** | 涉及「方向鍵在微軟速成的實際行為」，需要使用者實測 |
| **`build.ps1` 的示意圖檔名錯** | 找 `docs\候選字視窗示意圖.png`，實際檔名是 `screenshot.png`，所以示意圖從未進 zip |
| **`HookCallback` 缺 try/catch** | 掛鉤執行緒拋例外會令整個行程 crash |
| **掛鉤執行緒寫硬碟** | `RaiseModeChanged` → `Settings.Save()` 在輸入路徑上做 I/O |
| **無 watchdog** | 掛鉤被系統靜默移除後，圖示還在但打不到字 |

---

## 六、重要提醒（給接手的 AI）

1. **必讀 `AGENTS.md`**。裡面是不可改的硬規則，違反了會令使用者打錯字。
   特別是：空白鍵只有 1 個候選才上屏、一頁 9 個、候選次序永不按字頻重排。

2. **`build.ps1` 必須保持 UTF-8 with BOM**（目前是，CI 會檢查）。
   編輯器若設成「UTF-8 無 BOM」會弄壞它。

3. **不要執行 `releases/QuickIME.exe`**（CI 有同樣的警告）——檔案句柄會被佔住。

4. **改動任何掛鉤邏輯後，一定要做 mutation 驗證**：
   把修正改壞，確認對應測試真的 FAIL。不會失敗的測試等於沒有測試。
   這個專案連續五輪都栽在「測試看起來全過但守不住實作」。

5. **`src/VersionInfo.cs` 是產生物**，不要手改。改 `VERSION` 就好。

6. **`docs/使用說明.txt` 與 `Program.cs` 的 `HelpText()` 是兩份獨立文案**，
   改功能時兩邊都要更新（目前內容大致一致，但沒有自動同步機制）。

---

## 七、已經做過／還沒做的事（避免誤會）

已做（本 session）：

- ✅ commit（`2ef1d73` 程式碼 + 一個 docs commit，都在本機 `main`）
- ✅ 建置產物入庫（`releases/QuickIME.exe`、`releases/速成輸入法_v2.5.zip`）
- ✅ 重跑全套測試：152 / 177 / 打機情境全部通過

還沒做（**刻意**）：

- ❌ 沒有 push
- ❌ 沒有打 tag、沒有發 Release
- ❌ 沒有改 `README.md` 裡指向 v2.4 Release 的下載連結（刻意）
- ❌ 沒有修第五節 P2 列出的任何問題

---

## 八、快速驗證清單

接手後依序跑：

```powershell
cd E:\WSL_Projects\quick-ime-source

# 1. 確認工作區乾淨、版本正確
git status --short             # 應為空（或只剩未追蹤的臨時檔）
git log -2 --oneline           # 應見 2ef1d73 fix(v2.5)… 與其後的 docs commit
git rev-parse HEAD; git rev-parse origin/main   # 兩者不同：本地比遠端多 2 個 commit
Get-Content VERSION            # 應為 2.5

# 2. 建置 + 三套測試
powershell -ExecutionPolicy Bypass -File .\build.ps1 -SkipDict -RunTests
# 期望：152 / 177 / 打機情境全部通過

# 3. 確認版本
(Get-Item dist\QuickIME.exe).VersionInfo.FileVersion    # 應為 2.5.0.0

# 4. 確認 build.ps1 仍有 BOM
$b = [System.IO.File]::ReadAllBytes("build.ps1")
$b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF   # 應為 True

# 5. 確認沒有殘留的建置垃圾
git status --short   # 不應出現 .acl-report/、probe*.cs、*.orig 等
```
