using ChatClient.Core;

namespace ChatClient
{
    class Program
    {
        private static ChatFileClient? _client;
        private static CancellationTokenSource? _cancellationTokenSource;

        static async Task Main(string[] args)
        {
            Console.WriteLine("[CLIENT] Cliente de Chat y Transferencia de Archivos");
            Console.WriteLine("=".PadRight(50, '='));

            // Configuración inicial
            Console.Write("Ingrese su nombre: ");
            string clientName = Console.ReadLine() ?? "Cliente";
            
            Console.Write("Servidor (localhost): ");
            string server = Console.ReadLine() ?? "localhost";
            if (string.IsNullOrWhiteSpace(server)) server = "localhost";
            
            Console.Write("Puerto (8888): ");
            string portStr = Console.ReadLine() ?? "8888";
            int port = int.TryParse(portStr, out int p) ? p : 8888;

            _client = new ChatFileClient(server, port, clientName);
            _cancellationTokenSource = new CancellationTokenSource();

            // Configurar eventos
            _client.Connected += OnConnected;
            _client.Disconnected += OnDisconnected;
            _client.MessageReceived += OnMessageReceived;
            _client.FileTransferStarted += OnFileTransferStarted;
            _client.FileTransferCompleted += OnFileTransferCompleted;

            // Configurar manejo de señales
            Console.CancelKeyPress += OnCancelKeyPress;

            try
            {
                // Conectar al servidor
                if (await _client.ConnectAsync())
                {
                    Console.WriteLine();
                    ShowHelp();
                    Console.WriteLine();

                    // Bucle principal de comandos
                    await RunClientCommandLoopAsync(_cancellationTokenSource.Token);
                }
                else
                {
                    Console.WriteLine("[X] No se pudo conectar al servidor");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[X] Error fatal: {ex.Message}");
            }
            finally
            {
                await DisconnectAsync();
            }
        }

        private static async Task RunClientCommandLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _client?.IsConnected == true)
            {
                try
                {
                    Console.Write("> ");
                    var input = await ReadLineAsync(cancellationToken);
                    
                    if (string.IsNullOrWhiteSpace(input)) continue;

                    await ProcessCommandAsync(input.Trim());
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[X] Error procesando comando: {ex.Message}");
                }
            }
        }

        private static async Task ProcessCommandAsync(string input)
        {
            if (_client == null || !_client.IsConnected) return;

            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;

            var command = parts[0].ToLower();

            // Pattern matching moderno con switch expression
            await (command switch
            {
                "/help" or "/h" or "/?" => Task.Run(ShowHelp),
                "/quit" or "/exit" or "/q" => Task.Run(() => {
                    Console.WriteLine("[!] Desconectando...");
                    _cancellationTokenSource?.Cancel();
                }),
                "/send" or "/s" when parts.Length >= 3 => HandleSendCommand(parts),
                "/send" or "/s" => Task.Run(() => Console.WriteLine("[X] Uso: /send <cliente_id> <mensaje>")),
                "/file" or "/f" when parts.Length >= 3 => HandleFileCommand(parts),
                "/file" or "/f" => Task.Run(() => Console.WriteLine("[X] Uso: /file <cliente_id> <ruta_archivo>")),
                "/create" or "/c" when parts.Length >= 2 => CreateTestFileAsync(parts[1]),
                "/create" or "/c" => Task.Run(() => Console.WriteLine("[X] Uso: /create <nombre_archivo>")),
                "/downloads" or "/dl" => Task.Run(() => _client?.ShowPendingDownloads()),
                "/download" when parts.Length >= 2 && int.TryParse(parts[1], out int downloadId) => Task.Run(() => _client?.AcceptDownload(downloadId)),
                "/download" => Task.Run(() => Console.WriteLine("[X] Uso: /download <id>")),
                "/reject" when parts.Length >= 2 && int.TryParse(parts[1], out int rejectId) => Task.Run(() => _client?.RejectDownload(rejectId)),
                "/reject" => Task.Run(() => Console.WriteLine("[X] Uso: /reject <id>")),
                _ when !input.StartsWith("/") => _client.SendChatMessageAsync(input),
                _ => Task.Run(() => Console.WriteLine($"[?] Comando desconocido: {command}. Escribe /help para ver comandos disponibles."))
            });
        }

