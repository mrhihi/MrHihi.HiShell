using MrHihi.HiConsole;

namespace MrHihi.HiShell.InternalCommands;

public interface IInternalCommand
{
    bool NeedExecute(string cmdname, string cmd);
    bool Execute(string cmdname, string cmd, string buffer, EnterPressArgs? epr, TextWriter console);
    void Usage();
    TextWriter ConsoleOut { get; }
    bool KeepHistory { get; }
}