using System.Text;

namespace ChatServer.Protocol
{
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
}
