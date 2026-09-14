// test.js
// Script para teste direto do novo fluxo de scraping: Consultor
// Execução:
//   node test.js [matricula] [senha]
// Ou se configurado no .env (AVAPRO_MATRICULA / AVAPRO_PASSWORD):
//   node test.js

require('dotenv').config();
const { runConsultorScrape } = require('./scrapeConsultor');

async function main() {
  const args = process.argv.slice(2);

  const matricula = args[0] || process.env.AVAPRO_MATRICULA;
  const password = args[1] || process.env.AVAPRO_PASSWORD;

  // Headless false por padrão conforme solicitado, configurável por env var HEADLESS=true
  const isHeadless = process.env.HEADLESS === 'true' || process.env.HEADLESS === '1';

  if (!matricula || !password) {
    console.error('\n======================================================');
    console.error('❌ Erro: Credenciais do AVA PRO não encontradas!');
    console.error('======================================================');
    console.error('Você pode passar como argumentos diretamente:');
    console.error('  node test.js <matricula> <senha>');
    console.error('\nOu configurar no arquivo .env (pbi-scraper/.env):');
    console.error('  AVAPRO_MATRICULA=sua_matricula');
    console.error('  AVAPRO_PASSWORD=sua_senha');
    console.error('======================================================\n');
    process.exit(1);
  }

  console.log('Iniciando teste do fluxo Consultor via test.js...');

  try {
    const result = await runConsultorScrape({
      matricula,
      password,
      headless: isHeadless,
      timeoutNoQueryMs: 30000, // 30s de inatividade sem novas queries
      scrollIntervalMs: 2000,  // 2s entre cada scroll
      outputDir: './outputs'
    });

    console.log('Resultado do teste:', result);
  } catch (err) {
    console.error('\n❌ Erro durante o teste:', err.message);
    if (err.stack) console.error(err.stack);
    process.exit(1);
  }
}

main();
