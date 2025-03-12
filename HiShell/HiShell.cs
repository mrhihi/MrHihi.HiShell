using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using MrHihi.HiConsole;
using static MrHihi.HiConsole.CommandPrompt;

namespace MrHihi.HiShell;
public class HiShell
{
    private readonly NuGetInstaller _nuget;
    private readonly Dictionary<string, Func<string, string, string, bool>> _internalCommands;
    private readonly CommandPrompt _console;
    private readonly HistoryCollection _histories = new HistoryCollection();
    public HiShell()
    {
        var promptMessage = Environment.GetEnvironmentVariable("HiShellPromptMessage", EnvironmentVariableTarget.Process)??"─> ";
        var welcomeMessage = Environment.GetEnvironmentVariable("HiShellWelcomeMessage", EnvironmentVariableTarget.Process)??"Welcome to HiShell v1.0\nPress `Ctrl+D` to exit.\n";
        var workingDir = Environment.GetEnvironmentVariable("HiShellWorkingDirectory", EnvironmentVariableTarget.Process)??Environment.CurrentDirectory;
        _nuget = new NuGetInstaller(workingDir);
        if (!string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir))
        {
            Environment.CurrentDirectory = workingDir;
        }
        
        _internalCommands = new Dictionary<string, Func<string, string, string, bool>>()
        {
            { "/clear", (cmdname, cmd, buffer) => {
                Console.Clear();
                var cmds = cmd.Split(' ');
                if (cmds.Length > 1 && cmds[1].ToLower() == "nuget")
                {
                    try
                    {
                        _nuget.CleanNugetPackages();
                        Console.WriteLine("NuGet packages cleared.");
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine($"Error: {e.Message}");
                    }
                }
                return true;
            }},
            { "/run", (cmdname, cmd, buffer) => {
                runScript(Environment.CurrentDirectory, buffer, cmdname, cmd, string.Empty).Wait();
                return true;
            }},
            { "/history", (cmdname, cmd, buffer) => {
                if (_histories.Count == 0) return true;
                var cmds = cmd.Split(' ');
                if(cmds.Length == 1) {
                    var last = _histories.Last();
                    Console.WriteLine($"Last command: {last.Command}");
                    Console.WriteLine($"Result: {last.Result}");
                    Console.WriteLine($"Console output: {last.ConsoleOutput}");
                } else if (cmds[1].ToLower() == "all") {
                    foreach(var h in _histories) {
                        Console.WriteLine($"Command: {h.Command}");
                        Console.WriteLine($"Result: {h.Result}");
                        Console.WriteLine($"Console output: {h.ConsoleOutput}");
                        Console.WriteLine();
                    }
                } else if (cmds[1].ToLower() == "clear") {
                    _histories.Clear();
                    Console.WriteLine("History cleared.");
                } else if (cmds[1].ToLower() == "save") {
                    if (cmds.Length < 3) {
                        Console.WriteLine("Please specify the file name.");
                        return true;
                    }

                    var filePath = Path.GetFullPath(cmds[2], "/").Remove(0, 1);
                    filePath = filePath.Replace("~/", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/");

                    Console.WriteLine($"Saving history to {filePath} ...");
                    try {
                        StringBuilder sb = new StringBuilder();
                        foreach(var h in _histories) {
                            sb.AppendLine($"Command: {h.Command}");
                            sb.AppendLine($"Result: {h.Result}");
                            sb.AppendLine($"Console output: {h.ConsoleOutput}");
                            sb.AppendLine();
                        }
                        File.WriteAllText(filePath, sb.ToString());
                        Console.WriteLine($"History saved to {filePath}");
                    } catch (Exception e) {
                        Console.WriteLine($"Error: {e.Message}");
                    }
                }
                return true;
            }}
        };
        _console = new CommandPrompt(enumChatMode.MultiLineCommand);
        _console.CommandEnter += Console_CommandEnter;
        _console.BeforeCommandEnter += Console_BeforeCommandEnter;
        _console.Start(welcomeMessage, promptMessage);
    }

