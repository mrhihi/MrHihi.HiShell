using MrHihi.HiConsole;

namespace MrHihi.HiShell.InternalCommands;
public class CmdHelp: CommandBase
{
    public CmdHelp(HiShell shell): base(shell) { }
    protected override string[] Aliases => new string[] { "/help", "?" ,"/?" };
    protected override bool IsShowUsage(string cmdname, string[] cmds, EnterPressArgs? epr)
    {
        return (epr != null);
    }
    public override void Usage()
    {
        Console.WriteLine($"    {DisplayAliases} : Show this help.");
        Console.WriteLine();
        foreach(var ic in _shell._internalCommands.Where(x => x != this))
        {
            ic.Usage();
            Console.WriteLine();
        }
    }
    public override bool Run(string cmdname, string cmd, string[] cmds, string buffer, EnterPressArgs? epr)
    {
        Usage();
        return true;
    }
}
