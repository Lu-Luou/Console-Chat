using MySqlConnector;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace DatabaseTest
{
    // Clases de modelo
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? PasswordHash { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLogin { get; set; }
        public bool IsOnline { get; set; }
    }

    public class Channel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsPrivate { get; set; }
    }

    public class ChatMessage
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string MessageType { get; set; } = "text";
        public string? FilePath { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class DatabaseManager
    {
        private readonly string _connectionString;

        public DatabaseManager(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<MySqlConnection> GetConnectionAsync()
        {
            var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using var connection = await GetConnectionAsync();
                return connection.State == ConnectionState.Open;
            }
            catch
            {
                return false;
            }
        }

        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            using var connection = await GetConnectionAsync();
            var command = new MySqlCommand(
                "SELECT id, username, email, password_hash, created_at, last_login, is_online FROM users WHERE username = @username",
                connection);

            command.Parameters.AddWithValue("@username", username);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new User
                {
                    Id = reader.GetInt32("id"),
                    Username = reader.GetString("username"),
                    Email = reader.IsDBNull("email") ? null : reader.GetString("email"),
                    PasswordHash = reader.IsDBNull("password_hash") ? null : reader.GetString("password_hash"),
                    CreatedAt = reader.GetDateTime("created_at"),
                    LastLogin = reader.IsDBNull("last_login") ? null : reader.GetDateTime("last_login"),
                    IsOnline = reader.GetBoolean("is_online")
                };
            }

            return null;
        }

        public async Task<List<Channel>> GetChannelsAsync()
        {
            using var connection = await GetConnectionAsync();
            var command = new MySqlCommand(
                "SELECT id, name, description, created_by, created_at, is_private FROM channels ORDER BY name",
                connection);

            var channels = new List<Channel>();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                channels.Add(new Channel
                {
                    Id = reader.GetInt32("id"),
                    Name = reader.GetString("name"),
                    Description = reader.IsDBNull("description") ? null : reader.GetString("description"),
                    CreatedBy = reader.GetInt32("created_by"),
                    CreatedAt = reader.GetDateTime("created_at"),
                    IsPrivate = reader.GetBoolean("is_private")
                });
            }

            return channels;
        }

        public async Task<int> CreateMessageAsync(int userId, int channelId, string message, string messageType = "text")
        {
            using var connection = await GetConnectionAsync();
            var command = new MySqlCommand(
                "INSERT INTO messages (user_id, channel_id, message, message_type) VALUES (@user_id, @channel_id, @message, @message_type); SELECT LAST_INSERT_ID();",
                connection);

            command.Parameters.AddWithValue("@user_id", userId);
            command.Parameters.AddWithValue("@channel_id", channelId);
            command.Parameters.AddWithValue("@message", message);
            command.Parameters.AddWithValue("@message_type", messageType);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task<List<ChatMessage>> GetChannelMessagesAsync(int channelId, int limit = 50)
        {
            using var connection = await GetConnectionAsync();
            var command = new MySqlCommand(@"
                SELECT m.id, m.message, m.message_type, m.file_path, m.created_at,
                       u.username, u.id as user_id
                FROM messages m
                JOIN users u ON m.user_id = u.id
                WHERE m.channel_id = @channel_id
                ORDER BY m.created_at DESC
                LIMIT @limit", connection);

            command.Parameters.AddWithValue("@channel_id", channelId);
            command.Parameters.AddWithValue("@limit", limit);

            var messages = new List<ChatMessage>();
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                messages.Add(new ChatMessage
                {
                    Id = reader.GetInt32("id"),
                    UserId = reader.GetInt32("user_id"),
                    Username = reader.GetString("username"),
                    Message = reader.GetString("message"),
                    MessageType = reader.GetString("message_type"),
                    FilePath = reader.IsDBNull("file_path") ? null : reader.GetString("file_path"),
                    CreatedAt = reader.GetDateTime("created_at")
                });
            }

            return messages.OrderBy(m => m.CreatedAt).ToList();
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("[+] 🚀 Ejemplo de Integración C# con MySQL");
            Console.WriteLine("=".PadRight(60, '='));

            // Cadena de conexión directa
            var connectionString = "Server=localhost;Port=3306;Database=chatdb;Uid=chatuser;Pwd=chatpassword;";
            var dbManager = new DatabaseManager(connectionString);

            try
            {
                // Probar conexión
                Console.WriteLine("[INFO] 🔍 Probando conexión a MySQL...");
                bool isConnected = await dbManager.TestConnectionAsync();
                
                if (isConnected)
                {
                    Console.WriteLine("[SUCCESS] ✅ Conexión exitosa a MySQL");
                }
                else
                {
                    Console.WriteLine("[ERROR] ❌ No se pudo conectar a MySQL");
                    Console.WriteLine("[INFO] Asegúrate de que MySQL esté ejecutándose con: docker compose up -d mysql");
                    return;
                }

                // Ejemplo 1: Obtener usuarios existentes
                Console.WriteLine("\n[INFO] 👤 Obteniendo usuario admin...");
                var adminUser = await dbManager.GetUserByUsernameAsync("admin");
                if (adminUser != null)
                {
                    Console.WriteLine($"[SUCCESS] ✅ Usuario encontrado: {adminUser.Username} (ID: {adminUser.Id})");
                    Console.WriteLine($"         📧 Email: {adminUser.Email}");
                    Console.WriteLine($"         📅 Creado: {adminUser.CreatedAt}");
                }

                // Ejemplo 2: Obtener canales
                Console.WriteLine("\n[INFO] 💬 Obteniendo canales disponibles...");
                var channels = await dbManager.GetChannelsAsync();
                Console.WriteLine($"[SUCCESS] ✅ Se encontraron {channels.Count} canales:");
                
                foreach (var channel in channels)
                {
                    Console.WriteLine($"         🏷️  {channel.Name}: {channel.Description}");
                }

                // Ejemplo 3: Crear un mensaje en el canal general
                Console.WriteLine("\n[INFO] 📝 Enviando mensaje de prueba...");
                if (adminUser != null && channels.Any())
                {
                    var generalChannel = channels.First(c => c.Name == "general");
                    string testMessage = $"¡Hola desde C# con MySQL! Hora: {DateTime.Now:HH:mm:ss}";
                    
                    int messageId = await dbManager.CreateMessageAsync(
                        adminUser.Id, 
                        generalChannel.Id, 
                        testMessage
                    );
                    Console.WriteLine($"[SUCCESS] ✅ Mensaje enviado con ID: {messageId}");
                    Console.WriteLine($"         💬 Contenido: \"{testMessage}\"");
                }

                // Ejemplo 4: Obtener mensajes del canal general
                Console.WriteLine("\n[INFO] 📖 Obteniendo mensajes del canal general...");
                if (channels.Any())
                {
                    var generalChannel = channels.First(c => c.Name == "general");
                    var messages = await dbManager.GetChannelMessagesAsync(generalChannel.Id, 5);
                    
                    Console.WriteLine($"[SUCCESS] ✅ Se encontraron {messages.Count} mensajes recientes:");
                    
                    if (messages.Count == 0)
                    {
                        Console.WriteLine("         🔍 No hay mensajes en este canal aún");
                    }
                    else
                    {
                        Console.WriteLine("         📋 Mensajes:");
                        foreach (var message in messages)
                        {
                            Console.WriteLine($"         [{message.CreatedAt:yyyy-MM-dd HH:mm:ss}] {message.Username}: {message.Message}");
                        }
                    }
                }

                Console.WriteLine("\n[SUCCESS] 🎉 ¡Todos los ejemplos ejecutados correctamente!");
                Console.WriteLine("[INFO] 📋 La integración de MySQL con C# está funcionando perfectamente");
                
                Console.WriteLine("\n" + "=".PadRight(60, '='));
                Console.WriteLine("[INFO] 🔧 Para integrar esto en tu chat existente:");
                Console.WriteLine("       1. Copia la clase DatabaseManager a tu proyecto ChatServer");
                Console.WriteLine("       2. Agrega las dependencias NuGet: MySqlConnector, Microsoft.Extensions.Configuration");
                Console.WriteLine("       3. Usa los métodos del DatabaseManager en tu lógica de chat");
                Console.WriteLine("       4. Reemplaza el almacenamiento en archivos por llamadas a MySQL");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ❌ Error durante la ejecución: {ex.Message}");
                Console.WriteLine($"[DEBUG] 🔍 Detalles del error:");
                Console.WriteLine($"        Tipo: {ex.GetType().Name}");
                Console.WriteLine($"        Mensaje: {ex.Message}");
                
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"        Error interno: {ex.InnerException.Message}");
                }
                
                if (ex.Message.Contains("Unable to connect"))
                {
                    Console.WriteLine("\n[TIP] 💡 Posibles soluciones:");
                    Console.WriteLine("      - Verifica que MySQL esté ejecutándose: docker compose ps");
                    Console.WriteLine("      - Inicia MySQL: docker compose up -d mysql");
                    Console.WriteLine("      - Verifica la conexión: docker exec csharp-chat-mysql mysql -u chatuser -pchatpassword -e 'SELECT 1;'");
                }
            }

            Console.WriteLine("\n[INFO] ⌨️  Presiona cualquier tecla para salir...");
            Console.ReadKey();
        }
    }
}
