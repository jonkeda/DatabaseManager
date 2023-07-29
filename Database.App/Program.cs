using System;
using System.Windows.Forms;
using DatabaseInterpreter.Core;
using DatabaseManager.Core;
using Databases.Handlers;
using Databases.Handlers.MySql;
using Databases.Handlers.PlSql;
using Databases.Handlers.Sqlite;
using Databases.Handlers.TSql;

namespace DatabaseManager;

internal static class Program
{
    /// <summary>
    ///     The main entry point for the application.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        SqlHandler.RegisterHandler(new TSqlHandler());
        SqlHandler.RegisterHandler(new PlSqlHandler());
        SqlHandler.RegisterHandler(new MySqlHandler());
        SqlHandler.RegisterHandler(new SqliteHandler());
        SqlHandler.RegisterHandler(new PostgreSqlHandler());


        DbInterpreter.Setting = SettingManager.GetInterpreterSetting();

        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new frmMain());
    }
}