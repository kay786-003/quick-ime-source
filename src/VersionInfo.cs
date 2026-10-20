// ============================================================================
//  VersionInfo.cs - **自動產生，請勿手動編輯**
//
//  由 build.ps1 從 repo 根的 VERSION 檔產生。
//  改版本號：改 VERSION，然後重新跑 build.ps1。
// ============================================================================
using System.Reflection;

[assembly: AssemblyVersion("2.4.0.0")]
[assembly: AssemblyFileVersion("2.4.0.0")]
[assembly: AssemblyProduct("速成輸入法 (Quick IME)")]
[assembly: AssemblyTitle("QuickIME")]
[assembly: AssemblyDescription("免安裝速成輸入法 — 不依賴 Windows 輸入法框架")]

namespace QuickIME
{
    internal static class VersionInfo
    {
        /// <summary>程式版本（例如 "2.4"）。</summary>
        public const string Version = "2.4";

        /// <summary>給組件用的四段式版本（例如 "2.4.0.0"）。</summary>
        public const string AssemblyVersion = "2.4.0.0";
    }
}