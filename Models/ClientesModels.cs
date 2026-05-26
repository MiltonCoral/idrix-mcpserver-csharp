using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimpleMcpHttpServer.Models;

public class ClientesRequest
{
    public int Pagina { get; set; } = 1;
    public int? Cantidad { get; set; }
    public string Orden { get; set; } = "DESC";
    public string Columna { get; set; } = "fecha_registro";
    
    public JsonElement? Filtros { get; set; }
    public JsonElement? Agrupacion { get; set; }
    public AgregacionCliente? Agregacion { get; set; }
    public FiltroSimple? Having { get; set; }
}

public class AgregacionCliente
{
    public string Operacion { get; set; } = string.Empty;
    public string Columna { get; set; } = string.Empty;
    public string? Alias { get; set; }
}

public class FiltroSimple
{
    public string Columna { get; set; } = string.Empty;
    public string Operador { get; set; } = "=";
    public JsonElement? Valor { get; set; }
}