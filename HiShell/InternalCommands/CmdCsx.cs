using MrHihi.HiConsole;

namespace MrHihi.HiShell.InternalCommands;
public class CmdCsx: CommandBase
{
    public CmdCsx(HiShell shell): base(shell) { }
    protected override string[] Aliases => new string[] { "/csx" };
    protected override bool IsShowUsage(string cmdname, string[] cmds, EnterPressArgs? epr)
    {
        return (epr == null || cmds.Length < 2 || cmds[1].IsIn(true, "help", "?"));
    }
    public override void Usage()
    {
        Console.WriteLine($"    {DisplayAliases} [run | list | ls] :");
        Console.WriteLine("        run : Run the specified CS from buffer.");
        Console.WriteLine("        list, ls : List Predefined csx script.");
    }
    public override bool Run(string cmdname, string cmd, string[] cmds, string buffer, EnterPressArgs? epr)
    {
        if (cmds[1].ToLower() == "list" || cmds[1].ToLower() == "ls")
        {
            var csxDir = Directory.GetDirectories(Environment.CurrentDirectory);
            foreach(var csx in csxDir)
            {
                var n = Path.GetFileName(csx);
                if (n == "nuget_packages") continue;
                if (!File.Exists(Path.Combine(csx, $"{n}.csx"))) continue;
                Console.WriteLine(n);
            }
        }
        else if (cmds[1].ToLower() == "run")
        {
            _shell.runScript(buffer, cmdname, cmd, string.Empty).Wait();
        }
        else
        {
            ShowInvalidArgument();
        }
        return true;
    }
}
