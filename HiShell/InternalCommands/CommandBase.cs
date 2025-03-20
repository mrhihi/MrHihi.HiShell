using MrHihi.HiConsole;

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
                Console.WriteLine("\nUsage:");
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
            Console.SetOut(origOut);
        }
    }
    public virtual bool KeepHistory => true;
    protected virtual void ShowInvalidArgument()
    {
        Console.WriteLine("Invalid argument.");
        Console.WriteLine("\nUsage:");
        Usage();
    }
}