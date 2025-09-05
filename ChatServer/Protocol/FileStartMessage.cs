using System.Text;

namespace ChatServer.Protocol
{
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
}
