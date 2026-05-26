using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using MySqlConnector;
using SimpleMcpHttpServer.Models;
using SimpleMcpHttpServer.Utils;

namespace SimpleMcpHttpServer.Tools;

[McpServerToolType]
public static class BancosTools
{
    public static readonly Dictionary<string, string> ColumnMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "fecha_emision",          "MBAN_FECEMI" },
        { "origen_movimiento",      "MBAN_ORIGEN" },
        { "tipo_movimiento",        "MBAN_TIPOXX" },
        { "subtipo_1",              "MBAN_SUBTIP"  },
        { "subtipo_2",              "MBAN_SUBTIP2" },
        { "id_banco",               "BANC_CODIGO"  },
        { "banco_nombre",           "BANC_NOMBRE"  },
        { "cuenta_numero",          "BANC_CUENTA"  },
        { "tipo_cuenta",            "BANC_TIPOCU"  },
        { "titular_cuenta",         "BANC_TITULA"  },
        { "monto_movimiento",       "MBAN_MONTOX"  },
        { "saldo_detalle",          "MBAN_SALDET"  },
        { "cedula_cliente",         "MBAN_CEDULA"  },
        { "titular_movimiento",     "MBAN_TITULA"  },
        { "comprobante",            "MBAN_COMPRO"  },
        { "numero_cheque",          "MBAN_CHEQUE"  },
        { "fecha_cheque",           "MBAN_FECHEQ"  },
        { "cheque_anulado",         "MBAN_CHEQUEANULADO" },
        { "descripcion",            "MBAN_DESCRI"  },
        { "estado_movimiento",      "MBAN_ESTADO"  },
        { "usuario_creacion",       "MBAN_USUING"  },
        { "fecha_creacion",         "MBAN_FECING"  },
        { "usuario_modificacion",   "MBAN_USUMOD"  },
        { "fecha_modificacion",     "MBAN_FECMOD"  },
        { "usuario_anulacion",      "MBAN_USUANU"  },
        { "fecha_anulacion",        "MBAN_FECANU"  },
        { "saldo_cuenta",           "BANC_SALDOX"  }
    };

    [McpServerTool(Name = "bancos")]
    [Description(@"Consulta movimientos bancarios desde base de datos MySQL.

REGLAS IMPORTANTES:
- Con agregación: NO usar 'cantidad' (devuelve todos los grupos)
- Sin agregación: SIEMPRE usar 'cantidad' para limitar resultados
- Para conteos: usar agregaciones[].operacion = 'COUNT'
- Orden por defecto: fecha_emision DESC

CAMPOS PRINCIPALES:
- fecha_emision, banco_nombre, monto_movimiento, tipo_movimiento, origen_movimiento, subtipo_1

CAPACIDADES AVANZADAS:
- Agrupación múltiple: agrupa por varias columnas (string o array)
- Cláusulas HAVING: filtra grupos después de la agrupación
- Múltiples agregaciones: varias operaciones simultáneas (SUM, COUNT, AVG, MAX, MIN)
- DISTINCT: valores únicos (distinct: true + seleccionar_columnas)
- Campos calculados: expresiones SQL personalizadas (YEAR(), MONTH(), CASE WHEN, etc.)
- Selección específica: lista de columnas a retornar

OPERADORES PERMITIDOS:
=, !=, >, <, >=, <=, LIKE, BETWEEN, IN, IS NULL, IS NOT NULL

EJEMPLOS:
1. Últimos 20 movimientos: {cantidad: 20, orden: ""DESC"", columna: ""fecha_emision""}
2. Contar por banco: {agrupacion: ""banco_nombre"", agregaciones: [{operacion: ""COUNT"", columna: ""monto_movimiento""}]}
3. Total ingresos por tipo: {agrupacion: ""tipo_movimiento"", agregaciones: [{operacion: ""SUM"", columna: ""monto_movimiento"", alias: ""total""}]}
4. Bancos con más de 50 movimientos: {agrupacion: ""banco_nombre"", agregaciones: [{operacion: ""COUNT"", columna: ""monto_movimiento"", alias: ""total""}], having: {columna: ""monto_movimiento"", operador: "">"", valor: 50}}
5. DISTINCT cuentas: {distinct: true, seleccionar_columnas: [""cuenta_numero""]}
6. Análisis mensual: {campos_calculados: [{expresion: ""YEAR(MBAN_FECEMI)"", alias: ""anio""}, {expresion: ""MONTH(MBAN_FECEMI)"", alias: ""mes""}], agrupacion: [""anio"", ""mes""], agregaciones: [{operacion: ""SUM"", columna: ""monto_movimiento""}]}")]
    public static async Task<object> ConsultarBancosAsync(
        [Description("Parámetros de búsqueda, filtros, paginación, agrupaciones y campos calculados")] BancosRequest request)
    {
        try
        {
            var resultados = await EjecutarConsultaBancos(request);

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
            Console.WriteLine($"[ERROR MYSQL bancos]: {sqlEx.Message}");
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
            Console.WriteLine($"[ERROR GENERAL bancos]: {ex.Message}");
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

    private static async Task<List<Dictionary<string, object>>> EjecutarConsultaBancos(BancosRequest req)
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
        string distinctKeyword = req.Distinct ? "DISTINCT " : "";

        // ── SELECT dinámico ──
        var selectParts = new List<string>();

        if (hayAgregaciones)
        {
            // Primero las columnas de agrupación con alias amigables
            if (!string.IsNullOrEmpty(groupByPart))
            {
                for (int i = 0; i < agrupacionDbCols.Count; i++)
                    selectParts.Add($"{agrupacionDbCols[i]} AS {agrupacionFriendlyCols[i]}");
            }

            // Luego cada agregación
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
            // SELECT * con aliases amigables
            foreach (var kv in ColumnMap)
                selectParts.Add($"{kv.Value} AS {kv.Key}");
        }

        // Campos calculados (se añaden al SELECT con reemplazo de nombres amigables)
        if (req.CamposCalculados != null && req.CamposCalculados.Length > 0)
        {
            foreach (var campo in req.CamposCalculados)
            {
                string expresion = campo.Expresion;
                foreach (var kv in ColumnMap.OrderByDescending(x => x.Key.Length))
                {
                    string pattern = $@"\b{Regex.Escape(kv.Key)}\b";
                    expresion = Regex.Replace(expresion, pattern, kv.Value, RegexOptions.IgnoreCase);
                }
                selectParts.Add($"{expresion} AS {campo.Alias}");
            }
        }

        string selectPart = string.Join(", ", selectParts);
        string sql = $"SELECT {distinctKeyword}{selectPart} FROM vista_movimientos_bancos";

        // ── WHERE ──
        if (req.Filtros.HasValue)
        {
            string whereClause = SqlUtils.BuildWhereClause(req.Filtros.Value, command, ColumnMap, ref paramCounter);
            if (!string.IsNullOrEmpty(whereClause))
                sql += $" WHERE {whereClause}";
        }

        // ── GROUP BY ──
        if (!string.IsNullOrEmpty(groupByPart))
            sql += $" GROUP BY {groupByPart}";

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
                            valorObj = $"%{strVal}%";
                        command.Parameters.AddWithValue(p, valorObj);
                        havingClause = $"{havingDbCol} {havingOp} {p}";
                    }
                    break;
            }

            if (!string.IsNullOrEmpty(havingClause))
                sql += $" HAVING {havingClause}";
        }

        // ── ORDER BY ──
        var dbColumnaOrden = ColumnMap.GetValueOrDefault(req.Columna.ToLower(), req.Columna.ToUpper());
        var ordenSeguro = req.Orden.ToUpper() == "ASC" ? "ASC" : "DESC";
        sql += $" ORDER BY {dbColumnaOrden} {ordenSeguro}";

        // ── LIMIT / OFFSET (solo sin agregaciones) ──
        if (!hayAgregaciones)
        {
            sql += " LIMIT @limit OFFSET @offset";
            command.Parameters.AddWithValue("@limit", req.Cantidad);
            command.Parameters.AddWithValue("@offset", (req.Pagina - 1) * req.Cantidad);
        }

        command.CommandText = sql;

        Console.WriteLine($"[QUERY BANCOS EJECUTADO]: {sql}");
        foreach (MySqlParameter p in command.Parameters)
            Console.WriteLine($"  {p.ParameterName} = {p.Value}");

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