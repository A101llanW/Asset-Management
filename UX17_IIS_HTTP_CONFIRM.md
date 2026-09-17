# HTTP/deploy confirm after IIS publish — 2026-09-12 16:33 EAT

## Disk (inetpub) — PASS
- Index `isClasses` YES
- Sidebar Classes `domain=classes` YES
- Edit Kind lock YES
- Create domain/setupMode YES
- No `@section.Title` YES

## HTTP anon — PASS (no yellow errors)
- Login `/L35160674/Account/Login` 200
- Departments `?domain=org|classes` ? auth wall, returnUrl preserves domain (no Parser/Compilation)
- Create/Edit similarly auth-walled, no compile crash on gate

## Auth-gated UI content
- Needs QA headed session for full Org vs Classes / Kind lock prove (Debugging bot has no session cookies)

## Verdict
**DEPLOY PASS** for UX-1–7 files on inetpub. **HTTP smoke PASS** (login + no compile fail). **Headed content** = QA.
