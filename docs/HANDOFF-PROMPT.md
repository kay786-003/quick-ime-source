# 交接 Prompt

> 用途：把以下整段內容貼給下一個 session 作為開場指令。
> 配套文檔：`docs/HANDOFF.md`（詳細狀態與待辦）

---

## 直接複製以下內容

```
專案：E:\WSL_Projects\quick-ime-source（Windows 速成輸入法 QuickIME，C# / .NET Framework 4.x，
用 WH_KEYBOARD_LL 鍵盤掛鉤 + SendInput 送字，免安裝、免管理員權限）

先做兩件事，不要急著改任何程式碼：

1. 讀 AGENTS.md —— 裡面是不可改的硬規則（空白鍵只有 1 個候選才上屏、一頁 9 個候選、
   候選次序永不按字頻重排、Ctrl+符號鍵打全形、z 無特殊用途等）。
   違反這些會直接令使用者打錯字。特別是「已否決的建議」一節，不要再提。
2. 讀 docs/HANDOFF.md —— 上一個 session 的完整交接。重點：
   v2.5 的修復已完成並通過測試，**已 commit、尚未 push**（本機 main 比 origin/main 多 2 個 commit）。

然後跑這條確認基準沒漂移：

    cd E:\WSL_Projects\quick-ime-source
    git status --short
    Get-Content VERSION
    powershell -ExecutionPolicy Bypass -File .\build.ps1 -SkipDict -RunTests

期望結果：VERSION = 2.5；selftest 152 項、hooktest 177 項、gametest 全部通過。
如果數字對不上，先停下來報告，不要繼續。

---

## 這個專案的特殊風險（務必理解）

這個專案連續五輪都栽在同一件事：**測試看起來全過，但守不住實作**。

v2.4 有 148 項測試全過，卻同時存在 4 個會直接打錯字的 bug。原因是測試的
`Sim` 輔助類別自己實作了「HookResult 2/3 就 Flush」和吞鍵規則，
所以 `HookCallback` 真正的接線（回傳值 → 送字 → 吞鍵）從來沒被行使過。

上一個 session 重構了架構（`HookCallback` 外殼 / `OnHookMessage` 本體 /
`Win` 與 `CallNext` 可替換接縫 / `CallbackCase` 每情境新開 hook），
並用 10 個 mutation 驗證過。**你改動任何掛鉤邏輯之後，一定要重做 mutation 驗證**：
把修正改壞，確認對應測試真的 FAIL。不會失敗的測試等於沒有測試。

（`Keyboard.cs`、`Composer.cs` 是 LF；`HookTest.cs` 是 CRLF。
寫 mutation 腳本要用 `[System.IO.File]::ReadAllText` + `.Replace()` 精確替換，
不要用 regex 拼 `\n`。）

---

## 我現在要你做的事

【在這裡填寫你的需求】

例如：
- 「幫我 push，順便打 v2.5 tag 發 Release」
- 「先只 push，不要打 tag」
- 「修 docs/HANDOFF.md 第五節 P2 列出的某個問題」
- 「繼續修聯想窗 Shift+數字 的衝突，先幫我分析微軟速成的實際行為」

---

## 不要做的事

- 不要執行 releases/QuickIME.exe（會鎖住檔案句柄，CI 有同樣警告）
- 不要手改 src/VersionInfo.cs（那是 build.ps1 從 VERSION 產生的）
- 不要讓 build.ps1 失去 UTF-8 BOM（CI 會檢查，目前是 239,187,191）
- 不要改 README.md 裡指向 v2.4 Release 的下載連結（v2.5 Release 未存在，改了會變死連結）
- 不要把 NumPad、Tab/Delete 上屏、掛鉤 try/catch 這類「已知但刻意未修」的項目
  當成 bug 直接動手 —— 先跟使用者確認（見 HANDOFF.md 第五節 P2）
```

---

## 交接摘要（給你自己參考，不一定要貼）

| 項目 | 狀態 |
|---|---|
| 程式碼修復 | ✅ 完成，9 項，全部有測試守住 |
| 測試 | ✅ 152 / 177 / 6 全過 |
| Mutation 驗證 | ✅ 10 個全部能令測試變紅 |
| 版本號 | ✅ 已改為 2.5 |
| 文件 | ✅ AGENTS.md / README.md / DEVELOPMENT.md / 使用說明.txt 已對齊 |
| **commit** | ✅ 已做（`2ef1d73` + docs commit，本機 main） |
| **push** | ❌ **未做** |
| **tag / Release** | ❌ **未做** |

下一步完全取決於使用者的指示（push、打 tag 發 Release，或修 P2 的問題）。
