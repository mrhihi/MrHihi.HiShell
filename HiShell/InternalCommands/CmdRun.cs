using System.Diagnostics;
using MrHihi.HiConsole;

namespace MrHihi.HiShell.InternalCommands;
public class CmdRun: CommandBase
{
    public CmdRun(HiShell shell): base(shell) { }
    protected override string[] Aliases => new string[] { "~", "!" };
    protected override bool IsShowUsage(string cmdname, string[] cmds, EnterPressArgs? epr)
    {
        return (epr == null || cmds.Length > 1 && cmds[1].IsIn(true, "help", "?"));
    }
    public override void Usage()
    {
        Console.WriteLine($"    {DisplayAliases} : Run shell cli command.");
    }
    public override bool Run(string cmdname, string cmd, string[] cmds, string buffer, EnterPressArgs? epr)
    {
        string shell;

        if (OperatingSystem.IsWindows())
        {
            shell = "cmd.exe";
        }
        else
        {
            shell = "/bin/bash";
        }

        // 設定 ProcessStartInfo
        ProcessStartInfo processInfo = new ProcessStartInfo
        {
            FileName = shell,
            Arguments = $"-c \"{buffer}\"",
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

            Console.CursorLeft = 0;
            Console.WriteLine(output);
        }

        return true;
    }
    public override bool KeepHistory => false;
}
