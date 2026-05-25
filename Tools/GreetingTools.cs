using ModelContextProtocol.Server;
using System.ComponentModel;

namespace SimpleMcpHttpServer.Tools;

[McpServerToolType]
public static class GreetingTools
{
    [McpServerTool(Name = "get_greeting"), Description("Returns a personalized greeting string.")]
    public static string GetGreeting(
        [Description("The name of the person to greet.")] string name)
    {
        return $"Hello, {name}! This is your Streamable HTTP MCP server responding.";
    }
}
