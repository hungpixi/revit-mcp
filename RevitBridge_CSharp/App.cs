using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;

namespace RevitBridge
{
    /// <summary>
    /// FailuresPreprocessor tự động dismiss mọi warning (duplicate, overlap, etc.)
    /// để automation chạy 100% không cần con người tương tác.
    /// </summary>
    public class AutoDismissFailuresPreprocessor : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
        {
            var failures = failuresAccessor.GetFailureMessages();
            foreach (FailureMessageAccessor failure in failures)
            {
                // Nếu là Warning → xóa bỏ (auto-dismiss)
                if (failure.GetSeverity() == FailureSeverity.Warning)
                {
                    failuresAccessor.DeleteWarning(failure);
                }
            }
            return FailureProcessingResult.Continue;
        }
    }

    public class AntigravityApp : IExternalApplication
    {
        private ExternalEvent _externalEvent;
        private BIMCommandHandler _handler;
        private HttpListener _listener;

        public Result OnStartup(UIControlledApplication application)
        {
            // 1. Đăng ký Handler với Revit ngay khi bật máy
            _handler = new BIMCommandHandler();
            _externalEvent = ExternalEvent.Create(_handler);

            // 2. Tạo Ribbon UI chuyên nghiệp (giống WiseBIM/Pele AI)
            try {
                application.CreateRibbonTab("AI AGENT");
                RibbonPanel panel = application.CreateRibbonPanel("AI AGENT", "Bridge Controls");
                // Thêm một nút giả để hiển thị trạng thái (có thể nâng cấp thành nút thực sau này)
                PushButtonData btnData = new PushButtonData("AI_Status", "Bridge: ONLINE", 
                    System.Reflection.Assembly.GetExecutingAssembly().Location, "RevitBridge.DummyCommand");
                panel.AddItem(btnData);
            } catch { /* Tab đã tồn tại hoặc lỗi UI không nghiêm trọng */ }

            // 3. Auto-dismiss mọi Dialog box (TaskDialog, MessageBox)
            application.DialogBoxShowing += OnDialogBoxShowing;

            // 4. Auto-dismiss mọi FailuresProcessing warning
            application.ControlledApplication.FailuresProcessing += OnFailuresProcessing;

            // 5. Chạy Thread Web Server ngầm ở port 5050
            StartLocalWebServer();

            return Result.Succeeded;
        }

        public class DummyCommand : IExternalCommand {
            public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements) {
                TaskDialog.Show("Bridge Status", "AI Agent đang hoạt động bình thường trên Port 5050.");
                return Result.Succeeded;
            }
        }

        private void OnDialogBoxShowing(object sender, DialogBoxShowingEventArgs e)
        {
            // TaskDialog (các dialog cảnh báo)
            if (e is TaskDialogShowingEventArgs taskDialog)
            {
                // Bấm nút đầu tiên (thường là OK hoặc Close)
                taskDialog.OverrideResult((int)TaskDialogResult.Ok);
                return;
            }

            // MessageBox dialog
            if (e is MessageBoxShowingEventArgs msgBox)
            {
                msgBox.OverrideResult((int)System.Windows.Forms.DialogResult.OK);
                return;
            }

            // Các dialog khác → cố gắng override kết quả
            e.OverrideResult(1); // 1 = OK trong hầu hết các trường hợp
        }

        /// <summary>
        /// Tự động xử lý mọi failure/warning trong Transaction commits.
        /// </summary>
        private void OnFailuresProcessing(object sender, FailuresProcessingEventArgs e)
        {
            FailuresAccessor fa = e.GetFailuresAccessor();
            var failures = fa.GetFailureMessages();
            foreach (FailureMessageAccessor failure in failures)
            {
                if (failure.GetSeverity() == FailureSeverity.Warning)
                {
                    fa.DeleteWarning(failure);
                }
            }
            e.SetProcessingResult(FailureProcessingResult.Continue);
        }

        private void StartLocalWebServer()
        {
            Task.Run(() =>
            {
                try
                {
                    _listener = new HttpListener();
                    _listener.Prefixes.Add("http://localhost:5050/api/ai-command/");
                    _listener.Start();

                    while (_listener.IsListening)
                    {
                        // Dừng chờ AI gửi Request (block thread phụ)
                        HttpListenerContext context = _listener.GetContext();

                        Task.Run(() =>
                        {
                            try
                            {
                                var request = context.Request;
                                string jsonPayload;
                                using (var reader = new StreamReader(request.InputStream))
                                {
                                    jsonPayload = reader.ReadToEnd();
                                }

                                var tcs = new TaskCompletionSource<string>();
                                AICommandQueue.Commands.Enqueue(new AICommandItem { JsonPayload = jsonPayload, Tcs = tcs });
                                _externalEvent.Raise();

                                string resultData = "{\"status\": \"Event raised but no data returned\"}";
                                try
                                {
                                    // Timeout increased to 120s for Headless Building Compilation
                                    tcs.Task.Wait(TimeSpan.FromSeconds(120)); 
                                    if (tcs.Task.IsCompleted) resultData = tcs.Task.Result;
                                }
                                catch { }

                                HttpListenerResponse response = context.Response;
                                response.StatusCode = (int)HttpStatusCode.OK;
                                using (var writer = new StreamWriter(response.OutputStream))
                                {
                                    writer.Write(resultData);
                                }
                                response.Close();
                            }
                            catch { }
                        });
                    }
                }
                catch (Exception e)
                {
                    // Lỗi listen socket (ví dụ port bị trùng, bị tắt Revit)
                }
            });
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            if (_listener != null) _listener.Stop();
            if (_externalEvent != null) _externalEvent.Dispose();
            return Result.Succeeded;
        }
    }
}
