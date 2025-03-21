using MrHihi.HiConsole;

namespace MrHihi.HiShell.InternalCommands;
public class CmdRollup: CommandBase
{
    public CmdRollup(HiShell shell): base(shell) { }
    protected override string[] Aliases => new string[] { "/rollup", "/roll" };
    protected override bool IsShowUsage(string cmdname, string[] cmds, EnterPressArgs? epr)
    {
        return (epr == null || cmds.Length > 1 && cmds[1].IsIn(true, "help", "?"));
    }
    public override void Usage()
    {
        Console.WriteLine($"    {DisplayAliases} : Roll up the screen.");
    }
    public override bool Run(string cmdname, string cmd, string[] cmds, string buffer, EnterPressArgs? epr)
    {
        if (cmds.Length > 1) {
            ShowInvalidArgument();
        }
        else
        {
            for (int i = 0; i < Console.WindowHeight - 2; i++)
            {
                Console.WriteLine();
            }
            Console.CursorTop = 0;
            Console.Write(new string(' ', Console.WindowWidth - 1));
            Console.Write(new string('\b', Console.WindowWidth - 1));
            _shell._console.TextArea.Reset();
        }
        return true;
    }
    public override bool KeepHistory => false;
}
