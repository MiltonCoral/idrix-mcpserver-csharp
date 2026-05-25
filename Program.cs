var builder = WebApplication.CreateBuilder(args);

// Registrar el servidor MCP
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

// 👇 1. Habilitar páginas web (DEBE IR ANTES DE TUS RUTAS)
app.UseDefaultFiles(); // Busca "index.html" automáticamente cuando entras a la raíz ("/")
app.UseStaticFiles();  // Habilita que la carpeta "wwwroot" sea pública

// Mapear el endpoint de MCP
app.MapMcp();

// 👇 2. Si quieres una ruta específica llamada "/home" que devuelva tu HTML:
app.MapGet("/home", () => Results.File(Path.Combine(app.Environment.WebRootPath, "index.html"), "text/html"));


app.Run();