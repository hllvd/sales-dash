import { test, expect } from '@playwright/test';
import { loginAs } from './helpers/auth';


test.describe('Approval Requests E2E', () => {
  test.describe.configure({ mode: 'serial' });

  const RUN_ID = Array.from({ length: 8 }, () =>
    String.fromCharCode(97 + Math.floor(Math.random() * 26))
  ).join('') + Date.now().toString().slice(-4);
  const ADMIN_EMAIL = `parent.admin.${RUN_ID}@test.com`;
  const USER_EMAIL = `request.user.${RUN_ID}@test.com`;
  const GUIMEL_USER_EMAIL = `guimel.user.${RUN_ID}@test.com`;
  const BATCH_USER_EMAIL = `batch.user.${RUN_ID}@test.com`;
  const INSTITUTIONAL_EMAIL = `guimel.${RUN_ID}@autorizadoademicon.com.br`;
  const GUIMEL_TEAM_NAME = `Equipe Guimel ${RUN_ID}`;
  const PASSWORD = 'Password123!';

  let superadminToken = '';
  let superadminId = '';
  let adminId = '';

  test.beforeAll(async ({ request }) => {
    // 1. Login as SuperAdmin
    const loginRes = await request.post('/api/users/login', {
      data: { email: 'superadmin@salesapp.com', password: 'string' },
    });
    expect(loginRes.ok()).toBeTruthy();
    const loginData = await loginRes.json();
    superadminToken = loginData.data.token;

    const meRes = await request.get('/api/users/me', {
      headers: { Authorization: `Bearer ${superadminToken}` },
    });
    expect(meRes.ok()).toBeTruthy();
    superadminId = (await meRes.json()).data.id;

    // Proactive cleanup of old test users
    const getUsersRes = await request.get('/api/users?pageSize=1000', {
      headers: { Authorization: `Bearer ${superadminToken}` },
    });
    if (getUsersRes.ok()) {
      const body = await getUsersRes.json();
      const usersList = body.data?.items || [];
      for (const u of usersList) {
        if (
          u.email.toLowerCase().includes('parent.admin.') ||
          u.email.toLowerCase().includes('request.user.') ||
          u.email.toLowerCase().includes('guimel.') ||
          u.email.toLowerCase().includes('batch.user.') ||
          u.email.toLowerCase().includes('@autorizadoademicon.com.br')
        ) {
          await request.delete(`/api/users/${u.id}`, {
            headers: { Authorization: `Bearer ${superadminToken}` },
          });
        }
      }
    }

    const registerUser = async (name: string, email: string, role: string, parentUserId?: string) => {
      for (let attempt = 1; attempt <= 3; attempt++) {
        const res = await request.post('/api/users/register', {
          headers: { Authorization: `Bearer ${superadminToken}` },
          data: { name, email, password: PASSWORD, role, parentUserId },
        });
        if (res.ok()) return;
        await new Promise(r => setTimeout(r, 500));
      }
      throw new Error(`Failed to register user ${email}`);
    };

    // 2. Register a parent admin
    await registerUser('Parent Admin Test', ADMIN_EMAIL, 'admin', superadminId);
    await new Promise(r => setTimeout(r, 400));

    const adminLoginRes = await request.post('/api/users/login', {
      data: { email: ADMIN_EMAIL, password: PASSWORD },
    });
    expect(adminLoginRes.ok()).toBeTruthy();
    const adminLoginData = await adminLoginRes.json();
    const adminToken = adminLoginData.data.token;
    const adminMeRes = await request.get('/api/users/me', {
      headers: { Authorization: `Bearer ${adminToken}` },
    });
    adminId = (await adminMeRes.json()).data.id;

    // 3. Register user initially under SuperAdmin
    await registerUser('User Request Test', USER_EMAIL, 'user', superadminId);

    // 4. Register Guimel test user under Parent Admin
    await registerUser('Guimel Request Test', GUIMEL_USER_EMAIL, 'user', adminId);

    // 5. Register Batch test user under Parent Admin
    await registerUser('Batch User Test', BATCH_USER_EMAIL, 'user', adminId);
  });

  test('Superadmin can view requests page and tabs', async ({ page }) => {
    await loginAs(page);
    await page.goto('/#/requests');
    await page.waitForSelector('text=Central de Solicitações');

    await expect(page.locator('text=Solicitações Pendentes')).toBeVisible();
    await expect(page.locator('text=Minhas Solicitações')).toBeVisible();
  });

  test('User asks to change superior for the first time, parent admin accepts it', async ({ page }) => {
    test.setTimeout(60000);

    // 1. User logs in
    await loginAs(page, USER_EMAIL, PASSWORD);

    // 2. User goes to Requests page to request superior
    await page.goto('/#/requests');
    await expect(page.getByRole('heading', { name: 'Central de Solicitações' })).toBeVisible();
    await page.click('button:has-text("Nova Solicitação")');

    await expect(page.getByRole('dialog')).toBeVisible();
    const select = page.locator('input[readonly].mantine-Select-input');
    await select.click();
    await page.click('div[role="option"]:has-text("Alteração de Superior (E-mail)")');

    const emailInput = page.locator('input[placeholder="superior@exemplo.com"]');
    await emailInput.fill(ADMIN_EMAIL);
    await page.click('button:has-text("Enviar Solicitação")');

    // Wait for modal to close
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 15000 });

    // Verify request is listed under Minhas Solicitações
    await expect(page.getByRole('cell', { name: 'Alteração de Superior (ParentEmail)' }).first()).toBeVisible({ timeout: 15000 });

    // 3. Target Admin logs in to accept the request
    await loginAs(page, ADMIN_EMAIL, PASSWORD);


    // Go to Requests page
    await page.goto('/#/requests');
    await page.click('text=Solicitações Pendentes');

    // Find row with user and click Sim / Aprovar
    const pendingRow = page.locator('tr', { hasText: USER_EMAIL });
    await expect(pendingRow).toBeVisible({ timeout: 15000 });
    await pendingRow.locator('button:has-text("Sim")').click();

    // Confirm request is processed / removed from pending
    await expect(pendingRow).not.toBeVisible({ timeout: 15000 });

    // 4. Verify user now has target Admin as superior via My Profile page (displays parent Name)
    await loginAs(page, USER_EMAIL, PASSWORD);


    await page.goto('/#/my-profile');
    await expect(page.getByText('Parent Admin Test', { exact: false })).toBeVisible({ timeout: 15000 });
  });

  test('User/Admin can request classification level change and parent admin accepts it', async ({ page }) => {
    test.setTimeout(60000);

    // 1. User logs in
    await loginAs(page, USER_EMAIL, PASSWORD);

    // 2. User goes to Requests page and creates RequestClassificationLevel
    await page.goto('/#/requests');
    await expect(page.getByRole('heading', { name: 'Central de Solicitações' })).toBeVisible();
    await page.click('button:has-text("Nova Solicitação")');

    await expect(page.getByRole('dialog')).toBeVisible();
    const typeSelect = page.locator('input[readonly].mantine-Select-input').first();
    await typeSelect.click();
    await page.click('div[role="option"]:has-text("Solicitação de Nível de Classificação")');

    // Select level (e.g. Bronze or Prata)
    const levelSelect = page.locator('input[readonly].mantine-Select-input').nth(1);
    await levelSelect.click();
    await page.getByRole('option').first().click();

    await page.click('button:has-text("Enviar Solicitação")');
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 15000 });

    // Verify request is listed under Minhas Solicitações
    await expect(page.getByRole('cell', { name: 'Solicitação de Nível de Classificação' }).first()).toBeVisible({ timeout: 15000 });

    // 3. Parent Admin logs in to accept the level request
    await loginAs(page, ADMIN_EMAIL, PASSWORD);

    // Go to Requests page
    await page.goto('/#/requests');
    await page.click('text=Solicitações Pendentes');

    // Find row with user and click Sim
    const pendingRow = page.locator('tr', { hasText: USER_EMAIL });
    await expect(pendingRow).toBeVisible({ timeout: 15000 });
    await expect(pendingRow.getByText('Solicitação de Nível de Classificação')).toBeVisible();
    await pendingRow.locator('button:has-text("Sim")').click();

    await expect(pendingRow).not.toBeVisible({ timeout: 15000 });
  });

  test('User asks to be an admin, parent admin accepts it', async ({ page }) => {
    test.setTimeout(60000);

    // 1. User logs in
    await loginAs(page, USER_EMAIL, PASSWORD);

    // 2. User goes to Requests page and creates RequestAdminRole
    await page.goto('/#/requests');
    await expect(page.getByRole('heading', { name: 'Central de Solicitações' })).toBeVisible();
    await page.click('button:has-text("Nova Solicitação")');

    await expect(page.getByRole('dialog')).toBeVisible();
    const select = page.locator('input[readonly].mantine-Select-input');
    await select.click();
    await page.click('div[role="option"]:has-text("Solicitação de Perfil Administrador (Role Admin)")');

    await page.click('button:has-text("Enviar Solicitação")');
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 15000 });

    // Verify request is listed under Minhas Solicitações
    await expect(page.getByRole('cell', { name: 'Solicitação de Perfil Administrador (Role Admin)' }).first()).toBeVisible({ timeout: 15000 });

    // 3. Parent Admin logs in to accept the role request
    await loginAs(page, ADMIN_EMAIL, PASSWORD);

    // Go to Requests page
    await page.goto('/#/requests');
    await page.click('text=Solicitações Pendentes');

    // Find row with user and click Sim
    const pendingRow = page.locator('tr', { hasText: USER_EMAIL });
    await expect(pendingRow).toBeVisible({ timeout: 15000 });
    await pendingRow.locator('button:has-text("Sim")').click();

    await expect(pendingRow).not.toBeVisible({ timeout: 15000 });

    // 4. Verify user is now an Admin (can see Contratos admin link)
    await loginAs(page, USER_EMAIL, PASSWORD);

    await expect(page.locator('a[href="#/contracts"]')).toBeVisible({ timeout: 15000 });
  });

  test('User asks to become Guimel and change email to institutional, parent admin accepts it', async ({ page }) => {
    test.setTimeout(60000);

    // 1. Guimel user logs in
    await loginAs(page, GUIMEL_USER_EMAIL, PASSWORD);

    // 2. User goes to Requests page to request Guimel team creation
    await page.goto('/#/requests');
    await expect(page.getByRole('heading', { name: 'Central de Solicitações' })).toBeVisible();
    await page.click('button:has-text("Nova Solicitação")');

    await expect(page.getByRole('dialog')).toBeVisible();
    const select = page.locator('input[readonly].mantine-Select-input').first();
    await select.click();
    await page.click('div[role="option"]:has-text("Eu sou Guimel agora, quero criar minha equipe")');

    const teamInput = page.locator('input[placeholder="Ex: Equipe Comercial SP"]');
    await teamInput.fill(GUIMEL_TEAM_NAME);

    // Check option to change email to institutional
    const checkbox = page.locator('label:has-text("Quero mudar meu email para o institucional")');
    await checkbox.click();

    // Verify institutional email input appears
    const emailInput = page.locator('input[placeholder="nome@autorizadoademicon.com.br"]');
    await expect(emailInput).toBeVisible();

    // Test invalid domain validation
    await emailInput.fill('invalid@gmail.com');
    await page.click('button:has-text("Enviar Solicitação")');
    await expect(page.getByText('O e-mail institucional deve ter o domínio @autorizadoademicon.com.br.')).toBeVisible();

    // Fill valid institutional email and submit
    await emailInput.fill(INSTITUTIONAL_EMAIL);
    await page.click('button:has-text("Enviar Solicitação")');

    // Wait for modal to close
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 15000 });

    // Verify request is listed under Minhas Solicitações with details
    await expect(page.getByRole('cell', { name: 'Criar Equipe (Guimel)' }).first()).toBeVisible({ timeout: 15000 });
    await expect(page.getByText(`Nome da Equipe: ${GUIMEL_TEAM_NAME} | Novo E-mail Institucional: ${INSTITUTIONAL_EMAIL}`).first()).toBeVisible({ timeout: 15000 });

    // 3. Parent Admin logs in to accept the Guimel request
    await loginAs(page, ADMIN_EMAIL, PASSWORD);

    await page.goto('/#/requests');
    await page.click('text=Solicitações Pendentes');

    // Find row with Guimel user and verify payload details
    const pendingRow = page.locator('tr', { hasText: GUIMEL_USER_EMAIL });
    await expect(pendingRow).toBeVisible({ timeout: 15000 });
    await expect(pendingRow.getByText('Criar Equipe (Guimel)')).toBeVisible();
    await expect(pendingRow.getByText(`Nome da Equipe: ${GUIMEL_TEAM_NAME} | Novo E-mail Institucional: ${INSTITUTIONAL_EMAIL}`)).toBeVisible();

    // Approve the request
    await pendingRow.locator('button:has-text("Sim")').click();
    await expect(pendingRow).not.toBeVisible({ timeout: 15000 });

    // 4. Verify user can now log in using the NEW institutional email and is Admin
    await loginAs(page, INSTITUTIONAL_EMAIL, PASSWORD);
    await expect(page.locator('a[href="#/contracts"]')).toBeVisible({ timeout: 15000 });
  });

  test('Batch approval and rejection: select-all and mass actions appear when >3 requests', async ({ page, request }) => {
    test.setTimeout(90000);

    const batchLoginRes = await request.post('/api/users/login', {
      data: { email: BATCH_USER_EMAIL, password: PASSWORD },
    });
    expect(batchLoginRes.ok()).toBeTruthy();
    const batchUserToken = (await batchLoginRes.json()).data.token;

    const createMatriculaRequest = async (matricula: string) => {
      const res = await request.post('/api/approval-requests', {
        headers: { Authorization: `Bearer ${batchUserToken}` },
        data: {
          requestType: 'RequestMatricula',
          payloadJson: JSON.stringify({ matriculaNumber: matricula }),
        },
      });
      expect(res.ok()).toBeTruthy();
      return (await res.json()).data.id;
    };

    // 1. Create 3 requests (<= 3 threshold)
    await createMatriculaRequest('50001');
    await createMatriculaRequest('50002');
    await createMatriculaRequest('50003');

    // 2. Admin logs in and navigates to Requests page
    await loginAs(page, ADMIN_EMAIL, PASSWORD);
    await page.goto('/#/requests');
    await expect(page.getByRole('heading', { name: 'Central de Solicitações' })).toBeVisible();

    const pendingTab = page.locator('.mantine-Tabs-tab', { hasText: 'Solicitações Pendentes' });
    await pendingTab.click();

    // Verify exactly 3 pending requests appear in table
    const tableRows = page.locator('.requests-table-container table tbody tr');
    await expect(tableRows).toHaveCount(3);

    // Verify checkboxes do NOT exist when count <= 3
    await expect(page.locator('th input[type="checkbox"]')).not.toBeVisible();
    await expect(page.locator('td input[type="checkbox"]')).not.toBeVisible();

    // 3. Create 2 more requests (total 5 > 3)
    await createMatriculaRequest('50004');
    await createMatriculaRequest('50005');

    // Reload page to refresh pending requests
    await page.reload();
    await pendingTab.click();

    // Verify 5 requests are now in the table
    await expect(tableRows).toHaveCount(5);

    // Verify checkboxes ARE visible now (>3)
    const headerCheckbox = page.locator('th input[type="checkbox"]');
    await expect(headerCheckbox).toBeVisible();
    await expect(page.locator('td input[type="checkbox"]')).toHaveCount(5);

    // Action bar should not be visible before selecting
    await expect(page.getByText('solicitações selecionadas')).not.toBeVisible();

    // 4. Test "Select All"
    await headerCheckbox.click();
    await expect(page.getByText('5 solicitações selecionadas')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Aceitar Selecionadas' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Rejeitar Selecionadas' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Desmarcar todas' })).toBeVisible();

    // Test "Desmarcar todas"
    await page.getByRole('button', { name: 'Desmarcar todas' }).click();
    await expect(page.getByText('solicitações selecionadas')).not.toBeVisible();

    // 5. Test Batch Reject (select 2 rows)
    const rowCheckboxes = page.locator('td input[type="checkbox"]');
    await rowCheckboxes.nth(0).click();
    await rowCheckboxes.nth(1).click();
    await expect(page.getByText('2 solicitações selecionadas')).toBeVisible();

    await page.getByRole('button', { name: 'Rejeitar Selecionadas' }).click();
    const rejectModal = page.getByRole('dialog');
    await expect(rejectModal).toBeVisible();
    await expect(rejectModal.getByText('Rejeitar Solicitações em Massa')).toBeVisible();

    const rejectComment = rejectModal.locator('textarea[placeholder="Motivo da rejeição em massa (opcional)"]');
    await rejectComment.fill('Rejeitado em lote no teste E2E');
    await rejectModal.getByRole('button', { name: 'Confirmar Rejeição' }).click();

    // Dialog closes
    await expect(rejectModal).not.toBeVisible({ timeout: 15000 });

    // 3 requests remain in pending table (<= 3 threshold)
    await expect(tableRows).toHaveCount(3);
    // Because count <= 3, checkboxes should no longer be visible
    await expect(headerCheckbox).not.toBeVisible();

    // 6. Create 1 more request so count is 4 (> 3)
    await createMatriculaRequest('50006');
    await page.reload();
    await pendingTab.click();

    await expect(tableRows).toHaveCount(4);
    await expect(headerCheckbox).toBeVisible();

    // 7. Test Batch Approve all 4
    await headerCheckbox.click();
    await expect(page.getByText('4 solicitações selecionadas')).toBeVisible();

    await page.getByRole('button', { name: 'Aceitar Selecionadas' }).click();
    const approveModal = page.getByRole('dialog');
    await expect(approveModal).toBeVisible();
    await expect(approveModal.getByText('Aprovar Solicitações em Massa')).toBeVisible();
    await approveModal.getByRole('button', { name: 'Confirmar Aprovação' }).click();

    await expect(approveModal).not.toBeVisible({ timeout: 15000 });

    // Table should now be empty
    await expect(page.getByText('Nenhuma solicitação pendente para aprovação.')).toBeVisible({ timeout: 15000 });
  });
});

