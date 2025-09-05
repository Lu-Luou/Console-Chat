using System.Text;

namespace ChatServer.Protocol
{
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
            var senderBytes = Encoding.UTF8.GetBytes(SenderId);
            var targetBytes = Encoding.UTF8.GetBytes(TargetClientId);
            var transferIdBytes = Encoding.UTF8.GetBytes(TransferId);
            var errorBytes = Encoding.UTF8.GetBytes(ErrorMessage);
            
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

        public static FileEndMessage? DeserializeFileEnd(byte[] data)
        {
            try
            {
                using var ms = new MemoryStream(data);
                using var reader = new BinaryReader(ms);
                
                var type = (MessageType)reader.ReadByte();
                if (type != MessageType.FILE_END) return null;
                
                var senderLength = reader.ReadInt32();
                var senderBytes = reader.ReadBytes(senderLength);
                var senderId = Encoding.UTF8.GetString(senderBytes);
                
                var targetLength = reader.ReadInt32();
                var targetBytes = reader.ReadBytes(targetLength);
                var targetId = Encoding.UTF8.GetString(targetBytes);
                
                var transferIdLength = reader.ReadInt32();
                var transferIdBytes = reader.ReadBytes(transferIdLength);
                var transferId = Encoding.UTF8.GetString(transferIdBytes);
                
                var success = reader.ReadBoolean();
                var errorLength = reader.ReadInt32();
                var errorBytes = reader.ReadBytes(errorLength);
                var errorMessage = Encoding.UTF8.GetString(errorBytes);
                
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
    }
}
