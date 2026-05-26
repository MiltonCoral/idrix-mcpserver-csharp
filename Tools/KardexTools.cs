using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using MySqlConnector;
using SimpleMcpHttpServer.Models;
using SimpleMcpHttpServer.Utils;

namespace SimpleMcpHttpServer.Tools;

[McpServerToolType]
public static class KardexTools
{
    public static readonly Dictionary<string, string> ColumnMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "productoNombre", "PROD_NOMBRE" },
        { "productoCategoria", "PROD_CATEGO" },
        { "productoUnidad", "PROD_UNIDAD" },
        { "productoUnidadesCaja", "PROD_UNIDADESCAJA" },
        { "productoUnidadesIntInf", "PROD_UNIDADESINTINF" },
        { "productoUnidadSup", "PROD_UNIDAD_SUP" },
        { "productoUnidadInf", "PROD_UNIDAD_INF" },
        { "productoCosto", "PROD_PCOSTO" },
        { "productoPrecioPublico", "PROD_PVPUB" },
        { "productoEstado", "PROD_ESTADO" },
        { "productoPerecible", "PROD_PERECI" },
        { "productoFechaIngreso", "PROD_FECING" },
        { "kardexCodigo", "KARD_CODIGO" },
        { "kardexFechaTransaccion", "KARD_FECTRA" },
        { "kardexTipo", "KARD_TIPOXX" },
        { "kardexDetalle", "KARD_DETALL" },
        { "kardexObservacion", "KARD_OBSERV" },
        { "kardexProductoId", "KARD_PROIDE" },
        { "kardexProductoNombre", "KARD_PRONOM" },
        { "kardexTransaccionCodigo", "KARD_CODTRA" },
        { "kardexClienteNombre", "KARD_CLINOM" },
        { "kardexClienteId", "KARD_CLIIDE" },
        { "kardexUsuarioIngreso", "KARD_USUING" },
        { "kardexFechaIngreso", "KARD_FECING" },
        { "detalleEntradaCantidad", "DEKA_ENTCAN" },
        { "detalleEntradaCosto", "DEKA_ENTCOS" },
        { "detalleEntradaTotal", "DEKA_ENTTOT" },
        { "detalleSaldoCantidad", "DEKA_SALCAN" },
        { "detalleSaldoCosto", "DEKA_SALCOS" },
        { "detalleSaldoTotal", "DEKA_SALTOT" },
        { "detalleExistenciaCantidad", "DEKA_EXICAN" },
        { "detalleExistenciaCosto", "DEKA_EXICOS" },
        { "detalleExistenciaTotal", "DEKA_EXITOT" },
        { "bodegaNombre", "BODE_NOMBRE" },
        { "sucursalNombre", "SUCU_NOMBRE" }
    };

    [McpServerTool(Name = "kardex")]
    [Description(@"Consulta movimientos de inventario (Kardex) desde base de datos MySQL.

REGLAS IMPORTANTES:
- Con agregación: NO usar 'cantidad' (devuelve todos los grupos)
- Sin agregación: SIEMPRE usar 'cantidad' para limitar resultados
- Para conteos: usar agregacion.operacion = ""COUNT""
- Orden por defecto: kardexFechaTransaccion DESC

CAMPOS PRINCIPALES:
- kardexFechaTransaccion: Fecha de la transacción del producto
- kardexTipo: Tipo de movimiento (ENTRADAS, SALIDAS)
- kardexDetalle: Detalle del movimiento (INVENTARIO INICIAL, SALIDA POR TRANSFERENCIA, ENTRADA POR TRANSFERENCIA, VENTAS, NOTA DE CRÉDITO, ANULACIÓN DOCUMENTO DE VENTA, AJUSTES SALIDA, AJUSTES ENTRADA, COMPRAS)
- kardexObservacion: Observación (SN, TRANSFERENCIA DE BODEGA, VENTA GENERADA, NOTA DE CRÉDITO - DEVOLUCION, ANULACIÓN DOCUMENTO DE VENTA)
- kardexProductoId, kardexProductoNombre, kardexTransaccionCodigo, kardexClienteNombre, kardexClienteId, kardexUsuarioIngreso, kardexFechaIngreso

DATOS DEL PRODUCTO:
- productoNombre, productoCategoria, productoUnidad, productoUnidadesCaja, productoCosto, productoPrecioPublico, productoEstado, productoPerecible, productoFechaIngreso

DETALLE DE INVENTARIO:
- detalleEntradaCantidad, detalleEntradaCosto, detalleEntradaTotal
- detalleSaldoCantidad, detalleSaldoCosto, detalleSaldoTotal
- detalleExistenciaCantidad, detalleExistenciaCosto, detalleExistenciaTotal

UBICACIÓN:
- bodegaNombre, sucursalNombre

OPERADORES:
=, !=, >, <, >=, <=, LIKE (usar %), BETWEEN (array 2 valores), IN (array), IS NULL, IS NOT NULL

OPERACIONES ADICIONALES:
- DISTINCT: Valores únicos (distinct: true)
- Campos calculados: Expresiones SQL personalizadas (YEAR(), MONTH(), +, -, *, /, CASE WHEN, CONCAT(), etc.)
- Selección específica: Lista de columnas a retornar (seleccionar_columnas)
- Agregaciones múltiples: Array de funciones de agregación

EJEMPLOS:
1. Últimos 20 movimientos: {cantidad: 20, orden: ""DESC"", columna: ""kardexFechaTransaccion""}
2. Filtrar por producto: {filtros: {columna: ""kardexProductoNombre"", operador: ""="", valor: ""Aceite Girasol 1L""}}
3. Contar todos los movimientos: {agregaciones: [{operacion: ""COUNT"", columna: ""kardexCodigo""}]}
4. Sumar por tipo: {agrupacion: ""kardexTipo"", agregaciones: [{operacion: ""SUM"", columna: ""detalleEntradaCantidad""}]}
5. Filtro complejo AND: {filtros: {AND: [{columna: ""kardexTipo"", operador: ""="", valor: ""ENTRADA""}, {columna: ""detalleEntradaCantidad"", operador: "">"", valor: 100}]}}
6. Agrupar con HAVING: {agrupacion: ""productoCategoria"", agregaciones: [{operacion: ""COUNT"", columna: ""kardexCodigo""}], having: {columna: ""kardexCodigo"", operador: "">"", valor: 50}}
7. Rango de fechas: {filtros: {columna: ""kardexFechaTransaccion"", operador: ""BETWEEN"", valor: [""2024-01-01"", ""2024-12-31""]}}
8. DISTINCT productos: {distinct: true, seleccionar_columnas: [""kardexProductoNombre""]}
9. Análisis mensual: {campos_calculados: [{expresion: ""YEAR(kardexFechaTransaccion)"", alias: ""anio""}, {expresion: ""MONTH(kardexFechaTransaccion)"", alias: ""mes""}], agrupacion: [""anio"", ""mes""], agregaciones: [{operacion: ""SUM"", columna: ""detalleEntradaTotal""}]}
10. Costo con margen: {campos_calculados: [{expresion: ""detalleExistenciaCosto * 1.30"", alias: ""precio_sugerido""}], cantidad: 20}
11. Múltiples agregaciones por categoría: {agrupacion: ""productoCategoria"", agregaciones: [{operacion: ""COUNT"", columna: ""kardexCodigo"", alias: ""total_movimientos""}, {operacion: ""SUM"", columna: ""detalleEntradaCantidad"", alias: ""total_entradas""}]}

CONSEJOS:
- Para datasets grandes, usar filtros de fecha
- Combinar filtros con AND/OR para mayor precisión
- Usar LIKE con % para búsquedas parciales (ej: ""ACEITE%"")
- DISTINCT es útil para obtener listas únicas
- Seleccionar solo columnas necesarias mejora el rendimiento")]
    public static async Task<object> ConsultarKardexAsync(
        [Description("Parámetros de búsqueda, filtros, paginación, agrupaciones y campos calculados")] KardexRequest request)
    {
        try
        {
            var resultados = await EjecutarConsultaKardex(request);

            if (resultados.Count == 0)
            {
                return new
                {
                    success = true,
                    dataType = "empty_result",
                    message = "No se encontraron registros de Kardex que coincidan con los criterios de búsqueda.",
                    data = resultados,
                    suggestion = "Intente con diferentes criterios de búsqueda o verifique los filtros."
                };
            }

            return new
            {
                success = true,
                dataType = "data_found",
                message = $"Se encontraron {resultados.Count} registro(s) de Kardex.",
                data = resultados,
                count = resultados.Count
            };
        }
        catch (MySqlException sqlEx)
        {
            Console.WriteLine($"[ERROR MYSQL kardex]: {sqlEx.Message}");
            var errorType = sqlEx.Number switch
            {
                1146 => "TABLE_NOT_FOUND",
                1045 => "ACCESS_DENIED",
                2003 or 2002 => "CONNECTION_FAILED",
                1054 => "INVALID_FIELD",
                1064 => "SQL_SYNTAX_ERROR",
                1205 => "QUERY_TIMEOUT",
                1213 => "DATABASE_DEADLOCK",
                _ => "DATABASE_ERROR"
            };

            var suggestion = errorType switch
            {
                "TABLE_NOT_FOUND" => "Verifique que la base de datos esté configurada correctamente y que la tabla exista.",
                "ACCESS_DENIED" => "Verifique las credenciales de la base de datos y los permisos del usuario.",
                "CONNECTION_FAILED" => "Verifique la configuración de conexión.",
                "INVALID_FIELD" => "Verifique que los nombres de columnas en la consulta sean correctos.",
                "SQL_SYNTAX_ERROR" => "Hay un error en la sintaxis de la consulta SQL generada.",
                "QUERY_TIMEOUT" => "Intente con filtros más específicos para reducir el tiempo de consulta.",
                "DATABASE_DEADLOCK" => "Espere unos momentos y vuelva a intentar la consulta.",
                _ => "Error interno de la base de datos. Consulte los detalles para más información."
            };

            return new
            {
                success = false,
                error = true,
                errorType,
                message = "Error de base de datos.",
                details = sqlEx.Message,
                suggestion
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR GENERAL kardex]: {ex.Message}");
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

private static async Task<List<Dictionary<string, object>>> EjecutarConsultaKardex(KardexRequest req)
    {
        using var connection = new MySqlConnection(Config.ConnectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();

        int paramCounter = 0;

        // ── Normalizar agrupación ──
        var agrupacionDbCols = new List<string>();
        var agrupacionFriendlyCols = new List<string>();

        if (req.Agrupacion.HasValue)
        {
            var agr = req.Agrupacion.Value;
            if (agr.ValueKind == JsonValueKind.String)
            {
                var friendly = agr.GetString()!;
                var dbCol = ColumnMap.GetValueOrDefault(friendly.ToLower(), friendly.ToUpper());
                agrupacionDbCols.Add(dbCol);
                agrupacionFriendlyCols.Add(friendly);
            }
            else if (agr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in agr.EnumerateArray())
                {
                    var friendly = item.GetString()!;
                    var dbCol = ColumnMap.GetValueOrDefault(friendly.ToLower(), friendly.ToUpper());
                    agrupacionDbCols.Add(dbCol);
                    agrupacionFriendlyCols.Add(friendly);
                }
            }
        }

        var groupByPart = agrupacionDbCols.Count > 0 ? string.Join(", ", agrupacionDbCols) : null;
        bool hayAgregaciones = req.Agregaciones != null && req.Agregaciones.Length > 0;
        bool haySeleccionarColumnas = req.SeleccionarColumnas != null && req.SeleccionarColumnas.Length > 0;

        // ── SELECT dinámico ──
        var selectParts = new List<string>();
        string distinctKeyword = req.Distinct ? "DISTINCT " : "";

        if (hayAgregaciones)
        {
            if (!string.IsNullOrEmpty(groupByPart))
            {
                for (int i = 0; i < agrupacionDbCols.Count; i++)
                {
                    selectParts.Add($"{agrupacionDbCols[i]} AS {agrupacionFriendlyCols[i]}");
                }
            }

            foreach (var agg in req.Agregaciones!)
            {
                var colDB = ColumnMap.GetValueOrDefault(agg.Columna.ToLower(), agg.Columna.ToUpper());
                var alias = !string.IsNullOrEmpty(agg.Alias) ? agg.Alias : $"{agg.Operacion}_{agg.Columna}";
                selectParts.Add($"{agg.Operacion}({colDB}) AS {alias}");
            }
        }
        else if (haySeleccionarColumnas)
        {
            foreach (var col in req.SeleccionarColumnas!)
            {
                var dbCol = ColumnMap.GetValueOrDefault(col.ToLower(), col.ToUpper());
                selectParts.Add($"{dbCol} AS {col}");
            }
        }
        else
        {
            foreach (var kv in ColumnMap)
            {
                selectParts.Add($"{kv.Value} AS {kv.Key}");
            }
        }

        // Campos calculados
        if (req.CamposCalculados != null && req.CamposCalculados.Length > 0)
        {
            foreach (var campo in req.CamposCalculados)
            {
                string expresion = campo.Expresion;

                // Reemplazar nombres amigables por columnas DB (ordenar por longitud descendente para evitar reemplazos parciales)
                foreach (var kv in ColumnMap.OrderByDescending(x => x.Key.Length))
                {
                    string pattern = $@"\b{Regex.Escape(kv.Key)}\b";
                    expresion = Regex.Replace(expresion, pattern, kv.Value, RegexOptions.IgnoreCase);
                }

                selectParts.Add($"{expresion} AS {campo.Alias}");
            }
        }

        string selectPart = string.Join(", ", selectParts);

        string sql = $"SELECT {distinctKeyword}{selectPart} FROM vista_detallekardex";

        // ── WHERE ──
        if (req.Filtros.HasValue)
        {
            string whereClause = SqlUtils.BuildWhereClause(req.Filtros.Value, command, ColumnMap, ref paramCounter);
            if (!string.IsNullOrEmpty(whereClause))
            {
                sql += $" WHERE {whereClause}";
            }
        }

        // ── GROUP BY ──
        if (!string.IsNullOrEmpty(groupByPart))
        {
            sql += $" GROUP BY {groupByPart}";
        }

        // ── HAVING ──
        if (req.Having != null && !string.IsNullOrEmpty(groupByPart))
        {
            var havingDbCol = ColumnMap.GetValueOrDefault(req.Having.Columna.ToLower(), req.Having.Columna.ToUpper());
            var havingOp = req.Having.Operador.ToUpper();

            string? havingClause = null;

            switch (havingOp)
            {
                case "IS NULL":
                case "IS NOT NULL":
                    havingClause = $"{havingDbCol} {havingOp}";
                    break;

                case "BETWEEN":
                    if (req.Having.Valor.HasValue && req.Having.Valor.Value.ValueKind == JsonValueKind.Array)
                    {
                        var arr = req.Having.Valor.Value.EnumerateArray().ToList();
                        if (arr.Count == 2)
                        {
                            var p1 = $"@h{paramCounter++}";
                            var p2 = $"@h{paramCounter++}";
                            command.Parameters.AddWithValue(p1, GetJsonValue(arr[0]));
                            command.Parameters.AddWithValue(p2, GetJsonValue(arr[1]));
                            havingClause = $"{havingDbCol} BETWEEN {p1} AND {p2}";
                        }
                    }
                    break;

                case "IN":
                    if (req.Having.Valor.HasValue && req.Having.Valor.Value.ValueKind == JsonValueKind.Array)
                    {
                        var inParams = new List<string>();
                        foreach (var item in req.Having.Valor.Value.EnumerateArray())
                        {
                            var p = $"@h{paramCounter++}";
                            command.Parameters.AddWithValue(p, GetJsonValue(item));
                            inParams.Add(p);
                        }
                        havingClause = inParams.Count > 0 ? $"{havingDbCol} IN ({string.Join(", ", inParams)})" : null;
                    }
                    break;

                default:
                    if (req.Having.Valor.HasValue)
                    {
                        var p = $"@h{paramCounter++}";
                        var valorObj = GetJsonValue(req.Having.Valor.Value);
                        if (havingOp == "LIKE" && valorObj is string strVal && !strVal.Contains("%"))
                        {
                            valorObj = $"%{strVal}%";
                        }
                        command.Parameters.AddWithValue(p, valorObj);
                        havingClause = $"{havingDbCol} {havingOp} {p}";
                    }
                    break;
            }

            if (!string.IsNullOrEmpty(havingClause))
            {
                sql += $" HAVING {havingClause}";
            }
        }

        // ── ORDER BY ──
        var dbColumnaOrden = ColumnMap.GetValueOrDefault(req.Columna.ToLower(), req.Columna.ToUpper());
        var ordenSeguro = req.Orden.ToUpper() == "ASC" ? "ASC" : "DESC";
        sql += $" ORDER BY {dbColumnaOrden} {ordenSeguro}";

        // ── LIMIT / OFFSET ──
        if (!hayAgregaciones)
        {
            sql += " LIMIT @limit OFFSET @offset";
            command.Parameters.AddWithValue("@limit", req.Cantidad);
            command.Parameters.AddWithValue("@offset", (req.Pagina - 1) * req.Cantidad);
        }

        command.CommandText = sql;

        Console.WriteLine($"[QUERY KARDEX EJECUTADO]: {sql}");
        foreach (MySqlParameter p in command.Parameters)
        {
            Console.WriteLine($"  {p.ParameterName} = {p.Value}");
        }

        // ── Ejecutar y mapear resultados ──
        var resultados = new List<Dictionary<string, object>>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var fila = new Dictionary<string, object>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                string alias = reader.GetName(i);
                var value = reader.IsDBNull(i) ? null! : reader.GetValue(i);

                if (value is DateTime dt)
                    value = dt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

                fila[alias] = value;
            }
            resultados.Add(fila);
        }

        return resultados;
    }

    private static object GetJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.TryGetInt64(out long l) ? l : element.GetDouble(),
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => element.ToString()
        };
    }
}