using ChatClient.Core;

namespace ChatClient
{
    class Program
    {
        private static ChatFileClient? _client;
        private static CancellationTokenSource? _cancellationTokenSource;

        static async Task Main(string[] args)
        {
            Console.WriteLine("💬 Cliente de Chat y Transferencia de Archivos");
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
                    Console.WriteLine("❌ No se pudo conectar al servidor");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error fatal: {ex.Message}");
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
                    Console.WriteLine($"❌ Error procesando comando: {ex.Message}");
                }
            }
        }

        private static async Task ProcessCommandAsync(string input)
        {
            if (_client == null || !_client.IsConnected) return;

            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;

            var command = parts[0].ToLower();

            switch (command)
            {
                case "/help":
                case "/h":
                case "/?":
                    ShowHelp();
                    break;

                case "/quit":
                case "/exit":
                case "/q":
                    Console.WriteLine("👋 Desconectando...");
                    _cancellationTokenSource?.Cancel();
                    break;

                case "/send":
                case "/s":
                    if (parts.Length >= 3)
                    {
                        var targetClient = parts[1];
                        var message = string.Join(" ", parts.Skip(2));
                        await _client.SendChatMessageAsync(message, targetClient);
                    }
                    else
                    {
                        Console.WriteLine("❌ Uso: /send <cliente_id> <mensaje>");
                    }
                    break;

                case "/file":
                case "/f":
                    if (parts.Length >= 3)
                    {
                        var targetClient = parts[1];
                        var filePath = parts[2];
                        
                        if (File.Exists(filePath))
                        {
                            await _client.SendFileAsync(filePath, targetClient);
                        }
                        else
                        {
                            Console.WriteLine($"❌ Archivo no encontrado: {filePath}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("❌ Uso: /file <cliente_id> <ruta_archivo>");
                    }
                    break;

                case "/create":
                case "/c":
                    if (parts.Length >= 2)
                    {
                        var fileName = parts[1];
                        await CreateTestFileAsync(fileName);
                    }
                    else
                    {
                        Console.WriteLine("❌ Uso: /create <nombre_archivo>");
                    }
                    break;

                default:
                    // Si no es un comando, enviar como mensaje de chat público
                    if (!input.StartsWith("/"))
                    {
                        await _client.SendChatMessageAsync(input);
                    }
                    else
                    {
                        Console.WriteLine($"❓ Comando desconocido: {command}. Escribe /help para ver comandos disponibles.");
                    }
                    break;
            }
        }

        private static async Task CreateTestFileAsync(string fileName)
        {
            try
            {
                var content = $"Archivo de prueba creado el {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
                content += "Este es un archivo de prueba para transferencias.\n";
                content += "Contiene texto simple para demostrar el protocolo de transferencia de archivos.\n";
                content += new string('X', 1000); // Agregar algo de contenido

                await File.WriteAllTextAsync(fileName, content);
                Console.WriteLine($"✅ Archivo creado: {fileName} ({new FileInfo(fileName).Length} bytes)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error creando archivo: {ex.Message}");
            }
        }

        private static async Task<string> ReadLineAsync(CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (Console.KeyAvailable)
                    {
                        return Console.ReadLine() ?? "";
                    }
                    Thread.Sleep(100);
                }
                return "";
            }, cancellationToken);
        }

        private static void ShowHelp()
        {
            Console.WriteLine("💡 Comandos Disponibles:");
            Console.WriteLine("-".PadRight(30, '-'));
            Console.WriteLine("  <mensaje>                    - Enviar mensaje público");
            Console.WriteLine("  /send <id> <mensaje>         - Enviar mensaje privado");
            Console.WriteLine("  /file <id> <archivo>         - Enviar archivo");
            Console.WriteLine("  /create <nombre>             - Crear archivo de prueba");
            Console.WriteLine("  /help                        - Mostrar esta ayuda");
            Console.WriteLine("  /quit                        - Salir del cliente");
            Console.WriteLine();
            Console.WriteLine("💡 Ejemplos:");
            Console.WriteLine("  Hola a todos");
            Console.WriteLine("  /send abc12345 Hola cliente específico");
            Console.WriteLine("  /file abc12345 documento.txt");
            Console.WriteLine("  /create prueba.txt");
        }

        private static async Task DisconnectAsync()
        {
            try
            {
                if (_client?.IsConnected == true)
                {
                    await _client.DisconnectAsync();
                }
                Console.WriteLine("✅ Cliente cerrado exitosamente");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error cerrando cliente: {ex.Message}");
            }
        }

        // Event handlers
        private static void OnConnected(object? sender, EventArgs e)
        {
            Console.WriteLine("🎉 Conectado al servidor exitosamente");
        }

        private static void OnDisconnected(object? sender, EventArgs e)
        {
            Console.WriteLine("🔌 Desconectado del servidor");
        }

        private static void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
        {
            // Los mensajes ya se procesan en el cliente, no necesitamos hacer nada aquí
        }

        private static void OnFileTransferStarted(object? sender, FileTransferEventArgs e)
        {
            Console.WriteLine($"📤 Transferencia iniciada: {e.FileName}");
        }

        private static void OnFileTransferCompleted(object? sender, FileTransferEventArgs e)
        {
            Console.WriteLine($"✅ Transferencia completada: {e.FileName}");
        }

        private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true; // Evitar el cierre inmediato
            Console.WriteLine("\n🛑 Señal de cierre recibida. Desconectando...");
            _cancellationTokenSource?.Cancel();
        }
    }
}
