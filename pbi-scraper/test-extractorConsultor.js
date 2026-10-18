// test-extractorConsultor.js
// Unit test for pure helper functions in extractorConsultor.js

const assert = require('assert');
const {
  normalizeTargetMonths,
  buildDateRangeFilter
} = require('./extractorConsultor');

console.log('Testing normalizeTargetMonths...');
assert.deepStrictEqual(normalizeTargetMonths(null), []);
assert.deepStrictEqual(normalizeTargetMonths(''), []);
assert.deepStrictEqual(normalizeTargetMonths('2024-06, 2024-07'), ['2024-06', '2024-07']);
assert.deepStrictEqual(normalizeTargetMonths(['2024-06-01', '2024-07-15']), ['2024-06', '2024-07']);
console.log('✅ normalizeTargetMonths passed!');

console.log('Testing buildDateRangeFilter...');
const df1 = buildDateRangeFilter('2', ['2026-07']);
assert.strictEqual(df1.length, 1);
const leftComp = df1[0].Condition.And.Left.Comparison;
const rightComp = df1[0].Condition.And.Right.Comparison;
assert.strictEqual(leftComp.ComparisonKind, 2); // >=
assert.strictEqual(leftComp.Right.Literal.Value, "datetime'2026-07-01T00:00:00'");
assert.strictEqual(rightComp.ComparisonKind, 3); // <
assert.strictEqual(rightComp.Right.Literal.Value, "datetime'2026-08-01T00:00:00'");
console.log('✅ buildDateRangeFilter passed!');

console.log('Testing parseDSR (scrapeConsultor and extractor)...');
const fs = require('fs');
const path = require('path');
const { parseDSR: parseConsultor } = require('./scrapeConsultor');
const { parseDSR: parseExtractor } = require('./extractor');
const { resolveDictValue } = require('./dsrDict');

console.log('Testing resolveDictValue (pure resolver)...');
const sampleDict100 = Array.from({ length: 100 }, (_, i) => `DictItem_${i}`);
// Integer index: resolves
assert.strictEqual(resolveDictValue(sampleDict100, 0), 'DictItem_0');
assert.strictEqual(resolveDictValue(sampleDict100, 82), 'DictItem_82');
// String literal: NEVER resolves against dict, even when string is a valid numeric index
assert.strictEqual(resolveDictValue(sampleDict100, '82'), '82', 'Literal string "82" must NOT be resolved against dict');
assert.strictEqual(resolveDictValue(sampleDict100, '2'), '2', 'Literal string "2" must NOT be resolved against dict');
assert.strictEqual(resolveDictValue(sampleDict100, '0'), '0', 'Literal string "0" must NOT be resolved against dict');
assert.strictEqual(resolveDictValue(sampleDict100, 'TextValue'), 'TextValue');
// Null / undefined / out of bounds
assert.strictEqual(resolveDictValue(sampleDict100, null), null);
assert.strictEqual(resolveDictValue(sampleDict100, undefined), undefined);
assert.strictEqual(resolveDictValue(sampleDict100, 150), 150);
assert.strictEqual(resolveDictValue(sampleDict100, -1), -1);
assert.strictEqual(resolveDictValue(null, 5), 5);
console.log('✅ resolveDictValue passed!');

