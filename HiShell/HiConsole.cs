using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MrHihi.HiShell;

public class HiConsole : StringWriter
{
    public override void Write(char value)
    {
        base.Write(value);
        Console.Write(value);
    }
    public override void Write(char[] buffer, int index, int count)
    {
        base.Write(buffer, index, count);
        Console.Write(buffer, index, count);
    }
    public override void Write(ReadOnlySpan<char> buffer)
    {
        throw new NotImplementedException();
    }
    public override void Write(string? value)
    {
        base.Write(value);
        Console.Write(value);
    }
    public override void Write(StringBuilder? value)
    {
        base.Write(value);
        Console.Write(value);
    }
    public override void WriteLine(ReadOnlySpan<char> buffer)
    {
        throw new NotImplementedException();
    }
    public override void WriteLine(StringBuilder? value)
    {
        base.WriteLine(value);
        Console.WriteLine(value);
    }
    // public override async Task WriteAsync(char value)
    // {
    //     throw new NotImplementedException();
    // }
    public override Task WriteAsync(string? value)
    {
        throw new NotImplementedException();
    }
    public override Task WriteAsync(char[] buffer, int index, int count)
    {
        throw new NotImplementedException();
    }
    public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
    public override Task WriteAsync(StringBuilder? value, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
    public override Task WriteLineAsync(char value)
    {
        throw new NotImplementedException();
    }
    public override Task WriteLineAsync(string? value)
    {
        throw new NotImplementedException();
    }
    public override Task WriteLineAsync(StringBuilder? value, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
    public override Task WriteLineAsync(char[] buffer, int index, int count)
    {
        throw new NotImplementedException();
    }
    public override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
    public override Task FlushAsync()
    {
        throw new NotImplementedException();
    }
}