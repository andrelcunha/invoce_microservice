using System.Xml.Linq;
using FluentAssertions;
using InvoiceMicroservice.Application.Commands.EmitInvoice;
using InvoiceMicroservice.Domain.Entities;
using InvoiceMicroservice.Domain.Repositories;
using InvoiceMicroservice.Domain.ValueObjects;
using InvoiceMicroservice.Infrastructure.Xml;
using Moq;
using Xunit;

namespace InvoiceMicroservice.Tests.Infrastructure.Xml;

public class IpmXmlBuilderTests
{
    private readonly Mock<IServiceTypeTaxMappingRepository> _mockServiceTaxRepo;
    private readonly Mock<IMunicipalityRepository> _mockMunicipalityRepo;
    private readonly TaxConfig _taxConfig;
    private readonly IpmXmlBuilder _builder;

    public IpmXmlBuilderTests()
    {
        _mockServiceTaxRepo = new Mock<IServiceTypeTaxMappingRepository>();
        _mockMunicipalityRepo = new Mock<IMunicipalityRepository>();
        
        _taxConfig = new TaxConfig
        {
            PIbsUf = 0.025m,
            PIbsMun = 0.025m,
            PCbs = 0.009m,
            PRedAliqUf = 0.0m,
            PRedAliqMun = 0.0m,
            PRedAliqCbs = 0.0m,
            PAliquotaPis = 0.0065m,
            PAliquotaCofins = 0.03m
        };

        _builder = new IpmXmlBuilder(_taxConfig, _mockServiceTaxRepo.Object, _mockMunicipalityRepo.Object);
    }

    [Fact]
    public async Task BuildInvoiceXmlAsync_ShouldIncludeTestModeFlag_WhenTestModeIsTrue()
    {
        // Arrange
        var invoice = CreateSampleInvoice();
        SetupMockRepositories();

        // Act
        var xml = await _builder.BuildInvoiceXmlAsync(invoice, isTestMode: true);

        // Assert
        var doc = XDocument.Parse(xml);
        var testFlag = doc.Root?.Element("nfse_teste");
        testFlag.Should().NotBeNull();
        testFlag!.Value.Should().Be("1");
    }

    [Fact]
    public async Task BuildInvoiceXmlAsync_ShouldNotIncludeTestModeFlag_WhenTestModeIsFalse()
    {
        // Arrange
        var invoice = CreateSampleInvoice();
        SetupMockRepositories();

        // Act
        var xml = await _builder.BuildInvoiceXmlAsync(invoice, isTestMode: false);

        // Assert
        var doc = XDocument.Parse(xml);
        var testFlag = doc.Root?.Element("nfse_teste");
        testFlag.Should().BeNull();
    }

    [Fact]
    public async Task BuildInvoiceXmlAsync_ShouldIncludeUniqueIdentifier()
    {
        // Arrange
        var invoice = CreateSampleInvoice();
        SetupMockRepositories();

        // Act
        var xml = await _builder.BuildInvoiceXmlAsync(invoice, isTestMode: true);

        // Assert
        var doc = XDocument.Parse(xml);
        var identifier = doc.Root?.Element("identificador");
        identifier.Should().NotBeNull();
        identifier!.Value.Should().Be(invoice.Id.ToString("N"));
    }

    [Fact]
    public async Task BuildInvoiceXmlAsync_ShouldIncludeMandatoryNfSection()
    {
        // Arrange
        var invoice = CreateSampleInvoice();
        SetupMockRepositories();

        // Act
        var xml = await _builder.BuildInvoiceXmlAsync(invoice, isTestMode: true);

        // Assert
        var doc = XDocument.Parse(xml);
        var nf = doc.Root?.Element("nf");
        nf.Should().NotBeNull();
        
        // Check mandatory fields
        nf!.Element("data_fato_gerador").Should().NotBeNull();
        nf.Element("valor_total").Should().NotBeNull();
        nf.Element("valor_total")!.Value.Should().Be("1500.00");
        nf.Element("valor_desconto")!.Value.Should().Be("0.00");
    }

