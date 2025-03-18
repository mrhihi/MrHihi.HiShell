using MrHihi.HiConsole;

namespace MrHihi.HiShell.InternalCommands;
public class CmdNuget: CommandBase
{
    public CmdNuget(HiShell shell): base(shell) { }
    protected override string[] Aliases => new string[] { "/nuget" };
    protected override bool IsShowUsage(string cmdname, string[] cmds, EnterPressArgs? epr)
    {
        return (epr == null || cmds.Length > 1 && cmds[1].IsIn(true, "help", "?"));
    }
    public override void Usage()
    {
        Console.WriteLine($"    [{string.Join("|", Aliases)}]: [ls | remove | rm | delete | del] <nuget_package_name | all>");
        Console.WriteLine("        ls: Show cached nuget files.");
        Console.WriteLine("        [remove | delete | del]: Remove cached nuget files.");
    }
    public override bool Run(string cmdname, string cmd, string[] cmds, string buffer, EnterPressArgs? epr)
    {
        if (cmds.Length > 1) {
            if (cmds[1].ToLower() == "ls")
            {
                _shell._nuget.ShowNugetPackages();
            }
            else if (cmds.Length > 2 && cmds[1].IsIn(true, "remove", "rm", "delete", "del"))
            {
                try
                {
                    var delcount = (cmds[2].ToLower() == "all") ? _shell._nuget.CleanNugetPackages() : _shell._nuget.CleanNugetPackages(cmds[2]);
                    Console.WriteLine($"NuGet package {cmds[2]} cleared.");
                    Console.WriteLine($"Total deleted files: {delcount.delFiles}, directories: {delcount.delDirs}");
                }
                catch(Exception e)
                {
                    Console.WriteLine($"Error: {e.Message}");
                }
            }
            else
            {
                ShowInvalidArgument();
            }
        }
        else
        {
            ShowInvalidArgument();
        }
        return true;
    }
}
