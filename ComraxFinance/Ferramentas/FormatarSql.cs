// @created : 28/01/2026
// Copyright (c) 2026 Eder Rafael Telhado. Uso sujeito aos termos da licença LSPR-REVOGÁVEL.

#region

using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;

#endregion

namespace ComraxFinance.Ferramentas;

public class FormatarSql
{
    private static readonly Regex IfBlockRegex =
        new(@"\{\{#if\s+(.*?)\}\}([\s\S]*?)(\{\{#else\}\}([\s\S]*?))?\{\{\/if\}\}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BetweenRegex =
        new(@"\{\{#between\s+(\w+)\s+(\w+)\}\}([\s\S]*?)\{\{\/between\}\}",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly string _basePath;
    private readonly ILogger? _logger;

    public FormatarSql(string basePath, ILogger? logger = null)
    {
        _basePath = basePath;
        _logger = logger;
    }

    public string RenderFromFile(string templateName, object model)
    {
        var path = Path.Combine(_basePath, templateName);

        if (!File.Exists(path))
            throw new FileNotFoundException($"SQL template não encontrado: {path}");

        var template = File.ReadAllText(path);
        return Render(template, model, templateName);
    }

    public string Render(string template, object model, string? name = null)
    {
        var values = model.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => p.Name, p => p.GetValue(model));

        var sql = IfBlockRegex.Replace(template, m =>
        {
            var expr = m.Groups[1].Value.Trim();
            var ifBlock = m.Groups[2].Value;
            var elseBlock = m.Groups[4].Value;

            return Evaluate(expr, values) ? ifBlock : elseBlock;
        });

        sql = BetweenRegex.Replace(sql, m =>
        {
            var start = m.Groups[1].Value;
            var end = m.Groups[2].Value;
            var block = m.Groups[3].Value;

            return HasBetween(values, start, end) ? block : "";
        });

        sql = Normalize(sql);

        _logger?.LogDebug("SQL Template: {Template}\nSQL Gerado:\n{Sql}", name ?? "<inline>", sql);

        return sql;
    }

    private static bool HasBetween(
        Dictionary<string, object?> values,
        string start,
        string end)
    {
        values.TryGetValue(start, out var a);
        values.TryGetValue(end, out var b);

        return a != null && b != null;
    }

    private static bool Evaluate(string expr, Dictionary<string, object?> values)
    {
        var parts = expr.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 1)
            return Truthy(GetValue(parts[0], values));

        if (parts.Length == 2)
        {
            var value = GetValue(parts[0], values);

            return parts[1].ToLower() switch
            {
                "notnull" => value != null,
                "isnull" => value == null,
                "notempty" => value is string s && !string.IsNullOrWhiteSpace(s),
                "empty" => value == null || (value is string s2 && string.IsNullOrWhiteSpace(s2)),
                "any" => HasAny(value),
                _ => throw new InvalidOperationException($"Operador inválido: {parts[1]}")
            };
        }

        if (parts.Length == 3)
        {
            var left = GetValue(parts[0], values);
            var op = parts[1];
            var right = Parse(parts[2]);

            return Compare(left, right, op);
        }

        throw new InvalidOperationException($"Expressão inválida: {expr}");
    }

    private static object? GetValue(string key, Dictionary<string, object?> values)
    {
        var invert = key.StartsWith("!");
        var name = invert ? key[1..] : key;

        values.TryGetValue(name, out var value);

        if (!invert) return value;

        return !(value as bool? ?? false);
    }

    private static bool Truthy(object? value)
    {
        return value switch
        {
            null => false,
            bool b => b,
            string s => !string.IsNullOrWhiteSpace(s),
            int i => i != 0,
            long l => l != 0,
            _ => true
        };
    }

    private static bool HasAny(object? value)
    {
        return value switch
        {
            null => false,
            IEnumerable e => e.GetEnumerator().MoveNext(),
            _ => false
        };
    }

    private static object Parse(string value)
    {
        if (value == "null") return null!;
        if (bool.TryParse(value, out var b)) return b;
        if (int.TryParse(value, out var i)) return i;
        if (decimal.TryParse(value, out var d)) return d;

        return value.Trim('\'');
    }

    private static bool Compare(object? left, object? right, string op)
    {
        if (left == null || right == null) return false;
        if (left is not IComparable a || right is not IComparable b) return false;
        
        var converted = Convert.ChangeType(b, left.GetType());
        var cmp = a.CompareTo(converted);

        return op switch
        {
            "==" => cmp == 0,
            "!=" => cmp != 0,
            ">" => cmp > 0,
            "<" => cmp < 0,
            ">=" => cmp >= 0,
            "<=" => cmp <= 0,
            _ => throw new InvalidOperationException($"Operador inválido: {op}")
        };

    }

    private static string Normalize(string sql)
    {
        sql = Regex.Replace(sql, @"[ \t]+", " ");
        sql = Regex.Replace(sql, @"\s+\n", "\n");
        sql = Regex.Replace(sql, @"\n\s+", "\n");
        return sql.Trim();
    }
}