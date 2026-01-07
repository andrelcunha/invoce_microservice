using System.Globalization;
using System.Text;
using System.Text.Json;

#nullable enable

// Usage:
//   dotnet-script join_ibge_tom.cs --tomCsv ./municipios.csv --ibgeJson /tmp/municipios.json --out ./municipalities_normalized.csv
// Prereqs:
//   curl -o /tmp/municipios.json https://servicodados.ibge.gov.br/api/v1/localidades/municipios
//  Get TOM XLSX from https://dados.gov.br/dados/conjuntos-dados/tabela-de-rgos-e-municpios and convert to CSV.

return await new Runner().Run(Environment.GetCommandLineArgs());

internal sealed class Runner
{
    public async Task<int> Run(string[] argv)
    {
        var argsDict = ParseArgs(argv);
        if (!argsDict.TryGetValue("--tomCsv", out var tomCsvPath) || string.IsNullOrWhiteSpace(tomCsvPath) || !File.Exists(tomCsvPath))
        {
            Console.Error.WriteLine("Missing or invalid --tomCsv path");
            return 1;
        }
        if (!argsDict.TryGetValue("--ibgeJson", out var ibgeJsonPath) || string.IsNullOrWhiteSpace(ibgeJsonPath) || !File.Exists(ibgeJsonPath))
        {
            Console.Error.WriteLine("Missing or invalid --ibgeJson path");
            return 1;
        }
        var outPath = argsDict.GetValueOrDefault("--out") ?? "./municipalities_normalized.csv";

        // Load IBGE municipalities once and index by (UF|normalizedName)
        var ibgeText = await File.ReadAllTextAsync(ibgeJsonPath);
        var ibgeItems = JsonSerializer.Deserialize<List<IbgeMunicipio>>(ibgeText, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new();

        var ibgeIndex = new Dictionary<string, IbgeMunicipio>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in ibgeItems)
        {
            var uf = m.Microrregiao?.Mesorregiao?.UF?.Sigla
                     ?? m.RegiaoImediata?.RegiaoIntermediaria?.UF?.Sigla
                     ?? string.Empty;
            var key = MakeKey(uf, m.Nome);
            if (!string.IsNullOrEmpty(uf) && !ibgeIndex.ContainsKey(key))
                ibgeIndex[key] = m;
        }

        int total = 0, matched = 0, unmatched = 0;
        var sb = new StringBuilder();
        sb.AppendLine("ibge_code,name,uf,tom_code,created_at,extinguished_at");

        using var sr = new StreamReader(tomCsvPath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var header = await sr.ReadLineAsync();
        if (header is null)
        {
            Console.Error.WriteLine("Empty TOM CSV.");
            return 1;
        }

        // Expected header: MUNI_CD,MUNI_NM,MUNI_UF_SG,MUNI_DT_CRIACAO,MUNI_DT_EXTINCAO
        var cols = header.Split(',');
        int idxCode = Array.FindIndex(cols, c => c.Equals("MUNI_CD", StringComparison.OrdinalIgnoreCase));
        int idxName = Array.FindIndex(cols, c => c.Equals("MUNI_NM", StringComparison.OrdinalIgnoreCase));
        int idxUf = Array.FindIndex(cols, c => c.Equals("MUNI_UF_SG", StringComparison.OrdinalIgnoreCase));
        int idxCreated = Array.FindIndex(cols, c => c.Equals("MUNI_DT_CRIACAO", StringComparison.OrdinalIgnoreCase));
        int idxExt = Array.FindIndex(cols, c => c.Equals("MUNI_DT_EXTINCAO", StringComparison.OrdinalIgnoreCase));

        if (idxCode < 0 || idxName < 0 || idxUf < 0)
        {
            Console.Error.WriteLine("Header not recognized. Expected at least MUNI_CD,MUNI_NM,MUNI_UF_SG");
            return 1;
        }

        string? line;
        while ((line = await sr.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = SplitCsv(line);
            if (parts.Length <= Math.Max(idxUf, Math.Max(idxCode, idxName))) continue;
            total++;

            var tomCode = parts[idxCode].Trim();
            var name = parts[idxName].Trim();
            var uf = parts[idxUf].Trim();
            var created = idxCreated >= 0 && idxCreated < parts.Length ? NormalizeDate(parts[idxCreated]) : string.Empty;
            var extinguished = idxExt >= 0 && idxExt < parts.Length ? NormalizeDate(parts[idxExt]) : string.Empty;

            // Try to match with IBGE by UF + normalized name
            var key = MakeKey(uf, name);
            if (ibgeIndex.TryGetValue(key, out var ibge))
            {
                matched++;
                sb.AppendLine($"{ibge.Id}," + EscapeCsv(ibge.Nome) + $",{uf},{tomCode},{created},{extinguished}");
            }
            else
            {
                unmatched++;
                // Write row with empty IBGE code to allow manual correction later
                sb.AppendLine($",{EscapeCsv(name)},{uf},{tomCode},{created},{extinguished}");
            }
        }

        await File.WriteAllTextAsync(outPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        Console.WriteLine($"Wrote {outPath}. Total: {total}, Matched: {matched}, Unmatched: {unmatched}");
        return 0;
    }

    // --- Helpers ---

    private static string[] SplitCsv(string line)
    {
        // Simple CSV splitter (no embedded commas in quoted fields expected here)
        return line.Split(',');
    }

    private static string MakeKey(string uf, string name)
    {
        var norm = RemoveDiacritics(name).ToUpperInvariant();
        norm = norm.Replace("-", " ").Replace("'", " ").Replace("’", " ").Replace("`", " ");
        norm = string.Join(' ', norm.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return uf.Trim().ToUpperInvariant() + "|" + norm;
    }

    private static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string NormalizeDate(string raw)
    {
        raw = raw.Trim();
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        // Accept d/M/yyyy or dd/MM/yyyy -> ISO
        if (DateTime.TryParse(raw, CultureInfo.GetCultureInfo("pt-BR"), DateTimeStyles.None, out var dt))
            return dt.ToString("yyyy-MM-dd");
        if (DateTime.TryParse(raw, out dt))
            return dt.ToString("yyyy-MM-dd");
        return string.Empty;
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
            return '"' + value.Replace("\"", "\"\"") + '"';
        return value;
    }

    private static Dictionary<string, string> ParseArgs(string[] argv)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < argv.Length; i++)
        {
            var a = argv[i];
            if (a.StartsWith("--"))
            {
                var val = (i + 1 < argv.Length && !argv[i + 1].StartsWith("--")) ? argv[++i] : string.Empty;
                dict[a] = val;
            }
        }
        return dict;
    }
}

// Minimal types for IBGE JSON (/localidades/municipios)
public record IbgeMunicipio
{
    public int Id { get; init; }
    public string Nome { get; init; } = string.Empty;
    public Microrregiao? Microrregiao { get; init; }
    public RegiaoImediata? RegiaoImediata { get; init; }
}

public record Microrregiao
{
    public Mesorregiao? Mesorregiao { get; init; }
}

public record Mesorregiao
{
    public UF? UF { get; init; }
}

public record RegiaoImediata
{
    public RegiaoIntermediaria? RegiaoIntermediaria { get; init; }
}

public record RegiaoIntermediaria
{
    public UF? UF { get; init; }
}

public record UF
{
    public string Sigla { get; init; } = string.Empty;
}
