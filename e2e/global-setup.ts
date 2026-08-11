import { execSync } from 'child_process';
import path from 'path';

export default async function globalSetup(): Promise<void> {
  if ((process.env.E2E_SKIP_GLOBAL_SETUP ?? '').trim() === '1') {
    return;
  }

  const repoRoot = path.join(__dirname, '..');
  const script = path.join(repoRoot, 'tools', 'database', 'Reset-E2eDatabase.ps1');
  execSync(
    `powershell -NoProfile -ExecutionPolicy Bypass -File "${script}" -ConfirmDestructive`,
    {
      stdio: 'inherit',
      cwd: repoRoot,
    },
  );
}
