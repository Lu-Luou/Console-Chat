using System.Net.Sockets;
using ChatClient.Protocol;

namespace ChatClient.Core
{
    /// <summary>
    /// Cliente para conectarse al servidor de chat y transferencia de archivos
    /// </summary>
    public class ChatFileClient
    {
        private readonly string _serverHost;
        private readonly int _serverPort;
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly object _sendLock = new object();
        private readonly Dictionary<string, FileTransferInfo> _activeTransfers = new();
        private readonly string _storageDirectory;

        public string ClientName { get; set; } = "Cliente";
        public bool IsConnected => _tcpClient?.Connected == true;
        
        public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
        public event EventHandler<FileTransferEventArgs>? FileTransferStarted;
        public event EventHandler<FileTransferEventArgs>? FileTransferCompleted;
        public event EventHandler? Connected;
        public event EventHandler? Disconnected;

        public ChatFileClient(string serverHost = "localhost", int serverPort = 8888, string clientName = "Cliente")
        {
            _serverHost = serverHost;
            _serverPort = serverPort;
            ClientName = clientName;
            
            // Crear directorio de almacenamiento
            _storageDirectory = Path.Combine(Directory.GetCurrentDirectory(), "storage");
            if (!Directory.Exists(_storageDirectory))
            {
                Directory.CreateDirectory(_storageDirectory);
            }
        }

