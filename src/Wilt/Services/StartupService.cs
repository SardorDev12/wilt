using System;
using Microsoft.Win32;

namespace Wilt.Services;

/// <summary>
/// Registers/unregisters Wilt in the per-user Windows startup registry key
/// (PRD 9.5 — the one setting that reasonably requires acknowledging it's
/// not "instant" in the sense of affecting the currently running instance,
/// though the registry write itself happens immediately).
/// </summary>
public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Wilt";

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key == null)
            {
                return;
            }

            if (enabled)
            {
                var exePath = Environment.ProcessPath ?? Environment.GetCommandLineArgs()[0];
                key.SetValue(ValueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception)
        {
            // Registry access can fail under restricted policies; startup toggle
            // is a non-critical convenience, so fail silently rather than crash.
        }
    }
}