// Mock DSR data mimicking PowerBI response:
// Row 0 has S (schema), C (values), Ø (nulls).
// Row 1 has R (repeat bitmask: column 0 and 1 repeated, column 2 changed)
const mockDsrData = {
  results: [
    {
      result: {
        data: {
          descriptor: {
            Select: [
              { Value: 'G0', Name: 'Col0_PontoDeVenda' },
              { Value: 'G1', Name: 'Col1_Comissionado' },
              { Value: 'G2', Name: 'Col2_Cota' },
              { Value: 'G3', Name: 'Col3_Valor' },
              { Value: 'G4', Name: 'Col4_DataVenda' },
              { Value: 'G5', Name: 'Col5_Status' }
            ]
          },
          dsr: {
            DS: [
              {
                ValueDicts: {
                  D0: ['Ponto de Venda Alpha', 'Ponto de Venda Beta'],
                  D1: ['Comissionado X', 'Comissionado Y'],
                  D2: Array.from({ length: 100 }, (_, i) => `DictCota_${i}`)
                },
                PH: [
                  {
                    DM1: [
                      // Entry 0: Schema + Baseline Data (C) + Null bitmask (bit 3 is null)
                      // G2 is index 0 -> should resolve to 'DictCota_0'
                      {
                        S: [
                          { N: 'G0', T: 1, DN: 'D0' },
                          { N: 'G1', T: 1, DN: 'D1' },
                          { N: 'G2', T: 1, DN: 'D2' },
                          { N: 'G3', T: 3 },
                          { N: 'G4', T: 7 },
                          { N: 'G5', T: 1 }
                        ],
                        C: [0, 0, 0, '2026-01-15', 'Normal'],
                        'Ø': 8 // 1 << 3 (G3 is null)
                      },
                      // Entry 1: Col0/1 repeated. Col2 is literal string '82' (NOT in dict, overflow)
                      // MUST remain literal '82' and NOT be replaced by DictCota_82!
                      {
                        R: 3, // (1 << 0) | (1 << 1)
                        C: ['82', 5000, '2026-01-16', 'Normal']
                      },
                      // Entry 2: Col0 changes. Col2 is integer number 82 (dictionary index)
                      // MUST resolve to DictCota_82!
                      {
                        R: 2, // (1 << 1)
                        C: [1, 82, 7500, '2026-01-17', 'Normal']
                      },
                      // Entry 3: Col2 is literal string '2' -> MUST remain '2' and NOT become DictCota_2!
                      {
                        R: 3,
                        C: ['2', 3000, '2026-01-18', 'Normal']
                      }
                    ]
                  }
                ]
              }
            ]
          }
        }
      }
    }
  ]
};

// 1. Test scrapeConsultor parseDSR
const rows1 = parseConsultor(mockDsrData);
assert.strictEqual(rows1.length, 4, 'Deve retornar exatamente 4 registros');
assert.strictEqual(rows1[0]['Col0_PontoDeVenda'], 'Ponto de Venda Alpha', 'Baseline Col0 deve ser extraído');
assert.strictEqual(rows1[0]['Col1_Comissionado'], 'Comissionado X', 'Baseline Col1 deve ser extraído');
assert.strictEqual(rows1[0]['Col2_Cota'], 'DictCota_0', 'Index 0 deve resolver para DictCota_0');
assert.strictEqual(rows1[0]['Col3_Valor'], null);
assert.strictEqual(rows1[0]['Col4_DataVenda'], '2026-01-15');

assert.strictEqual(rows1[1]['Col0_PontoDeVenda'], 'Ponto de Venda Alpha', 'Col0 deve ser repetido de Row 0');
assert.strictEqual(rows1[1]['Col1_Comissionado'], 'Comissionado X', 'Col1 deve ser repetido de Row 0');
assert.strictEqual(rows1[1]['Col2_Cota'], '82', 'Literal string "82" NÃO pode ser substituída por item de dicionário');
assert.strictEqual(rows1[1]['Col3_Valor'], 5000);

assert.strictEqual(rows1[2]['Col0_PontoDeVenda'], 'Ponto de Venda Beta', 'Col0 deve refletir novo valor');
assert.strictEqual(rows1[2]['Col1_Comissionado'], 'Comissionado X', 'Col1 deve continuar repetido');
assert.strictEqual(rows1[2]['Col2_Cota'], 'DictCota_82', 'Index 82 (number) deve resolver para DictCota_82');
assert.strictEqual(rows1[2]['Col3_Valor'], 7500);

assert.strictEqual(rows1[3]['Col2_Cota'], '2', 'Literal string "2" NÃO pode ser substituída por DictCota_2');
console.log('✅ scrapeConsultor parseDSR validado (incluindo literais e índices de dicionário)!');

