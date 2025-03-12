namespace MrHihi.HiShell;

public class History
{
    public string? Buffer { get; set; }
    public string? CommandName { get; set; }
    public string? Command { get; set; }
    public string? Result { get; set; }
    public string? ConsoleOutput { get; set; }

    public History(string buffer, string commandName, string command)
    {
        Buffer = buffer;
        CommandName = commandName;
        Command = command;
    }
}