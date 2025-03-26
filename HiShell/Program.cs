using System.Text;
using System.IO;
using MrHihi.HiConsole;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace MrHihi.HiShell;
class Program
{
    static void ShowError(string msg)
    {
        Console.WriteLine($"Error: {msg}");
        Console.WriteLine();
    }
    static void ShowUsage()
    {
        Console.WriteLine("Usage: ");
        Console.WriteLine("  dotnet hishell                                     - Start HiShell Environment");
        Console.WriteLine("  dotnet hishell [new <zsh | cmd> <working path>]    - Create a new HiShell Environment");
        Console.WriteLine("  dotnet hishell [run <working path>]                - Run HiShell Environment"); 
        Console.WriteLine();
    }
    static void ShowResourceList()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string[] names = assembly.GetManifestResourceNames().Where(x => x.EndsWith(".txt")).ToArray();
        Console.WriteLine("Resource List:");
        foreach (var name in names)
        {
            Console.WriteLine($"  {name.Replace("HiShell.resource.","").Replace(".txt", "")}");
        }
        Console.WriteLine();
    }
    static string GetResourceTextFile(string resourceName)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream == null)
            {
                throw new Exception($"Resource Not Found: {resourceName.Replace(".txt", "")}");
            }
            using (StreamReader reader = new StreamReader(stream))
            {
                string result = reader.ReadToEnd();
                return result;
            }
        }
    }

    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            var shell = new HiShell();
            return;
        }

        if (args[0] == "new" && args.Length == 3)
        {
            var env = args[1];
            var resourceName = $"HiShell.resource.{env}.txt";
            string content = string.Empty;
            try
            {
                content = GetResourceTextFile(resourceName);
            }
            catch(Exception ex)
            {
                ShowError(ex.Message);
                ShowResourceList();
                ShowUsage();
                return;
            }
            if (string.IsNullOrEmpty(content))
            {
                ShowError($"{resourceName.Replace(".txt", "")}, Resource is empty.");
                ShowResourceList();
                ShowUsage();
                return;
            }
            var path = args[2];
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                path = Path.GetFullPath(path);
                content = content.Replace("<YOUR WORKING DIRECTORY>", path);
                // 把 content 寫到 path 下的 hishell 檔案裡
                var outputName = (resourceName == "cmd.txt")? "hishell.env.cmd" : "hishell.env";
                var hishellFile = Path.Combine(path, outputName);
                File.WriteAllText(hishellFile, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                Console.WriteLine($"Create HiShell Environment at {path}");
                Console.WriteLine($"  {hishellFile}");
            }
            else
            {
                ShowError($"Path {path} is already exists.");
                ShowUsage();
            }
        }
        else if (args[0] == "run" && args.Length == 2)
        {
            var path = args[1];
            if (Directory.Exists(path))
            {
                var iswindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                // 檢查有沒有 hishell.env 檔案
                var hishellFile = Path.Combine(path, $"hishell.env{(iswindows? ".cmd" : "")}");
                if (File.Exists(hishellFile))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = path
                    };
                    if (iswindows)
                    {
                        startInfo.FileName = "cmd.exe";
                        startInfo.Arguments = $"/c \"{hishellFile}\"";
                    }
                    else
                    {
                        startInfo.FileName = "zsh";
                        startInfo.Arguments = $"-c \"source {hishellFile}\"";
                    }
                    using (var process = new Process { StartInfo = startInfo })
                    {
                        process.Start();
                        process.WaitForExitAsync().Wait();
                    }
                }
                else
                {
                    Environment.SetEnvironmentVariable("HiShellWorkingDirectory", path, EnvironmentVariableTarget.Process);
                    var shell = new HiShell();
                }
            }
            else
            {
                ShowError($"Path {path} is not exists.");
                ShowUsage();
            }
        }
        else
        {
            ShowError("Invalid argument.");
            ShowUsage();
        }
    }
}
