namespace SimpleMcpHttpServer.Utils;

public static class Config
{
    // Puedes cambiar esto para que lea de variables de entorno si lo prefieres
    // ej: Environment.GetEnvironmentVariable("DB_HOST")
    public static string Host = "localhost";
    public static string User = "root";
    public static string Password = "";
    public static string Database = "mcp";
    public static int Port = 3306;

    public static string ConnectionString => 
        $"Server={Host};Port={Port};Database={Database};User={User};Password={Password};";

    public static int CantidadPorDefecto = 100;
}