        private static async Task CreateTestFileAsync(string fileName)
        {
            try
            {
                // Usando interpolación de strings moderna (.NET 8)
                var content = $$"""
                    Archivo de prueba creado el {{DateTime.Now:yyyy-MM-dd HH:mm:ss}}
                    Este es un archivo de prueba para transferencias.
                    Contiene texto simple para demostrar el protocolo de transferencia de archivos.
                    {{new string('X', 1000)}}
                    """;

                await File.WriteAllTextAsync(fileName, content);
                Console.WriteLine($"[OK] Archivo creado: {fileName} ({new FileInfo(fileName).Length} bytes)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[X] Error creando archivo: {ex.Message}");
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

        private static void ShowHelp()
        {
            Console.WriteLine("[HELP] Comandos Disponibles:");
            Console.WriteLine("-".PadRight(40, '-'));
            
            Console.WriteLine("MENSAJERIA:");
            Console.WriteLine("  <mensaje>                    - Enviar mensaje publico");
            Console.WriteLine("  /send <id> <mensaje>         - Enviar mensaje privado");
            Console.WriteLine();
            
            Console.WriteLine("ARCHIVOS:");
            Console.WriteLine("  /file <id> <archivo>         - Enviar archivo");
            Console.WriteLine("  /downloads                   - Ver peticiones pendientes");
            Console.WriteLine("  /download <id>               - Aceptar descarga");
            Console.WriteLine("  /reject <id>                 - Rechazar descarga");
            Console.WriteLine();
            
            Console.WriteLine("UTILIDADES:");
            Console.WriteLine("  /create <nombre>             - Crear archivo de prueba");
            Console.WriteLine("  /help                        - Mostrar esta ayuda");
            Console.WriteLine("  /quit                        - Salir del cliente");
            Console.WriteLine();
            
            Console.WriteLine("[INFO] Ejemplos:");
            Console.WriteLine("  Hola a todos");
            Console.WriteLine("  /send abc12345 Hola cliente específico");
            Console.WriteLine("  /file abc12345 documento.txt");
            Console.WriteLine("  /downloads");
            Console.WriteLine("  /download 1");
            Console.WriteLine("  /reject 2");
            Console.WriteLine("  /create prueba.txt");
        }

        // Métodos auxiliares modernos para .NET 8
        private static async Task HandleSendCommand(string[] parts)
        {
            if (_client?.IsConnected != true) return;
            
            var targetClient = parts[1];
            var message = string.Join(" ", parts.Skip(2));
            await _client.SendChatMessageAsync(message, targetClient);
        }

        private static async Task HandleFileCommand(string[] parts)
        {
            if (_client?.IsConnected != true) return;
            
            var targetClient = parts[1];
            var filePath = parts[2];
            
            if (File.Exists(filePath))
            {
                await _client.SendFileAsync(filePath, targetClient);
            }
            else
            {
                Console.WriteLine($"[X] Archivo no encontrado: {filePath}");
            }
        }

        private static async Task DisconnectAsync()
        {
            try
            {
                if (_client?.IsConnected == true)
                {
                    await _client.DisconnectAsync();
                }
                Console.WriteLine("[OK] Cliente cerrado exitosamente");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[X] Error cerrando cliente: {ex.Message}");
            }
        }

        // Event handlers
        private static void OnConnected(object? sender, EventArgs e)
        {
            Console.WriteLine("[+] Conectado al servidor exitosamente");
        }

        private static void OnDisconnected(object? sender, EventArgs e)
        {
            Console.WriteLine("[-] Desconectado del servidor");
        }

        private static void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
        {
            // Los mensajes ya se procesan en el cliente, no necesitamos hacer nada aquí
        }

        private static void OnFileTransferStarted(object? sender, FileTransferEventArgs e)
        {
            // Los mensajes de inicio ya se muestran en el cliente, evitar duplicados
            // Console.WriteLine($"[>] Transferencia iniciada: {e.FileName}");
        }

        private static void OnFileTransferCompleted(object? sender, FileTransferEventArgs e)
        {
            // Los mensajes de finalización ya se muestran en el cliente con mejor formato
            // Console.WriteLine($"[OK] Transferencia completada: {e.FileName}");
        }

        private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true; // Evitar el cierre inmediato
            Console.WriteLine("\n[!] Señal de cierre recibida. Desconectando...");
            _cancellationTokenSource?.Cancel();
        }
    }
}
