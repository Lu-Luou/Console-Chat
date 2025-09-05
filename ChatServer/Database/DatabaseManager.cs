using MySqlConnector;
using System.Data;

namespace ChatServer.Database
{
    public class DatabaseManager
    {
        private readonly string _connectionString;

        public DatabaseManager(string connectionString)
        {
            _connectionString = connectionString;
        }

        // Método para obtener conexión
        public async Task<MySqlConnection> GetConnectionAsync()
        {
            var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();
            return connection;
        }

        // Método para probar la conexión
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

        // Método para insertar usuario
        public async Task<int> CreateUserAsync(string username, string email, string passwordHash)
        {
            using var connection = await GetConnectionAsync();
            var command = new MySqlCommand(
                "INSERT INTO users (username, email, password_hash) VALUES (@username, @email, @password_hash); SELECT LAST_INSERT_ID();",
                connection);

            command.Parameters.AddWithValue("@username", username);
            command.Parameters.AddWithValue("@email", email);
            command.Parameters.AddWithValue("@password_hash", passwordHash);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        // Método para obtener usuario por username
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

        // Método para insertar mensaje
        public async Task<int> CreateMessageAsync(int userId, int channelId, string message, string messageType = "text", string? filePath = null)
        {
            using var connection = await GetConnectionAsync();
            var command = new MySqlCommand(
                "INSERT INTO messages (user_id, channel_id, message, message_type, file_path) VALUES (@user_id, @channel_id, @message, @message_type, @file_path); SELECT LAST_INSERT_ID();",
                connection);

            command.Parameters.AddWithValue("@user_id", userId);
            command.Parameters.AddWithValue("@channel_id", channelId);
            command.Parameters.AddWithValue("@message", message);
            command.Parameters.AddWithValue("@message_type", messageType);
            command.Parameters.AddWithValue("@file_path", filePath);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        // Método para obtener mensajes de un canal
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

        // Método para obtener canales
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

        // Método para actualizar estado online del usuario
        public async Task UpdateUserOnlineStatusAsync(int userId, bool isOnline)
        {
            using var connection = await GetConnectionAsync();
            var command = new MySqlCommand(
                "UPDATE users SET is_online = @is_online, last_login = @last_login WHERE id = @user_id",
                connection);

            command.Parameters.AddWithValue("@is_online", isOnline);
            command.Parameters.AddWithValue("@last_login", isOnline ? DateTime.UtcNow : (object)DBNull.Value);
            command.Parameters.AddWithValue("@user_id", userId);

            await command.ExecuteNonQueryAsync();
        }
    }

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
}
