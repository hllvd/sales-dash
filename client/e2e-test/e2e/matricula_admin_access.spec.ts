import { test, expect } from '@playwright/test';

test.describe('Admin Matrícula Scoping and Access (TEAR 3)', () => {
  // Use serial mode to maintain DB state cleanly across sequential verification steps
  test.describe.configure({ mode: 'serial' });

  const RUN_ID = Date.now().toString().slice(-4);
  const ADMIN_EMAIL = `matricula.admin.${RUN_ID}@test.com`;
  const OTHER_EMAIL = `matricula.other.${RUN_ID}@test.com`;
  const MATR_ADM = `MATR-ADM-${RUN_ID}`;
  const MATR_OTH = `MATR-OTH-${RUN_ID}`;

  let adminUserId = '';
  let otherUserId = '';
  let otherMatriculaId = 0;
  let adminToken = '';
  let superadminToken = '';
  let superadminId = '';

  // Cleanup helper to run at start
  async function cleanupUsers(request: any) {
    // Login as SuperAdmin
    const loginRes = await request.post('/api/users/login', {
      data: { email: 'superadmin@salesapp.com', password: 'string' },
    });
    if (!loginRes.ok()) return;
    const loginBody = await loginRes.json();
    const token = loginBody.data.token;

    // Get all users
    const res = await request.get('/api/users?page=1&pageSize=1000', {
      headers: { 'Authorization': `Bearer ${token}` }
    });
    const data = await res.json();
    if (!data.success || !data.data) return;

    for (const u of data.data.items) {
      if (u.email.includes('matricula.admin.') || u.email.includes('matricula.other.')) {
        await request.delete(`/api/users/${u.id}`, {
          headers: { 'Authorization': `Bearer ${token}` }
        });
      }
    }

    // Get and cleanup matriculas from previous runs
    const matRes = await request.get('/api/usermatriculas', {
      headers: { 'Authorization': `Bearer ${token}` }
    });
    const matData = await matRes.json();
    if (matData.success && matData.data) {
      for (const m of matData.data) {
        if (m.matriculaNumber.startsWith('MATR-ADM-') || m.matriculaNumber.startsWith('MATR-OTH-')) {
          await request.delete(`/api/usermatriculas/${m.id}`, {
            headers: { 'Authorization': `Bearer ${token}` }
          });
        }
      }
    }
  }

  test.beforeAll(async ({ request }) => {
    // Proactive cleanup at test start
    await cleanupUsers(request);

    // 1. Login as SuperAdmin to setup test users
    const loginRes = await request.post('/api/users/login', {
      data: { email: 'superadmin@salesapp.com', password: 'string' },
    });
    expect(loginRes.ok()).toBeTruthy();
    const loginBody = await loginRes.json();
    superadminToken = loginBody.data.token;

    const meRes = await request.get('/api/users/me', {
      headers: { Authorization: `Bearer ${superadminToken}` },
    });
    expect(meRes.ok()).toBeTruthy();
    superadminId = (await meRes.json()).data.id;

    // 2. Register Admin and Other Users
    const registerUser = async (name: string, email: string, role: string, parentUserId?: string) => {
      const res = await request.post('/api/users/register', {
        headers: { Authorization: `Bearer ${superadminToken}` },
        data: { name, email, password: 'Password123!', role, parentUserId },
      });
      if (!res.ok()) {
        console.error(`FAILED to register user ${email}: status=${res.status()}, error=${await res.text()}`);
      }
      expect(res.ok()).toBeTruthy();
      return (await res.json()).data.id as string;
    };

    adminUserId = await registerUser('Admin Matricula Test', ADMIN_EMAIL, 'admin', superadminId);
    otherUserId = await registerUser('Other Matricula Test', OTHER_EMAIL, 'user', adminUserId);

    // 3. Assign Matriculas
    const assignMatricula = async (userId: string, matriculaNumber: string, isOwner: boolean) => {
      const res = await request.post('/api/usermatriculas', {
        headers: { Authorization: `Bearer ${superadminToken}` },
        data: {
          userId,
          matriculaNumber,
          isOwner,
          isActive: true,
          startDate: new Date().toISOString()
        }
      });
      expect(res.ok()).toBeTruthy();
      return (await res.json()).data.id as number;
    };

    await assignMatricula(adminUserId, MATR_ADM, true);
    otherMatriculaId = await assignMatricula(otherUserId, MATR_OTH, true);

    // Get Admin Token for API testing
    const adminLoginRes = await request.post('/api/users/login', {
      data: { email: ADMIN_EMAIL, password: 'Password123!' },
    });
    expect(adminLoginRes.ok()).toBeTruthy();
    adminToken = (await adminLoginRes.json()).data.token;
  });

  test('should restrict Admin to see only their owned matriculas and hide write controls', async ({ page }) => {
    // Login as Admin
    await page.goto('/');
    await page.fill('input[type="email"]', ADMIN_EMAIL);
    await page.fill('input[type="password"]', 'Password123!');
    await page.click('button.login-button');

    // Verify left menu shows "Matrículas" (since they have the permission by default via Access Control seeder)
    const matriculasMenuLink = page.locator('a[href="#/matriculas"]');
    await expect(matriculasMenuLink).toBeVisible({ timeout: 15000 });

    // Navigate to Matrículas page
    await matriculasMenuLink.click();
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Matrículas' })).toBeVisible({ timeout: 15000 });

    // Verify table has Admin's owned matrícula
    const adminMatRow = page.locator('tr', { hasText: MATR_ADM });
    await expect(adminMatRow).toBeVisible({ timeout: 10000 });
    await expect(adminMatRow).toContainText('Proprietário');

    // Verify table does NOT contain the other matrícula
    const otherMatRow = page.locator('tr', { hasText: MATR_OTH });
    await expect(otherMatRow).not.toBeVisible();

    // Verify write action buttons are hidden
    await expect(page.locator('button:has-text("Nova Matrícula")')).not.toBeVisible();
    await expect(page.locator('button:has-text("Importar CSV")')).not.toBeVisible();

    // Verify Ações column header and row action buttons are hidden
    await expect(page.locator('th:has-text("Ações")')).not.toBeVisible();
    await expect(page.locator('button[title="Editar"], .tabler-icon-edit')).not.toBeVisible();
    await expect(page.locator('button[title="Excluir"], .tabler-icon-trash')).not.toBeVisible();
  });

  test('should enforce API security on UserMatricula endpoints for Admin', async ({ request }) => {
    // 1. GET /api/usermatriculas (GetAll) should only return the Admin's own owner matrícula
    const getAllRes = await request.get('/api/usermatriculas', {
      headers: { Authorization: `Bearer ${adminToken}` }
    });
    expect(getAllRes.ok()).toBeTruthy();
    const getAllBody = await getAllRes.json();
    expect(getAllBody.success).toBeTruthy();
    expect(getAllBody.data).toBeInstanceOf(Array);
    
    // Ensure all returned items belong to the logged-in admin and they are the owner
    const items = getAllBody.data;
    for (const item of items) {
      expect(item.userId).toBe(adminUserId);
      expect(item.isOwner).toBe(true);
      expect(item.matriculaNumber).toBe(MATR_ADM);
    }

    // 2. GET /api/usermatriculas/{id} (GetById) of another user's matrícula should return 403 Forbidden
    const getByIdRes = await request.get(`/api/usermatriculas/${otherMatriculaId}`, {
      headers: { Authorization: `Bearer ${adminToken}` }
    });
    expect(getByIdRes.status()).toBe(403);

    // 3. GET /api/usermatriculas/user/{userId} for another user should return 403 Forbidden
    const getByUserIdRes = await request.get(`/api/usermatriculas/user/${otherUserId}`, {
      headers: { Authorization: `Bearer ${adminToken}` }
    });
    expect(getByUserIdRes.status()).toBe(403);
  });
});
