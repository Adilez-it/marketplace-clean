@echo off
title Marketplace - Status
echo.
echo === Etat des conteneurs ===
docker-compose ps
echo.
echo === Health checks ===
curl -s -o nul -w "Product API  (8001): %%{http_code}\n" http://localhost:8001/health
curl -s -o nul -w "Order API    (8004): %%{http_code}\n" http://localhost:8004/health
curl -s -o nul -w "Recomm. API  (8005): %%{http_code}\n" http://localhost:8005/health
curl -s -o nul -w "API Gateway  (8000): %%{http_code}\n" http://localhost:8000/health
echo.
pause
