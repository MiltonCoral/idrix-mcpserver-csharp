using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using MySqlConnector;
using SimpleMcpHttpServer.Models;
using SimpleMcpHttpServer.Utils;

namespace SimpleMcpHttpServer.Tools;

[McpServerToolType]
public static class CuentasXCobrarTools
{
    public static readonly Dictionary<string, string> ColumnMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "numero_documento", "FACTURA" },
        { "fecha_factura", "DOCU_FECFAC" },
        { "total_factura", "DOCU_TOTALX" },
        { "tipo_documento", "DOCU_TIPOXX" },
        { "total_pagado", "DOCU_TOTPAG" },
        { "abono", "DOCU_ABONOX" },
        { "valor_extra_1", "DOCU_VALOR2" },
        { "valor_extra_2", "DOCU_VALOR3" },
        { "saldo_pendiente", "DOCU_SALDOX" },
        { "fecha_cobro", "DOCU_FECHACOBRO" },
        { "forma_pago_1", "DOCU_FORPAG" },
        { "forma_pago_2", "DOCU_FORPAG2" },
        { "forma_pago_3", "DOCU_FORPAG3" },
        { "estado_documento", "DOCU_ESTADO" },
        { "estado_cuenta", "ESTADO_CUENTA" },
        { "cedula_cliente", "CLIE_IDENTI" },
        { "razon_social", "CLIE_RAZONS" },
        { "nombre_comercial", "CLIE_NOMCOM" },
        { "canal_venta", "DOCU_CANALV" },
        { "nombre_colaborador", "COLA_NOMBRE" },
        { "apellido_colaborador", "COLA_APELLI" }
    };

    [McpServerTool(Name = "cuentasXCobrar")]
    [Description(@"Esta herramienta obtiene información de cuentas por cobrar con soporte de paginación, filtros, ordenamiento, agrupación y agregaciones.

Devuelve los siguientes campos:
- numero_documento: Numero de documento de factura
- fecha_factura: Fecha de la factura Emitida
- total_factura: Total de pago
- tipo_documento: Tipo de documento
- total_pagado: Total que ha realizado el pago
- abono: Cantidad de Abono realizado
- valor_extra_1: Valor extra 1
- valor_extra_2: Valor extra 2
- saldo_pendiente: Saldo pendiente por pagar
- fecha_cobro: Fecha de cobro
- forma_pago_1: Forma de pago 1
- forma_pago_2: Forma de pago 2
- forma_pago_3: Forma de pago 3
- estado_documento: EMITIDA, ANULADO, ABONADA, PAGADA
- estado_cuenta: PENDIENTE, VENCIDA, COBRADA
- cedula_cliente: Cedula del Cliente
- razon_social: Nombre del Cliente / Razon Social
- nombre_comercial: Nombre del Comercial
- canal_venta: VENTA DIRECTA, VENTA MOSTRADOR, VENTA TELEFONICA, VENTA INTERNET
- nombre_colaborador: Nombres del Colaborador / Cobrador
- apellido_colaborador: Apellidos del Colaborador / Cobrador

CAPACIDADES AVANZADAS:
- **Agrupación múltiple**: Agrupa por varias columnas simultáneamente
- **Cláusulas HAVING**: Filtra grupos después de la agrupación
- **Análisis multidimensional**: Combina agrupación con filtros complejos

IMPORTANTE:
- Para contar el total de cuentas por cobrar, usa agregacion con operacion ""COUNT"" y columna ""numero_documento"".
- NO uses el parámetro cantidad para contar registros - solo para limitar resultados.
- Si no especificas cantidad, se devolverán hasta 100 registros (si no hay agregación).
- **USA TODA LA INFORMACIÓN DEVUELTA**: Analiza y menciona TODOS los datos disponibles.

Parámetros disponibles:
- pagina: número de página
- cantidad: cantidad por página (solo cuando se solicite una cantidad específica)
- columna: campo por el que se ordenará
- orden: orden de los resultados (ASC o DESC)
- filtros: condiciones de búsqueda con soporte AND/OR
- agrupacion: columna(s) por la cual agrupar - puede ser string o array
- agregacion: operaciones de agregación (SUM, COUNT, AVG, MAX, MIN)
- having: condición para filtrar grupos después de la agrupación

Ejemplos de uso avanzado:
- ""¿Cuántas cuentas por cobrar tengo?"" → agregacion: {operacion: ""COUNT"", columna: ""numero_documento""}
- ""Dame los primeros 10 registros"" → cantidad: 10
- ""Agrupa por colaborador y canal de venta"" → agrupacion: [""nombre_colaborador"", ""canal_venta""]
- ""Clientes con más de 100 pagos realizados"" → agrupacion: ""razon_social"", agregacion: {operacion: ""COUNT"", columna: ""numero_documento""}, having: {columna: ""numero_documento"", operador: "">"", valor: 100}")]
    public static async Task<object> ConsultarCuentasXCobrarAsync(
        [Description("Parámetros de búsqueda, filtros, paginación y agrupaciones")] CuentasXCobrarRequest request)
    {
        try
        {
            var resultados = await EjecutarConsultaCuentasXCobrar(request);

            if (resultados.Count == 0)
            {
                return new
                {
                    success = true,
                    dataType = "empty_result",
                    message = "No se encontraron registros que coincidan con los criterios de búsqueda.",
                    data = resultados,
                    suggestion = "Intente con diferentes criterios de búsqueda o verifique los filtros."
                };
            }

            return new
            {
                success = true,
                dataType = "data_found",
                message = $"Se encontraron {resultados.Count} registro(s).",
                data = resultados,
                count = resultados.Count
            };
        }
        catch (MySqlException sqlEx)
        {
            Console.WriteLine($"[ERROR MYSQL cuentasXCobrar]: {sqlEx.Message}");
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
            Console.WriteLine($"[ERROR GENERAL cuentasXCobrar]: {ex.Message}");
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

    private static async Task<List<Dictionary<string, object>>> EjecutarConsultaCuentasXCobrar(CuentasXCobrarRequest req)
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

        // ── SELECT dinámico ──
        string selectPart;
        string? agregacionAlias = null;

        if (req.Agregacion != null)
        {
            var agregacionDbCol = ColumnMap.GetValueOrDefault(req.Agregacion.Columna.ToLower(), req.Agregacion.Columna.ToUpper());
            agregacionAlias = !string.IsNullOrEmpty(req.Agregacion.Alias)
                ? req.Agregacion.Alias
                : $"{req.Agregacion.Operacion}_{req.Agregacion.Columna}";

            if (!string.IsNullOrEmpty(groupByPart))
            {
                selectPart = $"{groupByPart}, {req.Agregacion.Operacion}({agregacionDbCol}) AS {agregacionAlias}";
            }
            else
            {
                selectPart = $"{req.Agregacion.Operacion}({agregacionDbCol}) AS {agregacionAlias}";
            }
        }
        else
        {
            // Sin agregación: SELECT con aliases amigables para mapeo automático
            var cols = ColumnMap.Select(kv => $"{kv.Value} AS {kv.Key}");
            selectPart = string.Join(", ", cols);
        }

        string sql = $"SELECT {selectPart} FROM vista_tesoreria_cuentasxcobrar";

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
        bool aplicarLimit = req.Agregacion == null;
        if (aplicarLimit)
        {
            var limit = req.Cantidad ?? Config.CantidadPorDefecto;
            sql += " LIMIT @limit OFFSET @offset";
            command.Parameters.AddWithValue("@limit", limit);
            command.Parameters.AddWithValue("@offset", (req.Pagina - 1) * limit);
        }

        command.CommandText = sql;

        Console.WriteLine($"[QUERY CUENTASXCOBRAR EJECUTADO]: {sql}");
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

            if (req.Agregacion != null)
            {
                // Columnas de agrupación con nombres amigables
                for (int i = 0; i < agrupacionFriendlyCols.Count; i++)
                {
                    var friendly = agrupacionFriendlyCols[i];
                    var dbCol = agrupacionDbCols[i];
                    var ordinal = reader.GetOrdinal(dbCol);
                    fila[friendly] = reader.IsDBNull(ordinal) ? null! : reader.GetValue(ordinal);
                }

                // Valor agregado (por alias)
                var alias = agregacionAlias!;
                var aggOrdinal = reader.GetOrdinal(alias);
                fila[alias] = reader.IsDBNull(aggOrdinal) ? null! : reader.GetValue(aggOrdinal);
            }
            else
            {
                // Sin agregación: los nombres de columna ya son los aliases amigables (FACTURA AS numero_documento)
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string alias = reader.GetName(i);
                    var value = reader.IsDBNull(i) ? null! : reader.GetValue(i);

                    if (value is DateTime dt)
                        value = dt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

                    fila[alias] = value;
                }
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
