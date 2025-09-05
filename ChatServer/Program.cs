using ChatServer.Core;

namespace ChatServer
{
    class Program
    {
        private static ChatFileServer? _server;
        private static CancellationTokenSource? _cancellationTokenSource;

        static async Task Main(string[] args)
        {
            Console.WriteLine("🚀 Iniciando Servidor de Chat y Transferencia de Archivos");
            Console.WriteLine("=".PadRight(60, '='));

            int port = 8888;
            
            // Permitir configurar el puerto desde argumentos
            if (args.Length > 0 && int.TryParse(args[0], out int customPort))
            {
                port = customPort;
            }

            _server = new ChatFileServer(port);
            _cancellationTokenSource = new CancellationTokenSource();

            // Configurar eventos del servidor
            _server.ClientConnected += OnClientConnected;
            _server.ClientDisconnected += OnClientDisconnected;
            _server.MessageReceived += OnMessageReceived;
            _server.FileTransferStarted += OnFileTransferStarted;
            _server.FileTransferCompleted += OnFileTransferCompleted;

            // Configurar manejo de señales para cierre limpio
            Console.CancelKeyPress += OnCancelKeyPress;

            try
            {
                await _server.StartAsync();

                Console.WriteLine($"📡 Servidor escuchando en puerto {port}");
                Console.WriteLine("💡 Comandos disponibles:");
                Console.WriteLine("   'stats' - Mostrar estadísticas del servidor");
                Console.WriteLine("   'clients' - Listar clientes conectados");
                Console.WriteLine("   'quit' - Detener el servidor");
                Console.WriteLine();

                // Bucle principal para comandos del servidor
                await RunServerCommandLoopAsync(_cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error fatal del servidor: {ex.Message}");
            }
            finally
            {
                await StopServerAsync();
            }
        }

        private static async Task RunServerCommandLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _server?.IsRunning == true)
            {
                try
                {
                    var input = await ReadLineAsync(cancellationToken);
                    
                    if (string.IsNullOrWhiteSpace(input)) continue;

                    var command = input.ToLower().Trim();
                    
                    // Pattern matching moderno con switch expression
                    switch (command)
                    {
                        case "stats":
                            ShowStats();
                            break;
                        case "clients":
                            ShowConnectedClients();
                            break;
                        case "quit" or "exit" or "stop":
                            Console.WriteLine("🛑 Deteniendo servidor...");
                            _cancellationTokenSource?.Cancel();
                            return;
                        case "help" or "?":
                            ShowHelp();
                            break;
                        default:
                            Console.WriteLine($"❓ Comando desconocido: {input}. Escribe 'help' para ver comandos disponibles.");
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error procesando comando: {ex.Message}");
                }
            }
        }

        private static async Task<string> ReadLineAsync(CancellationToken cancellationToken)
        {
            // Versión moderna con TaskCompletionSource (.NET 8)
            var tcs = new TaskCompletionSource<string>();
            
            using var registration = cancellationToken.Register(() => tcs.TrySetCanceled());
            
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        if (Console.KeyAvailable)
                        {
                            var result = Console.ReadLine() ?? "";
                            tcs.TrySetResult(result);
                            return;
                        }
                        await Task.Delay(50, cancellationToken); // Más eficiente que Thread.Sleep
                    }
                }
                catch (OperationCanceledException)
                {
                    tcs.TrySetCanceled();
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }, cancellationToken);

            try
            {
                return await tcs.Task;
            }
            catch (OperationCanceledException)
            {
                return "";
            }
        }

        private static void ShowStats()
        {
            if (_server == null) return;

            var stats = _server.GetStats();
            Console.WriteLine();
            Console.WriteLine("📊 Estadísticas del Servidor");
            Console.WriteLine("-".PadRight(30, '-'));
            Console.WriteLine($"   Clientes conectados: {stats.ConnectedClients}");
            Console.WriteLine($"   Transferencias activas: {stats.ActiveTransfers}");
            Console.WriteLine($"   Datos transferidos: {FormatBytes(stats.TotalDataTransferred)}");
            Console.WriteLine($"   Tiempo activo: {stats.UptimeMinutes:F1} minutos");
            Console.WriteLine();
        }

        private static void ShowConnectedClients()
        {
            if (_server == null) return;

            var clients = _server.GetConnectedClients();
            Console.WriteLine();
            Console.WriteLine($"👥 Clientes Conectados ({clients.Count})");
            Console.WriteLine("-".PadRight(40, '-'));
            
            if (clients.Count == 0)
            {
                Console.WriteLine("   No hay clientes conectados");
            }
            else
            {
                foreach (var client in clients)
                {
                    var duration = DateTime.UtcNow - client.ConnectedAt;
                    Console.WriteLine($"   {client.Name} ({client.Id}) - {duration.TotalMinutes:F1}min conectado");
                }
            }
            Console.WriteLine();
        }

        private static void ShowHelp()
        {
            Console.WriteLine();
            Console.WriteLine("💡 Comandos Disponibles");
            Console.WriteLine("-".PadRight(25, '-'));
            Console.WriteLine("   stats   - Mostrar estadísticas del servidor");
            Console.WriteLine("   clients - Listar clientes conectados");
            Console.WriteLine("   help    - Mostrar esta ayuda");
            Console.WriteLine("   quit    - Detener el servidor");
            Console.WriteLine();
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

        private static async Task StopServerAsync()
        {
            try
            {
                if (_server?.IsRunning == true)
                {
                    await _server.StopAsync();
                }
                Console.WriteLine("✅ Servidor detenido exitosamente");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error deteniendo servidor: {ex.Message}");
            }
        }

        // Event handlers
        private static void OnClientConnected(object? sender, ClientEventArgs e)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ {e.Client.Name} conectado");
        }

        private static void OnClientDisconnected(object? sender, ClientEventArgs e)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ❌ {e.Client.Name} desconectado");
        }

        private static void OnMessageReceived(object? sender, MessageEventArgs e)
        {
            // Los mensajes ya se logean en el servidor, no necesitamos hacer nada aquí
        }

        private static void OnFileTransferStarted(object? sender, FileTransferEventArgs e)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 📤 Iniciando transferencia: {e.FileName}");
        }

        private static void OnFileTransferCompleted(object? sender, FileTransferEventArgs e)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ✅ Transferencia completada: {e.FileName}");
        }

        private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true; // Evitar el cierre inmediato
            Console.WriteLine("\n🛑 Señal de cierre recibida. Deteniendo servidor...");
            _cancellationTokenSource?.Cancel();
        }
    }
}
