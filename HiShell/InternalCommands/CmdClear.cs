using MrHihi.HiConsole;

namespace MrHihi.HiShell.InternalCommands;
public class CmdClear: CommandBase
{
    public CmdClear(HiShell shell): base(shell) { }
    protected override string[] Aliases => new string[] { "/clear", "/cls" };
    protected override bool IsShowUsage(string cmdname, string[] cmds, EnterPressArgs? epr)
    {
        return (epr == null || cmds.Length > 1 && cmds[1].IsIn(true, "help", "?"));
    }
    public override void Usage()
    {
        Console.WriteLine($"    [{string.Join("|", Aliases)}]: Clean screen.");
    }
    public override bool Run(string cmdname, string cmd, string[] cmds, string buffer, EnterPressArgs? epr)
    {
        if (cmds.Length > 1) {
            ShowInvalidArgument();
        }
        else
        {
            System.Console.Clear();
        }
        return true;
    }
    public override bool KeepHistory => false;
}
