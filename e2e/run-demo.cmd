@echo off
setlocal
cd /d "%~dp0"
set "E2E_PORT=51918"
echo Running internal demo lifecycle on port %E2E_PORT% ...
call npx playwright test tests/internal-demo-lifecycle.spec.ts --headed
endlocal
