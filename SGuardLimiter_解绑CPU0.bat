@echo off
chcp 65001 >nul
title SGuard Limiter Max (解绑 CPU 0 版)

:: 自动请求管理员权限
fltmc >nul 2>&1 || (
    powershell Start-Process -FilePath """%~f0""" -Verb RunAs
    exit /b
)

:: 清屏并执行 UI 与优化逻辑
cls
powershell -NoProfile -Command "$ErrorActionPreference='SilentlyContinue'; Write-Host ''; Write-Host '  ╔════════════════════════════════════════╗' -ForegroundColor Cyan; Write-Host '  ║       SGuard Limiter Max (解绑版)      ║' -ForegroundColor Cyan; Write-Host '  ╚════════════════════════════════════════╝' -ForegroundColor Cyan; Write-Host ''; Write-Host '  [*] 任务1: SGuard 降权为[低]并绑定尾核' -ForegroundColor DarkGray; Write-Host '  [*] 任务2: 游戏提权为[高]并解绑 CPU 0' -ForegroundColor DarkGray; Write-Host '  [*] 状态: 正在执行...' -ForegroundColor Yellow; Write-Host ''; $mask=[IntPtr](1 -shl ([Environment]::ProcessorCount - 1)); $f1=0; Get-Process 'SGuard64','SGuardSvc64' | ForEach-Object { $p=$_; $f1=1; try { $p.PriorityClass='Idle'; $p.ProcessorAffinity=$mask; Write-Host '      [ √ ] SGuard 优化成功 -> ' -ForegroundColor Green -NoNewline; Write-Host $p.ProcessName -ForegroundColor White } catch { Write-Host '      [ x ] SGuard 优化失败 -> ' -ForegroundColor Red -NoNewline; Write-Host \"$($p.ProcessName) (底层驱动保护拦截)\" -ForegroundColor Gray } }; if($f1 -eq 0){ Write-Host '      [ - ] 未检测到 SGuard 进程运行' -ForegroundColor DarkGray }; Write-Host ''; $f2=0; Get-Process 'VALORANT-Win64-Shipping','DeltaForceClient-Win64-Shipping' | ForEach-Object { $p=$_; $f2=1; try { $p.PriorityClass='High'; $p.ProcessorAffinity=[IntPtr]($p.ProcessorAffinity.ToInt64() -band -2); Write-Host '      [ √ ] 游戏优化成功 -> ' -ForegroundColor Green -NoNewline; Write-Host $p.ProcessName -ForegroundColor White } catch { Write-Host '      [ x ] 游戏优化失败 -> ' -ForegroundColor Red -NoNewline; Write-Host \"$($p.ProcessName) (权限/反作弊受限)\" -ForegroundColor Gray } }; if($f2 -eq 0){ Write-Host '      [ - ] 未检测到目标游戏进程' -ForegroundColor DarkGray }; Write-Host ''; Write-Host '  ==========================================' -ForegroundColor Cyan"

echo.
echo  操作完成，按任意键关闭窗口...
pause >nul