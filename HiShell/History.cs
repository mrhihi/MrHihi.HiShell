namespace MrHihi.HiShell;

public class History
{
    public string? Buffer { get; set; }
    public string? CommandName { get; set; }
    public string? Command { get; set; }
    public string? Result { get; set; }
    public string? ConsoleOutput { get; set; }

    public History(History h)
    {
        Buffer = h.Buffer;
        CommandName = h.CommandName;
        Command = h.Command;
        Result = h.Result;
        ConsoleOutput = h.ConsoleOutput;
    }

    public History(string buffer, string commandName, string command)
    {
        Buffer = buffer;
        CommandName = commandName;
        Command = command;
    }
}