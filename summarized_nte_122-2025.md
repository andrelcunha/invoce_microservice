# NFS-e Testing Web Service Specification - Tax Reform (IBS/CBS)
**Technical Note NTE-122/2025 v1.4** | **Date: 17/12/2025** | **Internal: 122/2025**

## 1. Objective
Guide users and technical consultants on using and parameterizing the Web Service for testing NFS-e (Electronic Service Invoice) issuance with data required for the Tax Reform (IBS and CBS taxes).

## 2. Functioning Mode

### Test Mode
- **Tag**: `<nfse_teste>`
- **Value**: `1` (test), `0` (normal)
- **Behavior**:
  - Value `1`: Returns error to prevent NFS-e issuance. If XML is valid, returns: `<mensagem><codigo>NFS-e válida para emissão.</codigo></mensagem>`
  - Value `0` or tag omitted: System ignores IBS/CBS tags and issues note normally (until 01/01/2026)
- **Note**: Simples Nacional/MEI taxpayers cannot test with new tags initially

### Timeline
- **Until 31/12/2025**: Test phase with `<nfse_teste>1`
- **From 01/01/2026**: IBS/CBS tags become optional but recommended

## 3. PIS and COFINS (Retained or Own)

### Configuration
| `TipoRetencao` Value | Description |
|---------------------|-------------|
| 1 | PIS/COFINS Retido (Retained) |
| 2 | PIS/COFINS Não Retido (Not Retained) |
| 3 | PIS Retido/COFINS Não Retido |
| 4 | PIS Não Retido/COFINS Retido |

### Important Rules
- When PIS/COFINS is **retained**, value is deducted from IBS/CBS calculation base
- When **not retained**, no deduction occurs from invoice net value
- Always fill `pis_cofins` group when applicable

## 4. XML Structure Overview

### Root Structure
```
<nfse>
  <nfse_teste>1</nfse_teste>
  <identificador>UNIQUE_ID</identificador>
  <rps>...</rps> (optional)
  <pedagio>...</pedagio> (optional)
  <nf>...</nf>
  <prestador>...</prestador>
  <tomador>...</tomador>
  <itens>...</itens>
  <IBSCBS>...</IBSCBS> (REQUIRED for IBS/CBS tests)
  <genericos>...</genericos> (optional)
  <produtos>...</produtos> (optional)
  <forma_pagamento>...</forma_pagamento> (optional)
</nfse>
```

## 5. Critical XML Tags

### Test Tag (Mandatory for Tests)
```xml
<nfse_teste>1</nfse_teste>
```

### Identifier (Prevents Duplicate Processing)
```xml
<identificador>unique_id_here</identificador>
```

### Service Provider (Emitter)
```xml
<prestador>
  <cpfcnpj>00000000000000</cpfcnpj> (14 digits)
  <cidade>0000</cidade> (TOM code)
</prestador>
```

### Service Taker (Receiver)
```xml
<tomador>
  <tipo>J|F|E</tipo> (J=Legal, F=Natural, E=Foreign)
  <cpfcnpj>00000000000000</cpfcnpj>
  <nome_razao_social>Name</nome_razao_social>
  <!-- ... other address fields ... -->
</tomador>
```

### Invoice Values
```xml
<nf>
  <valor_total>1500.00</valor_total>
  <valor_desconto>100.00</valor_desconto>
  <!-- Tax values (do not affect IBS/CBS calculation) -->
  <valor_ir>50.00</valor_ir>
  <valor_inss>30.00</valor_inss>
  <pis_cofins>...</pis_cofins>
  <IBSCBS>...</IBSCBS> (NOT required - auto-calculated)
</nf>
```

### PIS/COFINS Group
```xml
<pis_cofins>
  <cst>XX</cst> (2 digits)
  <tipo_retencao>1</tipo_retencao> (1-4)
  <base_calculo>1000.00</base_calculo>
  <aliquota_pis>0.65</aliquota_pis> (%)
  <aliquota_cofins>3.00</aliquota_cofins> (%)
</pis_cofins>
```

## 6. IBS/CBS Tags (Tax Reform)