    [Fact]
    public async Task BuildInvoiceXmlAsync_ShouldIncludePisCofinsSection()
    {
        // Arrange
        var invoice = CreateSampleInvoice();
        SetupMockRepositories();

        // Act
        var xml = await _builder.BuildInvoiceXmlAsync(invoice, isTestMode: true);

        // Assert
        var doc = XDocument.Parse(xml);
        var pisCofins = doc.Root?.Element("nf")?.Element("pis_cofins");
        pisCofins.Should().NotBeNull();
        
        pisCofins!.Element("cst")!.Value.Should().Be("01");
        pisCofins.Element("tipo_retencao")!.Value.Should().Be("2");
        pisCofins.Element("base_calculo")!.Value.Should().Be("1500.00");
        pisCofins.Element("aliquota_pis")!.Value.Should().Be("0,6500");
        pisCofins.Element("aliquota_cofins")!.Value.Should().Be("3,0000");
    }

    [Fact]
    public async Task BuildInvoiceXmlAsync_ShouldIncludeTopLevelIbsCbsSection()
    {
        // Arrange
        var invoice = CreateSampleInvoice();
        SetupMockRepositories();

        // Act
        var xml = await _builder.BuildInvoiceXmlAsync(invoice, isTestMode: true);

        // Assert
        var doc = XDocument.Parse(xml);
        var ibscbs = doc.Root?.Element("IBSCBS");
        ibscbs.Should().NotBeNull();
        
        ibscbs!.Element("finNFSe")!.Value.Should().Be("0");
        ibscbs.Element("indFinal")!.Value.Should().Be("1");
        ibscbs.Element("cIndOp")!.Value.Should().Be("140101");
        
        var gIBSCBS = ibscbs.Element("valores")?.Element("trib")?.Element("gIBSCBS");
        gIBSCBS.Should().NotBeNull();
        gIBSCBS!.Element("CST")!.Value.Should().Be("200");
        gIBSCBS.Element("cClassTrib")!.Value.Should().Be("140001");
    }

    [Fact]
    public async Task BuildInvoiceXmlAsync_ShouldIncludeIbsCbsRatesInsideNf()
    {
        // Arrange
        var invoice = CreateSampleInvoice();
        SetupMockRepositories();

        // Act
        var xml = await _builder.BuildInvoiceXmlAsync(invoice, isTestMode: true);

        // Assert
        var doc = XDocument.Parse(xml);
        var ibscbsNf = doc.Root?.Element("nf")?.Element("IBSCBS");
        ibscbsNf.Should().NotBeNull();
        
        // IBS UF
        ibscbsNf!.Element("valor_ibs_uf")!.Value.Should().Be("37.50");
        ibscbsNf.Element("aliquota_ibs_uf")!.Value.Should().Be("2,5000");
        
        // IBS MUN
        ibscbsNf.Element("valor_ibs_mun")!.Value.Should().Be("37.50");
        ibscbsNf.Element("aliquota_ibs_mun")!.Value.Should().Be("2,5000");
        
        // CBS
        ibscbsNf.Element("valor_cbs")!.Value.Should().Be("13.50");
        ibscbsNf.Element("aliquota_cbs")!.Value.Should().Be("0,9000");
    }

    [Fact]
    public async Task BuildInvoiceXmlAsync_ShouldIncludeMandatoryNbsCode()
    {
        // Arrange
        var invoice = CreateSampleInvoice();
        SetupMockRepositories();

        // Act
        var xml = await _builder.BuildInvoiceXmlAsync(invoice, isTestMode: true);

        // Assert
        var doc = XDocument.Parse(xml);
        var nbsCode = doc.Root?.Element("itens")?.Element("lista")?.Element("codigo_nbs");
        nbsCode.Should().NotBeNull("NBS code is mandatory per IPM spec (error 00366)");
        nbsCode!.Value.Should().Be("149.01.00");
    }

