using System.Text.Json;

namespace SimpleMcpHttpServer.Models;

public class KardexRequest
{
    public int Pagina { get; set; } = 1;
    public int Cantidad { get; set; } = 100;
    public string Orden { get; set; } = "DESC";
    public string Columna { get; set; } = "kardexFechaTransaccion";
    
    public JsonElement? Filtros { get; set; }
    public JsonElement? Agrupacion { get; set; }
    public Agregacion[]? Agregaciones { get; set; }
    public FiltroSimple? Having { get; set; }
    public bool Distinct { get; set; } = false;
    public CampoCalculado[]? CamposCalculados { get; set; }
    public string[]? SeleccionarColumnas { get; set; }
}

// Nota: Si ya tienes FiltroSimple en ClientesModels.cs, quita esta definición duplicada
public class FiltroSimple
{
    public string Columna { get; set; } = string.Empty;
    public string Operador { get; set; } = "=";
    public JsonElement? Valor { get; set; }
}