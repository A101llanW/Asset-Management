import { execSync } from 'child_process';
import path from 'path';

export default async function globalSetup(): Promise<void> {
  const script = path.join(__dirname, 'scripts', 'prepare-e2e-db.ps1');
  execSync(`powershell -NoProfile -ExecutionPolicy Bypass -File "${script}"`, {
    stdio: 'inherit',
    cwd: path.join(__dirname, '..'),
  });
}
