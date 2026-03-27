# Security Policy

## Supported Versions

Only the latest release is actively maintained.

## Reporting a Vulnerability

**Please do NOT open a public GitHub Issue for security vulnerabilities.**

If you find a security issue (e.g., privilege escalation, arbitrary code execution, or a way to abuse this tool against other users), please report it via one of these channels:

- **GitHub Private Security Advisory**: Go to the **Security** tab of this repository → *Report a vulnerability*.
- This ensures the issue can be assessed and patched before public disclosure.

We aim to respond within **7 days** and to release a fix within **30 days** where feasible.

## Verifying Release Integrity

All release binaries are built directly from source by **GitHub Actions** and are never manually uploaded.
Each release includes a `SHA256SUMS.txt` file. Verify your download before running:

```powershell
# PowerShell (Windows)
(Get-FileHash .\SGuardLimiterMax_standalone.exe -Algorithm SHA256).Hash
```

Compare the output against the hash in `SHA256SUMS.txt`. **They must match exactly.**

A mismatch means the file may have been tampered with — do not run it.

## Scope

This tool operates exclusively through documented Windows APIs:

- `System.Diagnostics.Process` — process priority and affinity
- `powercfg.exe` — power plan switching
- `ipconfig /flushdns` — DNS cache flush
- `winmm.dll timeBeginPeriod` — timer resolution
- `HKCU\...\Run` registry key — startup registration

It does **not** inject code, modify game files, read game memory, install drivers, or communicate with any remote server.
