// test-scrape-consultor.js
// Executable CLI script for testing the Consultor scraping mode.
// Run with: npm run scrape:consultor
// Or: node test-scrape-consultor.js [matricula] [password]

require('dotenv').config();
const { runConsultorScrape } = require('./scrapeConsultor');
const { scrapeConsultorDirect } = require('./extractorConsultor');

async function main() {
  const args = process.argv.slice(2);
  
  const matricula = args[0] || process.env.AVAPRO_MATRICULA;
  const password = args[1] || process.env.AVAPRO_PASSWORD;
  const scrapeDates = args[2] || process.env.SCRAPE_DATES || null;
  
  // Headless: default is false as requested, but can be set via env var HEADLESS=true
  const isHeadless = process.env.HEADLESS === 'true' || process.env.HEADLESS === '1';

  if (!matricula || !password) {
    console.error('\n❌ Erro: Credenciais não encontradas!');
    console.error('Passe os parâmetros na linha de comando ou configure no arquivo .env:');
    console.error('  node test-scrape-consultor.js <matricula> <senha> [meses]');
    console.error('Exemplo com meses (Possibilidade A - HTTP POST direto):');
    console.error('  SCRAPE_DATES="2024-06,2024-07" node test-scrape-consultor.js <matricula> <senha>');
    console.error('  ou: node test-scrape-consultor.js <matricula> <senha> 2024-06,2024-07');
    console.error('Ou defina no .env:');
    console.error('  AVAPRO_MATRICULA=sua_matricula');
    console.error('  AVAPRO_PASSWORD=sua_senha\n');
    process.exit(1);
  }

  try {
    const isDirectMode = Boolean(scrapeDates) || process.env.DIRECT === 'true';

    if (isDirectMode) {
      console.log('⚡ Modo Direto (HTTP POST / Opção 2) ativado.');
      console.log(`📅 Meses selecionados: ${scrapeDates || 'Nenhum'}`);
      const result = await scrapeConsultorDirect({
        matricula,
        password,
        scrapeDates,
        outputDir: './outputs'
      });
      console.log('Resultado do teste direto:', result);
    } else {
      console.log(`🖥️ Modo Navegador VISÍVEL (Headless: ${isHeadless}) ativado...`);
      await runConsultorScrape({
        matricula,
        password,
        headless: isHeadless,
        timeoutNoQueryMs: 30000, // 30 segundos sem novas queries
        scrollIntervalMs: 2000,  // 2 segundos entre scrolls
        outputDir: './outputs'
      });
    }
  } catch (err) {
    console.error('\n❌ Falha durante execução do teste de Consultor:', err.message);
    if (err.stack) console.error(err.stack);
    process.exit(1);
  }
}

main();
