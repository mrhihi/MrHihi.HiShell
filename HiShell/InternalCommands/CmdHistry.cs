using System.Text;
using MrHihi.HiConsole;
using TextCopy;

namespace MrHihi.HiShell.InternalCommands;
public class CmdHistory: CommandBase
{
    public CmdHistory(HiShell shell): base(shell) { }
    protected override string[] Aliases => new string[] { "/history" };
    protected override bool IsShowUsage(string cmdname, string[] cmds, EnterPressArgs? epr)
    {
        return (epr == null || cmds.Length < 2 || cmds[1].IsIn(true, "help", "?"));
    }
    public override void Usage()
    {
        Console.WriteLine($"    [{string.Join("|", Aliases)}]: [ <number> | all | clear | clip | save <file>]");
        Console.WriteLine("        <number>: Show the specified command history.");
        Console.WriteLine("        all: Show all command history.");
        Console.WriteLine("        clear: Clear command history.");
        Console.WriteLine("        clip: Copy the last console output to clipboard.");
        Console.WriteLine("        save <file>: Save command history to file.");
    }
    private string IndentOutput(string output)
    {
        return string.Join('\n', output.Split('\n').Select(x => $"|    {x}"));
    }
    public override bool Run(string cmdname, string cmd, string[] cmds, string buffer, EnterPressArgs? epr)
    {
        if (_shell._histories.Count == 0) return true;
        if(cmds.Length == 1) {
            var last = _shell._histories.Last();
            Console.WriteLine($"Last command: {last.Command}");
            Console.WriteLine($"Result: {last.Result}");
            Console.WriteLine($"Console output: {last.ConsoleOutput}");

        } else if (cmds[1].ToLower() == "all") {
            int c = 0;
            foreach(var h in _shell._histories) {
                Console.WriteLine($"History: [{c++}]");
                Console.WriteLine($"Command: {h.Command}");
                Console.WriteLine($"Result: {h.Result}");
                var output = (h.ConsoleOutput?.ToString()??string.Empty).Split('\n').Select(x => $"|    {x}");
                Console.WriteLine($"Console output:\n{IndentOutput(h.ConsoleOutput??string.Empty)}");
                Console.WriteLine();
            }

        } else if (cmds[1].ToLower() == "clear") {
            _shell._histories.Clear();
            Console.WriteLine("History cleared.");

        } else if (cmds[1].ToLower() == "clip") {
            var last = _shell._histories.Last();
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
                foreach(var h in _shell._histories) {
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

        } else if (cmds[1].IsNumeric()) {
            var index = int.Parse(cmds[1]);
            if (index >= 0 && index < _shell._histories.Count) {
                var h = _shell._histories[index];
                Console.WriteLine($"Command: {h.Command}");
                Console.WriteLine($"Result: {h.Result}");
                Console.WriteLine($"Console output:\n{IndentOutput(h.ConsoleOutput??string.Empty)}");
            }

        } else {
            ShowInvalidArgument();
        }
        return true;
    }
}
