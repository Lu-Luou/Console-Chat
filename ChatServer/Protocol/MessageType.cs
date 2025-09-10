namespace ChatServer.Protocol
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
        CLIENT_DISCONNECT = 0x08,
        
        /// <summary>
        /// Respuesta del servidor con el ID del cliente
        /// </summary>
        CLIENT_ID_RESPONSE = 0x09,
        
        /// <summary>
        /// Cliente acepta descarga de archivo
        /// </summary>
        DOWNLOAD_ACCEPT = 0x0A,
        
        /// <summary>
        /// Cliente rechaza descarga de archivo
        /// </summary>
        DOWNLOAD_REJECT = 0x0B,
        
        /// <summary>
        /// Servidor confirma que el receptor aceptó la descarga
        /// </summary>
        UPLOAD_CONFIRMED = 0x0C
    }
}
