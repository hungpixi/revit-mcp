using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RevitMCPPlugin.Commands
{
    /// <summary>
    /// Compiles and executes arbitrary C# code snippets inside Revit.
    ///
    /// The snippet runs in a scripting context with:
    ///   Document  doc        – active Revit document
    ///   JObject   parameters – the params object from the JSON-RPC request
    ///
    /// The snippet may return any value; it is serialised to JSON.
    /// Compilation errors and runtime exceptions are reported separately.
    /// </summary>
    public static class CodeExecutionCommand
    {
        // ─── Script globals class ──────────────────────────────────────────

        /// <summary>Globals visible inside the user script.</summary>
        public sealed class ScriptGlobals
        {
            public Document doc        { get; set; }
            public JObject  parameters { get; set; }

            // Convenience helpers available in scripts
            public static double MmToFeet(double mm) => mm / 304.8;
            public static double FeetToMm(double ft) => ft * 304.8;

            /// <summary>
            /// Collects all elements of the given built-in category.
            /// </summary>
            public IList<Element> Collect(BuiltInCategory category) =>
                new FilteredElementCollector(doc)
                    .OfCategory(category)
                    .WhereElementIsNotElementType()
                    .ToElements();

            /// <summary>Logs a message to the plugin log file.</summary>
            public void Log(string msg) => App.Log($"[Script] {msg}");
        }

        // ─── Script options (shared; reused across calls) ─────────────────

        private static readonly ScriptOptions _scriptOptions = ScriptOptions.Default
            .AddReferences(
                typeof(Document).Assembly,                  // RevitAPI.dll
                typeof(JObject).Assembly,                   // Newtonsoft.Json
                typeof(object).Assembly,                    // mscorlib / System.Private.CoreLib
                typeof(System.Linq.Enumerable).Assembly,    // System.Linq
                typeof(System.Collections.Generic.List<>).Assembly,
                Assembly.GetExecutingAssembly()             // RevitMCPPlugin itself
            )
            .AddImports(
                "System",
                "System.Collections.Generic",
                "System.Linq",
                "Autodesk.Revit.DB",
                "Autodesk.Revit.DB.Architecture",
                "Autodesk.Revit.DB.Structure",
                "Newtonsoft.Json.Linq"
            )
            .WithOptimizationLevel(OptimizationLevel.Debug)
            .WithEmitDebugInformation(false);

        // ─── Entry point ─────────────────────────────────────────────────

        /// <summary>
        /// params:
        ///   code        (string) – C# code to execute
        ///   parameters  (object) – arbitrary JSON passed as script globals.parameters
        ///   timeout     (int)    – max execution seconds (default 30)
        /// </summary>
        public static JObject Execute(Document doc, JObject @params)
        {
            if (doc == null)
                return Error("compilation", "No active document");

            string userCode = @params["code"]?.Value<string>();
            if (string.IsNullOrWhiteSpace(userCode))
                return Error("compilation", "No code provided in 'code' parameter");

            int timeoutSec = @params["timeout"]?.Value<int>() ?? 30;
            JObject scriptParams = @params["parameters"] as JObject ?? new JObject();

            App.Log($"CodeExecution: running {userCode.Length} chars of code");

            // ── Wrap user code in a helper template ─────────────────────
            // The user code runs directly in script scope; doc and parameters
            // are exposed through the ScriptGlobals class.
            string wrappedCode = BuildWrappedScript(userCode);

            // ── Compilation + execution ──────────────────────────────────
            try
            {
                var globals = new ScriptGlobals
                {
                    doc        = doc,
                    parameters = scriptParams
                };

                // Run synchronously on the Revit main thread (we are already on it)
                Task<object> task = CSharpScript.EvaluateAsync<object>(
                    wrappedCode,
                    _scriptOptions,
                    globals,
                    typeof(ScriptGlobals));

                bool completed = task.Wait(TimeSpan.FromSeconds(timeoutSec));

                if (!completed)
                    return Error("timeout", $"Script timed out after {timeoutSec} s");

                if (task.IsFaulted)
                {
                    Exception inner = task.Exception?.InnerException ?? task.Exception;
                    App.Log($"CodeExecution runtime error: {inner?.Message}");
                    return Error("runtime", inner?.Message ?? "Unknown runtime error");
                }

                object result = task.Result;
                JToken resultToken = SerialiseResult(result);

                App.Log("CodeExecution completed successfully");
                return new JObject
                {
                    ["status"]   = "success",
                    ["result"]   = resultToken,
                    ["resultType"] = result?.GetType().Name ?? "null"
                };
            }
            catch (CompilationErrorException cex)
            {
                string diagnostics = string.Join("\n",
                    cex.Diagnostics.Select(d => d.ToString()));
                App.Log($"CodeExecution compilation error:\n{diagnostics}");
                return Error("compilation", diagnostics);
            }
            catch (AggregateException aex)
            {
                Exception inner = aex.InnerException ?? aex;
                App.Log($"CodeExecution aggregate error: {inner.Message}");

                // Check if it's a compilation error wrapped in AggregateException
                if (inner is CompilationErrorException cex2)
                {
                    string diagnostics = string.Join("\n",
                        cex2.Diagnostics.Select(d => d.ToString()));
                    return Error("compilation", diagnostics);
                }

                return Error("runtime", inner.Message);
            }
            catch (Exception ex)
            {
                App.Log($"CodeExecution unexpected error: {ex.Message}\n{ex.StackTrace}");
                return Error("runtime", $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        // ─── Template builder ─────────────────────────────────────────────

        /// <summary>
        /// Wraps user code so it can access Transaction through a helper method
        /// while still running in script scope.
        /// </summary>
        private static string BuildWrappedScript(string userCode)
        {
            // The Roslyn scripting engine compiles the code as a sequence of
            // statements.  We prepend helper declarations so the user can call
            // Transaction() / using(Tx()) etc.  The last expression value is
            // the return value of the script.
            return $@"
// ── Auto-generated script preamble ────────────────────────────────
// Helpers available:
//   doc            : Autodesk.Revit.DB.Document
//   parameters     : Newtonsoft.Json.Linq.JObject
//   MmToFeet(mm)   : double
//   FeetToMm(ft)   : double
//   Collect(cat)   : IList<Element>
//   Log(msg)       : void
// ──────────────────────────────────────────────────────────────────

{userCode}
";
        }

        // ─── Serialisation ─────────────────────────────────────────────────

        private static JToken SerialiseResult(object result)
        {
            if (result == null) return JValue.CreateNull();

            try
            {
                // If already a JToken, return as-is
                if (result is JToken jt) return jt;

                // Primitive types
                if (result is string s)    return new JValue(s);
                if (result is bool   b)    return new JValue(b);
                if (result is double d)    return new JValue(d);
                if (result is float  f)    return new JValue(f);
                if (result is int    i)    return new JValue(i);
                if (result is long   l)    return new JValue(l);

                // Revit ElementId
                if (result is ElementId eid) return new JValue(eid.IntegerValue);

                // Collections
                if (result is System.Collections.IEnumerable enumerable
                    && !(result is string))
                {
                    var arr = new JArray();
                    foreach (object item in enumerable)
                        arr.Add(SerialiseResult(item));
                    return arr;
                }

                // Try JSON serialisation
                string json = JsonConvert.SerializeObject(result,
                    new JsonSerializerSettings
                    {
                        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                        Error = (sender, args) => { args.ErrorContext.Handled = true; }
                    });
                return JToken.Parse(json);
            }
            catch
            {
                return new JValue(result.ToString());
            }
        }

        // ─── Error helper ─────────────────────────────────────────────────

        private static JObject Error(string errorType, string message) =>
            new JObject
            {
                ["status"]    = "error",
                ["errorType"] = errorType,
                ["message"]   = message
            };
    }
}
