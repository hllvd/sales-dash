import { test, expect } from '@playwright/test';

test.describe('Surveys / QA Feature (TEAR 3B)', () => {
  test.describe.configure({ mode: 'serial' });

  const surveyTitle = `Pesquisa E2E ${Date.now()}`;
  const surveyQuestion = 'Você confirma a participação nos testes pontuais automatizados?';

  test('should create, view results, answer via QA page, and track aggregate responses', async ({ page }) => {
    test.setTimeout(90000);

    // 1. Navigate to Surveys page as superadmin
    await page.goto('/#/surveys');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Perguntas / QA' })).toBeVisible({ timeout: 15000 });

    // 2. Fill question creation form
    const titleInput = page.locator('input[placeholder*="Confirmação de Matrícula"]');
    await titleInput.fill(surveyTitle);

    const questionInput = page.locator('textarea[placeholder*="procedimento de validação"]');
    await questionInput.fill(surveyQuestion);

    // 3. Filter and select superadmin user as recipient
    const nameFilterInput = page.locator('input[placeholder="Buscar por nome..."]');
    await nameFilterInput.fill('Super');

    // Wait for filtered users in table
    await page.waitForTimeout(500);
    const selectAllBtn = page.getByRole('button', { name: /Selecionar Todos os Filtrados/i });
    await selectAllBtn.click();

    // Verify at least 1 user is selected
    await expect(page.locator('text=usuário(s) selecionado(s)')).toBeVisible();

    // 4. Distribute question
    const sendBtn = page.locator('button:has-text("Distribuir Pergunta")');
    await expect(sendBtn).toBeEnabled();
    await sendBtn.click();

    // 5. Verify automatic transition to "Perguntas Enviadas" tab and row presence
    const surveyRow = page.locator('tr', { hasText: surveyTitle });
    await expect(surveyRow).toBeVisible({ timeout: 15000 });

    // 6. Click row to open SurveyResultModal
    await surveyRow.click();
    const resultModal = page.getByRole('dialog');
    await expect(resultModal).toBeVisible({ timeout: 10000 });
    await expect(resultModal.getByText(surveyTitle)).toBeVisible();
    await expect(resultModal.getByText(surveyQuestion)).toBeVisible();

    // Close result modal
    await resultModal.locator('.mantine-Modal-close').click();
    await expect(resultModal).not.toBeVisible();

    // 7. Navigate to "Meu QA" page
    await page.goto('/#/qa');
    await expect(page.getByRole('heading', { name: 'Meu Histórico de Perguntas / QA' })).toBeVisible({ timeout: 15000 });

    // Locate the survey card
    const qaCard = page.locator('.mantine-Card-root', { hasText: surveyTitle });
    await expect(qaCard).toBeVisible({ timeout: 10000 });
    await expect(qaCard.getByText('Pendente')).toBeVisible();

    // 8. Open answer modal from history card
    const answerBtn = qaCard.getByRole('button', { name: 'Responder agora' });
    await answerBtn.click();

    const answerModal = page.getByRole('dialog');
    await expect(answerModal).toBeVisible({ timeout: 10000 });
    await expect(answerModal.getByText(surveyQuestion)).toBeVisible();

    // Select "Sim"
    await answerModal.locator('.survey-option-card', { hasText: 'Sim' }).click();

    // Submit answer
    const submitAnswerBtn = answerModal.getByRole('button', { name: 'Enviar resposta' });
    await expect(submitAnswerBtn).toBeEnabled();
    await submitAnswerBtn.click();

    // Wait for modal to close and card to update
    await expect(answerModal).not.toBeVisible({ timeout: 10000 });
    await expect(qaCard.getByText('Respondida', { exact: true })).toBeVisible({ timeout: 10000 });
    await expect(qaCard.getByText('Sua resposta:')).toBeVisible();
    await expect(qaCard.getByText('Sim')).toBeVisible();

    // 9. Navigate back to Surveys page and verify aggregate response
    await page.goto('/#/surveys');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Perguntas / QA' })).toBeVisible({ timeout: 15000 });

    // Switch to "Perguntas Enviadas" tab
    await page.getByRole('tab', { name: /Perguntas Enviadas/i }).click();

    const updatedSurveyRow = page.locator('tr', { hasText: surveyTitle });
    await expect(updatedSurveyRow).toBeVisible({ timeout: 15000 });

    // Open results modal
    await updatedSurveyRow.click();
    await expect(resultModal).toBeVisible({ timeout: 10000 });

    // Verify response registered in table and aggregate
    await expect(resultModal.locator('td', { hasText: 'Sim' })).toBeVisible({ timeout: 10000 });
    await expect(resultModal.getByText('1 voto(s)')).toBeVisible();

    // Close result modal
    await resultModal.locator('.mantine-Modal-close').click();
    await expect(resultModal).not.toBeVisible();
  });
});
