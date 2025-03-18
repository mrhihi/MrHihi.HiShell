using MrHihi.HiConsole;

namespace MrHihi.HiShell.InternalCommands;
public class CmdExit: CommandBase
{
    public CmdExit(HiShell shell): base(shell) { }
    protected override string[] Aliases => new string[] { "/exit", "/q" };
    protected override bool IsShowUsage(string cmdname, string[] cmds, EnterPressArgs? epr)
    {
        return (epr == null || cmds.Length > 1 && cmds[1].IsIn(true, "help", "?"));
    }
    public override void Usage()
    {
        Console.WriteLine($"    [{string.Join(" | ", Aliases)}]: Exit HiShell.");
    }
    public override bool Run(string cmdname, string cmd, string[] cmds, string buffer, EnterPressArgs? epr)
    {
        if (epr != null)
        {
            epr.Cancel = true;
        }
        return true;
    }
}
