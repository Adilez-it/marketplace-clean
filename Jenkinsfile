pipeline {
    agent any

    environment {
        JENKINS_PORT   = '7070'
        NGROK_REGION   = 'eu'
        SONAR_HOST_URL = 'http://localhost:9000'
    }

    stages {

        // ─── 1. Checkout ──────────────────────────────────────────────
        stage('Checkout') {
            steps {
                dir('D:/marketplace-clean') {
                    echo '📦 Using local code from D:/marketplace-clean'
                }
            }
        }

        // ─── 2. Ngrok Webhook ─────────────────────────────────────────
        stage('Setup Webhook URL') {
            steps {
                script {
                    echo '🚀 Démarrage du tunnel ngrok...'
                    bat 'ngrok kill || exit 0'
                    sleep(5)
                    bat """
                        start /B cmd /c "ngrok http ${JENKINS_PORT} --region=${NGROK_REGION} --log=stdout > ngrok_jenkins.log 2>&1"
                    """
                    sleep(10)
                    def ngrokUrl = powershell(
                        script: '''
                            try {
                                $json = Invoke-RestMethod -Uri "http://localhost:4040/api/tunnels" -ErrorAction Stop
                                if ($json.tunnels.Count -gt 0) { $json.tunnels[0].public_url } else { "" }
                            } catch { "" }
                        ''',
                        returnStdout: true
                    ).trim()

                    if (ngrokUrl) {
                        env.WEBHOOK_URL = ngrokUrl
                        echo "✅ Webhook URL : ${env.WEBHOOK_URL}"
                    } else {
                        echo "⚠️ ngrok indisponible"
                        env.WEBHOOK_URL = "http://localhost:${JENKINS_PORT}"
                    }
                }
            }
        }

        // ─── 3. Build en parallèle ────────────────────────────────────
        stage('Build Microservices') {
            parallel {
                stage('Product API') {
                    steps {
                        dir('D:/marketplace-clean/Product.API') {
                            bat 'dotnet restore'
                            bat 'dotnet build --configuration Release --no-restore'
                        }
                    }
                }
                stage('Order API') {
                    steps {
                        dir('D:/marketplace-clean/Order.API') {
                            bat 'dotnet restore'
                            bat 'dotnet build --configuration Release --no-restore'
                        }
                    }
                }
                stage('Recommendation API') {
                    steps {
                        dir('D:/marketplace-clean/Recommendation.API') {
                            bat 'dotnet restore'
                            bat 'dotnet build --configuration Release --no-restore'
                        }
                    }
                }
                stage('ApiGateway') {
                    steps {
                        dir('D:/marketplace-clean/ApiGateway') {
                            bat 'dotnet restore'
                            bat 'dotnet build --configuration Release --no-restore'
                        }
                    }
                }
            }
        }

        // ─── 4. Unit Tests ────────────────────────────────────────────
        stage('Unit Tests') {
            steps {
                dir('D:/marketplace-clean') {
                    bat 'dotnet test Tests/Product.API.Tests/Product.API.Tests.csproj --configuration Release --collect:"XPlat Code Coverage" --results-directory TestResults/Product --logger "trx;LogFileName=product-results.trx"'
                    bat 'dotnet test Tests/Order.API.Tests/Order.API.Tests.csproj --configuration Release --collect:"XPlat Code Coverage" --results-directory TestResults/Order --logger "trx;LogFileName=order-results.trx"'
                    bat 'dotnet test Tests/Recommendation.API.Tests/Recommendation.API.Tests.csproj --configuration Release --collect:"XPlat Code Coverage" --results-directory TestResults/Recommendation --logger "trx;LogFileName=recommendation-results.trx"'
                }
            }
            post {
                always {
                    junit allowEmptyResults: true,
                          testResults: 'D:/marketplace-clean/**/TestResults/**/*.trx'
                }
            }
        }

        // ─── 5. Install SonarScanner ──────────────────────────────────
        stage('Install SonarScanner') {
            steps {
                script {
                    def installResult = bat(
                        script: 'dotnet tool install --global dotnet-sonarscanner',
                        returnStatus: true
                    )
                    if (installResult != 0) {
                        echo '⚠️ Installation échouée ou déjà installé — tentative de mise à jour...'
                        bat 'dotnet tool update --global dotnet-sonarscanner || echo Already up to date'
                    } else {
                        echo '✅ dotnet-sonarscanner installé avec succès'
                    }

                    def checkResult = bat(
                        script: 'dotnet sonarscanner --version',
                        returnStatus: true
                    )
                    if (checkResult != 0) {
                        echo '🔧 sonarscanner non trouvé dans PATH — ajout manuel...'
                        powershell '''
                            $toolsPath = "$env:USERPROFILE\\.dotnet\\tools"
                            $currentPath = [System.Environment]::GetEnvironmentVariable("PATH", "Machine")
                            if ($currentPath -notlike "*$toolsPath*") {
                                [System.Environment]::SetEnvironmentVariable(
                                    "PATH",
                                    "$toolsPath;$currentPath",
                                    "Machine"
                                )
                                Write-Host "✅ PATH mis à jour : $toolsPath ajouté"
                            } else {
                                Write-Host "✅ PATH déjà configuré"
                            }
                        '''
                    } else {
                        echo '✅ dotnet-sonarscanner accessible dans le PATH'
                    }
                }
            }
        }

        // ─── 5.5 Start SonarQube ─────────────────────────────────────
        stage('Start SonarQube') {
            steps {
                dir('D:/marketplace-clean') {
                    bat 'docker-compose up -d sonarqube'
                    echo '⏳ Attente que SonarQube soit prêt...'
                    
                    bat 'ping 127.0.0.1 -n 15 > nul'
                    
                    script {
                        def sonarReady = false
                        def maxRetries = 12
                        
                        for (int i = 0; i < maxRetries; i++) {
                            def status = bat(
                                script: 'curl -s -o nul -w "%%{http_code}" http://localhost:9000 || echo 0',
                                returnStdout: true
                            ).trim()
                            
                            if (status == '200' || status == '302') {
                                sonarReady = true
                                echo '✅ SonarQube est prêt !'
                                break
                            }
                            
                            echo "⏳ SonarQube pas encore prêt (tentative ${i+1}/${maxRetries}), nouvelle tentative dans 5 secondes..."
                            bat 'ping 127.0.0.1 -n 6 > nul'
                        }
                        
                        if (!sonarReady) {
                            echo '⚠️ SonarQube ne répond pas après plusieurs tentatives, mais on continue...'
                        }
                    }
                }
            }
        }

// ─── 6. SonarQube Analysis ────────────────────────────────────
stage('SonarQube Analysis') {
    environment {
        SONAR_TOKEN = credentials('sonar-token-id')
    }
    steps {
        dir('D:/marketplace-clean') {
            script {
                // Chemin absolu vers le scanner (fixe pour votre utilisateur)
                def scannerPath = 'C:\\Users\\adile\\.dotnet\\tools\\dotnet-sonarscanner.exe'
                
                // Vérifier que le fichier existe
                def fileExists = bat(
                    script: "if exist \"${scannerPath}\" (exit 0) else (exit 1)",
                    returnStatus: true
                )
                
                if (fileExists != 0) {
                    error "❌ Scanner introuvable à : ${scannerPath}"
                }
                
                echo "🔍 SonarScanner path : ${scannerPath}"

                // BEGIN
                bat """
                    "${scannerPath}" begin ^
                        /k:"marketplace" ^
                        /n:"Marketplace Microservices" ^
                        /v:"1.0" ^
                        /d:sonar.host.url=${SONAR_HOST_URL} ^
                        /d:sonar.token=%SONAR_TOKEN% ^
                        /d:sonar.cs.opencover.reportsPaths="**/TestResults/**/coverage.opencover.xml" ^
                        /d:sonar.exclusions="**/bin/**,**/obj/**,**/Migrations/**" ^
                        /d:sonar.coverage.exclusions="**/Tests/**,**/Program.cs" ^
                        /d:sonar.sourceEncoding=UTF-8
                """

                // BUILD - Chaque projet individuellement
                echo '🏗️ Building Product.API...'
                bat 'dotnet build Product.API/Product.API.csproj --configuration Release'
                
                echo '🏗️ Building Order.API...'
                bat 'dotnet build Order.API/Order.API.csproj --configuration Release'
                
                echo '🏗️ Building Recommendation.API...'
                bat 'dotnet build Recommendation.API/Recommendation.API.csproj --configuration Release'
                
                echo '🏗️ Building ApiGateway...'
                bat 'dotnet build ApiGateway/ApiGateway.csproj --configuration Release'

                // TESTS avec couverture
                echo '🧪 Running Product.API.Tests...'
                bat 'dotnet test Tests/Product.API.Tests/Product.API.Tests.csproj --configuration Release --no-build /p:CollectCoverage=true /p:CoverletOutputFormat=opencover /p:CoverletOutput=TestResults/Product/coverage.opencover.xml'
                
                echo '🧪 Running Order.API.Tests...'
                bat 'dotnet test Tests/Order.API.Tests/Order.API.Tests.csproj --configuration Release --no-build /p:CollectCoverage=true /p:CoverletOutputFormat=opencover /p:CoverletOutput=TestResults/Order/coverage.opencover.xml'
                
                echo '🧪 Running Recommendation.API.Tests...'
                bat 'dotnet test Tests/Recommendation.API.Tests/Recommendation.API.Tests.csproj --configuration Release --no-build /p:CollectCoverage=true /p:CoverletOutputFormat=opencover /p:CoverletOutput=TestResults/Recommendation/coverage.opencover.xml'

                // END
                echo '📤 Sending results to SonarQube...'
                bat "${scannerPath} end /d:sonar.token=%SONAR_TOKEN%"
            }
        }
    }
}

        // ─── 7. Quality Gate ──────────────────────────────────────────
        stage('Quality Gate') {
            steps {
                script {
                    echo '⏳ Attente Quality Gate SonarQube...'
                    def qg = waitForQualityGate abortPipeline: false
                    if (qg.status != 'OK') {
                        echo "⚠️ Quality Gate : ${qg.status} — voir ${SONAR_HOST_URL}/dashboard?id=marketplace"
                    } else {
                        echo '✅ Quality Gate : PASSED'
                    }
                }
            }
        }

        // ─── 8. Docker Build ──────────────────────────────────────────
        stage('Docker Build') {
            steps {
                dir('D:/marketplace-clean') {
                    bat 'docker build -t product-api:latest ./Product.API'
                    bat 'docker build -t order-api:latest ./Order.API'
                    bat 'docker build -t recommendation-api:latest ./Recommendation.API'
                    bat 'docker build -t apigateway:latest ./ApiGateway'
                }
            }
        }

        // ─── 9. Deploy ────────────────────────────────────────────────
        stage('Deploy') {
            steps {
                dir('D:/marketplace-clean') {
                    bat 'docker-compose down'
                    bat 'docker-compose up -d'
                }
            }
        }

        // ─── 10. Health Check ─────────────────────────────────────────
        stage('Health Check') {
            steps {
                script {
                    bat 'ping 127.0.0.1 -n 20 > nul'

                    def services = [
                        [name: 'API Gateway',       url: 'http://localhost:8000/health'],
                        [name: 'Product API',        url: 'http://localhost:8001/health'],
                        [name: 'Order API',          url: 'http://localhost:8004/health'],
                        [name: 'Recommendation API', url: 'http://localhost:8005/health'],
                    ]

                    def maxRetries = 12
                    def allHealthy = false

                    for (int i = 0; i < maxRetries; i++) {
                        echo "Tentative ${i+1}/${maxRetries}..."
                        allHealthy = true

                        for (service in services) {
                            def safeName = service.name.replace(' ', '_')
                            def tempFile = "health_${safeName}.txt"
                            bat "curl -s -o nul -w \"%%{http_code}\" --connect-timeout 5 --max-time 10 ${service.url} > ${tempFile} 2>&1"
                            def result = readFile(file: tempFile).trim().replaceAll('[^0-9]', '')
                            bat "del ${tempFile} 2>nul || exit 0"

                            if (result == '200') {
                                echo "✅ ${service.name} — HTTP 200"
                            } else {
                                echo "⚠️ ${service.name} — HTTP ${result ?: 'pas de réponse'}"
                                allHealthy = false
                            }
                        }

                        if (allHealthy) {
                            echo '✅ Tous les services sont opérationnels !'
                            break
                        } else if (i < maxRetries - 1) {
                            echo 'Nouvelle tentative dans 10 secondes...'
                            bat 'ping 127.0.0.1 -n 10 > nul'
                        }
                    }

                    if (!allHealthy) {
                        dir('D:/marketplace-clean') {
                            bat 'docker-compose logs --tail=30 apigateway'
                            bat 'docker-compose logs --tail=30 product.api'
                            bat 'docker-compose logs --tail=30 order.api'
                            bat 'docker-compose logs --tail=30 recommendation.api'
                            bat 'docker-compose ps'
                        }
                        error('❌ Health check échoué — certains services ne répondent pas')
                    }
                }
            }
        }

        // ─── 11. Résumé final ─────────────────────────────────────────
        stage('Display Webhook Info') {
            when { expression { env.WEBHOOK_URL != null } }
            steps {
                script {
                    echo """
╔══════════════════════════════════════════════════════════════╗
║         🌐 WEBHOOK JENKINS PUBLIC — PRÊT À L'EMPLOI         ║
╚══════════════════════════════════════════════════════════════╝

📡 URL Publique Jenkins : ${env.WEBHOOK_URL}
🔗 Webhook GitHub       : ${env.WEBHOOK_URL}/github-webhook/
📊 Interface ngrok      : http://localhost:4040
📈 SonarQube Dashboard  : ${SONAR_HOST_URL}/dashboard?id=marketplace

📝 Services déployés:
   • API Gateway              : http://localhost:8000
   • Product API              : http://localhost:8001
   • Order API                : http://localhost:8004
   • Recommendation API       : http://localhost:8005
   • Mongo Express (Products) : http://localhost:8081
   • Mongo Express (Orders)   : http://localhost:8082
   • RabbitMQ Management      : http://localhost:15672
   • Neo4j Browser            : http://localhost:7474
   • SonarQube                : http://localhost:9000
                    """
                }
            }
        }
    }

    // ─── Post ─────────────────────────────────────────────────────────
    post {
        success {
            echo '✅ Pipeline terminé avec succès !'
        }
        failure {
            echo '❌ Pipeline échoué — consultez les logs ci-dessus.'
            dir('D:/marketplace-clean') {
                bat 'docker-compose ps || exit 0'
            }
        }
        always {
            script {
                if (env.WEBHOOK_URL) {
                    writeFile file: 'last_webhook_url.txt', text: env.WEBHOOK_URL
                }
            }
        }
    }
}