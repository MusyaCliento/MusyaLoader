using System.IO.Pipes;
using System.Text;
using Marsey.Misc;

namespace Marsey.IPC;

public class Server
{
    public async Task ReadySend(string name, string data)
    {
        const int maxAttempts = 5;
        const int retryDelayMs = 150;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                MarseyLogger.Log(MarseyLogger.LogType.INFO, "IPC-SERVER", $"Opening {name} (attempt {attempt}/{maxAttempts})");

                var pipeServer = new NamedPipeServerStream(
                    name,
                    PipeDirection.Out,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                var wait = pipeServer.WaitForConnectionAsync();
                _ = SendWhenConnected(pipeServer, wait, name, data);
                return;
            }
            catch (IOException ex) when (attempt < maxAttempts)
            {
                MarseyLogger.Log(MarseyLogger.LogType.WARN, "IPC-SERVER", $"Pipe {name} busy, retrying: {ex.Message}");
                await Task.Delay(retryDelayMs);
            }
        }

        MarseyLogger.Log(MarseyLogger.LogType.ERRO, "IPC-SERVER", $"Failed to open pipe {name}: all attempts exhausted.");
    }

    private static async Task SendWhenConnected(NamedPipeServerStream pipeServer, Task wait, string name, string data)
    {
        try
        {
            await wait;
            byte[] buffer = Encoding.UTF8.GetBytes(data);
            await pipeServer.WriteAsync(buffer);
            MarseyLogger.Log(MarseyLogger.LogType.INFO, "IPC-SERVER", $"Closing {name}");
        }
        catch (Exception ex)
        {
            MarseyLogger.Log(MarseyLogger.LogType.ERRO, "IPC-SERVER", $"Failed to send {name}: {ex.Message}");
        }
        finally
        {
            await pipeServer.DisposeAsync();
        }
    }
}
