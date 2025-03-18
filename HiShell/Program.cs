using System.Text;
using MrHihi.HiConsole;

namespace MrHihi.HiShell;
class Program
{
    static void Main(string[] args)
    {
        if (args.Length > 0)
        {
            if (args[0] == "newsh" && args.Length > 1)
            {
                var path = args[1];
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                // get full path 
                path = Path.GetFullPath(path);
                var shellfile = Path.Combine(path, "hishell");
                if (!File.Exists(shellfile))
                {
                    File.WriteAllText(shellfile, @$"#!/bin/zsh
export HiShellPromptMessage=""[HiShell] > ""
export HiShellWelcomeMessage=""Welcome to HiShell v1.0
Press \`Ctrl+D\` to exit. Input \`/help\` for help.""

export HiShellWorkingDirectory=""{path}""

HiShell
".Replace("\r\n", "\n\n".ToCharArray().First().ToString()));
                    Console.WriteLine("HiShell Environment created.");
                }
            }
            else
            {
                Console.WriteLine("Error: Invalid argument.");
                Console.WriteLine("Usage: ");
                Console.WriteLine("  HiShell                   - Start HiShell Environment");
                Console.WriteLine("  HiShell [new] [path]      - Create a new HiShell Environment");
            }
        }
        else
        {
            var shell = new HiShell();
        }
    }
}
