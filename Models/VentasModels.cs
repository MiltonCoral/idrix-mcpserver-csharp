using System.Text.Json;

namespace SimpleMcpHttpServer.Models;

public class VentasRequest
{
    public int Pagina { get; set; } = 1;
    public int? Cantidad { get; set; }
    public string Orden { get; set; } = "DESC";
    public string Columna { get; set; } = "fecha";

    public JsonElement? Filtros { get; set; }
    public JsonElement? Agrupacion { get; set; }
    public Agregacion[]? Agregaciones { get; set; }
    public FiltroSimple? Having { get; set; }
    public bool Distinct { get; set; } = false;
    public CampoCalculado[]? CamposCalculados { get; set; }
    public string[]? SeleccionarColumnas { get; set; }
}