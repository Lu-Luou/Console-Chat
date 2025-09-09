using System.Net.Sockets;
using ChatServer.Protocol;

namespace ChatServer.Core
{
    /// <summary>
    /// Representa un cliente conectado al servidor
    /// </summary>
    public class ConnectedClient
    {
        public string Id { get; }
        public string Name { get; set; }
        public TcpClient TcpClient { get; }
        public NetworkStream Stream { get; }
        public DateTime ConnectedAt { get; }
        public CancellationTokenSource CancellationTokenSource { get; }
        
        private readonly object _sendLock = new object();

        public ConnectedClient(string id, TcpClient tcpClient, string name = "")
        {
            Id = id;
            Name = string.IsNullOrEmpty(name) ? $"Client_{id}" : name;
            TcpClient = tcpClient;
            Stream = tcpClient.GetStream();
            ConnectedAt = DateTime.UtcNow;
            CancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Envía un mensaje al cliente de forma thread-safe
        /// </summary>
        public Task<bool> SendMessageAsync(Message message)
        {
            try
            {
                if (!TcpClient.Connected) return Task.FromResult(false);

                var data = message.Serialize();
                
                lock (_sendLock)
                {
                    if (!TcpClient.Connected) return Task.FromResult(false);
                    
                    // Primero enviamos la longitud del mensaje
                    var lengthBytes = BitConverter.GetBytes(data.Length);
                    Stream.Write(lengthBytes, 0, 4);
                    
                    // Luego enviamos el mensaje
                    Stream.Write(data, 0, data.Length);
                    Stream.Flush();
                }
                
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error enviando mensaje a cliente {Id}: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Recibe un mensaje del cliente
        /// </summary>
        public async Task<Message?> ReceiveMessageAsync()
        {
            try
            {
                if (!TcpClient.Connected) return null;

                // Primero leemos la longitud del mensaje (4 bytes)
                var lengthBytes = new byte[4];
                int bytesRead = 0;
                while (bytesRead < 4)
                {
                    int read = await Stream.ReadAsync(lengthBytes, bytesRead, 4 - bytesRead, CancellationTokenSource.Token);
                    if (read == 0) return null; // Conexión cerrada
                    bytesRead += read;
                }

                int messageLength = BitConverter.ToInt32(lengthBytes, 0);
                if (messageLength <= 0 || messageLength > 100 * 1024 * 1024) // Máximo 100MB
                {
                    throw new InvalidDataException($"Tamaño de mensaje inválido: {messageLength}");
                }

                // Luego leemos el mensaje completo
                var messageBytes = new byte[messageLength];
                bytesRead = 0;
                while (bytesRead < messageLength)
                {
                    int read = await Stream.ReadAsync(messageBytes, bytesRead, messageLength - bytesRead, CancellationTokenSource.Token);
                    if (read == 0) return null; // Conexión cerrada
                    bytesRead += read;
                }

                return Message.Deserialize(messageBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error recibiendo mensaje de cliente {Id}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Cierra la conexión del cliente
        /// </summary>
        public void Disconnect()
        {
            try
            {
                CancellationTokenSource.Cancel();
                Stream?.Close();
                TcpClient?.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error desconectando cliente {Id}: {ex.Message}");
            }
        }

        public override string ToString()
        {
            return $"Cliente {Name} ({Id}) - Conectado desde {ConnectedAt:yyyy-MM-dd HH:mm:ss}";
        }
    }
}
