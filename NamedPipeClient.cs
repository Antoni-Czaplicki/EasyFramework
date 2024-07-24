namespace EasyFramework;

using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class NamedPipeClient
{
    public enum NamedPipeClientStatus
    {
        Connected,
        Disconnected,
        Error,
        ReceivedCount
    }

    private NamedPipeClientStream? _pipeClient;
    private int _receivedCount;
    private readonly byte[] _buffer = new byte[1024];
    private readonly int[] _retryDelays = { 100, 1000, 3000 }; // Retry delays in milliseconds
    private string _pipeName;

    #region Actions

    public Action<NamedPipeClientStatus, int> StatusAction = (_, _) => { };
    public Action<string> MessageReceivedAction = _ => { };
    public Action? ExitAction;

    #endregion

    #region Manage

    public async Task Start(string pipeName)
    {
        // Stop in case of already running
        await Stop();

        // Initialize and connect the named pipe client
        _pipeName = pipeName;
        _pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await ConnectAndStartReading();
    }

    public async Task Stop()
    {
        if (_pipeClient != null)
        {
            await _pipeClient.DisposeAsync();
            _pipeClient = null;
        }

        StatusAction.Invoke(NamedPipeClientStatus.Disconnected, 0);
    }

    private async Task ConnectAndStartReading()
    {
        if (_pipeClient == null)
            return;

        try
        {
            await _pipeClient.ConnectAsync();
            StatusAction.Invoke(NamedPipeClientStatus.Connected, 0);
            BeginRead();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error connecting to pipe: {ex.Message}");
            StatusAction.Invoke(NamedPipeClientStatus.Error, 0);
        }
    }

    #endregion

    #region Listeners

    private void BeginRead()
    {
        if (_pipeClient == null)
            return;

        Console.WriteLine("BeginRead");

        try
        {
            _pipeClient.BeginRead(_buffer, 0, _buffer.Length, OnMessageReceived, _pipeClient);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error starting read: {ex.Message}");
            StatusAction.Invoke(NamedPipeClientStatus.Error, 0);
        }
    }

    private void OnMessageReceived(IAsyncResult ar)
    {
        try
        {
            if (_pipeClient == null)
                return;

            var bytesRead = _pipeClient.EndRead(ar);

            if (bytesRead == 0)
            {
                // Disconnection detected, attempt to reconnect
                HandleDisconnection();
                return;
            }

            if (bytesRead > 0)
            {
                var message = Encoding.UTF8.GetString(_buffer, 0, bytesRead);
                if (message == "exit")
                {
                    ExitAction?.Invoke();
                    Interlocked.Increment(ref _receivedCount);
                    StatusAction(NamedPipeClientStatus.ReceivedCount, _receivedCount);
                    return;
                }

                MessageReceivedAction.Invoke(message);
                Interlocked.Increment(ref _receivedCount);
                StatusAction(NamedPipeClientStatus.ReceivedCount, _receivedCount);

                // Continue reading for more messages
                BeginRead();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error on message receive: {ex.Message}");
            StatusAction.Invoke(NamedPipeClientStatus.Error, 0);
        }
    }

    private async void HandleDisconnection()
    {
        if (_pipeClient == null)
            return;

        await _pipeClient.DisposeAsync();
        _pipeClient = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        StatusAction.Invoke(NamedPipeClientStatus.Disconnected, 0);


        await ConnectAndStartReading();
    }

    #endregion

    #region Send

    public async Task SendMessage(string message)
    {
        if (_pipeClient == null)
        {
            Debug.WriteLine("Pipe client is not connected.");
            return;
        }

        try
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            await _pipeClient.WriteAsync(buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to send message: {ex.Message}");
            StatusAction.Invoke(NamedPipeClientStatus.Error, 0);
        }
    }

    #endregion
}