import { chromium, FullConfig } from '@playwright/test';
import path from 'path';
import fs from 'fs';
import { loginAs, SUPERADMIN_EMAIL, SUPERADMIN_PASSWORD, ADMIN_EMAIL, ADMIN_PASSWORD } from './e2e/helpers/auth';

async function globalSetup(config: FullConfig) {
  const { baseURL } = config.projects[0].use;
  const authDir = path.resolve(__dirname, '.auth');

  if (!fs.existsSync(authDir)) {
    fs.mkdirSync(authDir, { recursive: true });
  }

  const browser = await chromium.launch();

  // 1. Superadmin authentication
  const superadminCtx = await browser.newContext({ baseURL: baseURL || 'http://localhost' });
  const superadminPage = await superadminCtx.newPage();
  try {
    await loginAs(superadminPage, SUPERADMIN_EMAIL, SUPERADMIN_PASSWORD);
    await superadminCtx.storageState({ path: path.join(authDir, 'superadmin.json') });
  } catch (err) {
    console.error('Failed global setup login for superadmin:', err);
    throw err;
  } finally {
    await superadminCtx.close();
  }

  // 2. Admin authentication
  const adminCtx = await browser.newContext({ baseURL: baseURL || 'http://localhost' });
  const adminPage = await adminCtx.newPage();
  try {
    await loginAs(adminPage, ADMIN_EMAIL, ADMIN_PASSWORD);
    await adminCtx.storageState({ path: path.join(authDir, 'admin.json') });
  } catch (err) {
    console.error('Failed global setup login for admin:', err);
    throw err;
  } finally {
    await adminCtx.close();
  }

  await browser.close();
}

export default globalSetup;
