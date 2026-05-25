using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimpleMcpHttpServer.Models;

public class BancosRequest
{
    public int Pagina { get; set; } = 1;
    public int Cantidad { get; set; } = 100;
    public string Orden { get; set; } = "DESC";
    public string Columna { get; set; } = "fecha_emision";
    
    public JsonElement? Filtros { get; set; }
    
    public string[]? Agrupacion { get; set; }
    public Agregacion[]? Agregaciones { get; set; }
    public bool Distinct { get; set; } = false;
    public CampoCalculado[]? CamposCalculados { get; set; }
    public string[]? SeleccionarColumnas { get; set; }
}

public class Agregacion
{
    public string Operacion { get; set; } = string.Empty;
    public string Columna { get; set; } = string.Empty;
    public string? Alias { get; set; }
}

public class CampoCalculado
{
    public string Expresion { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
}