    [Fact]
    public async Task BuildInvoiceXmlAsync_ShouldIncludePrestadorWithTomCode()
    {
        // Arrange
        var invoice = CreateSampleInvoice();
        SetupMockRepositories();

        // Act
        var xml = await _builder.BuildInvoiceXmlAsync(invoice, isTestMode: true);

        // Assert
        var doc = XDocument.Parse(xml);
        var prestador = doc.Root?.Element("prestador");
        prestador.Should().NotBeNull();
        
        prestador!.Element("cpfcnpj")!.Value.Should().Be("12345678000195");
        prestador.Element("cidade")!.Value.Should().Be("8083"); // Concórdia TOM code
    }

    [Fact]
    public async Task BuildInvoiceXmlAsync_ShouldIncludeTomadorWithCorrectType_ForCpf()
    {
        // Arrange
        var invoice = CreateSampleInvoiceWithCpfConsumer();
        SetupMockRepositories();

        // Act
        var xml = await _builder.BuildInvoiceXmlAsync(invoice, isTestMode: true);

        // Assert
        var doc = XDocument.Parse(xml);
        var tomador = doc.Root?.Element("tomador");
        tomador.Should().NotBeNull();
        
        tomador!.Element("tipo")!.Value.Should().Be("F"); // F = Física (CPF)
        tomador.Element("cpfcnpj")!.Value.Should().Be("12345678909");
    }

    [Fact]
    public async Task BuildInvoiceXmlAsync_ShouldIncludeTomadorWithCorrectType_ForCnpj()
    {
        // Arrange
        var invoice = CreateSampleInvoice(); // Default has CNPJ consumer
        SetupMockRepositories();

        // Act
        var xml = await _builder.BuildInvoiceXmlAsync(invoice, isTestMode: true);

        // Assert
        var doc = XDocument.Parse(xml);
        var tomador = doc.Root?.Element("tomador");
        tomador.Should().NotBeNull();
        
        tomador!.Element("tipo")!.Value.Should().Be("J"); // J = Jurídica (CNPJ)
    }

    [Fact]
    public async Task BuildInvoiceXmlAsync_ShouldEscapeSpecialCharacters()
    {
        // Arrange
        var invoice = CreateInvoiceWithSpecialCharacters();
        SetupMockRepositories();

        // Act
        var xml = await _builder.BuildInvoiceXmlAsync(invoice, isTestMode: true);

        // Assert
        var doc = XDocument.Parse(xml);
        var descritivo = doc.Root?.Element("itens")?.Element("lista")?.Element("descritivo");
        descritivo.Should().NotBeNull();
        
        // XML should be parseable (special chars escaped)
        descritivo!.Value.Should().Contain("&lt;"); // < escaped
        descritivo.Value.Should().Contain("&gt;");  // > escaped
        descritivo.Value.Should().NotContain("/");   // / removed per spec
    }

    [Fact]
    public async Task BuildInvoiceXmlAsync_ShouldFormatMonetaryValuesWithPeriod()
    {
        // Arrange
        var invoice = CreateSampleInvoice();
        SetupMockRepositories();

        // Act
        var xml = await _builder.BuildInvoiceXmlAsync(invoice, isTestMode: true);

        // Assert
        var doc = XDocument.Parse(xml);
        var valorTotal = doc.Root?.Element("nf")?.Element("valor_total");
        valorTotal!.Value.Should().MatchRegex(@"^\d+\.\d{2}$", "monetary values use period separator");
    }

    [Fact]
    public async Task BuildInvoiceXmlAsync_ShouldFormatRatesWithComma()
    {
        // Arrange
        var invoice = CreateSampleInvoice();
        SetupMockRepositories();

        // Act
        var xml = await _builder.BuildInvoiceXmlAsync(invoice, isTestMode: true);

        // Assert
        var doc = XDocument.Parse(xml);
        var aliquotaPis = doc.Root?.Element("nf")?.Element("pis_cofins")?.Element("aliquota_pis");
        aliquotaPis!.Value.Should().MatchRegex(@"^\d+,\d{4}$", "rates use comma separator with 4 decimals");
    }

