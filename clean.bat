@echo off
title Marketplace - Clean
echo ATTENTION: Ceci va supprimer TOUS les conteneurs ET volumes (donnees perdues)
set /p confirm="Confirmer ? (oui/non): "
if /i "%confirm%"=="oui" (
    docker-compose down -v --rmi local
    echo [OK] Nettoyage termine.
) else (
    echo Annule.
)
pause
