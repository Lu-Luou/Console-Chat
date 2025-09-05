using System.Text;

namespace ChatClient.Protocol
{
    /// <summary>
    /// Tipos de mensajes que se pueden enviar entre cliente y servidor
    /// </summary>
    public enum MessageType : byte
    {
        /// <summary>
        /// Mensaje de chat normal
        /// </summary>
        CHAT = 0x01,
        
        /// <summary>
        /// Inicio de transferencia de archivo
        /// </summary>
        FILE_START = 0x02,
        
        /// <summary>
        /// Bloque de datos de archivo
        /// </summary>
        FILE_DATA = 0x03,
        
        /// <summary>
        /// Fin de transferencia de archivo
        /// </summary>
        FILE_END = 0x04,
        
        /// <summary>
        /// Confirmación de recepción
        /// </summary>
        ACK = 0x05,
        
        /// <summary>
        /// Error en la transferencia
        /// </summary>
        ERROR = 0x06,
        
        /// <summary>
        /// Cliente se está conectando
        /// </summary>
        CLIENT_CONNECT = 0x07,
        
        /// <summary>
        /// Cliente se está desconectando
        /// </summary>
        CLIENT_DISCONNECT = 0x08
    }

    /// <summary>
    /// Clase base para todos los mensajes del protocolo
    /// </summary>
    public abstract class Message
    {
        public MessageType Type { get; protected set; }
        public DateTime Timestamp { get; protected set; }
        public string SenderId { get; set; } = string.Empty;

        protected Message(MessageType type)
        {
            Type = type;
            Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Serializa el mensaje a bytes para envío por red
        /// </summary>
        public abstract byte[] Serialize();

        /// <summary>
        /// Deserializa un mensaje desde bytes
        /// </summary>
        public static Message? Deserialize(byte[] data)
        {
            if (data.Length < 1) return null;

            MessageType type = (MessageType)data[0];
            
            return type switch
            {
                MessageType.CHAT => ChatMessage.DeserializeChat(data),
                MessageType.FILE_START => FileStartMessage.DeserializeFileStart(data),
                MessageType.FILE_DATA => null, // Se implementa en ChatFileClient.cs
                MessageType.FILE_END => null, // Se implementa en ChatFileClient.cs
                MessageType.ACK => AckMessage.DeserializeAck(data),
                MessageType.ERROR => ErrorMessage.DeserializeError(data),
                MessageType.CLIENT_CONNECT => ClientConnectMessage.DeserializeClientConnect(data),
                MessageType.CLIENT_DISCONNECT => null, // No necesario en cliente
                _ => null
            };
        }
    }

    /// <summary>
    /// Mensaje de chat normal
    /// </summary>
    public class ChatMessage : Message
    {
        public string Content { get; set; }
        public string TargetClientId { get; set; } = string.Empty; // Si está vacío, es broadcast

        public ChatMessage(string content, string targetClientId = "") : base(MessageType.CHAT)
        {
            Content = content;
            TargetClientId = targetClientId;
        }

        public override byte[] Serialize()
        {
            var contentBytes = Encoding.UTF8.GetBytes(Content);
            var senderBytes = Encoding.UTF8.GetBytes(SenderId);
            var targetBytes = Encoding.UTF8.GetBytes(TargetClientId);
            
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            
            writer.Write((byte)Type);
            writer.Write(senderBytes.Length);
            writer.Write(senderBytes);
            writer.Write(targetBytes.Length);
            writer.Write(targetBytes);
            writer.Write(contentBytes.Length);
            writer.Write(contentBytes);
            
            return ms.ToArray();
        }

