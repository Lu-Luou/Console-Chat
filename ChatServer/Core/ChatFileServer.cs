using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using ChatServer.Protocol;

namespace ChatServer.Core
{
    /// <summary>
    /// Servidor de chat y transferencia de archivos
    /// </summary>
    public class ChatFileServer
    {
        private readonly int _port;
        private readonly ConcurrentDictionary<string, ConnectedClient> _clients = new();
        private readonly FileTransferManager _fileTransferManager = new();
        private TcpListener? _listener;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly object _lockObject = new object();
        private Timer? _cleanupTimer;
        private DateTime _startTime;
        private long _totalBytesTransferred = 0;

        public event EventHandler<ClientEventArgs>? ClientConnected;
        public event EventHandler<ClientEventArgs>? ClientDisconnected;
        public event EventHandler<MessageEventArgs>? MessageReceived;
        public event EventHandler<FileTransferEventArgs>? FileTransferStarted;
        public event EventHandler<FileTransferEventArgs>? FileTransferCompleted;

        public bool IsRunning { get; private set; }
        public int ConnectedClientsCount => _clients.Count;

        public ChatFileServer(int port = 8888)
        {
            _port = port;
        }

        /// <summary>
        /// Inicia el servidor
        /// </summary>
        public async Task StartAsync()
        {
            if (IsRunning) return;

            try
            {
                _listener = new TcpListener(IPAddress.Any, _port);
                _cancellationTokenSource = new CancellationTokenSource();
                
                _listener.Start();
                IsRunning = true;
                _startTime = DateTime.UtcNow; // Registrar tiempo de inicio

                Console.WriteLine($"[+] Servidor iniciado en puerto {_port}");
                
                // Timer para limpiar transferencias expiradas cada minuto
                _cleanupTimer = new Timer(CleanupExpiredTransfers, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

                // Iniciar el bucle de aceptación de clientes
                _ = Task.Run(AcceptClientsAsync, _cancellationTokenSource.Token);
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[X] Error iniciando servidor: {ex.Message}");
                IsRunning = false;
                throw;
            }
        }

        /// <summary>
        /// Detiene el servidor
        /// </summary>
        public async Task StopAsync()
        {
            if (!IsRunning) return;

            try
            {
                IsRunning = false;
                
                _cancellationTokenSource?.Cancel();
                _cleanupTimer?.Dispose();
                
                // Desconectar todos los clientes
                var clients = _clients.Values.ToList();
                foreach (var client in clients)
                {
                    await DisconnectClientAsync(client.Id, "Servidor detenido");
                }

                _listener?.Stop();
                Console.WriteLine("[!] Servidor detenido");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[X] Error deteniendo servidor: {ex.Message}");
            }
        }

        /// <summary>
        /// Acepta nuevas conexiones de clientes
        /// </summary>
        private async Task AcceptClientsAsync()
        {
            while (IsRunning && !_cancellationTokenSource!.Token.IsCancellationRequested)
            {
                try
                {
                    var tcpClient = await _listener!.AcceptTcpClientAsync();
                    var clientId = Guid.NewGuid().ToString("N")[..8];
                    
                    var client = new ConnectedClient(clientId, tcpClient);
                    
                    if (_clients.TryAdd(clientId, client))
                    {
                        Console.WriteLine($"[+] Cliente {clientId} conectado desde {tcpClient.Client.RemoteEndPoint}");
                        
                        // Iniciar el manejo del cliente en un hilo separado
                        _ = Task.Run(() => HandleClientAsync(client), client.CancellationTokenSource.Token);
                        
                        ClientConnected?.Invoke(this, new ClientEventArgs(client));
                    }
                    else
                    {
                        client.Disconnect();
                    }
                }
                catch (ObjectDisposedException)
                {
                    // El listener fue cerrado, esto es esperado al detener el servidor
                    break;
                }
                catch (Exception ex)
                {
                    if (IsRunning)
                    {
                        Console.WriteLine($"[X] Error aceptando cliente: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Maneja las comunicaciones con un cliente específico
        /// </summary>
        private async Task HandleClientAsync(ConnectedClient client)
        {
            try
            {
                while (IsRunning && client.TcpClient.Connected && !client.CancellationTokenSource.Token.IsCancellationRequested)
                {
                    var message = await client.ReceiveMessageAsync();
                    
                    if (message == null)
                    {
                        break; // Cliente desconectado
                    }

                    message.SenderId = client.Id;
                    await ProcessMessageAsync(message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[X] Error manejando cliente {client.Id}: {ex.Message}");
            }
            finally
            {
                await DisconnectClientAsync(client.Id, "Conexión perdida");
            }
        }

        /// <summary>
        /// Procesa un mensaje recibido de un cliente
        /// </summary>
        private async Task ProcessMessageAsync(Message message)
        {
            try
            {
                MessageReceived?.Invoke(this, new MessageEventArgs(message));

                switch (message)
                {
                    case ChatMessage chatMessage:
                        await HandleChatMessageAsync(chatMessage);
                        break;
                    
                    case FileStartMessage fileStartMessage:
                        await HandleFileStartAsync(fileStartMessage);
                        break;
                    
                    case FileDataMessage fileDataMessage:
                        await HandleFileDataAsync(fileDataMessage);
                        break;
                    
                    case FileEndMessage fileEndMessage:
                        await HandleFileEndAsync(fileEndMessage);
                        break;
                    
                    case ClientConnectMessage connectMessage:
                        await HandleClientConnectAsync(connectMessage);
                        break;
                    
                    case ClientDisconnectMessage disconnectMessage:
                        await HandleClientDisconnectAsync(disconnectMessage);
                        break;
                    
                    case DownloadAcceptMessage acceptMessage:
                        await HandleDownloadAcceptAsync(acceptMessage);
                        break;
                    
                    case DownloadRejectMessage rejectMessage:
                        await HandleDownloadRejectAsync(rejectMessage);
                        break;
                    
                    default:
                        Console.WriteLine($"[WARN] Tipo de mensaje desconocido: {message.Type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[X] Error procesando mensaje: {ex.Message}");
            }
        }

        /// <summary>
        /// Maneja mensajes de chat
        /// </summary>
        private async Task HandleChatMessageAsync(ChatMessage chatMessage)
        {
            Console.WriteLine($"[MSG] [{DateTime.Now:HH:mm:ss}] {GetClientName(chatMessage.SenderId)}: {chatMessage.Content}");

            if (string.IsNullOrEmpty(chatMessage.TargetClientId))
            {
                // Broadcast a todos los clientes excepto el emisor
                await BroadcastMessageAsync(chatMessage, chatMessage.SenderId);
            }
            else
            {
                // Mensaje privado
                await SendMessageToClientAsync(chatMessage.TargetClientId, chatMessage);
            }
        }

        /// <summary>
        /// Maneja inicio de transferencia de archivos
        /// </summary>
        private async Task HandleFileStartAsync(FileStartMessage fileStart)
        {
            Console.WriteLine($"📁 Iniciando transferencia: {fileStart.FileName} ({fileStart.FileSize} bytes) de {GetClientName(fileStart.SenderId)} a {GetClientName(fileStart.TargetClientId)}");

            if (_fileTransferManager.StartTransfer(fileStart))
            {
                // Reenviar el mensaje al cliente destino
                await SendMessageToClientAsync(fileStart.TargetClientId, fileStart);
                
                FileTransferStarted?.Invoke(this, new FileTransferEventArgs(fileStart.TransferId, fileStart.FileName));
            }
            else
            {
                // Enviar error al emisor
                var error = new ErrorMessage("No se pudo iniciar la transferencia", fileStart.SenderId);
                await SendMessageToClientAsync(fileStart.SenderId, error);
            }
        }

        /// <summary>
        /// Maneja bloques de datos de archivo
        /// </summary>
        private async Task HandleFileDataAsync(FileDataMessage fileData)
        {
            var result = _fileTransferManager.ProcessFileData(fileData);
            
            if (result.Success)
            {
                // Contabilizar bytes transferidos
                Interlocked.Add(ref _totalBytesTransferred, fileData.Data.Length);
                
                // Reenviar datos al cliente destino
                await SendMessageToClientAsync(fileData.TargetClientId, fileData);
                
                // Enviar ACK al emisor
                var ack = new AckMessage(fileData.TransferId, fileData.SequenceNumber, fileData.SenderId);
                await SendMessageToClientAsync(fileData.SenderId, ack);

                if (result.IsComplete)
                {
                    Console.WriteLine($"[<] Transferencia completada: {result.Transfer?.FileName}");
                    
                    // La transferencia está completa, enviar FILE_END
                    var fileEnd = new FileEndMessage(fileData.TransferId, fileData.TargetClientId, true);
                    fileEnd.SenderId = "SERVER";
                    await SendMessageToClientAsync(fileData.TargetClientId, fileEnd);
                    
                    FileTransferCompleted?.Invoke(this, new FileTransferEventArgs(fileData.TransferId, result.Transfer?.FileName ?? ""));
                }
            }
            else
            {
                var error = new ErrorMessage(result.ErrorMessage, fileData.SenderId);
                await SendMessageToClientAsync(fileData.SenderId, error);
            }
        }

        /// <summary>
        /// Maneja fin de transferencia de archivos
        /// </summary>
        private async Task HandleFileEndAsync(FileEndMessage fileEnd)
        {
            var transfer = _fileTransferManager.GetTransfer(fileEnd.TransferId);
            
            if (transfer != null)
            {
                if (fileEnd.Success)
                {
                    Console.WriteLine($"[OK] Transferencia finalizada exitosamente: {transfer.FileName}");
                }
                else
                {
                    Console.WriteLine($"[X] Transferencia fallo: {transfer.FileName} - {fileEnd.ErrorMessage}");
                }
                
                _fileTransferManager.CancelTransfer(fileEnd.TransferId);
            }

            // Reenviar mensaje al destino
            await SendMessageToClientAsync(fileEnd.TargetClientId, fileEnd);
        }

        /// <summary>
        /// Maneja conexión de cliente
        /// </summary>
        private async Task HandleClientConnectAsync(ClientConnectMessage connectMessage)
        {
            if (_clients.TryGetValue(connectMessage.SenderId, out var client))
            {
                client.Name = connectMessage.ClientName;
                Console.WriteLine($"[INFO] Cliente {connectMessage.SenderId} se identificó como: {connectMessage.ClientName}");
                
                // Enviar el ID del cliente de vuelta
                var idResponse = new ClientIdResponseMessage(connectMessage.SenderId)
                {
                    SenderId = "SERVER"
                };
                await client.SendMessageAsync(idResponse);
            }
        }

        /// <summary>
        /// Maneja desconexión de cliente
        /// </summary>
        private async Task HandleClientDisconnectAsync(ClientDisconnectMessage disconnectMessage)
        {
            await DisconnectClientAsync(disconnectMessage.SenderId, disconnectMessage.Reason);
        }

        /// <summary>
        /// Envía un mensaje a un cliente específico
        /// </summary>
        private async Task<bool> SendMessageToClientAsync(string clientId, Message message)
        {
            if (_clients.TryGetValue(clientId, out var client))
            {
                return await client.SendMessageAsync(message);
            }
            return false;
        }

        /// <summary>
        /// Envía un mensaje a todos los clientes conectados (excepto al emisor)
        /// </summary>
        private async Task BroadcastMessageAsync(Message message, string? excludeClientId = null)
        {
            var tasks = new List<Task<bool>>();
            
            foreach (var client in _clients.Values)
            {
                if (client.Id != excludeClientId)
                {
                    tasks.Add(client.SendMessageAsync(message));
                }
            }
            
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Desconecta un cliente
        /// </summary>
        private Task DisconnectClientAsync(string clientId, string reason = "")
        {
            if (_clients.TryRemove(clientId, out var client))
            {
                Console.WriteLine($"[INFO] Cliente {client.Name} ({clientId}) desconectado: {reason}");
                
                client.Disconnect();
                ClientDisconnected?.Invoke(this, new ClientEventArgs(client));
                
                // Ya no notificamos a otros clientes sobre desconexiones
            }
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Obtiene el nombre de un cliente
        /// </summary>
        private string GetClientName(string clientId)
        {
            if (_clients.TryGetValue(clientId, out var client))
            {
                return client.Name;
            }
            return clientId;
        }

        /// <summary>
        /// Limpia transferencias expiradas
        /// </summary>
        private void CleanupExpiredTransfers(object? state)
        {
            _fileTransferManager.CleanupExpiredTransfers();
        }

        /// <summary>
        /// Obtiene estadísticas del servidor
        /// </summary>
        public ServerStats GetStats()
        {
            var transferStats = _fileTransferManager.GetStats();
            var uptime = IsRunning ? (DateTime.UtcNow - _startTime).TotalMinutes : 0;
            
            return new ServerStats
            {
                ConnectedClients = _clients.Count,
                ActiveTransfers = transferStats.ActiveTransfers,
                TotalDataTransferred = _totalBytesTransferred + transferStats.TotalDataTransferred,
                UptimeMinutes = uptime
            };
        }

        /// <summary>
        /// Obtiene lista de clientes conectados
        /// </summary>
        public List<ConnectedClient> GetConnectedClients()
        {
            return _clients.Values.ToList();
        }

        /// <summary>
        /// Maneja la aceptación de una descarga
        /// </summary>
        private async Task HandleDownloadAcceptAsync(DownloadAcceptMessage acceptMessage)
        {
            Console.WriteLine($"✅ Cliente {GetClientName(acceptMessage.SenderId)} aceptó la descarga: {acceptMessage.TransferId}");
            
            if (_fileTransferManager.ConfirmTransfer(acceptMessage.TransferId))
            {
                // Obtener información de la transferencia para notificar al emisor
                var transfer = _fileTransferManager.GetTransfer(acceptMessage.TransferId);
                if (transfer != null)
                {
                    // Enviar confirmación al emisor para que comience a enviar datos
                    var confirmMessage = new UploadConfirmedMessage(acceptMessage.TransferId) { SenderId = "server" };
                    await SendMessageToClientAsync(transfer.SenderId, confirmMessage);
                }
            }
        }

        /// <summary>
        /// Maneja el rechazo de una descarga
        /// </summary>
        private Task HandleDownloadRejectAsync(DownloadRejectMessage rejectMessage)
        {
            Console.WriteLine($"❌ Cliente {GetClientName(rejectMessage.SenderId)} rechazó la descarga: {rejectMessage.TransferId}");
            
            _fileTransferManager.RejectTransfer(rejectMessage.TransferId);
            return Task.CompletedTask;
        }
    }

    // Clases para eventos
    public class ClientEventArgs : EventArgs
    {
        public ConnectedClient Client { get; }
        public ClientEventArgs(ConnectedClient client) => Client = client;
    }

    public class MessageEventArgs : EventArgs
    {
        public Message Message { get; }
        public MessageEventArgs(Message message) => Message = message;
    }

    public class FileTransferEventArgs : EventArgs
    {
        public string TransferId { get; }
        public string FileName { get; }
        public FileTransferEventArgs(string transferId, string fileName)
        {
            TransferId = transferId;
            FileName = fileName;
        }
    }

    // Estadísticas del servidor
    public class ServerStats
    {
        public int ConnectedClients { get; set; }
        public int ActiveTransfers { get; set; }
        public long TotalDataTransferred { get; set; }
        public double UptimeMinutes { get; set; }
    }
}
