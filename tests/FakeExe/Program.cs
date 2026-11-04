using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;

namespace FakeExe;

internal static class Program
{
    private static int Main(string[] args)
    {
        var scriptPath = Environment.GetEnvironmentVariable("FAKE_EXE_SCRIPT");
        var logPath = Environment.GetEnvironmentVariable("FAKE_EXE_LOG");

        if (!string.IsNullOrEmpty(logPath))
        {
            try
            {
                File.AppendAllText(logPath, string.Join("\u001F", args) + "\n");
            }
            catch (IOException)
            {
            }
        }

        if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
        {
            return 0;
        }

        var argvJoined = string.Join(" ", args);

        foreach (var line in File.ReadAllLines(scriptPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            JsonNode? action;
            try
            {
                action = JsonNode.Parse(line);
            }
            catch (JsonException)
            {
                return 99;
            }

            if (action is not JsonObject obj)
            {
                return 99;
            }

            if (obj.TryGetPropertyValue("if", out var ifNode))
            {
                var ifText = ifNode?.GetValue<string>();
                if (ifText is null || !argvJoined.Contains(ifText))
                {
                    continue;
                }
            }

            if (obj.TryGetPropertyValue("stdout", out var stdoutNode))
            {
                Console.Out.WriteLine(stdoutNode?.GetValue<string>());
            }

            if (obj.TryGetPropertyValue("stderr", out var stderrNode))
            {
                Console.Error.WriteLine(stderrNode?.GetValue<string>());
            }

            if (obj.TryGetPropertyValue("sleepMs", out var sleepNode))
            {
                var ms = sleepNode?.GetValue<int>() ?? 0;
                Thread.Sleep(ms);
            }

            if (obj.TryGetPropertyValue("hang", out var hangNode) && hangNode?.GetValue<bool>() == true)
            {
                Thread.Sleep(Timeout.Infinite);
            }

            if (obj.TryGetPropertyValue("createFile", out var createNode))
            {
                var path = createNode?.GetValue<string>();
                if (path == "@last" && args.Length > 0)
                {
                    path = args[^1];
                }
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        using var fs = File.Create(path);
                    }
                    catch (IOException)
                    {
                    }
                }
            }

            if (obj.TryGetPropertyValue("exit", out var exitNode))
            {
                return exitNode?.GetValue<int>() ?? 0;
            }
        }

        return 0;
    }
}
