@echo off
title Marketplace - Logs
echo Affichage des logs (Ctrl+C pour quitter)...
docker-compose logs -f --tail=50