    private async Task runExternalCommand(string commandName, string command, string buffer)
    {
        var csxFilePath = $"{Environment.CurrentDirectory}/{commandName}/{commandName}.csx";
        if (!File.Exists(csxFilePath))
        {
            Console.WriteLine($"Command not found: {commandName}");
            return;
        }
       try {
            var scriptCode = File.ReadAllText(csxFilePath);
            var baseDir = Path.Combine(Environment.CurrentDirectory, commandName);
            var result = await runScript(baseDir, scriptCode, commandName, command, buffer);
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error: {e.Message}");
            Console.WriteLine(e.StackTrace);
            Console.WriteLine();
        }
    }
    private bool runInternalCommand(string commandName, string command, string buffer)
    {
        var lcmdName = commandName.ToLower();;
        if (_internalCommands.ContainsKey(lcmdName))
        {
            _internalCommands[lcmdName](lcmdName, command, trimEndString(buffer, command).TrimEnd('\n'));
            return true;
        }
        return false;
    }

    private async Task<Script<string>> createScript(string scriptCode, string baseDir)
    {
        var (processedScript, references) = await _nuget.ProcessNugetReferences(scriptCode);
        var srcResolver = ScriptSourceResolver.Default.WithBaseDirectory(baseDir).WithSearchPaths(baseDir);
        var scsResolver = ScriptMetadataResolver.Default.WithBaseDirectory(baseDir).WithSearchPaths(baseDir);
        var ccsOptions = ScriptOptions.Default
                            .WithFilePath(baseDir)
                            .WithSourceResolver(srcResolver)
                            .WithMetadataResolver(scsResolver)
                            .AddReferences(Assembly.GetExecutingAssembly())
                            .AddReferences(references)
                            .AddImports("System", "System.Text", "System.IO", "System.Net.Http", "System.Collections.Generic", "System.Threading.Tasks")
                            ;
        return CSharpScript.Create<string>(processedScript, globalsType: typeof(ShellEnvironment), options: ccsOptions);
    }

    private async Task<string> runScript(string baseDir, string scriptCode, string cmdName, string command, string buffer)
    {
        var history = new History(buffer, cmdName, command);
        Console.WriteLine($"Running command: {command} ...");
        var script = await createScript(scriptCode, baseDir);
        var args = new ShellEnvironment(trimEndString(buffer, command).TrimEnd('\n'), cmdName, command);
        var result = (await script.RunAsync(globals: args)).ReturnValue;
        history.Result = result;
        history.ConsoleOutput = args.Console.ToString();
        _histories.Add(history);
        Console.WriteLine();
       return result;
    }

    private string getCommandName(string command)
    {
        var commandParts = command.Split(' ');
        var commandName = commandParts[0];
        return commandName;
    }

    private void runCommand(string command, string buffer)
    {
        var commandName = getCommandName(command);
        if (runInternalCommand(commandName, command, buffer))
        {
            return;
        }
        runExternalCommand(commandName, command, buffer).Wait();
    }

    private bool checkCommand(string command)
    {
        var commandName = getCommandName(command);
        if (_internalCommands.ContainsKey(commandName.ToLower()))
        {
            return true;
        }
        else if (Directory.Exists(Path.Combine(Environment.CurrentDirectory, commandName)) 
                && File.Exists(Path.Combine(Environment.CurrentDirectory, commandName, $"{commandName}.csx")))
        {
            // 檢查 csx 目錄下是否有 command 的目錄及 command.csx 的檔案
            return true;
        }
        return false;
    }

    private void Console_CommandEnter(object? sender, CommandEnterArgs e)
    {
        if (e.Trigger == string.Empty) // onelinecommand 才會跑這邊
        {
            runCommand(e.Command, string.Empty);
        }
        else
        {
            runCommand(e.Trigger, e.Command);
        }
    }

    private void Console_BeforeCommandEnter(object? sender, BeforeCommandEnterArgs e)
    {
        e.TriggerSend = checkCommand(e.InputLine);
    }

    private string trimEndString(string input, string suffix)
    {
        if (input != null && suffix != null && input.EndsWith(suffix, StringComparison.CurrentCultureIgnoreCase))
        {
            return input.Substring(0, input.Length - suffix.Length);
        }
        return input??"";
    }
}