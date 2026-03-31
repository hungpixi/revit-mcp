using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;

namespace RevitBridge
{
    // Đóng gói Payload và TaskCompletionSource để trả dữ liệu ngược lại cho HttpListener
    public class AICommandItem
    {
        public string JsonPayload { get; set; }
        public System.Threading.Tasks.TaskCompletionSource<string> Tcs { get; set; }
    }

    public static class AICommandQueue
    {
        public static ConcurrentQueue<AICommandItem> Commands = new ConcurrentQueue<AICommandItem>();
    }

    public class BIMCommandHandler : IExternalEventHandler
    {
        public void Execute(UIApplication uiapp)
        {
            // Xử lý tất cả các tin nhắn trong hàng đợi
            AICommandItem cmdItem;
            while (AICommandQueue.Commands.TryDequeue(out cmdItem))
            {
                string jsonCommand = cmdItem.JsonPayload;
                string resultData = "{\"status\": \"OK\"}";
                try
                {
                    // Lệnh mặc định cũ không có Dictionary format
                    if (jsonCommand.Contains("open_new_project") && !jsonCommand.Contains("action"))
                    {
                        RevitCommandId id = RevitCommandId.LookupCommandId("ID_FILE_NEW_CHOOSE_TEMPLATE");
                        if (id != null && uiapp.CanPostCommand(id)) uiapp.PostCommand(id);
                        return;
                    }

                    // Parse JSON chuẩn của Phiên bản 2.0: {"action": "xxx", "payload": {...}}
                    JObject cmdObj = JObject.Parse(jsonCommand);
                    if (cmdObj["action"] == null) continue;

                    string action = cmdObj["action"].ToString();
                    JToken payloadToken = cmdObj["payload"];

                    // Convert JToken payload back to Dictionary for Legacy Handlers (if needed)
                    Dictionary<string, object> payload = new Dictionary<string, object>();
                    if (payloadToken != null && payloadToken.Type == JTokenType.Object) {
                        payload = payloadToken.ToObject<Dictionary<string, object>>();
                    }

                    // 1. Nhóm thao tác Blueprint Batch (Kiến trúc mới nhất)
                    if (action == "build_from_blueprint")
                    {
                        string path = payloadToken?["path"]?.ToString() ?? "";
                        resultData = RevitBridge.Handlers.BlueprintCompiler.Compile(uiapp, path);
                    }
                    // 2. Nhóm thao tác Vòng Đời File
                    else if (action == "create_project_from_template" || action == "save_project" || action == "close_project" || action == "open_new_project")
                    {
                        if (action == "open_new_project")
                        {
                            string[] possiblePaths = new string[] {
                                @"C:\ProgramData\Autodesk\RVT 2020\Templates\US Metric\Electrical-Default_Metric.rte",
                                @"C:\ProgramData\Autodesk\RVT 2020\Templates\US Metric\DefaultMetric.rte",
                                @"C:\ProgramData\Autodesk\RVT 2020\Templates\US Metric\Construction-Default_Metric.rte"
                            };
                            string tmpl = null;
                            foreach(var p in possiblePaths) {
                                if(System.IO.File.Exists(p)) { tmpl = p; break; }
                            }
                            if(tmpl == null) throw new Exception("Không tìm thấy Template Revit 2020 mặc định trên máy!");
                            
                            Document newDoc = uiapp.Application.NewProjectDocument(tmpl);
                            string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AutoMEP_" + Guid.NewGuid().ToString() + ".rvt");
                            newDoc.SaveAs(tempFile);
                            newDoc.Close(false);
                            // uiapp.OpenAndActivateDocument(tempFile); // Vô hiệu hoá tránh deadlock UI
                            resultData = "{\"status\": \"OK\", \"msg\": \"Đã tạo và mở Project MEP mới tại: " + tempFile.Replace("\\", "\\\\") + "\"}";
                        }
                        else
                        {
                            RevitBridge.Handlers.FileHandler.Handle(uiapp, action, payload);
                        }
                    }
                    // 2. Nhóm thao tác Hệ Thống Cơ Điện (Phase 2 MEP)
                    else if (action == "create_duct")
                    {
                        resultData = RevitBridge.Handlers.HvacHandler.Handle(uiapp, action, payload);
                    }
                    else if (action == "create_pipe")
                    {
                        resultData = RevitBridge.Handlers.PlumbingHandler.Handle(uiapp, action, payload);
                    }
                    // 3. Nhóm thao tác Kiến trúc/Xây dựng cơ bản
                    else if (action == "create_grid" || action == "create_level" || action == "create_wall" || action == "place_family_instance" || action == "import_image")
                    {
                        resultData = RevitBridge.Handlers.ArchHandler.Handle(uiapp, action, payload);
                    }
                    else if (action == "extract_5d_quantities")
                    {
                        resultData = RevitBridge.Handlers.VDCHandler.HandleExtract(uiapp, payload);
                    }
                    else if (action == "update_4d_schedule")
                    {
                        resultData = RevitBridge.Handlers.VDCHandler.HandleUpdate(uiapp, payload);
                    }
                    // 4. Nhóm thao tác Kết Cấu và Điện (Phase 3)
                    else if (action == "create_structural_column" || action == "create_structural_framing")
                    {
                        resultData = RevitBridge.Handlers.StructHandler.Handle(uiapp, action, payload);
                    }
                    else if (action == "create_cable_tray" || action == "create_conduit" || action == "place_electrical_equipment" || action == "place_lighting_fixture" || action == "create_electrical_circuit")
                    {
                        resultData = RevitBridge.Handlers.ElectricalHandler.Handle(uiapp, action, payload);
                    }
                    // 5. Nhóm truy vấn dữ liệu
                    else if (action == "get_element_ids_by_category" || action == "get_project_info" || action == "get_levels")
                    {
                        if (action == "get_levels")
                        {
                            // Legacy get_levels compatibility
                            resultData = RevitBridge.Handlers.QueryHandler.Handle(uiapp, "get_project_info", payload);
                        }
                        else
                        {
                            resultData = RevitBridge.Handlers.QueryHandler.Handle(uiapp, action, payload);
                        }
                    }
                    // 5. Gọi phím tắt / lệnh mặc định của Revit
                    else if (action == "execute_revit_command")
                    {
                        string commandIdStr = payload["commandId"].ToString();
                        RevitCommandId cId = RevitCommandId.LookupCommandId(commandIdStr);
                        if (cId != null && uiapp.CanPostCommand(cId))
                        {
                            uiapp.PostCommand(cId);
                            resultData = "{\"status\": \"OK\", \"msg\": \"Đã trigger lệnh " + commandIdStr + "\"}";
                        }
                        else
                        {
                            throw new Exception("Không tìm thấy lệnh hoặc Revit không cho phép PostCommand lúc này: " + commandIdStr);
                        }
                    }

                    cmdItem.Tcs.TrySetResult(resultData);
                }
                catch (Exception e)
                {
                    cmdItem.Tcs.TrySetResult("{\"status\": \"Error\", \"msg\": \"" + e.Message.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}");
                }
            }
        }

        public string GetName() { return "Antigravity BIM Events"; }
    }
}
