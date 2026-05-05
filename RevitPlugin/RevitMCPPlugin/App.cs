using System;
using System.IO;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;

namespace RevitMCPPlugin
{
    public class App : IExternalApplication
    {
        public static UIApplication UIApp { get; private set; }
        public static RevitCommandHandler CommandHandler { get; private set; }
        public static ExternalEvent RevitEvent { get; private set; }
        private static SocketServer _server;

        private static readonly string LogPath = Path.Combine(
            Path.GetTempPath(), "RevitMCPPlugin.log");

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Register an idling event to capture UIApplication on first idle tick
                application.Idling += OnFirstIdle;

                CommandHandler = new RevitCommandHandler();
                RevitEvent = ExternalEvent.Create(CommandHandler);
                _server = new SocketServer(8080);
                _server.Start();

                Log("RevitMCPPlugin started on port 8080");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Log($"Startup failed: {ex.Message}\n{ex.StackTrace}");
                return Result.Failed;
            }
        }

        private void OnFirstIdle(object sender, Autodesk.Revit.UI.Events.IdlingEventArgs e)
        {
            if (sender is UIApplication uiApp)
            {
                UIApp = uiApp;
                // Unsubscribe after first capture
                uiApp.Idling -= OnFirstIdle;
                Log("UIApplication captured on first idle event");
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                _server?.Stop();
                Log("RevitMCPPlugin stopped");
            }
            catch (Exception ex)
            {
                Log($"Shutdown error: {ex.Message}");
            }
            return Result.Succeeded;
        }

        public static void Log(string msg)
        {
            try
            {
                File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n");
            }
            catch { /* swallow logging failures */ }
        }
    }
}
