var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();


app.UseDefaultFiles(); 
app.UseStaticFiles();  
app.MapMcp();

app.MapGet("/home", () => Results.File(Path.Combine(app.Environment.WebRootPath, "index.html"), "text/html"));


app.Run();