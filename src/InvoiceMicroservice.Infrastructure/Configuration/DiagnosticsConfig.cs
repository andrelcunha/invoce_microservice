namespace InvoiceMicroservice.Infrastructure.Configuration;

/// <summary>
/// Recursos de depuração. Todos desligados por padrão: são conveniência de
/// desenvolvimento local e não podem interferir na emissão.
/// </summary>
public class DiagnosticsConfig
{
    public const string SectionName = "Diagnostics";

    /// <summary>
    /// Grava em disco o XML enviado ao portal e o devolvido por ele. Desligado por
    /// padrão — o mesmo conteúdo já é persistido em <c>invoice_emission_results</c>
    /// (<c>RequestXml</c> / <c>ResponseRaw</c>), então o arquivo é redundante.
    /// </summary>
    public bool XmlDumpEnabled { get; set; }

    /// <summary>
    /// Onde gravar. Caminho relativo é resolvido a partir do diretório de trabalho.
    /// Aponte para um local gravável quando ligar isto em contêiner read-only.
    /// </summary>
    public string XmlDumpPath { get; set; } = "xml-output";
}
