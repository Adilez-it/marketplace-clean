@echo off
title Marketplace — Tests Unitaires
color 0B

echo.
echo  =====================================================
echo   MARKETPLACE — TESTS UNITAIRES
echo  =====================================================
echo.

:: Clean stale obj folders from old test locations to prevent conflicts
if exist "Product.API\Tests\obj" rmdir /s /q "Product.API\Tests\obj"
if exist "Order.API\Tests\obj"   rmdir /s /q "Order.API\Tests\obj"
if exist "Recommendation.API\Tests\obj" rmdir /s /q "Recommendation.API\Tests\obj"

set FAILED=0
set PASS=0
set TOTAL=0

echo [1/3] Product.API Tests...
dotnet test Tests\Product.API.Tests\Product.API.Tests.csproj -v minimal 2>&1
if %errorlevel% neq 0 ( set FAILED=1 ) else ( set /a PASS+=1 )
set /a TOTAL+=1

echo.
echo [2/3] Order.API Tests...
dotnet test Tests\Order.API.Tests\Order.API.Tests.csproj -v minimal 2>&1
if %errorlevel% neq 0 ( set FAILED=1 ) else ( set /a PASS+=1 )
set /a TOTAL+=1

echo.
echo [3/3] Recommendation.API Tests...
dotnet test Tests\Recommendation.API.Tests\Recommendation.API.Tests.csproj -v minimal 2>&1
if %errorlevel% neq 0 ( set FAILED=1 ) else ( set /a PASS+=1 )
set /a TOTAL+=1

echo.
echo  =====================================================
if %FAILED%==0 (
    color 0A
    echo   RESULTAT : TOUS LES TESTS PASSES ^(%PASS%/%TOTAL%^)
) else (
    color 0C
    echo   RESULTAT : %PASS%/%TOTAL% suites passees - ECHECS DETECTES
)
echo  =====================================================
echo.
pause