### Main IBS/CBS Group (REQUIRED for tests)
```xml
<IBSCBS> (at <nfse> level, not inside <nf>)
  <finNFSe>0</finNFSe>
  <indFinal>1</indFinal>
  <cIndOp>030102</cIndOp>
  
  <!-- Real Estate Operations (if applicable) -->
  <imovel>
    <end>
      <CEP>88505162</CEP>
      <xLgr>rua imovel</xLgr>
      <nro>10</nro>
    </end>
  </imovel>
  
  <valores>
    <trib>
      <gIBSCBS>
        <CST>200</CST>
        <cClassTrib>200028</cClassTrib>
        <cCredPres>XX</cCredPres> (optional)
      </gIBSCBS>
    </trib>
  </valores>
</IBSCBS>
```

### IBS/CBS Codes Reference
- **CST**: Tax Situation Code (3 digits)
- **cClassTrib**: Tax Classification Code (6 digits)
- **cIndOp**: Operation Indicator Code (6 digits)
- **finNFSe**: Purpose indicator (0=regular)

## 7. Items/Service List

```xml
<itens>
  <lista>
    <codigo_item_lista_servico>0101</codigo_item_lista_servico>
    <codigo_nbs>123456789</codigo_nbs> (MANDATORY for Tax Reform)
    <descritivo>Service description</descritivo>
    <aliquota_item_lista_servico>5.0000</aliquota_item_lista_servico>
    <situacao_tributaria>0000</situacao_tributaria>
    <valor_tributavel>1000.00</valor_tributavel>
    <tributa_municipio_prestador>S</tributa_municipio_prestador>
  </lista>
</itens>
```

## 8. Validation Rules & Error Codes

### Critical Validations (00336-00368)
| Code | Description |
|------|-------------|
| 00336 | When IBS/CBS rates in `/nfse/nf/IBSCBS`, must also fill `/nfse/IBSCBS` |
| 00337 | When IBS/CBS taxes in `/nfse/IBSCBS`, must also fill rates in `/nfse/nf/IBSCBS` |
| 00338 | Calculation base value (vBC) incorrect |
| 00345 | IBS rate incorrect |
| 00350 | CBS rate incorrect |
| 00352 | Tax Classification cannot be used with regular taxation |
| 00366 | Brazilian Service Nomenclature (NBS) is mandatory |
| 00368 | Link between Service List × NBS × Operation Indicator × Tax Classification doesn't exist |

### Operation Types (tpOper)
| Value | Description |
|-------|-------------|
| 1 | Supply with subsequent payment |
| 2 | Payment receipt with supply already done |
| 3 | Supply with payment already done |
| 4 | Payment receipt with subsequent supply |
| 5 | Simultaneous supply and payment |

## 9. Character Restrictions

| Invalid Character | Replace With |
|------------------|--------------|
| < | `&lt;` |
| > | `&gt;` |
| ' | `&apos;` |
| " | `&quot;` |
| / | Not allowed |
| & | `&amp;` |

## 10. Return Configuration

To receive complete NFS-e return with IBS/CBS data:
1. Access "Personalização do Prestador"
2. Enable "Utiliza Retorno Completo na Importação de XML" in "Webservice" tab

## 11. Example XML (Test Mode)

```xml
<?xml version="1.0" encoding="UTF-8"?>
<nfse>
  <nfse_teste>1</nfse_teste>
  
  <nf>
    <valor_total>3447.91</valor_total>
    <valor_desconto>0.00</valor_desconto>
    <valor_ir>0.00</valor_ir>
    <valor_inss>0.00</valor_inss>
    <valor_contribuicao_social>0.00</valor_contribuicao_social>
    <valor_rps>0.00</valor_rps>
    <pis_cofins>
      <cst>01</cst>
      <tipo_retencao>2</tipo_retencao>
      <base_calculo>3447.91</base_calculo>
      <aliquota_pis>0.65</aliquota_pis>
      <aliquota_cofins>3.00</aliquota_cofins>
    </pis_cofins>
    <valor_pis>22.41</valor_pis>
    <valor_cofins>103.44</valor_cofins>
    <observacao>Test invoice</observacao>
  </nf>
  
  <prestador>
    <cpfcnpj>12345678000195</cpfcnpj>
    <cidade>8055</cidade>
  </prestador>
  
  <tomador>
    <tipo>J</tipo>
    <cpfcnpj>98765432000187</cpfcnpj>
    <nome_razao_social>Company Name</nome_razao_social>
    <logradouro>Street Address</logradouro>
    <numero_residencia>123</numero_residencia>
    <bairro>Neighborhood</bairro>
    <cidade>8055</cidade>
    <cep>89000000</cep>
  </tomador>
  
  <itens>
    <lista>
      <codigo_item_lista_servico>0101</codigo_item_lista_servico>
      <codigo_nbs>123456789</codigo_nbs>
      <descritivo>Technical consulting services</descritivo>
      <aliquota_item_lista_servico>5.0000</aliquota_item_lista_servico>
      <situacao_tributaria>0000</situacao_tributaria>
      <valor_tributavel>3447.91</valor_tributavel>
      <tributa_municipio_prestador>S</tributa_municipio_prestador>
    </lista>
  </itens>
  
  <IBSCBS>
    <finNFSe>0</finNFSe>
    <indFinal>1</indFinal>
    <cIndOp>030102</cIndOp>
    <valores>
      <trib>
        <gIBSCBS>
          <CST>200</CST>
          <cClassTrib>200028</cClassTrib>
        </gIBSCBS>
      </trib>
    </valores>
  </IBSCBS>
</nfse>
```