        public static ChatMessage? DeserializeChat(byte[] data)
        {
            try
            {
                using var ms = new MemoryStream(data);
                using var reader = new BinaryReader(ms);
                
                var type = (MessageType)reader.ReadByte();
                if (type != MessageType.CHAT) return null;
                
                var senderLength = reader.ReadInt32();
                var senderBytes = reader.ReadBytes(senderLength);
                var senderId = Encoding.UTF8.GetString(senderBytes);
                
                var targetLength = reader.ReadInt32();
                var targetBytes = reader.ReadBytes(targetLength);
                var targetId = Encoding.UTF8.GetString(targetBytes);
                
                var contentLength = reader.ReadInt32();
                var contentBytes = reader.ReadBytes(contentLength);
                var content = Encoding.UTF8.GetString(contentBytes);
                
                return new ChatMessage(content, targetId) { SenderId = senderId };
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Mensaje de inicio de transferencia de archivo
    /// </summary>
    public class FileStartMessage : Message
    {
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public string TargetClientId { get; set; }
        public string TransferId { get; set; }

        public FileStartMessage(string fileName, long fileSize, string targetClientId) : base(MessageType.FILE_START)
        {
            FileName = fileName;
            FileSize = fileSize;
            TargetClientId = targetClientId;
            TransferId = Guid.NewGuid().ToString();
        }

        public override byte[] Serialize()
        {
            var fileNameBytes = Encoding.UTF8.GetBytes(FileName);
            var senderBytes = Encoding.UTF8.GetBytes(SenderId);
            var targetBytes = Encoding.UTF8.GetBytes(TargetClientId);
            var transferIdBytes = Encoding.UTF8.GetBytes(TransferId);
            
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            
            writer.Write((byte)Type);
            writer.Write(senderBytes.Length);
            writer.Write(senderBytes);
            writer.Write(targetBytes.Length);
            writer.Write(targetBytes);
            writer.Write(transferIdBytes.Length);
            writer.Write(transferIdBytes);
            writer.Write(fileNameBytes.Length);
            writer.Write(fileNameBytes);
            writer.Write(FileSize);
            
            return ms.ToArray();
        }

        public static FileStartMessage? DeserializeFileStart(byte[] data)
        {
            try
            {
                using var ms = new MemoryStream(data);
                using var reader = new BinaryReader(ms);
                
                var type = (MessageType)reader.ReadByte();
                if (type != MessageType.FILE_START) return null;
                
                var senderLength = reader.ReadInt32();
                var senderBytes = reader.ReadBytes(senderLength);
                var senderId = Encoding.UTF8.GetString(senderBytes);
                
                var targetLength = reader.ReadInt32();
                var targetBytes = reader.ReadBytes(targetLength);
                var targetId = Encoding.UTF8.GetString(targetBytes);
                
                var transferIdLength = reader.ReadInt32();
                var transferIdBytes = reader.ReadBytes(transferIdLength);
                var transferId = Encoding.UTF8.GetString(transferIdBytes);
                
                var fileNameLength = reader.ReadInt32();
                var fileNameBytes = reader.ReadBytes(fileNameLength);
                var fileName = Encoding.UTF8.GetString(fileNameBytes);
                
                var fileSize = reader.ReadInt64();
                
                return new FileStartMessage(fileName, fileSize, targetId) 
                { 
                    SenderId = senderId,
                    TransferId = transferId
                };
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Mensaje de conexión de cliente
    /// </summary>
    public class ClientConnectMessage : Message
    {
        public string ClientName { get; set; }

        public ClientConnectMessage(string clientName) : base(MessageType.CLIENT_CONNECT)
        {
            ClientName = clientName;
        }

        public override byte[] Serialize()
        {
            var nameBytes = Encoding.UTF8.GetBytes(ClientName);
            var senderBytes = Encoding.UTF8.GetBytes(SenderId);
            
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            
            writer.Write((byte)Type);
            writer.Write(senderBytes.Length);
            writer.Write(senderBytes);
            writer.Write(nameBytes.Length);
            writer.Write(nameBytes);
            
            return ms.ToArray();
        }

        public static ClientConnectMessage? DeserializeClientConnect(byte[] data)
        {
            try
            {
                using var ms = new MemoryStream(data);
                using var reader = new BinaryReader(ms);
                
                var type = (MessageType)reader.ReadByte();
                if (type != MessageType.CLIENT_CONNECT) return null;
                
                var senderLength = reader.ReadInt32();
                var senderBytes = reader.ReadBytes(senderLength);
                var senderId = Encoding.UTF8.GetString(senderBytes);
                
                var nameLength = reader.ReadInt32();
                var nameBytes = reader.ReadBytes(nameLength);
                var clientName = Encoding.UTF8.GetString(nameBytes);
                
                return new ClientConnectMessage(clientName) 
                { 
                    SenderId = senderId 
                };
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Mensaje de error
    /// </summary>
    public class ErrorMessage : Message
    {
        public string ErrorDescription { get; set; }
        public string TargetClientId { get; set; }

        public ErrorMessage(string errorDescription, string targetClientId = "") : base(MessageType.ERROR)
        {
            ErrorDescription = errorDescription;
            TargetClientId = targetClientId;
        }

        public override byte[] Serialize()
        {
            var senderBytes = Encoding.UTF8.GetBytes(SenderId);
            var targetBytes = Encoding.UTF8.GetBytes(TargetClientId);
            var errorBytes = Encoding.UTF8.GetBytes(ErrorDescription);
            
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            
            writer.Write((byte)Type);
            writer.Write(senderBytes.Length);
            writer.Write(senderBytes);
            writer.Write(targetBytes.Length);
            writer.Write(targetBytes);
            writer.Write(errorBytes.Length);
            writer.Write(errorBytes);
            
            return ms.ToArray();
        }

        public static ErrorMessage? DeserializeError(byte[] data)
        {
            try
            {
                using var ms = new MemoryStream(data);
                using var reader = new BinaryReader(ms);
                
                var type = (MessageType)reader.ReadByte();
                if (type != MessageType.ERROR) return null;
                
                var senderLength = reader.ReadInt32();
                var senderBytes = reader.ReadBytes(senderLength);
                var senderId = Encoding.UTF8.GetString(senderBytes);
                
                var targetLength = reader.ReadInt32();
                var targetBytes = reader.ReadBytes(targetLength);
                var targetId = Encoding.UTF8.GetString(targetBytes);
                
                var errorLength = reader.ReadInt32();
                var errorBytes = reader.ReadBytes(errorLength);
                var errorDescription = Encoding.UTF8.GetString(errorBytes);
                
                return new ErrorMessage(errorDescription, targetId) 
                { 
                    SenderId = senderId 
                };
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Mensaje de confirmación (ACK)
    /// </summary>
    public class AckMessage : Message
    {
        public string TransferId { get; set; }
        public int SequenceNumber { get; set; }
        public string TargetClientId { get; set; }

        public AckMessage(string transferId, int sequenceNumber, string targetClientId) : base(MessageType.ACK)
        {
            TransferId = transferId;
            SequenceNumber = sequenceNumber;
            TargetClientId = targetClientId;
        }

        public override byte[] Serialize()
        {
            var senderBytes = Encoding.UTF8.GetBytes(SenderId);
            var targetBytes = Encoding.UTF8.GetBytes(TargetClientId);
            var transferIdBytes = Encoding.UTF8.GetBytes(TransferId);
            
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
            
            return ms.ToArray();
        }

        public static AckMessage? DeserializeAck(byte[] data)
        {
            try
            {
                using var ms = new MemoryStream(data);
                using var reader = new BinaryReader(ms);
                
                var type = (MessageType)reader.ReadByte();
                if (type != MessageType.ACK) return null;
                
                var senderLength = reader.ReadInt32();
                var senderBytes = reader.ReadBytes(senderLength);
                var senderId = Encoding.UTF8.GetString(senderBytes);
                
                var targetLength = reader.ReadInt32();
                var targetBytes = reader.ReadBytes(targetLength);
                var targetId = Encoding.UTF8.GetString(targetBytes);
                
                var transferIdLength = reader.ReadInt32();
                var transferIdBytes = reader.ReadBytes(transferIdLength);
                var transferId = Encoding.UTF8.GetString(transferIdBytes);
                
                var sequenceNumber = reader.ReadInt32();
                
                return new AckMessage(transferId, sequenceNumber, targetId) 
                { 
                    SenderId = senderId 
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
