using System.Text.Json;
using MySqlConnector;

namespace SimpleMcpHttpServer.Utils;

public static class SqlUtils
{
    // Traduce el JSON recursivo a una cláusula WHERE y añade los parámetros seguros al comando
    public static string BuildWhereClause(JsonElement filtro, MySqlCommand command, Dictionary<string, string> columnMap, ref int paramCounter)
    {
        // Si es un Array (Filtros planos) -> se unen con AND
        if (filtro.ValueKind == JsonValueKind.Array)
        {
            var parts = new List<string>();
            foreach (var item in filtro.EnumerateArray())
            {
                string clause = BuildWhereClause(item, command, columnMap, ref paramCounter);
                if (!string.IsNullOrEmpty(clause)) parts.Add($"({clause})");
            }
            return parts.Count > 0 ? string.Join(" AND ", parts) : "";
        }

        // Si es un Objeto
        if (filtro.ValueKind == JsonValueKind.Object)
        {
            // Caso compuesto AND
            if (filtro.TryGetProperty("AND", out var andProp) && andProp.ValueKind == JsonValueKind.Array)
            {
                var parts = new List<string>();
                foreach (var item in andProp.EnumerateArray())
                {
                    string clause = BuildWhereClause(item, command, columnMap, ref paramCounter);
                    if (!string.IsNullOrEmpty(clause)) parts.Add($"({clause})");
                }
                return parts.Count > 0 ? string.Join(" AND ", parts) : "";
            }

            // Caso compuesto OR
            if (filtro.TryGetProperty("OR", out var orProp) && orProp.ValueKind == JsonValueKind.Array)
            {
                var parts = new List<string>();
                foreach (var item in orProp.EnumerateArray())
                {
                    string clause = BuildWhereClause(item, command, columnMap, ref paramCounter);
                    if (!string.IsNullOrEmpty(clause)) parts.Add($"({clause})");
                }
                return parts.Count > 0 ? string.Join(" OR ", parts) : "";
            }

            // Caso Simple (FiltroSimple)
            if (filtro.TryGetProperty("columna", out var colProp))
            {
                string rawColumna = colProp.GetString() ?? "";
                string columna = columnMap.GetValueOrDefault(rawColumna.ToLower(), rawColumna.ToUpper());
                
                string operador = filtro.TryGetProperty("operador", out var opProp) ? opProp.GetString()?.ToUpper() ?? "=" : "=";

                switch (operador)
                {
                    case "IS NULL":
                    case "IS NOT NULL":
                        return $"{columna} {operador}";

                    case "BETWEEN":
                        if (filtro.TryGetProperty("valor", out var valBetween) && valBetween.ValueKind == JsonValueKind.Array)
                        {
                            var bArray = valBetween.EnumerateArray().ToList();
                            if (bArray.Count == 2)
                            {
                                string p1 = $"@p{paramCounter++}";
                                string p2 = $"@p{paramCounter++}";
                                command.Parameters.AddWithValue(p1, GetJsonValue(bArray[0]));
                                command.Parameters.AddWithValue(p2, GetJsonValue(bArray[1]));
                                return $"{columna} BETWEEN {p1} AND {p2}";
                            }
                        }
                        throw new Exception("BETWEEN requiere un array con 2 valores");

                    case "IN":
                        if (filtro.TryGetProperty("valor", out var valIn) && valIn.ValueKind == JsonValueKind.Array)
                        {
                            var inParams = new List<string>();
                            foreach (var item in valIn.EnumerateArray())
                            {
                                string p = $"@p{paramCounter++}";
                                command.Parameters.AddWithValue(p, GetJsonValue(item));
                                inParams.Add(p);
                            }
                            return inParams.Count > 0 ? $"{columna} IN ({string.Join(", ", inParams)})" : "";
                        }
                        throw new Exception("IN requiere un array con al menos 1 valor");

                    default:
                        if (filtro.TryGetProperty("valor", out var valDefault))
                        {
                            string p = $"@p{paramCounter++}";
                            object valorObj = GetJsonValue(valDefault);
                            
                            // Lógica para LIKE que hiciste en Node
                            if (operador == "LIKE" && valorObj is string strVal && !strVal.Contains("%"))
                            {
                                valorObj = $"%{strVal}%";
                            }

                            command.Parameters.AddWithValue(p, valorObj);
                            return $"{columna} {operador} {p}";
                        }
                        break;
                }
            }
        }

        return "";
    }

    // Extrae el valor nativo (string o numero) de un elemento JSON de forma segura
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