    [Fact]
    public async Task BuildInvoiceXmlAsync_ShouldIncludeFormaPagamento()
    {
        // Arrange
        var invoice = CreateSampleInvoice();
        SetupMockRepositories();

        // Act
        var xml = await _builder.BuildInvoiceXmlAsync(invoice, isTestMode: true);

        // Assert
        var doc = XDocument.Parse(xml);
        var formaPagamento = doc.Root?.Element("forma_pagamento");
        formaPagamento.Should().NotBeNull();
        formaPagamento!.Element("tipo_pagamento")!.Value.Should().Be("1"); // À vista
    }

    [Fact]
    public async Task BuildInvoiceXmlAsync_ShouldUseServiceTypeTaxCodes_WhenAvailable()
    {
        // Arrange
        var invoice = CreateSampleInvoice();
        var customMapping = ServiceTypeTaxMapping.Create(
            "custom-service",
            "99.99-9-99",
            "Custom Service",
            "999.99.99",
            "99.99",
            "999999",
            "999",
            "999999"
        );
        
        _mockServiceTaxRepo
            .Setup(x => x.GetByServiceTypeKeyAsync(It.IsAny<string>(), default))
            .ReturnsAsync(customMapping);
        
        SetupMockMunicipalities();

        // Act
        var xml = await _builder.BuildInvoiceXmlAsync(invoice, isTestMode: true);

        // Assert
        var doc = XDocument.Parse(xml);
        var nbsCode = doc.Root?.Element("itens")?.Element("lista")?.Element("codigo_nbs");
        nbsCode!.Value.Should().Be("999.99.99");
    }

    [Fact]
    public async Task BuildInvoiceXmlAsync_ShouldFallbackToDefaults_WhenNoServiceTypeMapping()
    {
        // Arrange
        var invoice = CreateSampleInvoice();
        _mockServiceTaxRepo
            .Setup(x => x.GetByServiceTypeKeyAsync(It.IsAny<string>(), default))
            .ReturnsAsync((ServiceTypeTaxMapping?)null);
        
        _mockServiceTaxRepo
            .Setup(x => x.GetByCnaeCodeAsync(It.IsAny<string>(), default))
            .ReturnsAsync((ServiceTypeTaxMapping?)null);
        
        SetupMockMunicipalities();

        // Act
        var xml = await _builder.BuildInvoiceXmlAsync(invoice, isTestMode: true);

        // Assert - Should use default codes
        var doc = XDocument.Parse(xml);
        var nbsCode = doc.Root?.Element("itens")?.Element("lista")?.Element("codigo_nbs");
        nbsCode!.Value.Should().Be("123456789"); // Default NBS
    }

    // Helper methods

    private Invoice CreateSampleInvoice()
    {
        var issuer = new Issuer
        {
            Cnpj = "12.345.678/0001-95",
            Name = "Empresa Teste Ltda",
            // Email = "contato@empresa.com.br",
            Cnae = "45.20-0-05",
            Address = new Address
            {
                Street = "Rua Principal",
                Number = "123",
                Complement = "Sala 1",
                Neighborhood = "Centro",
                City = "Concórdia",
                Uf = "SC",
                ZipCode = "89700-000"
            }
        };

        var consumer = new Consumer
        {
            CpfCnpj = "98.765.432/0001-10",
            Name = "Cliente Teste S.A.",
            Email = "cliente@teste.com.br",
            Phone = "(49) 3441-1234",
            Address = new Address
            {
                Street = "Av Secundária",
                Number = "456",
                Neighborhood = "Bairro Novo",
                City = "Florianópolis",
                Uf = "SC",
                ZipCode = "88000-000"
            }
        };

        var issuerJson = System.Text.Json.JsonSerializer.Serialize(issuer);
        var consumerJson = System.Text.Json.JsonSerializer.Serialize(consumer);

        return Invoice.Create(
            "client-123",
            new Cnpj("12345678000195"),
            issuerJson,
            consumerJson,
            "Serviço de lavagem completa do veículo",
            1500.00m,
            DateTime.UtcNow,
            "vehicle-wash-45200-05"
        );
    }

