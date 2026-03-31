using System;
using System.Collections.Generic;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;

namespace RevitBridge.Handlers
{
    public static class FileHandler
    {
        public static void Handle(UIApplication uiapp, string action, Dictionary<string, object> payload)
        {
            if (action == "create_project_from_template")
            {
                string templatePath = payload.ContainsKey("templatePath") ? payload["templatePath"].ToString() : "";
                if (System.IO.File.Exists(templatePath))
                {
                    uiapp.OpenAndActivateDocument(templatePath);
                }
                else
                {
                    throw new Exception("Không tìm thấy Template tại: " + templatePath);
                }
            }
            else if (action == "save_project")
            {
                UIDocument uidoc = uiapp.ActiveUIDocument;
                if (uidoc == null) return;
                Document doc = uidoc.Document;

                string savePath = payload["savePath"].ToString();
                
                SaveAsOptions options = new SaveAsOptions();
                options.OverwriteExistingFile = true;
                
                doc.SaveAs(savePath, options);
            }
            else if (action == "close_project")
            {
                UIDocument uidoc = uiapp.ActiveUIDocument;
                if (uidoc != null)
                {
                    // Đóng bản vẽ hiện tại (không save)
                    uidoc.Document.Close(false); 
                }
            }
        }
    }
}
