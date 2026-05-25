using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using MySqlConnector;
using SimpleMcpHttpServer.Models; // Importamos los modelos
using SimpleMcpHttpServer.Utils;  // Importamos Config y SqlUtils

namespace SimpleMcpHttpServer.Tools;

[McpServerToolType]
public static class BancosTools
{
    // Diccionario de mapping amigable -> columna BD (Igual que en Node)
    public static readonly Dictionary<string, string> ColumnMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "fecha_emision", "MBAN_FECEMI" },
        { "origen_movimiento", "MBAN_ORIGEN" },
        { "tipo_movimiento", "MBAN_TIPOXX" },
        { "subtipo_1", "MBAN_SUBTIP" },
        { "subtipo_2", "MBAN_SUBTIP2" },
        { "id_banco", "BANC_CODIGO" },
        { "banco_nombre", "BANC_NOMBRE" },
        { "cuenta_numero", "BANC_CUENTA" },
        { "tipo_cuenta", "BANC_TIPOCU" },
        { "titular_cuenta", "BANC_TITULA" },
        { "monto_movimiento", "MBAN_MONTOX" },
        { "saldo_detalle", "MBAN_SALDET" },
        { "cedula_cliente", "MBAN_CEDULA" },
        { "titular_movimiento", "MBAN_TITULA" },
        { "comprobante", "MBAN_COMPRO" },
        { "numero_cheque", "MBAN_CHEQUE" },
        { "fecha_cheque", "MBAN_FECHEQ" },
        { "cheque_anulado", "MBAN_CHEQUEANULADO" },
        { "descripcion", "MBAN_DESCRI" },
        { "estado_movimiento", "MBAN_ESTADO" },
        { "usuario_creacion", "MBAN_USUING" },
        { "fecha_creacion", "MBAN_FECING" },
        { "usuario_modificacion", "MBAN_USUMOD" },
        { "fecha_modificacion", "MBAN_FECMOD" },
        { "usuario_anulacion", "MBAN_USUANU" },
        { "fecha_anulacion", "MBAN_FECANU" },
        { "saldo_cuenta", "BANC_SALDOX" }
    };

    [McpServerTool(Name = "bancos")]
    [Description(@"Consulta movimientos bancarios desde base de datos MySQL.

REGLAS IMPORTANTES:
- Con agregación: NO usar 'cantidad' (devuelve todos los grupos)
- Sin agregación: SIEMPRE usar 'cantidad' para limitar resultados
- Para conteos: usar agregacion.operacion = 'COUNT'
- Orden por defecto: fecha_emision DESC

CAMPOS PRINCIPALES:
- fecha_emision, banco_nombre, monto_movimiento, tipo_movimiento, origen_movimiento, subtipo_1

OPERADORES PERMITIDOS:
=, !=, >, <, >=, <=, LIKE, BETWEEN, IN, IS NULL, IS NOT NULL")]
    public static async Task<object> ConsultarBancosAsync(
        [Description("Parámetros de búsqueda, filtros, paginación y agrupaciones")] BancosRequest request)
    {
        try
        {
            var resultados = await EjecutarConsultaProcedimental(request);
            
            if (resultados.Count == 0)
            {
                return new 
                { 
                    success = true, 
                    dataType = "empty_result", 
                    message = "No se encontraron movimientos que coincidan con los criterios de búsqueda.", 
                    data = resultados,
                    suggestion = "Intente con diferentes criterios de búsqueda o verifique los filtros."
                };
            }
            
            return new 
            { 
                success = true, 
                dataType = "data_found", 
                message = $"Se encontraron {resultados.Count} movimiento(s).", 
                data = resultados, 
                count = resultados.Count 
            };
        }
        catch (MySqlException sqlEx)
        {
            Console.WriteLine($"[ERROR MYSQL]: {sqlEx.Message}");
            return new 
            { 
                success = false, 
                error = true, 
                errorType = "DATABASE_ERROR", 
                message = "Error de base de datos.",
                details = sqlEx.Message 
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR GENERAL]: {ex.Message}");
            return new 
            { 
                success = false, 
                error = true, 
                errorType = "WORKER_ERROR", 
                message = "Error inesperado en el procesamiento.", 
                details = ex.Message 
            };
        }
    }

    private static async Task<List<Dictionary<string, object>>> EjecutarConsultaProcedimental(BancosRequest req)
    {
        using var connection = new MySqlConnection(Config.ConnectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();

        int cantidadReal = req.Cantidad; 
        
        // 1. Resolver columnas y partes iniciales de la consulta
        string dbColumna = ColumnMap.GetValueOrDefault(req.Columna, req.Columna.ToUpper());
        string distinctKeyword = req.Distinct ? "DISTINCT " : "";
        
        // Lógica simplificada de selección (Si necesitas la lógica compleja de agregaciones, se expande aquí)
        string selectPart = "*"; 
        
        if (req.SeleccionarColumnas != null && req.SeleccionarColumnas.Length > 0)
        {
            var selectCols = req.SeleccionarColumnas.Select(col => 
            {
                string dbCol = ColumnMap.GetValueOrDefault(col.ToLower(), col.ToUpper());
                return $"{dbCol} AS {col}";
            });
            selectPart = string.Join(", ", selectCols);
        }

        string sql = $"SELECT {distinctKeyword}{selectPart} FROM vista_movimientos_bancos";

        // 2. Aplicar Filtros (Cláusula WHERE dinámica usando SqlUtils)
        if (req.Filtros.HasValue)
        {
            int paramCounter = 0; // Se pasa por referencia para generar @p0, @p1, etc.
            string whereClause = SqlUtils.BuildWhereClause(req.Filtros.Value, command, ColumnMap, ref paramCounter);
            
            if (!string.IsNullOrEmpty(whereClause))
            {
                sql += $" WHERE {whereClause}";
            }
        }

        // 3. Ordenamiento Seguro
        string ordenSeguro = req.Orden.ToUpper() == "ASC" ? "ASC" : "DESC";
        sql += $" ORDER BY {dbColumna} {ordenSeguro}";

        // 4. Paginación (Solo si no hay agregaciones complejas que lo impidan)
        if (req.Agregaciones == null || req.Agregaciones.Length == 0)
        {
            sql += " LIMIT @limit OFFSET @offset";
            command.Parameters.AddWithValue("@limit", cantidadReal);
            command.Parameters.AddWithValue("@offset", (req.Pagina - 1) * cantidadReal);
        }

        command.CommandText = sql;

        Console.WriteLine($"[QUERY EJECUTADO]: {sql}");
        foreach(MySqlParameter p in command.Parameters)
        {
            Console.WriteLine($"  {p.ParameterName} = {p.Value}");
        }

        // 5. Ejecutar y mapear
        var resultados = new List<Dictionary<string, object>>();
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            var fila = new Dictionary<string, object>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                string dbName = reader.GetName(i);
                
                string nombreAmigable = ColumnMap.FirstOrDefault(x => x.Value.Equals(dbName, StringComparison.OrdinalIgnoreCase)).Key ?? dbName;
                
                fila[nombreAmigable] = reader.IsDBNull(i) ? null! : reader.GetValue(i);
            }
            resultados.Add(fila);
        }

        return resultados;
    }
}