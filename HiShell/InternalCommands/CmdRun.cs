using System.Diagnostics;
using MrHihi.HiConsole;

namespace MrHihi.HiShell.InternalCommands;
public class CmdRun: CommandBase
{
    private readonly string _shebang;
    public CmdRun(HiShell shell): base(shell)
    {
        _shebang = Environment.GetEnvironmentVariable("HiShellShebang", EnvironmentVariableTarget.Process)??"/bin/bash";
    }
    protected override string[] Aliases => new string[] { "~" };
    protected override bool IsShowUsage(string cmdname, string[] cmds, EnterPressArgs? epr)
    {
        return (epr == null || cmds.Length > 1 && cmds[1].IsIn(true, "help", "?"));
    }
    public override void Usage()
    {
        Console.WriteLine($"    {DisplayAliases}<cli cmd> : Run shell cli command.");
    }
    public override bool NeedExecute(string cmdname, string cmd)
    {
        return Aliases.Any(x => cmdname.ToLower().StartsWith(x.ToLower()) && cmdname.Length > 1);
    }
    public override bool Run(string cmdname, string cmd, string[] cmds, string buffer, EnterPressArgs? epr)
    {
        string shell;
        var cliCmd = cmd.Substring(1).Trim();

        shell = _shebang;

        // 設定 ProcessStartInfo
        ProcessStartInfo processInfo = new ProcessStartInfo
        {
            FileName = shell,
            Arguments = $"-c \"{cliCmd}\"",
            RedirectStandardOutput = true, // 捕獲輸出
            UseShellExecute = false,       // 不使用系統 shell 直接執行
            CreateNoWindow = true          // 不顯示命令視窗
        };

        // 啟動進程
        using (Process process = new Process())
        {
            process.StartInfo = processInfo;
            process.Start();

            // 讀取輸出
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            Console.WriteLine(output);
        }

        return true;
    }
    public override bool KeepHistory => true;
}
