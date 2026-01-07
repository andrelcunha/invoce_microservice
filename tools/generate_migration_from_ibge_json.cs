using System.Text;

// Generate a migration InsertData block from the already normalized CSV produced by join_ibge_tom.cs
// Input expected: municipalities_normalized.csv with header:
// ibge_code,name,uf,tom_code,created_at,extinguished_at

const string inputCsv = "./municipalities_normalized.csv";
const string outputFile = "migration_data.cs";

if (!File.Exists(inputCsv))
{
    Console.Error.WriteLine($"Input CSV not found: {inputCsv}");
    return;
}

var lines = File.ReadAllLines(inputCsv)
    .Where(l => !string.IsNullOrWhiteSpace(l))
    .ToList();

if (lines.Count == 0)
{
    Console.Error.WriteLine("Input CSV is empty.");
    return;
}

var sb = new StringBuilder();
sb.AppendLine("migrationBuilder.InsertData(");
sb.AppendLine("    table: \"municipalities\",");
sb.AppendLine("    columns: new[] { \"ibge_code\", \"name\", \"uf\", \"tom_code\", \"created_at\", \"extinguished_at\" },");
sb.AppendLine("    values: new object[,] {");

// Skip header
foreach (var line in lines.Skip(1))
{
    var parts = SplitCsv(line, 6);
    if (parts.Length < 4) continue; // need ibge_code,name,uf,tom_code at minimum

    var ibge = parts[0];
    var name = parts[1];
    var uf = parts[2];
    var tom = parts[3];
    var created = parts.Length > 4 ? parts[4] : string.Empty;
    var extinct = parts.Length > 5 ? parts[5] : string.Empty;

    sb.AppendLine($"        {{ \"{ibge}\", \"{Escape(name)}\", \"{uf}\", \"{tom}\", \"{created}\", \"{extinct}\" }},");
}

sb.AppendLine("    });");

File.WriteAllText(outputFile, sb.ToString());
Console.WriteLine($"Wrote {outputFile} with InsertData block.");

static string Escape(string value) =>
    value.Replace("\\", "\\\\").Replace("\"", "\\\"");

static string[] SplitCsv(string line, int expected)
{
    // Simple CSV splitter; if quoted fields with commas appear, this should be replaced with a robust CSV parser.
    var parts = new List<string>();
    var current = new StringBuilder();
    bool inQuotes = false;
    for (int i = 0; i < line.Length; i++)
    {
        var ch = line[i];
        if (ch == '"')
        {
            if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
            {
                current.Append('"');
                i++; // skip escaped quote
            }
            else
            {
                inQuotes = !inQuotes;
            }
        }
        else if (ch == ',' && !inQuotes)
        {
            parts.Add(current.ToString());
            current.Clear();
        }
        else
        {
            current.Append(ch);
        }
    }
    parts.Add(current.ToString());

    // Ensure length
    while (parts.Count < expected)
        parts.Add(string.Empty);

    return parts.ToArray();
}