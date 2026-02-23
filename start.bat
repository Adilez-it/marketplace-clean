@echo off
title Marketplace Microservices
color 0A

echo.
echo  ╔══════════════════════════════════════╗
echo  ║     MARKETPLACE MICROSERVICES        ║
echo  ╚══════════════════════════════════════╝
echo.

:: Check Docker
docker --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERREUR] Docker n'est pas installe ou pas demarre !
    echo Lancez Docker Desktop d'abord.
    pause
    exit /b 1
)
echo [OK] Docker detecte

:: STEP 1 - Build
echo.
echo [1/4] Build des images Docker...
echo       (3-5 min au premier lancement)
docker-compose build --parallel
if %errorlevel% neq 0 (
    echo [ERREUR] Build echoue. Verifiez les Dockerfiles.
    pause
    exit /b 1
)
echo [OK] Images buildees

:: STEP 2 - Start infrastructure
echo.
echo [2/4] Demarrage bases de donnees + RabbitMQ...
docker-compose up -d productdb orderdb neo4j redis rabbitmq
echo [OK] Infrastructure demarree

:: STEP 3 - Wait
echo.
echo [3/4] Attente des healthchecks (40 secondes)...
echo       MongoDB, Neo4j, RabbitMQ...
timeout /t 40 /nobreak >nul
echo [OK] Attente terminee

:: STEP 4 - Start services
echo.
echo [4/4] Demarrage des microservices...
docker-compose up -d product.api order.api recommendation.api apigateway
echo [OK] Microservices demarres

timeout /t 10 /nobreak >nul

:: Summary
echo.
echo  ╔══════════════════════════════════════════════════════════╗
echo  ║              MARKETPLACE EN LIGNE !                      ║
echo  ╠══════════════════════════════════════════════════════════╣
echo  ║                                                          ║
echo  ║  API Gateway       http://localhost:8000                 ║
echo  ║  Product Swagger   http://localhost:8001/swagger         ║
echo  ║  Order Swagger     http://localhost:8004/swagger         ║
echo  ║  Recomm. Swagger   http://localhost:8005/swagger         ║
echo  ║                                                          ║
echo  ║  RabbitMQ UI       http://localhost:15672                ║
echo  ║                    (guest / guest)                       ║
echo  ║  Neo4j Browser     http://localhost:7474                 ║
echo  ║                    (neo4j / password123)                 ║
echo  ║  Portainer         http://localhost:9443                 ║
echo  ║                                                          ║
echo  ╠══════════════════════════════════════════════════════════╣
echo  ║  stop.bat   -> arreter tous les services                 ║
echo  ║  logs.bat   -> voir les logs                             ║
echo  ║  status.bat -> voir l'etat des conteneurs                ║
echo  ╚══════════════════════════════════════════════════════════╝
echo.
pause
