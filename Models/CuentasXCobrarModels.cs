using System.Text.Json;

namespace SimpleMcpHttpServer.Models;

public class CuentasXCobrarRequest
{
    public int Pagina { get; set; } = 1;
    public int? Cantidad { get; set; }
    public string Orden { get; set; } = "DESC";
    public string Columna { get; set; } = "fecha_factura";
    
    public JsonElement? Filtros { get; set; }
    public JsonElement? Agrupacion { get; set; }
    public AgregacionCliente? Agregacion { get; set; }
    public FiltroSimple? Having { get; set; }
}