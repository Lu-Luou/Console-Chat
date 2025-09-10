using System.Text;

namespace ChatServer.Protocol
{
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
    /// Mensaje de desconexión de cliente
    /// </summary>
    public class ClientDisconnectMessage : Message
    {
        public string Reason { get; set; }

        public ClientDisconnectMessage(string reason = "") : base(MessageType.CLIENT_DISCONNECT)
        {
            Reason = reason;
        }

        public override byte[] Serialize()
        {
            var reasonBytes = Encoding.UTF8.GetBytes(Reason);
            var senderBytes = Encoding.UTF8.GetBytes(SenderId);
            
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            
            writer.Write((byte)Type);
            writer.Write(senderBytes.Length);
            writer.Write(senderBytes);
            writer.Write(reasonBytes.Length);
            writer.Write(reasonBytes);
            
            return ms.ToArray();
        }

        public static ClientDisconnectMessage? DeserializeClientDisconnect(byte[] data)
        {
            try
            {
                using var ms = new MemoryStream(data);
                using var reader = new BinaryReader(ms);
                
                var type = (MessageType)reader.ReadByte();
                if (type != MessageType.CLIENT_DISCONNECT) return null;
                
                var senderLength = reader.ReadInt32();
                var senderBytes = reader.ReadBytes(senderLength);
                var senderId = Encoding.UTF8.GetString(senderBytes);
                
                var reasonLength = reader.ReadInt32();
                var reasonBytes = reader.ReadBytes(reasonLength);
                var reason = Encoding.UTF8.GetString(reasonBytes);
                
                return new ClientDisconnectMessage(reason) 
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
    /// Mensaje con la respuesta del ID del cliente
    /// </summary>
    public class ClientIdResponseMessage : Message
    {
        public string ClientId { get; set; }

        public ClientIdResponseMessage(string clientId) : base(MessageType.CLIENT_ID_RESPONSE)
        {
            ClientId = clientId;
        }

        public override byte[] Serialize()
        {
            var senderBytes = Encoding.UTF8.GetBytes(SenderId);
            var clientIdBytes = Encoding.UTF8.GetBytes(ClientId);
            
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            
            writer.Write((byte)Type);
            writer.Write(senderBytes.Length);
            writer.Write(senderBytes);
            writer.Write(clientIdBytes.Length);
            writer.Write(clientIdBytes);
            
            return ms.ToArray();
        }

        public static ClientIdResponseMessage? DeserializeClientIdResponse(byte[] data)
        {
            try
            {
                using var ms = new MemoryStream(data);
                using var reader = new BinaryReader(ms);
                
                var type = (MessageType)reader.ReadByte();
                if (type != MessageType.CLIENT_ID_RESPONSE) return null;
                
                var senderLength = reader.ReadInt32();
                var senderBytes = reader.ReadBytes(senderLength);
                var senderId = Encoding.UTF8.GetString(senderBytes);
                
                var clientIdLength = reader.ReadInt32();
                var clientIdBytes = reader.ReadBytes(clientIdLength);
                var clientId = Encoding.UTF8.GetString(clientIdBytes);
                
                return new ClientIdResponseMessage(clientId) 
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
    /// Mensaje de aceptación de descarga
    /// </summary>
    public class DownloadAcceptMessage : Message
    {
        public string TransferId { get; set; }

        public DownloadAcceptMessage(string transferId) : base(MessageType.DOWNLOAD_ACCEPT)
        {
            TransferId = transferId;
        }

        public override byte[] Serialize()
        {
            var senderBytes = Encoding.UTF8.GetBytes(SenderId);
            var transferIdBytes = Encoding.UTF8.GetBytes(TransferId);
            
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            
            writer.Write((byte)Type);
            writer.Write(senderBytes.Length);
            writer.Write(senderBytes);
            writer.Write(transferIdBytes.Length);
            writer.Write(transferIdBytes);
            
            return ms.ToArray();
        }

        public static DownloadAcceptMessage? DeserializeDownloadAccept(byte[] data)
        {
            try
            {
                using var ms = new MemoryStream(data);
                using var reader = new BinaryReader(ms);
                
                var type = (MessageType)reader.ReadByte();
                if (type != MessageType.DOWNLOAD_ACCEPT) return null;
                
                var senderLength = reader.ReadInt32();
                var senderBytes = reader.ReadBytes(senderLength);
                var senderId = Encoding.UTF8.GetString(senderBytes);
                
                var transferIdLength = reader.ReadInt32();
                var transferIdBytes = reader.ReadBytes(transferIdLength);
                var transferId = Encoding.UTF8.GetString(transferIdBytes);
                
                return new DownloadAcceptMessage(transferId) 
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
    /// Mensaje de rechazo de descarga
    /// </summary>
    public class DownloadRejectMessage : Message
    {
        public string TransferId { get; set; }

        public DownloadRejectMessage(string transferId) : base(MessageType.DOWNLOAD_REJECT)
        {
            TransferId = transferId;
        }

        public override byte[] Serialize()
        {
            var senderBytes = Encoding.UTF8.GetBytes(SenderId);
            var transferIdBytes = Encoding.UTF8.GetBytes(TransferId);
            
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            
            writer.Write((byte)Type);
            writer.Write(senderBytes.Length);
            writer.Write(senderBytes);
            writer.Write(transferIdBytes.Length);
            writer.Write(transferIdBytes);
            
            return ms.ToArray();
        }

        public static DownloadRejectMessage? DeserializeDownloadReject(byte[] data)
        {
            try
            {
                using var ms = new MemoryStream(data);
                using var reader = new BinaryReader(ms);
                
                var type = (MessageType)reader.ReadByte();
                if (type != MessageType.DOWNLOAD_REJECT) return null;
                
                var senderLength = reader.ReadInt32();
                var senderBytes = reader.ReadBytes(senderLength);
                var senderId = Encoding.UTF8.GetString(senderBytes);
                
                var transferIdLength = reader.ReadInt32();
                var transferIdBytes = reader.ReadBytes(transferIdLength);
                var transferId = Encoding.UTF8.GetString(transferIdBytes);
                
                return new DownloadRejectMessage(transferId) 
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
    /// Mensaje de confirmación de subida (al emisor)
    /// </summary>
    public class UploadConfirmedMessage : Message
    {
        public string TransferId { get; set; }

        public UploadConfirmedMessage(string transferId) : base(MessageType.UPLOAD_CONFIRMED)
        {
            TransferId = transferId;
        }

        public override byte[] Serialize()
        {
            var senderBytes = Encoding.UTF8.GetBytes(SenderId);
            var transferIdBytes = Encoding.UTF8.GetBytes(TransferId);
            
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            
            writer.Write((byte)Type);
            writer.Write(senderBytes.Length);
            writer.Write(senderBytes);
            writer.Write(transferIdBytes.Length);
            writer.Write(transferIdBytes);
            
            return ms.ToArray();
        }

        public static UploadConfirmedMessage? DeserializeUploadConfirmed(byte[] data)
        {
            try
            {
                using var ms = new MemoryStream(data);
                using var reader = new BinaryReader(ms);
                
                var type = (MessageType)reader.ReadByte();
                if (type != MessageType.UPLOAD_CONFIRMED) return null;
                
                var senderLength = reader.ReadInt32();
                var senderBytes = reader.ReadBytes(senderLength);
                var senderId = Encoding.UTF8.GetString(senderBytes);
                
                var transferIdLength = reader.ReadInt32();
                var transferIdBytes = reader.ReadBytes(transferIdLength);
                var transferId = Encoding.UTF8.GetString(transferIdBytes);
                
                return new UploadConfirmedMessage(transferId) 
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
