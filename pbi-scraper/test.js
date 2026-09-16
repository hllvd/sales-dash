// test.js
// Script para teste direto do novo fluxo de scraping: Consultor
// Execução:
//   node test.js [matricula] [senha]
// Ou se configurado no .env (AVAPRO_MATRICULA / AVAPRO_PASSWORD):
//   node test.js

require('dotenv').config();
const { runConsultorScrape } = require('./scrapeConsultor');
const { scrapeConsultorDirect } = require('./extractorConsultor');

async function main() {
  const args = process.argv.slice(2);

  const matricula = args[0] || process.env.AVAPRO_MATRICULA;
  const password = args[1] || process.env.AVAPRO_PASSWORD;
  const scrapeDates = args[2] || process.env.SCRAPE_DATES || null;

  // Headless false por padrão conforme solicitado, configurável por env var HEADLESS=true
  const isHeadless = process.env.HEADLESS === 'true' || process.env.HEADLESS === '1';

  if (!matricula || !password) {
    console.error('\n======================================================');
    console.error('❌ Erro: Credenciais do AVA PRO não encontradas!');
    console.error('======================================================');
    console.error('Uso:');
    console.error('  node test.js <matricula> <senha> [meses]');
    console.error('Exemplos com filtro de meses (Possibilidade A - HTTP POST direto):');
    console.error('  SCRAPE_DATES="2024-06,2024-07" node test.js <matricula> <senha>');
    console.error('  ou: node test.js <matricula> <senha> 2024-06,2024-07');
    console.error('\nOu configurar no arquivo .env (pbi-scraper/.env):');
    console.error('  AVAPRO_MATRICULA=sua_matricula');
    console.error('  AVAPRO_PASSWORD=sua_senha');
    console.error('======================================================\n');
    process.exit(1);
  }

  try {
    const isDirectMode = process.env.DIRECT === 'true';

    if (isDirectMode) {
      console.log('⚡ Modo Direto (HTTP POST / Possibilidade A) ativado.');
      console.log(`📅 Meses selecionados: ${scrapeDates || 'Nenhum (extração direta completa)'}`);
      const result = await scrapeConsultorDirect({
        matricula,
        password,
        scrapeDates,
        outputDir: './outputs'
      });
      console.log('Resultado do teste direto:', result);
    } else {
      console.log(`🖥️ Modo Navegador VISÍVEL (Headless: ${isHeadless}) ativado...`);
      const result = await runConsultorScrape({
        matricula,
        password,
        headless: isHeadless,
        timeoutNoQueryMs: 30000, // 30s de inatividade sem novas queries
        scrollIntervalMs: 2000,  // 2s entre cada scroll
        outputDir: './outputs'
      });
      console.log('Resultado do teste:', result);
    }
  } catch (err) {
    console.error('\n❌ Erro durante o teste:', err.message);
    if (err.stack) console.error(err.stack);
    process.exit(1);
  }
}

main();
