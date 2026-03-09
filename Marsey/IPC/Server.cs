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

                await using NamedPipeServerStream pipeServer = new NamedPipeServerStream(
                    name,
                    PipeDirection.Out,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipeServer.WaitForConnectionAsync();

                byte[] buffer = Encoding.UTF8.GetBytes(data);
                await pipeServer.WriteAsync(buffer);

                MarseyLogger.Log(MarseyLogger.LogType.INFO, "IPC-SERVER", $"Closing {name}");
                pipeServer.Close();
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
}