## 12. Important Dates & Transition

| Date | Requirement |
|------|-------------|
| Up to 31/12/2025 | Test with `<nfse_teste>1` |
| From 01/01/2026 | IBS/CBS tags optional but recommended |
| Future | IBS/CBS will become mandatory for validation |

## 13. Resources & Links
- **National NFS-e Documentation**: https://www.gov.br/nfse/pt-br/biblioteca
- **NT-004**: https://www.gov.br/nfse/pt-br/biblioteca/documentacao-tecnica/rtc-producao-restrita-piloto/nt-004-se-cgnfse-novo-layout-rtc-v2-00-2025110.pdf
- **Operation Indicators**: https://www.gov.br/nfse/pt-br/biblioteca/documentacao-tecnica/rtc/anexovii-indop_ibscbs_v1-00-00.xlsx

## 14. Support
- **IPM Support**: (47) 3531-1500
- **Online Support Tool**: Available in IPM system


## IBS/CBS (NF level)

```XML
<IBSCBS>
    <pRedutor>0,00</pRedutor>
    <valores>
        <vBC>0,00</vBC> //Valor da base de cálculo (BC) do IBS/CBS antes das reduções para cálculo do tributo bruto. 
        
                                      // vBC = vServ - descIncond – vCalcReeRepRes – vISSQN – vPIS - vCOFINS (até 2026) 
                                      // ou 
                                      // vBC = vServ - descIncond – vCalcReeRepRes – vISSQN (até 2032) 
        <uf>
            <pIBSUF></pIBSUF> //Alíquota da UF para IBS da localidade de incidência parametrizada no sistema.
            <pRedAliqUF></pRedAliqUF> // Percentual de redução de alíquota estadual.
            <pAliqEfetUF></pAliqEfetUF> // pAliqEfetUF = pIBSUF x (1 - pRedAliqUF) x (1 - pRedutor)
                                                              // Se pRedAliqUF não for informado na DPS, então pAliqEfetUF é a própria pIBSUF.
        </uf> // Grupo de Informações relativas aos valores do IBS Estadual 
        <mun> // Grupo de Informações relativas aos valores do IBS Municipal 
            <pIBSMun></pIBSMun> // Alíquota da UF para IBS da localidade de incidência parametrizada no sistema.
            <pRedAliqMun></pRedAliqMun> //Percentual de redução de alíquota municipal
            <pAliqEfetMun><pAliqEfetMun> // pAliqEfetMun = pIBSMun x (1 - pRedAliqMun) x (1 - pRedutor) 
                                                                  // Se pRedAliqMun não for informado na DPS, então pAliqEfetMun é a própria pIBSMun. 
        </mun>
        <fed> // Grupo de Informações relativas aos valores da CBS 
            <pCBS> </pCBS> // Alíquota CBS parametrizada no sistema.
            <pRedAliqCBS> </pRedAliqCBS> // Percentual da redução de alíquota.
            <pAliqEfetCBS></pAliqEfetCBS> // pAliqEfetCBS = pCBS x (1 - pRedAliqCBS) x (1 - pRedutor) 
                                                                  // Se pRedAliqCBS não for informado na DPS, então pAliqEfetCBS é a própria pCBS.
        </fed>
    </valores>
    <totCIBS> // Grupo de Totalizadores
      <vTotNF></vTotNF> // Valor Total da NF considerando os impostos por fora: IBS e CBS. O IBS e a CBS são por fora, por isso seus valores devem sera dicionados ao valor total da NF. 
                                        // vTotNF = vLiq (em 2026) --> current case
                                        // vTotNF = vLiq + vCBS + vIBSTot (a partir de 2027) --> future
      <gTribRegular>
        <pAliqEfeRegIBSUF></pAliqEfeRegIBSUF> //Alíquota efetiva de tributação regular do IBS estadual
        <vTribRegIBSUF></vTribRegIBSUF> // Valor da tributação regular do IBS estadual. 
                                                                  // vTribRegIBSUF = vBC x pAliqEfeRegIBSUF
        <pAliqEfeRegIBSMun> </pAliqEfeRegIBSMun> // Alíquota efetiva de tributação regular do IBS municipal
        <vTribRegIBSMun></vTribRegIBSMun> // Valor da tributação regular do IBS municipal.
                                                                        // vTribRegIBSMun = vBC x pAliqEfeRegIBSMun 
        <pAliqEfeRegCBS></pAliqEfeRegCBS> // Alíquota efetiva de tributação regular da CBS 
        <vTribRegCBS> </vTribRegCBS> // Valor da tributação regular da CBS. 
                                                            //  vTribRegCBS = vBC x pAliqEfeRegCBS 
    </gTribRegular>
    <gTribCompraGov> // Grupo de informações da composição do valor 
do IBS e da CBS em compras governamentais. --> Generally don't apply on our case.
      <pIBSUF></pIBSUF> // Alíquota do IBS de competência do Estado 
      <vIBSUF> </vIBSUF>  // Valor do Tributo do IBS da UF calculado
      <pIBSMun> </pIBSMun> // Alíquota do IBS de competência do Município 
      <vIBSMun> <vIBSMun> // Valor do Tributo do IBS do Município calculado
      <pCBS></pCBS> // Alíquota da CBS 
      <vCBS> </vCBS> // Valor do Tributo da CBS calculado 
    </gTribCompraGov>
    <gIBS> // Grupo de totalizadores referentes ao IBS
      <vIBSTot> </vIBSTot> // Valor total do IBS. 
                                          // vIBSTot = vIBSUF + vIBSMun
      <gIBSCredPres> // Grupo de valores referentes ao crédito presumido para IBS. 
        <pCredPresIBS> </pCredPresIBS>  // Alíquota do crédito presumido para o IBS
        <vCredPresIBS> </vCredPresIBS> // Valor do Crédito Presumido para o IBS 
      </gIBSCredPres>
      <gIBSUFTot> // Grupo de valores referentes ao IBS Estadual 
        <vDifUF></vDifUF> // Total do Diferimento do IBS estadual. 
                                          //vDifUF = vIBSUF x pDifUF
        <vIBSUF> </vIBSUF> // Total valor do IBS municipal. 
                                            // vIBSUF = vBC x (pIBSUF ou pAliqEfetUF) 
      </gIBSUFTot> 
      <gIBSMunTot> // Grupo de valores referentes ao IBS Municipal 
        <vDifMun></vDifMun> // Total do Diferimento do IBS municipal. 
                                                // vDifMun = vIBSMun x pDifMun
        <vIBSMun> </vIBSMun> // Total valor do IBS municipal. 
                                                  // vIBSMun = vBC x (pIBSMun ou pAliqEfetMun) 
      </gIBSMunTot> 
    </gIBS>
    <gCBS> // Grupo de valores referentes à CBS 
            <gCBSCredPres> // Grupo de valores referentes ao crédito presumido para CBS
              <pCredPresCBS><pCredPresCBS> // Alíquota do crédito presumido para a CBS.
              <vCredPresCBS></vCredPresCBS> // Valor do Crédito Presumido da CBS. 
                                                                       // vCredPresCBS = vBC x pCredPresCBS
            </gCBSCredPres> 
            <vDivCBS></vDifCBS> // Total do Diferimento CBS. 
                                                  // vDifCBS = vCBS x pDifCBS 
            <vCBS></vCBS> // Total valor da CBS da União.
                                        // vCBS = vBC x (pCBS ou pAliqEfetCBS) 
    </gCBS>
  </totCIBS>
</IBSCBS>

```
---

*Document version: 1.4 (17/12/2025)*  
*Based on NTE-122/2025 - Web Service for NFS-e Emission Tests with Tax Reform Data*



