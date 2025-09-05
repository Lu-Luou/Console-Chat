using System.Text;

namespace ChatServer.Protocol
{
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
                MessageType.FILE_DATA => FileDataMessage.DeserializeFileData(data),
                MessageType.FILE_END => FileEndMessage.DeserializeFileEnd(data),
                MessageType.ACK => AckMessage.DeserializeAck(data),
                MessageType.ERROR => ErrorMessage.DeserializeError(data),
                MessageType.CLIENT_CONNECT => ClientConnectMessage.DeserializeClientConnect(data),
                MessageType.CLIENT_DISCONNECT => ClientDisconnectMessage.DeserializeClientDisconnect(data),
                _ => null
            };
        }
    }
}
