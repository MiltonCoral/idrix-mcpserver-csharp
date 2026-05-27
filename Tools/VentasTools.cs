using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using MySqlConnector;
using SimpleMcpHttpServer.Models;
using SimpleMcpHttpServer.Utils;

namespace SimpleMcpHttpServer.Tools;

[McpServerToolType]
public static class VentasTools
{
    public static readonly Dictionary<string, string> ColumnMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "fecha",                    "FECHA"                         },
        { "tipo_documento",           "TIPO_DOCUMENTO"                },
        { "numero_documento",         "NUMERO_DOCUMENTO"              },
        { "cliente_nombre",           "CLIENTE_NOMBRE"                },
        { "mes",                      "MES"                           },
        { "semana",                   "SEMANA"                        },
        { "numero_nota_credito",      "NUMERO_NOTA_CREDITO"           },
        { "prod_codigo",              "PROD_CODIGO"                   },
        { "prod_codpro",              "PROD_CODPRO"                   },
        { "producto",                 "DESCRIPCION_PRODUCTO"          },
        { "vendedor",                 "VENDEDOR"                      },
        { "familia",                  "FAMILIA"                       },
        { "unidades_vendidas",        "UNIDADES_VENDIDAS"             },
        { "costo_unitario",           "COSTO_UNITARIO"                },
        { "precio_venta_unitario",    "PRECIO_VENTA_UNITARIO"         },
        { "total_sin_iva",            "TOTAL_SIN_IVA"                 },
        { "total_neto",               "TOTAL_NETO"                    },
        { "pais",                     "PAIS"                          },
        { "provincia",                "PROVINCIA"                     },
        { "canton",                   "CANTON"                        },
        { "observacion_nc",           "OBSERVACION_NC"                }
    };

    [McpServerTool(Name = "ventas")]
    [Description(@"Esta herramienta obtiene un resumen de ventas con soporte avanzado de paginación, filtros, ordenamiento, agregaciones y agrupaciones complejas.

Devuelve los siguientes campos:
- fecha → Fecha de la venta
- tipo_documento → Tipo de documento (factura, nota de crédito, etc.)
- numero_documento → Número del documento
- cliente_nombre → Nombre del cliente
- mes → Mes de la venta
- semana → Semana de la venta
- numero_nota_credito → Número de nota de crédito (si aplica)
- prod_codigo / prod_codpro → Códigos del producto
- producto → Nombre del producto (usar LIKE para buscar)
- tipo_producto → Tipo de producto o servicio
- vendedor → Nombre del vendedor
- familia → Familia del producto
- unidades_vendidas → Cantidad de unidades vendidas
- costo_unitario / precio_venta_unitario → Precios
- total_sin_iva / total_neto → Totales
- pais / provincia / canton → Ubicación
- observacion_nc → Observaciones de nota de crédito

CAPACIDADES AVANZADAS:
- Agrupación múltiple: agrupa por varias columnas simultáneamente
- Cláusulas HAVING: filtra grupos después de la agrupación
- Múltiples agregaciones: varias operaciones simultáneas (SUM, COUNT, AVG, MAX, MIN)
- DISTINCT: valores únicos (distinct: true + seleccionar_columnas)
- Campos calculados: expresiones SQL personalizadas (YEAR(), MONTH(), CASE WHEN, etc.)
- Selección específica: lista de columnas a retornar

IMPORTANTE:
- Para contar el total de ventas: agregaciones: [{operacion: ""COUNT"", columna: ""fecha""}]
- NO uses el parámetro cantidad para contar registros - solo para limitar resultados
- Si no especificas cantidad, se devolverán hasta 100 registros (si no hay agregación)
- USA TODA LA INFORMACIÓN DEVUELTA: analiza y menciona TODOS los datos disponibles
- EXPLORAR DATOS: usa distinct: true + seleccionar_columnas para ver valores únicos disponibles

Parámetros disponibles:
- pagina, cantidad, columna, orden
- filtros: condiciones AND/OR
- agrupacion: string o array de columnas
- agregaciones: array de {operacion, columna, alias?}
- having: {columna, operador, valor}
- distinct: true/false
- campos_calculados: [{expresion, alias}]
- seleccionar_columnas: [""col1"", ""col2""]

Ejemplos de uso avanzado:
- ""¿Cuántas ventas tengo?"" → agregaciones: [{operacion: ""COUNT"", columna: ""fecha""}]
- ""Dame las primeras 10 ventas"" → cantidad: 10
- ""Agrupa ventas por vendedor y mes"" → agrupacion: [""vendedor"", ""mes""]
- ""Vendedores con más de 100 ventas"" → agrupacion: ""vendedor"", agregaciones: [{operacion: ""COUNT"", columna: ""fecha"", alias: ""total""}], having: {columna: ""fecha"", operador: "">"", valor: 100}
- ""¿Qué productos únicos hay?"" → distinct: true, seleccionar_columnas: [""producto""]
- ""¿Qué vendedores únicos hay?"" → distinct: true, seleccionar_columnas: [""vendedor""]
- ""Análisis mensual"" → campos_calculados: [{expresion: ""YEAR(fecha)"", alias: ""anio""}, {expresion: ""MONTH(fecha)"", alias: ""mes""}], agrupacion: [""anio"", ""mes""], agregaciones: [{operacion: ""SUM"", columna: ""total_neto"", alias: ""total""}]")]
    public static async Task<object> ConsultarVentasAsync(
        [Description("Parámetros de búsqueda, filtros, paginación, agrupaciones y campos calculados")] VentasRequest request)
    {
        try
        {
            var resultados = await EjecutarConsultaVentas(request);

            if (resultados.Count == 0)
            {
                return new
                {
                    success = true,
                    dataType = "empty_result",
                    message = "No se encontraron ventas que coincidan con los criterios de búsqueda.",
                    data = resultados,
                    suggestion = "SUGERENCIA: Si buscas por nombre específico (producto, vendedor, cliente), usa distinct: true + seleccionar_columnas para explorar los valores disponibles."
                };
            }

            return new
            {
                success = true,
                dataType = "data_found",
                message = $"Se encontraron {resultados.Count} resultado(s).",
                data = resultados,
                count = resultados.Count
            };
        }
        catch (MySqlException sqlEx)
        {
            Console.WriteLine($"[ERROR MYSQL ventas]: {sqlEx.Message}");
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
            Console.WriteLine($"[ERROR GENERAL ventas]: {ex.Message}");
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

    private static async Task<List<Dictionary<string, object>>> EjecutarConsultaVentas(VentasRequest req)
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
            if (!string.IsNullOrEmpty(groupByPart))
            {
                for (int i = 0; i < agrupacionDbCols.Count; i++)
                    selectParts.Add($"{agrupacionDbCols[i]} AS {agrupacionFriendlyCols[i]}");
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
                selectParts.Add($"{kv.Value} AS {kv.Key}");
        }

        // Campos calculados
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
        string sql = $"SELECT {distinctKeyword}{selectPart} FROM vista_reportes_resumen_ventas_productos";

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
            var limit = req.Cantidad ?? Config.CantidadPorDefecto;
            sql += " LIMIT @limit OFFSET @offset";
            command.Parameters.AddWithValue("@limit", limit);
            command.Parameters.AddWithValue("@offset", (req.Pagina - 1) * limit);
        }

        command.CommandText = sql;

        Console.WriteLine($"[QUERY VENTAS EJECUTADO]: {sql}");
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