using Newtonsoft.Json.Linq;

namespace RevitMCP.Handlers
{
    internal static class HandlerResponse
    {
        public static JObject Success(JObject data)
        {
            data["success"] = true;
            return data;
        }

        public static JObject Error(string message)
        {
            return new JObject
            {
                ["success"] = false,
                ["error"] = message
            };
        }
    }
}

