using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using TextCopy;
using MrHihi.HiConsole;
using static MrHihi.HiConsole.CommandPrompt;

namespace MrHihi.HiShell;
public class HiShell
{
    public class Globals
    {
        static ShellEnvironment? _env;
        public static ShellEnvironment? Env
        {
            get => _env;
            set => _env = value;
        }
        public static string GetBuffer() => _env?.GetBuffer()??string.Empty;
        public static string[] GetCommandlineArgs() => _env?.GetCommandlineArgs()??[];
        public static TextWriter Console => _env?.Console??throw new InvalidOperationException("Console is not initialized.");
    }
    private readonly NuGetInstaller _nuget;
    private readonly Dictionary<string, Func<EnterPressArgs, string, string, string, bool>> _internalCommands;
    private readonly CommandPrompt _console;
    private readonly HistoryCollection _histories = new HistoryCollection();
    private bool clear(EnterPressArgs epr, string cmdname, string cmd, string buffer)
    {
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
    }
    private bool csx(EnterPressArgs epr, string cmdname, string cmd, string buffer)
    {
        var cmds = cmd.Split(' ');
        if (cmds.Length < 2)
        {
            Console.WriteLine("Usage: /csx <run | [list | ls]>");
            Console.WriteLine("  run: Run the specified CS from buffer.");
            return true;
        }
        if (cmds[1].ToLower() == "list" || cmds[1].ToLower() == "ls")
        {
            var csxDir = Directory.GetDirectories(Environment.CurrentDirectory);
            foreach(var csx in csxDir)
            {
                var n = Path.GetFileName(csx);
                if (n == "nuget_packages") continue;
                Console.WriteLine(n);
            }
        }
        else if (cmds[1].ToLower() == "run")
        {
            runScript(Environment.CurrentDirectory, buffer, cmdname, cmd, string.Empty).Wait();
        }
        return true;
    }
    private bool history(EnterPressArgs epr, string cmdname, string cmd, string buffer)
    {
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
                Console.WriteLine($"Console output:\n{h.ConsoleOutput}");
                Console.WriteLine();
            }
        } else if (cmds[1].ToLower() == "clear") {
            _histories.Clear();
            Console.WriteLine("History cleared.");
        } else if (cmds[1].ToLower() == "clip") {
            var last = _histories.Last();
            if (last == null) return true;
            if (string.IsNullOrEmpty(last.ConsoleOutput)) return true;
            ClipboardService.SetText(last.ConsoleOutput);
            Console.WriteLine("Result copied to clipboard.");
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
    }
    private bool help(EnterPressArgs epr, string cmdname, string cmd, string buffer)
    {
        Console.WriteLine("HiShell v1.0");
        Console.WriteLine("Commands:");
        Console.WriteLine("  /clear: Clear console output.");
        Console.WriteLine("  /clear nuget: Clear NuGet packages.");
        Console.WriteLine("  /csx <run | [list | ls]>: Run the specified CS from buffer.");
        Console.WriteLine("  /history [all | clear | clip | save <file>]: Show command history.");
        Console.WriteLine("  /exit: Exit HiShell.");
        Console.WriteLine("  /help: Show this help message.");
        Console.WriteLine();
        return true;
    }
    private bool exit(EnterPressArgs epr, string cmdname, string cmd, string buffer)
    {
        epr.Cancel = true;
        return true;
    }
    public HiShell()
    {
        var promptMessage = Environment.GetEnvironmentVariable("HiShellPromptMessage", EnvironmentVariableTarget.Process)??"─> ";
        var welcomeMessage = Environment.GetEnvironmentVariable("HiShellWelcomeMessage", EnvironmentVariableTarget.Process)??"Welcome to HiShell v1.0\nPress `Ctrl+D` to exit. Input `/help` for help.\n";
        var workingDir = Environment.GetEnvironmentVariable("HiShellWorkingDirectory", EnvironmentVariableTarget.Process)??Environment.CurrentDirectory;
        _nuget = new NuGetInstaller(workingDir);
        if (!string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir))
        {
            Environment.CurrentDirectory = workingDir;
        }
        
        _internalCommands = new Dictionary<string, Func<EnterPressArgs, string, string, string, bool>>()
        {
            { "/clear", clear },
            { "/csx", csx },
            { "/history", history },
            { "/exit", exit },
            { "/help", help }
        };
        _console = new CommandPrompt(enumChatMode.MultiLineCommand, welcomeMessage, promptMessage);
        _console.MultiLineCommand_EnterPress += Console_MultiLineCommand_EnterPress;
        _console.Start();
    }

    private void Console_MultiLineCommand_EnterPress(object? sender, EnterPressArgs e)
    {
        if ( checkCommand(e.Command) )
        {
            e.Triggered = true;
            e.WriteResult(()=>{
                runCommand(e);
            });
        }
    }

    private async Task runExternalCommand(EnterPressArgs epr, string commandName)
    {
        string command = epr.Command;
        string buffer = epr.Buffer;
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
            // Console.WriteLine(e.StackTrace);
            Console.WriteLine();
        }
    }
    private bool runInternalCommand(EnterPressArgs e, string commandName)
    {
        string command = e.Command;
        string buffer = e.Buffer;
        var lcmdName = commandName.ToLower();;
        if (_internalCommands.ContainsKey(lcmdName))
        {
            _internalCommands[lcmdName](e, lcmdName, command, trimEndString(buffer, command).TrimEnd('\n'));
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
        return CSharpScript.Create<string>(processedScript, globalsType: typeof(Globals), options: ccsOptions);
    }

    private async Task<string> runScript(string baseDir, string scriptCode, string cmdName, string command, string buffer)
    {
        var history = new History(buffer, cmdName, command);
        Console.WriteLine($"Running command: {command} ...");
        Console.WriteLine();
        var script = await createScript(scriptCode, baseDir);
        var args = new ShellEnvironment(trimEndString(buffer, command).TrimEnd('\n'), cmdName, command);
        Globals.Env = args;
        var result = (await script.RunAsync(globals: new Globals())).ReturnValue;
        history.Result = result;
        history.ConsoleOutput = args.Console.ToString();
        _histories.Add(history);
        Console.WriteLine();
        Console.WriteLine();
       return result;
    }

    private string getCommandName(string command)
    {
        var commandParts = command.Split(' ');
        var commandName = commandParts[0];
        return commandName;
    }

    private void runCommand(EnterPressArgs e)
    {
        var commandName = getCommandName(e.Command);
        if (runInternalCommand(e, commandName))
        {
            return;
        }
        runExternalCommand(e, commandName).Wait();
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

    private string trimEndString(string input, string suffix)
    {
        if (input != null && suffix != null && input.EndsWith(suffix, StringComparison.CurrentCultureIgnoreCase))
        {
            return input.Substring(0, input.Length - suffix.Length);
        }
        return input??"";
    }
}