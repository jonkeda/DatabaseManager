using System;
using System.Windows.Forms;
using DatabaseInterpreter.Core;
using DatabaseManager.Core;

namespace DatabaseManager;

internal static class Program
{
    /// <summary>
    ///     The main entry point for the application.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        DbInterpreter.Setting = SettingManager.GetInterpreterSetting();

        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new frmMain());
    }
}