    private Invoice CreateSampleInvoiceWithCpfConsumer()
    {
        var issuer = new Issuer
        {
            Cnpj = "12.345.678/0001-95",
            Name = "Empresa Teste Ltda",
            // Email = "contato@empresa.com.br",
            Cnae = "45.20-0-05",
            Address = new Address
            {
                Street = "Rua Principal",
                Number = "123",
                City = "Concórdia",
                Uf = "SC",
                ZipCode = "89700-000"
            }
        };

        var consumer = new Consumer
        {
            CpfCnpj = "123.456.789-09",
            Name = "João da Silva",
            Email = "joao@exemplo.com",
            Address = new Address
            {
                Street = "Rua Teste",
                Number = "789",
                City = "Florianópolis",
                Uf = "SC",
                ZipCode = "88000-000"
            }
        };

        var issuerJson = System.Text.Json.JsonSerializer.Serialize(issuer);
        var consumerJson = System.Text.Json.JsonSerializer.Serialize(consumer);

        return Invoice.Create(
            "client-456",
            new Cnpj("12345678000195"),
            issuerJson,
            consumerJson,
            "Serviço de polimento",
            500.00m,
            DateTime.UtcNow
        );
    }

    private Invoice CreateInvoiceWithSpecialCharacters()
    {
        var issuer = new Issuer
        {
            Cnpj = "12.345.678/0001-95",
            Name = "Empresa <Test> & Cia",
            // Email = "test@empresa.com",
            Cnae = "45.20-0-05",
            Address = new Address
            {
                Street = "Rua 'Principal'",
                Number = "123",
                City = "Concórdia",
                Uf = "SC",
                ZipCode = "89700-000"
            }
        };

        var consumer = new Consumer
        {
            CpfCnpj = "98.765.432/0001-10",
            Name = "Cliente \"Especial\"",
            Email = "cliente@teste.com",
            Address = new Address
            {
                Street = "Av <Norte/Sul>",
                Number = "456",
                City = "Florianópolis",
                Uf = "SC",
                ZipCode = "88000-000"
            }
        };

        var issuerJson = System.Text.Json.JsonSerializer.Serialize(issuer);
        var consumerJson = System.Text.Json.JsonSerializer.Serialize(consumer);

        return Invoice.Create(
            "client-789",
            new Cnpj("12345678000195"),
            issuerJson,
            consumerJson,
            "Serviço <premium> com 'aspas' & caracteres especiais / barras",
            750.00m,
            DateTime.UtcNow
        );
    }

    private void SetupMockRepositories()
    {
        var vehicleWashMapping = ServiceTypeTaxMapping.Create(
            "vehicle-wash-45200-05",
            "45.20-0-05",
            "Serviços de lavagem, lubrificação e polimento de veículos automotivos",
            "149.01.00",
            "14.01",
            "140101",
            "200",
            "140001"
        );

        _mockServiceTaxRepo
            .Setup(x => x.GetByServiceTypeKeyAsync("vehicle-wash-45200-05", default))
            .ReturnsAsync(vehicleWashMapping);

        SetupMockMunicipalities();
    }

    private void SetupMockMunicipalities()
    {
        var concordia = Municipality.Create("4204301", "Concórdia", "SC", "8083");
        var florianopolis = Municipality.Create("4205407", "Florianópolis", "SC", "8105");

        _mockMunicipalityRepo
            .Setup(x => x.GetByCityAndUfAsync("Concórdia", "SC", default))
            .ReturnsAsync(concordia);

        _mockMunicipalityRepo
            .Setup(x => x.GetByCityAndUfAsync("Florianópolis", "SC", default))
            .ReturnsAsync(florianopolis);
    }
}
