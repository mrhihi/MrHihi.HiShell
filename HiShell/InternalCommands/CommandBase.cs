using MrHihi.HiConsole;
using MrHihi.HiConsole.Draw;

namespace MrHihi.HiShell.InternalCommands;
public abstract class CommandBase: IInternalCommand
{
    public TextWriter ConsoleOut { get; private set; } = new HiConsole();
    internal readonly HiShell _shell;
    public CommandBase(HiShell shell)
    {
        _shell = shell;
    }
    protected abstract bool IsShowUsage(string cmdname, string[] cmds, EnterPressArgs? epr);
    protected abstract string[] Aliases { get; }
    public abstract bool Run(string cmdname, string cmd, string[] cmds, string buffer, EnterPressArgs? epr);
    public abstract void Usage();
    protected virtual string DisplayAliases => $"[{string.Join(" | ", Aliases)}]";
    public virtual bool NeedExecute(string cmdname, string cmd)
    {
        return Aliases.Any(x => x.ToLower() == cmdname.ToLower());
    }
    public virtual bool Execute(string cmdname, string cmd, string buffer, EnterPressArgs? epr, TextWriter console)
    {
        var origOut = Console.Out;
        ConsoleOut = console;
        Console.SetOut(ConsoleOut);
        try 
        {
            var cmds = cmd.Split(' ');
            if (IsShowUsage(cmdname, cmds, epr))
            {
                Console.WriteLine("\nUsage:".Color(ConsoleColor.DarkGreen));
                Usage();
                return true;
            }
            else
            {
                return Run(cmdname, cmd, cmds, buffer, epr);
            }
        }
        finally
        {
            Console.WriteLine();
            Console.SetOut(origOut);
        }
    }
    public virtual bool KeepHistory => true;
    protected virtual void ShowInvalidArgument(string msg = "")
    {
        Console.WriteLine("Invalid argument.".Color(ConsoleColor.Red));
        if (!string.IsNullOrEmpty(msg))
        {
            Console.WriteLine(msg.Color(ConsoleColor.Red));
        }
        Console.WriteLine("\nUsage:".Color(ConsoleColor.DarkGreen));
        Usage();
    }
}