        /// <summary>
        /// Conecta al servidor
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            try
            {
                if (IsConnected) return true;

                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(_serverHost, _serverPort);
                _stream = _tcpClient.GetStream();
                _cancellationTokenSource = new CancellationTokenSource();

                Console.WriteLine($"✅ Conectado al servidor {_serverHost}:{_serverPort}");

                // Enviar mensaje de conexión
                var connectMessage = new ClientConnectMessage(ClientName);
                await SendMessageAsync(connectMessage);

                // Iniciar el bucle de recepción de mensajes
                _ = Task.Run(ReceiveMessagesAsync, _cancellationTokenSource.Token);

                Connected?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error conectando al servidor: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Desconecta del servidor
        /// </summary>
        public Task DisconnectAsync()
        {
            try
            {
                if (!IsConnected) return Task.CompletedTask;

                _cancellationTokenSource?.Cancel();
                
                // Limpiar transferencias activas
                foreach (var transfer in _activeTransfers.Values)
                {
                    transfer.FileStream?.Dispose();
                }
                _activeTransfers.Clear();

                _stream?.Close();
                _tcpClient?.Close();

                Console.WriteLine("🔌 Desconectado del servidor");
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error desconectando: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Envía un mensaje de chat
        /// </summary>
        public async Task<bool> SendChatMessageAsync(string message, string targetClientId = "")
        {
            if (!IsConnected) return false;

            var chatMessage = new ChatMessage(message, targetClientId);
            return await SendMessageAsync(chatMessage);
        }

        /// <summary>
        /// Envía un archivo a otro cliente
        /// </summary>
        public async Task<bool> SendFileAsync(string filePath, string targetClientId)
        {
            try
            {
                if (!IsConnected) return false;
                if (!File.Exists(filePath)) return false;

                var fileInfo = new FileInfo(filePath);
                var fileName = fileInfo.Name;
                var fileSize = fileInfo.Length;

                Console.WriteLine($"📤 Enviando archivo: {fileName} ({FormatBytes(fileSize)}) a cliente {targetClientId}");

                // Enviar mensaje de inicio
                var fileStart = new FileStartMessage(fileName, fileSize, targetClientId);
                if (!await SendMessageAsync(fileStart))
                {
                    Console.WriteLine("❌ Error enviando mensaje FILE_START");
                    return false;
                }

                // Leer y enviar archivo en chunks
                const int chunkSize = 8192;
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                var buffer = new byte[chunkSize];
                int sequenceNumber = 0;
                int bytesRead;

                while ((bytesRead = await fileStream.ReadAsync(buffer, 0, chunkSize)) > 0)
                {
                    var chunk = new byte[bytesRead];
                    Array.Copy(buffer, chunk, bytesRead);

                    var fileData = new FileDataMessage(fileStart.TransferId, chunk, sequenceNumber, targetClientId);
                    
                    if (!await SendMessageAsync(fileData))
                    {
                        Console.WriteLine($"❌ Error enviando chunk {sequenceNumber}");
                        return false;
                    }

                    sequenceNumber++;
                    
                    // Pequeña pausa para no saturar la red
                    await Task.Delay(10);
                }

                // Enviar mensaje de fin
                var fileEnd = new FileEndMessage(fileStart.TransferId, targetClientId, true);
                await SendMessageAsync(fileEnd);

                Console.WriteLine($"✅ Archivo enviado: {fileName}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error enviando archivo: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Envía un mensaje al servidor
        /// </summary>
        private Task<bool> SendMessageAsync(Message message)
        {
            try
            {
                if (!IsConnected || _stream == null) return Task.FromResult(false);

                var data = message.Serialize();
                
                lock (_sendLock)
                {
                    if (!IsConnected || _stream == null) return Task.FromResult(false);
                    
                    // Enviar longitud del mensaje
                    var lengthBytes = BitConverter.GetBytes(data.Length);
                    _stream.Write(lengthBytes, 0, 4);
                    
                    // Enviar mensaje
                    _stream.Write(data, 0, data.Length);
                    _stream.Flush();
                }
                
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error enviando mensaje: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Bucle de recepción de mensajes
        /// </summary>
        private async Task ReceiveMessagesAsync()
        {
            try
            {
                while (IsConnected && !_cancellationTokenSource!.Token.IsCancellationRequested)
                {
                    var message = await ReceiveMessageAsync();
                    if (message == null) break;

                    await ProcessReceivedMessageAsync(message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error en bucle de recepción: {ex.Message}");
            }
            finally
            {
                await DisconnectAsync();
            }
        }

        /// <summary>
        /// Recibe un mensaje del servidor
        /// </summary>
        private async Task<Message?> ReceiveMessageAsync()
        {
            try
            {
                if (!IsConnected || _stream == null) return null;

                // Leer longitud del mensaje
                var lengthBytes = new byte[4];
                int bytesRead = 0;
                while (bytesRead < 4)
                {
                    int read = await _stream.ReadAsync(lengthBytes, bytesRead, 4 - bytesRead, _cancellationTokenSource!.Token);
                    if (read == 0) return null;
                    bytesRead += read;
                }

                int messageLength = BitConverter.ToInt32(lengthBytes, 0);
                if (messageLength <= 0 || messageLength > 10 * 1024 * 1024)
                {
                    throw new InvalidDataException($"Tamaño de mensaje inválido: {messageLength}");
                }

                // Leer mensaje completo
                var messageBytes = new byte[messageLength];
                bytesRead = 0;
                while (bytesRead < messageLength)
                {
                    if (_stream == null || _cancellationTokenSource == null) return null;
                    int read = await _stream.ReadAsync(messageBytes, bytesRead, messageLength - bytesRead, _cancellationTokenSource.Token);
                    if (read == 0) return null;
                    bytesRead += read;
                }

                // Deserializar mensaje con manejo especial para FILE_DATA y FILE_END
                var messageType = (MessageType)messageBytes[0];
                return messageType switch
                {
                    MessageType.FILE_DATA => DeserializeFileData(messageBytes),
                    MessageType.FILE_END => DeserializeFileEnd(messageBytes),
                    _ => Message.Deserialize(messageBytes)
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error recibiendo mensaje: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Deserializa un mensaje FILE_DATA
        /// </summary>
        private FileDataMessage? DeserializeFileData(byte[] data)
        {
            try
            {
                using var ms = new MemoryStream(data);
                using var reader = new BinaryReader(ms);
                
                var type = (MessageType)reader.ReadByte();
                if (type != MessageType.FILE_DATA) return null;
                
                var senderLength = reader.ReadInt32();
                var senderBytes = reader.ReadBytes(senderLength);
                var senderId = System.Text.Encoding.UTF8.GetString(senderBytes);
                
                var targetLength = reader.ReadInt32();
                var targetBytes = reader.ReadBytes(targetLength);
                var targetId = System.Text.Encoding.UTF8.GetString(targetBytes);
                
                var transferIdLength = reader.ReadInt32();
                var transferIdBytes = reader.ReadBytes(transferIdLength);
                var transferId = System.Text.Encoding.UTF8.GetString(transferIdBytes);
                
                var sequenceNumber = reader.ReadInt32();
                var dataLength = reader.ReadInt32();
                var fileData = reader.ReadBytes(dataLength);
                
                return new FileDataMessage(transferId, fileData, sequenceNumber, targetId) 
                { 
                    SenderId = senderId 
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Deserializa un mensaje FILE_END
        /// </summary>
        private FileEndMessage? DeserializeFileEnd(byte[] data)
        {
            try
            {
                using var ms = new MemoryStream(data);
                using var reader = new BinaryReader(ms);
                
                var type = (MessageType)reader.ReadByte();
                if (type != MessageType.FILE_END) return null;
                
                var senderLength = reader.ReadInt32();
                var senderBytes = reader.ReadBytes(senderLength);
                var senderId = System.Text.Encoding.UTF8.GetString(senderBytes);
                
                var targetLength = reader.ReadInt32();
                var targetBytes = reader.ReadBytes(targetLength);
                var targetId = System.Text.Encoding.UTF8.GetString(targetBytes);
                
                var transferIdLength = reader.ReadInt32();
                var transferIdBytes = reader.ReadBytes(transferIdLength);
                var transferId = System.Text.Encoding.UTF8.GetString(transferIdBytes);
                
                var success = reader.ReadBoolean();
                var errorLength = reader.ReadInt32();
                var errorBytes = reader.ReadBytes(errorLength);
                var errorMessage = System.Text.Encoding.UTF8.GetString(errorBytes);
                
                return new FileEndMessage(transferId, targetId, success, errorMessage) 
                { 
                    SenderId = senderId 
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Procesa un mensaje recibido
        /// </summary>
        private async Task ProcessReceivedMessageAsync(Message message)
        {
            try
            {
                MessageReceived?.Invoke(this, new MessageReceivedEventArgs(message));

                switch (message)
                {
                    case ChatMessage chatMessage:
                        await HandleChatMessageAsync(chatMessage);
                        break;
                    
                    case FileStartMessage fileStart:
                        await HandleFileStartAsync(fileStart);
                        break;
                    
                    case FileDataMessage fileData:
                        await HandleFileDataAsync(fileData);
                        break;
                    
                    case FileEndMessage fileEnd:
                        await HandleFileEndAsync(fileEnd);
                        break;
                    
                    case AckMessage ack:
                        await HandleAckAsync(ack);
                        break;
                    
                    case ErrorMessage error:
                        await HandleErrorAsync(error);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error procesando mensaje: {ex.Message}");
            }
        }

        private Task HandleChatMessageAsync(ChatMessage chatMessage)
        {
            if (chatMessage.SenderId == "SERVER")
            {
                Console.WriteLine($"🔔 {chatMessage.Content}");
            }
            else
            {
                Console.WriteLine($"💬 [{DateTime.Now:HH:mm:ss}] {chatMessage.SenderId}: {chatMessage.Content}");
            }
            return Task.CompletedTask;
        }

        private Task HandleFileStartAsync(FileStartMessage fileStart)
        {
            try
            {
                Console.WriteLine($"📥 Recibiendo archivo: {fileStart.FileName} ({FormatBytes(fileStart.FileSize)}) de {fileStart.SenderId}");
                
                // Crear ruta única para el archivo (evitar sobrescribir)
                var fileName = fileStart.FileName;
                var filePath = Path.Combine(_storageDirectory, fileName);
                var counter = 1;
                
                while (File.Exists(filePath))
                {
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    var extension = Path.GetExtension(fileName);
                    var newFileName = $"{nameWithoutExt}_{counter}{extension}";
                    filePath = Path.Combine(_storageDirectory, newFileName);
                    counter++;
                }
                
                // Crear info de transferencia
                var transferInfo = new FileTransferInfo(fileStart.TransferId, fileName, fileStart.FileSize, fileStart.SenderId);
                transferInfo.FileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                
                _activeTransfers[fileStart.TransferId] = transferInfo;
                
                Console.WriteLine($"📤 Transferencia iniciada: {Path.GetFileName(filePath)}");
                FileTransferStarted?.Invoke(this, new FileTransferEventArgs(fileStart.TransferId, fileStart.FileName));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error iniciando transferencia: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }

        private async Task HandleFileDataAsync(FileDataMessage fileData)
        {
            try
            {
                if (_activeTransfers.TryGetValue(fileData.TransferId, out var transferInfo))
                {
                    // Verificar secuencia esperada
                    if (fileData.SequenceNumber == transferInfo.ExpectedSequence)
                    {
                        // Escribir datos al archivo
                        await transferInfo.FileStream!.WriteAsync(fileData.Data, 0, fileData.Data.Length);
                        await transferInfo.FileStream.FlushAsync();
                        
                        transferInfo.ExpectedSequence++;
                        transferInfo.BytesReceived += fileData.Data.Length;
                        
                        var progress = (double)transferInfo.BytesReceived / transferInfo.FileSize * 100;
                        Console.WriteLine($"📦 Recibido chunk {fileData.SequenceNumber} ({fileData.Data.Length} bytes) - {progress:F1}%");
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ Chunk fuera de orden: esperado {transferInfo.ExpectedSequence}, recibido {fileData.SequenceNumber}");
                    }
                }
                else
                {
                    Console.WriteLine($"⚠️ Transferencia no encontrada: {fileData.TransferId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error escribiendo datos del archivo: {ex.Message}");
            }
        }

        private Task HandleFileEndAsync(FileEndMessage fileEnd)
        {
            try
            {
                if (_activeTransfers.TryGetValue(fileEnd.TransferId, out var transferInfo))
                {
                    // Cerrar y liberar el archivo
                    transferInfo.FileStream?.Dispose();
                    
                    if (fileEnd.Success)
                    {
                        var filePath = Path.Combine(_storageDirectory, transferInfo.FileName);
                        Console.WriteLine($"✅ Transferencia completada: {transferInfo.FileName}");
                        Console.WriteLine($"✅ Archivo guardado en: {filePath}");
                    }
                    else
                    {
                        Console.WriteLine($"❌ Transferencia falló: {fileEnd.ErrorMessage}");
                        
                        // Eliminar archivo parcial si existe
                        var filePath = Path.Combine(_storageDirectory, transferInfo.FileName);
                        if (File.Exists(filePath))
                        {
                            try
                            {
                                File.Delete(filePath);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"⚠️ No se pudo eliminar archivo parcial: {ex.Message}");
                            }
                        }
                    }
                    
                    // Remover de transferencias activas
                    _activeTransfers.Remove(fileEnd.TransferId);
                }
                else
                {
                    Console.WriteLine($"⚠️ Transferencia no encontrada para finalizar: {fileEnd.TransferId}");
                }
                
                FileTransferCompleted?.Invoke(this, new FileTransferEventArgs(fileEnd.TransferId, ""));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error finalizando transferencia: {ex.Message}");
            }
            
            return Task.CompletedTask;
        }

        private Task HandleAckAsync(AckMessage ack)
        {
            Console.WriteLine($"✓ ACK recibido para chunk {ack.SequenceNumber}");
            return Task.CompletedTask;
        }

        private Task HandleErrorAsync(ErrorMessage error)
        {
            Console.WriteLine($"❌ Error del servidor: {error.ErrorDescription}");
            return Task.CompletedTask;
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            
            return $"{number:n1} {suffixes[counter]}";
        }
    }

    // Event args
    public class MessageReceivedEventArgs : EventArgs
    {
        public Message Message { get; }
        public MessageReceivedEventArgs(Message message) => Message = message;
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

    /// <summary>
    /// Mensaje con bloque de datos de archivo
    /// </summary>
    public class FileDataMessage : Message
    {
        public string TransferId { get; set; }
        public byte[] Data { get; set; }
        public int SequenceNumber { get; set; }
        public string TargetClientId { get; set; }

        public FileDataMessage(string transferId, byte[] data, int sequenceNumber, string targetClientId) : base(MessageType.FILE_DATA)
        {
            TransferId = transferId;
            Data = data;
            SequenceNumber = sequenceNumber;
            TargetClientId = targetClientId;
        }

        public override byte[] Serialize()
        {
            var senderBytes = System.Text.Encoding.UTF8.GetBytes(SenderId);
            var targetBytes = System.Text.Encoding.UTF8.GetBytes(TargetClientId);
            var transferIdBytes = System.Text.Encoding.UTF8.GetBytes(TransferId);
            
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            
            writer.Write((byte)Type);
            writer.Write(senderBytes.Length);
            writer.Write(senderBytes);
            writer.Write(targetBytes.Length);
            writer.Write(targetBytes);
            writer.Write(transferIdBytes.Length);
            writer.Write(transferIdBytes);
            writer.Write(SequenceNumber);
            writer.Write(Data.Length);
            writer.Write(Data);
            
            return ms.ToArray();
        }
    }

    /// <summary>
    /// Mensaje de fin de transferencia de archivo
    /// </summary>
    public class FileEndMessage : Message
    {
        public string TransferId { get; set; }
        public string TargetClientId { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public FileEndMessage(string transferId, string targetClientId, bool success = true, string errorMessage = "") : base(MessageType.FILE_END)
        {
            TransferId = transferId;
            TargetClientId = targetClientId;
            Success = success;
            ErrorMessage = errorMessage;
        }

        public override byte[] Serialize()
        {
            var senderBytes = System.Text.Encoding.UTF8.GetBytes(SenderId);
            var targetBytes = System.Text.Encoding.UTF8.GetBytes(TargetClientId);
            var transferIdBytes = System.Text.Encoding.UTF8.GetBytes(TransferId);
            var errorBytes = System.Text.Encoding.UTF8.GetBytes(ErrorMessage);
            
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            
            writer.Write((byte)Type);
            writer.Write(senderBytes.Length);
            writer.Write(senderBytes);
            writer.Write(targetBytes.Length);
            writer.Write(targetBytes);
            writer.Write(transferIdBytes.Length);
            writer.Write(transferIdBytes);
            writer.Write(Success);
            writer.Write(errorBytes.Length);
            writer.Write(errorBytes);
            
            return ms.ToArray();
        }
    }

    /// <summary>
    /// Información de transferencia de archivo en curso
    /// </summary>
    public class FileTransferInfo
    {
        public string TransferId { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public string SenderId { get; set; }
        public FileStream? FileStream { get; set; }
        public int ExpectedSequence { get; set; }
        public long BytesReceived { get; set; }

        public FileTransferInfo(string transferId, string fileName, long fileSize, string senderId)
        {
            TransferId = transferId;
            FileName = fileName;
            FileSize = fileSize;
            SenderId = senderId;
            ExpectedSequence = 0;
            BytesReceived = 0;
        }
    }
}
