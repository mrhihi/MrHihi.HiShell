using System.Text;
using MrHihi.HiConsole;
using TextCopy;

namespace MrHihi.HiShell.InternalCommands;
public class CmdHistory : CommandBase
{
    public CmdHistory(HiShell shell) : base(shell) { }
    protected override string[] Aliases => new string[] { "/history" };
    protected override bool IsShowUsage(string cmdname, string[] cmds, EnterPressArgs? epr)
    {
        return (epr == null || cmds.Length < 2 || cmds[1].IsIn(true, "help", "?"));
    }
    public override void Usage()
    {
        Console.WriteLine($"    [{string.Join("|", Aliases)}]: [ <number> | all | clear | clip <number> | clipbuff <number> | save <file>]");
        Console.WriteLine("        <number>: Show the specified command history.");
        Console.WriteLine("        all: Show all command history.");
        Console.WriteLine("        clear: Clear command history.");
        Console.WriteLine("        clip <number>: Copy the console output to clipboard. if <number> is not specified, copy the last command output.");
        Console.WriteLine("        clipbuff <number>: Copy the buffer to clipboard. if <number> is not specified, copy the last command buffer.");
        Console.WriteLine("        save <file>: Save command history to file.");
    }
    private string IndentOutput(string? output)
    {
        return string.Join('\n', (output ?? "").Split('\n').Select(x => $"│    {x}"));
    }
    public override bool KeepHistory => false;
    public StringWriter PrintHistory(History h, bool showIdentOutput = true)
    {
        Func<string?, string> show = showIdentOutput ? IndentOutput : x => x ?? "";
        var writer = new StringWriter();
        if (!string.IsNullOrEmpty(h.Buffer))
        {
            writer.WriteLine($"│ Buffer:\n{show(h.Buffer)}");
        }
        writer.WriteLine($"│ Command: {h.Command}");
        writer.WriteLine($"│ Console output:\n{show(h.ConsoleOutput)}");
        writer.WriteLine($"└{new string('─', Console.WindowWidth - 1)}");
        return writer;
    }
    public override bool Run(string cmdname, string cmd, string[] cmds, string buffer, EnterPressArgs? epr)
    {
        Console.WriteLine();
        if (_shell._histories.Count == 0)
        {
            Console.WriteLine("No history.");
            return true;
        }
        if (cmds.Length == 1)
        {
            var last = _shell._histories.Last();
            Console.WriteLine(PrintHistory(last));

        }
        else if (cmds[1].ToLower() == "all")
        {
            int c = 0;
            foreach (var h in _shell._histories)
            {
                Console.WriteLine($"┌ History: [{c++}]");
                Console.WriteLine(PrintHistory(h));
            }

        }
        else if (cmds[1].ToLower() == "clear")
        {
            _shell._histories.Clear();
            Console.WriteLine("History cleared.");

        }
        else if (cmds[1].ToLower() == "clipbuff")
        {
            if (cmds.Length == 3)
            {
                if (cmds[2].IsNumeric())
                {
                    var index = int.Parse(cmds[2]);
                    if (index >= 0 && index < _shell._histories.Count)
                    {
                        var h = _shell._histories[index];
                        ClipboardService.SetText(h.Buffer??"");
                        Console.WriteLine("Buffer copied to clipboard.");
                    }
                }
                else
                {
                    ShowInvalidArgument();
                }
            }
            else if (cmds.Length == 2)
            {
                var last = _shell._histories.LastOrDefault();
                if (last == null) return true;
                if (string.IsNullOrEmpty(last.Buffer)) return true;
                ClipboardService.SetText(last.Buffer);
                Console.WriteLine("Result copied to clipboard.");
            }
            else
            {
                ShowInvalidArgument();
            }
        }
        else if (cmds[1].ToLower() == "clip")
        {
            if (cmds.Length == 3)
            {
                if (cmds[2].IsNumeric())
                {
                    var index = int.Parse(cmds[2]);
                    if (index >= 0 && index < _shell._histories.Count)
                    {
                        var h = _shell._histories[index];
                        ClipboardService.SetText(h.ConsoleOutput??"");
                        Console.WriteLine("Result copied to clipboard.");
                    }
                }
                else
                {
                    ShowInvalidArgument();
                }
            }
            else if (cmds.Length == 2)
            {
                var last = _shell._histories.Last();
                if (last == null) return true;
                if (string.IsNullOrEmpty(last.ConsoleOutput)) return true;
                ClipboardService.SetText(last.ConsoleOutput);
                Console.WriteLine("Result copied to clipboard.");
            }
            else
            {
                ShowInvalidArgument();
            }
        }
        else if (cmds[1].ToLower() == "save")
        {
            if (cmds.Length < 3)
            {
                Console.WriteLine();
                Console.WriteLine("Please specify the file name.");
                return true;
            }

            var filePath = Path.GetFullPath(cmds[2], "/").Remove(0, 1);
            filePath = filePath.Replace("~/", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/");

            Console.WriteLine($"Saving history to {filePath} ...");
            try
            {
                StringBuilder sb = new StringBuilder();
                foreach (var h in _shell._histories)
                {
                    sb.AppendLine(PrintHistory(h).ToString());
                    sb.AppendLine();
                }
                File.WriteAllText(filePath, sb.ToString());
                Console.WriteLine($"History saved to {filePath}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.Message}");
            }

        }
        else if (cmds[1].IsNumeric())
        {
            var index = int.Parse(cmds[1]);
            if (index >= 0 && index < _shell._histories.Count)
            {
                var h = _shell._histories[index];
                Console.WriteLine(PrintHistory(h));
            }

        }
        else
        {
            ShowInvalidArgument();
        }
        return true;
    }
}
