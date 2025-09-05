using System.Collections.Concurrent;
using ChatServer.Protocol;

namespace ChatServer.Core
{
    /// <summary>
    /// Maneja las transferencias de archivos activas
    /// </summary>
    public class FileTransferManager
    {
        private readonly ConcurrentDictionary<string, FileTransfer> _activeTransfers = new();
        private readonly object _lockObject = new object();
        private long _totalCompletedBytes = 0;

        /// <summary>
        /// Inicia una nueva transferencia de archivo
        /// </summary>
        public bool StartTransfer(FileStartMessage fileStart)
        {
            try
            {
                var transfer = new FileTransfer
                {
                    TransferId = fileStart.TransferId,
                    FileName = fileStart.FileName,
                    FileSize = fileStart.FileSize,
                    SenderId = fileStart.SenderId,
                    TargetClientId = fileStart.TargetClientId,
                    StartTime = DateTime.UtcNow,
                    ExpectedSequences = (int)Math.Ceiling((double)fileStart.FileSize / FileTransfer.ChunkSize),
                    ReceivedData = new ConcurrentDictionary<int, byte[]>()
                };

                return _activeTransfers.TryAdd(fileStart.TransferId, transfer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error iniciando transferencia: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Procesa un bloque de datos de archivo
        /// </summary>
        public FileTransferResult ProcessFileData(FileDataMessage fileData)
        {
            try
            {
                if (!_activeTransfers.TryGetValue(fileData.TransferId, out var transfer))
                {
                    return new FileTransferResult 
                    { 
                        Success = false, 
                        ErrorMessage = "Transferencia no encontrada" 
                    };
                }

                // Añadir datos al transfer
                transfer.ReceivedData[fileData.SequenceNumber] = fileData.Data;
                transfer.BytesReceived += fileData.Data.Length;
                transfer.LastActivityTime = DateTime.UtcNow;

                // Verificar si hemos recibido todos los bloques
                bool isComplete = transfer.ReceivedData.Count >= transfer.ExpectedSequences;
                bool hasAllSequences = true;
                
                if (isComplete)
                {
                    for (int i = 0; i < transfer.ExpectedSequences; i++)
                    {
                        if (!transfer.ReceivedData.ContainsKey(i))
                        {
                            hasAllSequences = false;
                            break;
                        }
                    }
                }

                return new FileTransferResult
                {
                    Success = true,
                    IsComplete = isComplete && hasAllSequences,
                    Transfer = transfer,
                    Progress = (double)transfer.BytesReceived / transfer.FileSize
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error procesando datos de archivo: {ex.Message}");
                return new FileTransferResult 
                { 
                    Success = false, 
                    ErrorMessage = ex.Message 
                };
            }
        }

        /// <summary>
        /// Completa una transferencia y obtiene los datos del archivo
        /// </summary>
        public byte[]? CompleteTransfer(string transferId)
        {
            try
            {
                if (!_activeTransfers.TryRemove(transferId, out var transfer))
                {
                    return null;
                }

                // Agregar bytes completados al total
                Interlocked.Add(ref _totalCompletedBytes, transfer.FileSize);

                // Combinar todos los bloques en orden
                using var ms = new MemoryStream();
                for (int i = 0; i < transfer.ExpectedSequences; i++)
                {
                    if (transfer.ReceivedData.TryGetValue(i, out var data))
                    {
                        ms.Write(data, 0, data.Length);
                    }
                    else
                    {
                        Console.WriteLine($"Bloque {i} faltante en transferencia {transferId}");
                        return null;
                    }
                }

                return ms.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error completando transferencia: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Cancela una transferencia
        /// </summary>
        public bool CancelTransfer(string transferId)
        {
            return _activeTransfers.TryRemove(transferId, out _);
        }

        /// <summary>
        /// Obtiene información de una transferencia
        /// </summary>
        public FileTransfer? GetTransfer(string transferId)
        {
            _activeTransfers.TryGetValue(transferId, out var transfer);
            return transfer;
        }

        /// <summary>
        /// Limpia transferencias expiradas (más de 5 minutos sin actividad)
        /// </summary>
        public void CleanupExpiredTransfers()
        {
            var expiredTime = DateTime.UtcNow.AddMinutes(-5);
            var expiredTransfers = _activeTransfers.Where(kvp => kvp.Value.LastActivityTime < expiredTime)
                                                   .Select(kvp => kvp.Key)
                                                   .ToList();

            foreach (var transferId in expiredTransfers)
            {
                if (_activeTransfers.TryRemove(transferId, out var transfer))
                {
                    Console.WriteLine($"Transferencia expirada eliminada: {transferId}");
                }
            }
        }

        /// <summary>
        /// Obtiene estadísticas de las transferencias activas
        /// </summary>
        public TransferStats GetStats()
        {
            var activeBytes = _activeTransfers.Values.Sum(t => t.BytesReceived);
            return new TransferStats
            {
                ActiveTransfers = _activeTransfers.Count,
                TotalDataTransferred = _totalCompletedBytes + activeBytes
            };
        }
    }

    /// <summary>
    /// Representa una transferencia de archivo en progreso
    /// </summary>
    public class FileTransfer
    {
        public const int ChunkSize = 8192; // 8KB por chunk

        public string TransferId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string SenderId { get; set; } = string.Empty;
        public string TargetClientId { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime LastActivityTime { get; set; }
        public long BytesReceived { get; set; }
        public int ExpectedSequences { get; set; }
        public ConcurrentDictionary<int, byte[]> ReceivedData { get; set; } = new();

        public double Progress => FileSize > 0 ? (double)BytesReceived / FileSize : 0;
        public TimeSpan Duration => DateTime.UtcNow - StartTime;
    }

    /// <summary>
    /// Resultado del procesamiento de datos de archivo
    /// </summary>
    public class FileTransferResult
    {
        public bool Success { get; set; }
        public bool IsComplete { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public FileTransfer? Transfer { get; set; }
        public double Progress { get; set; }
    }

    /// <summary>
    /// Estadísticas de transferencias
    /// </summary>
    public class TransferStats
    {
        public int ActiveTransfers { get; set; }
        public long TotalDataTransferred { get; set; }
    }
}
