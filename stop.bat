@echo off
title Marketplace - Stop
echo Arret de tous les services...
docker-compose down
echo [OK] Tous les services sont arretes.
pause
