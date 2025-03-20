using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using MrHihi.HiConsole;
using MrHihi.HiConsole.Draw;
using MrHihi.HiShell.InternalCommands;

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
        public static void Print(List<List<object>> data, ConsoleTableEnums.Format format = ConsoleTableEnums.Format.Alternative) => ConsoleTable.Print(data, format);
        public static void Print<T>(IEnumerable<T> data, ConsoleTableEnums.Format format = ConsoleTableEnums.Format.Alternative) => ConsoleTable.Print(data, format);
        public static void Print<T>(IEnumerable<IEnumerable<T>> tables, ConsoleTableEnums.Format format = ConsoleTableEnums.Format.Alternative) => ConsoleTable.Print(tables, format);
        public static TextWriter Console => _env?.Console??throw new InvalidOperationException("Console is not initialized.");
    }
    internal readonly NuGetInstaller _nuget;
    internal readonly List<IInternalCommand> _internalCommands;
    private readonly CommandPrompt _console;
    internal readonly HistoryCollection _histories = new HistoryCollection();
    internal readonly string _runCmdPrefix;

    public HiShell()
    {
        _runCmdPrefix = Environment.GetEnvironmentVariable("HiShellRunCmdPrefix", EnvironmentVariableTarget.Process)??"!";
        var promptMessage = Environment.GetEnvironmentVariable("HiShellPromptMessage", EnvironmentVariableTarget.Process)??"─> ";
        var welcomeMessage = Environment.GetEnvironmentVariable("HiShellWelcomeMessage", EnvironmentVariableTarget.Process)??"Welcome to HiShell v1.0\nPress `Ctrl+D` to exit. Input `/help` for help.\n";
        var workingDir = Environment.GetEnvironmentVariable("HiShellWorkingDirectory", EnvironmentVariableTarget.Process)??Environment.CurrentDirectory;

        _nuget = new NuGetInstaller(workingDir);
        if (!string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir))
        {
            Environment.CurrentDirectory = workingDir;
        }

        // 尋找 namespace InternalCommands 下所有 IInternalCommand 的實作
        _internalCommands = Assembly.GetExecutingAssembly().GetTypes()
            .Where(x => x.Namespace == "MrHihi.HiShell.InternalCommands" 
                && x.GetInterfaces().Contains(typeof(IInternalCommand))
                && !x.IsAbstract)
            .Select(x => Activator.CreateInstance(x, this) as IInternalCommand??throw new InvalidOperationException($"Cannot create instance of {x.Name}"))
            .ToList()??throw new InvalidOperationException("No internal commands found.");

        _console = new CommandPrompt(enumChatMode.MultiLineCommand, welcomeMessage, promptMessage);
        _console.MultiLineCommand_EnterPress += Console_MultiLineCommand_EnterPress;
        _console.CommandInput_TouchTop += Console_CommandInput_TouchTop;
        _console.CommandInput_TouchBottom += Console_CommandInput_TouchBottom;
        _console.Console_PrintInfo += Console_Console_PrintInfo;
        _console.Start();
    }

    private void Console_Console_PrintInfo(object? sender, PrintInfoArgs e)
    {
        e.Info = $"╭─┤{e.Info} HI/HC:{_histories.SeekIndex}/{_histories.Count}";
    }

    private void Console_CommandInput_TouchTop(object? sender, CommandTouchArgs e)
    {
        var last = _histories.SeekPrevious();
        e.Command = last?.Command??"";
        e.Setting = true;
    }

    private void Console_CommandInput_TouchBottom(object? sender, CommandTouchArgs e)
    {
        var last = _histories.SeekNext();
        e.Command = last?.Command??"";
        e.Setting = true;
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
        var scriptCode = File.ReadAllText(csxFilePath);
        var result = await runScript(scriptCode, commandName, command, buffer);
    }
    private bool runInternalCommand(EnterPressArgs e, string commandName)
    {
        string command = e.Command;
        string buffer = e.Buffer;
        var lcmdName = commandName.ToLower();;

        var ic = _internalCommands.FirstOrDefault(x => x.NeedExecute(lcmdName, command));
        if (ic == null) return false;

        var history = new History(buffer, lcmdName, command);
        try {
            ic.Execute(lcmdName, command, trimEndString(buffer, command).TrimEnd('\n'), e, new HiConsole());
        } catch (Exception ex) {
            Console.WriteLine($"Error: {ex.Message}");
            // Console.WriteLine(ex.StackTrace);
            Console.WriteLine();
        }
        history.ConsoleOutput = ic.ConsoleOut.ToString();
        if (ic.KeepHistory) _histories.Add(history);
        Console.WriteLine();
        return true;
    }

    private async Task<Script<string>> createScript(string scriptCode, string commandName)
    {
        var csxDir = Path.Combine(Environment.CurrentDirectory, commandName);
        var (processedScript, references) = await _nuget.ProcessNugetReferences(scriptCode);
        var (processedScript2, references2) = _nuget.ProcessDllReference(csxDir, processedScript);
        var srcResolver = ScriptSourceResolver.Default.WithBaseDirectory(csxDir).WithSearchPaths(csxDir);
        var scsResolver = ScriptMetadataResolver.Default.WithBaseDirectory(csxDir).WithSearchPaths(csxDir);
        var ccsOptions = ScriptOptions.Default
                            .WithFilePath(csxDir)
                            .WithMetadataResolver(scsResolver)
                            .WithSourceResolver(srcResolver)
                            // .WithEmitDebugInformation(true)
                            // .WithFileEncoding(Encoding.UTF8)
                            .AddReferences(Assembly.GetExecutingAssembly())
                            .AddReferences(references)
                            .AddReferences(references2)
                            .AddImports("System", "System.Text", "System.IO", "System.Net.Http", "System.Collections.Generic", "System.Threading.Tasks")
                            ;
        return CSharpScript.Create<string>(processedScript2, globalsType: typeof(Globals), options: ccsOptions);
    }

    internal async Task<string> runScript(string scriptCode, string cmdName, string command, string buffer)
    {
        var history = new History(buffer, cmdName, command);
        ShellEnvironment? args = null;
        string? result = null;
        Console.WriteLine($"Running command: {command} ...");
        Console.WriteLine();
        try {
            var script = await createScript(scriptCode, cmdName);
            var compiled = script.Compile();
            args = new ShellEnvironment(trimEndString(buffer, command).TrimEnd('\n'), cmdName, command);
            Globals.Env = args;
            result = (await script.RunAsync(globals: new Globals())).ReturnValue;
            history.Result = result;
        } catch (Exception ex) {
            Console.WriteLine($"Error: {ex.Message}");
            // Console.WriteLine(ex.StackTrace);
            Console.WriteLine();
        }
        history.ConsoleOutput = args?.Console?.ToString()??"";
        _histories.Add(history);
        Console.WriteLine();
        Console.WriteLine();
       return result??"";
    }

    private (string cmdname, string cmdnameWithoutPrefix, bool prefixok) getCommandName(string command)
    {
        var commandParts = command.Split(' ');
        (string cmdname, string cmdnameWithoutPrefix, bool prefixok) result = (commandParts[0], commandParts[0], false);
        if (hasCmdPrefix())
        {
            if (commandParts[0].StartsWith(_runCmdPrefix))
            {
                result.cmdnameWithoutPrefix = commandParts[0].Substring(_runCmdPrefix.Length);
                result.prefixok = true;
            }
            else
            {
                result.prefixok = false;
            }
        }
        return result;
    }

    private void runCommand(EnterPressArgs e)
    {
        var cmd = getCommandName(e.Command);
        if (runInternalCommand(e, cmd.cmdname))
        {
            return;
        }
        runExternalCommand(e, cmd.cmdnameWithoutPrefix).Wait();
    }

    private bool hasCmdPrefix()
    {
        return _runCmdPrefix.Length > 0;
    }   

    private bool checkExternalCmd(string cmdname, string cmdnameWithoutPrefix, bool prefixok)
    {

        if ((hasCmdPrefix() && prefixok) || _runCmdPrefix.Length == 0)
        {
            // 檢查 csx 目錄下是否有 command 的目錄及 command.csx 的檔案
            if (Directory.Exists(Path.Combine(Environment.CurrentDirectory, cmdnameWithoutPrefix)) 
                && File.Exists(Path.Combine(Environment.CurrentDirectory, cmdnameWithoutPrefix, $"{cmdnameWithoutPrefix}.csx")))
            {
                return true;
            }
        }
        return false;
    }

    private bool checkCommand(string command)
    {
        var cmd = getCommandName(command);
        if (_internalCommands.Any(x => x.NeedExecute(cmd.cmdname, command)))
        {
            return true;
        }
        else if (checkExternalCmd(cmd.cmdname, cmd.cmdnameWithoutPrefix, cmd.prefixok))
        {
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