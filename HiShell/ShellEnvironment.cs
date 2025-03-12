namespace MrHihi.HiShell;
public class ShellEnvironment
{
    List<string> _args = new List<string>();
    private string _buffer;
    private string _command;
    private string _cmdName;
    public ShellEnvironment(string buffer, string cmdName, string command)
    {
        _buffer = buffer;
        _cmdName = cmdName;
        _command = cmdName;
        _args.AddRange(command.Split(' '));
        Console = new HiConsole();
    }
    public string GetBuffer()
    {
        return _buffer;
    }
    public string[] GetCommandlineArgs()
    {
        return _args.ToArray();
    }
    public TextWriter Console { get; set; }
}
