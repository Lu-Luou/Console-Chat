# MySQL Configuration

## Configuración rápida

### 1. Iniciar MySQL con Docker Compose

```bash
# Iniciar el contenedor MySQL
docker-compose up -d mysql

# Iniciar también phpMyAdmin (opcional)
docker-compose up -d
```

### 2. Verificar que MySQL está funcionando

```bash
# Ver logs del contenedor
docker-compose logs mysql

# Conectar al contenedor MySQL
docker exec -it csharp-chat-mysql mysql -u chatuser -p
# Password: chatpassword
```

### 3. Configuración de la aplicación Csharp

Para usar MySQL en la app, necesitarás:

1. **Instalar el paquete NuGet de MySQL**:

```bash
dotnet add package MySql.EntityFrameworkCore --version 8.0.5
# o
dotnet add package MySqlConnector --version 2.3.7
```

1. **Strings de conexión**:
   - Para desarrollo local: `Server=localhost;Port=3306;Database=chatdb;Uid=chatuser;Pwd=chatpassword;`
   - Para Docker: `Server=mysql;Port=3306;Database=chatdb;Uid=chatuser;Pwd=chatpassword;`

## Estructura de la base de datos

### Tablas principales

- **users**: Información de usuarios
- **channels**: Canales/salas de chat
- **messages**: Mensajes del chat
- **file_transfers**: Archivos transferidos
- **channel_members**: Miembros de los canales

### Puertos expuestos

- **MySQL**: localhost:3306
- **phpMyAdmin**: localhost:8080

## Administración

### phpMyAdmin

Accede a [http://localhost:8080](http://localhost:8080) para administrar la base de datos gráficamente.

**Credenciales:**

- Servidor: mysql
- Usuario: chatuser
- Contraseña: chatpassword

### Comandos que uso

```bash
# Detener los contenedores
docker-compose down

# Detener y eliminar volúmenes (Elimina los datos)
docker-compose down -v

# Ver estado de los contenedores
docker-compose ps

# Reiniciar solo MySQL
docker-compose restart mysql

# Backup de la base de datos
docker exec csharp-chat-mysql mysqldump -u chatuser -pchatpassword chatdb > backup.sql

# Restaurar backup
docker exec -i csharp-chat-mysql mysql -u chatuser -pchatpassword chatdb < backup.sql
```

## Integración con Csharp

### Ejemplo de clase de conexión

```csharp
using MySqlConnector;

public class DatabaseConnection
{
    private readonly string _connectionString;
    
    public DatabaseConnection(string connectionString)
    {
        _connectionString = connectionString;
    }
    
    public async Task<MySqlConnection> GetConnectionAsync()
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }
}
```

### Ejemplo de uso

```csharp
var connectionString = "Server=localhost;Port=3306;Database=chatdb;Uid=chatuser;Pwd=chatpassword;";
var dbConnection = new DatabaseConnection(connectionString);

using var connection = await dbConnection.GetConnectionAsync();
var command = new MySqlCommand("SELECT * FROM users", connection);
var reader = await command.ExecuteReaderAsync();
```
