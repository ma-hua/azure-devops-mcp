using System.Text;

namespace AzureMcp.Server.Protocol;

public sealed class StdioMessageIO
{
    private readonly StreamReader _reader;
    private readonly Stream _output;

    public StdioMessageIO(Stream input, Stream output)
    {
        _reader = new StreamReader(input, Encoding.UTF8);
        _output = output;
    }

    public async Task<string?> ReadMessageAsync(CancellationToken cancellationToken)
    {
        var line = await _reader.ReadLineAsync(cancellationToken);
        if (line is null)
        {
            return null;
        }

        // Skip empty lines
        while (line.Length == 0)
        {
            line = await _reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                return null;
            }
        }

        return line;
    }

    public async Task WriteMessageAsync(string json, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(json + "\n");
        await _output.WriteAsync(bytes, cancellationToken);
        await _output.FlushAsync(cancellationToken);
    }

}