// 2. Test extractor.js parseDSR
const rows2 = parseExtractor(mockDsrData);
assert.strictEqual(rows2.length, 4, 'extractor.js deve retornar 4 registros');
assert.strictEqual(rows2[0]['Col2_Cota'], 'DictCota_0');
assert.strictEqual(rows2[1]['Col2_Cota'], '82', 'extractor.js: Literal string "82" preservada');
assert.strictEqual(rows2[2]['Col2_Cota'], 'DictCota_82', 'extractor.js: Index 82 resolvido');
assert.strictEqual(rows2[3]['Col2_Cota'], '2', 'extractor.js: Literal string "2" preservada');
assert.strictEqual(rows2[0]['Col0_PontoDeVenda'], 'Ponto de Venda Alpha');
assert.strictEqual(rows2[1]['Col0_PontoDeVenda'], 'Ponto de Venda Alpha');
assert.strictEqual(rows2[2]['Col0_PontoDeVenda'], 'Ponto de Venda Beta');
console.log('✅ extractor parseDSR validado (incluindo literais e índices de dicionário)!');

// 3. Test paginationContext preservation across pages
const context = { prev: [] };
const page1Data = {
  results: [
    {
      result: {
        data: {
          descriptor: mockDsrData.results[0].result.data.descriptor,
          dsr: {
            DS: [
              {
                ValueDicts: mockDsrData.results[0].result.data.dsr.DS[0].ValueDicts,
                PH: [
                  {
                    DM1: [
                      mockDsrData.results[0].result.data.dsr.DS[0].PH[0].DM1[0] // only row 0
                    ]
                  }
                ]
              }
            ]
          }
        }
      }
    }
  ]
};

const p1Rows = parseConsultor(page1Data, context);
assert.strictEqual(p1Rows.length, 1);
assert.strictEqual(p1Rows[0]['Col0_PontoDeVenda'], 'Ponto de Venda Alpha');

const page2Data = {
  results: [
    {
      result: {
        data: {
          descriptor: mockDsrData.results[0].result.data.descriptor,
          dsr: {
            DS: [
              {
                ValueDicts: mockDsrData.results[0].result.data.dsr.DS[0].ValueDicts,
                PH: [
                  {
                    DM1: [
                      {
                        R: 3, // repeat col0 and col1 from previous page!
                        C: [1005, 9000, '2026-01-20', 'Normal']
                      }
                    ]
                  }
                ]
              }
            ]
          }
        }
      }
    }
  ]
};

const p2Rows = parseConsultor(page2Data, context);
assert.strictEqual(p2Rows.length, 1);
assert.strictEqual(p2Rows[0]['Col0_PontoDeVenda'], 'Ponto de Venda Alpha', 'Deve herdar Ponto de Venda da página anterior');
assert.strictEqual(p2Rows[0]['Col1_Comissionado'], 'Comissionado X', 'Deve herdar Comissionado da página anterior');
console.log('✅ Preservação de contexto entre páginas validada!');

// 4. Test real captured rawData if available
const realJsonPath = path.resolve(__dirname, 'outputs', 'consultor_direct_8203_2026-01_2026-02_2026-09-16T19-08-58-914Z.json');
if (fs.existsSync(realJsonPath)) {
  const fileContent = JSON.parse(fs.readFileSync(realJsonPath, 'utf8'));
  if (fileContent.rawData) {
    const realRows = parseConsultor(fileContent.rawData);
    assert.strictEqual(realRows.length, 156, 'Deve extrair todas as 156 linhas da página bruta capturada');
    assert.strictEqual(realRows[0]['2 Rel Carteira.Ponto de Venda'], 'LOGOS SOLUCOES - ANDREMAX');
    assert.strictEqual(realRows[1]['2 Rel Carteira.Ponto de Venda'], 'LOGOS SOLUCOES - ANDREMAX');
    assert.strictEqual(realRows[0]['2 Rel Carteira.Comissionado'], 'TMID LTDA');
    assert.strictEqual(realRows[1]['2 Rel Carteira.Comissionado'], 'TMID LTDA');
    console.log('✅ Teste com dados reais capturados do PowerBI (156 linhas) validado!');
  }
}

console.log('\n🎉 ALL UNIT TESTS PASSED!');
