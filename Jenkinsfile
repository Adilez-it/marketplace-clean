pipeline {
    agent any

    environment {
        JENKINS_PORT   = '7070'
        NGROK_REGION   = 'eu'
        SONAR_HOST_URL = 'http://172.18.0.10:9000'
        // Token dans Jenkins Credentials (ID: sonar-token-id)
        // Jenkins > Manage > Credentials > Global > Add: Secret text
        // Secret: squ_f2e70195d0c3235c6c65a373d3edd54f5976f648
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

        // ─── 3. Build + Tests en parallèle ───────────────────────────
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

        // ─── 4. Tests unitaires avec rapport TRX ─────────────────────
        stage('Unit Tests') {
            steps {
                dir('D:/marketplace-clean') {
                    bat '''
                        dotnet test Tests\Product.API.Tests\Product.API.Tests.csproj ^
                            --configuration Release ^
                            --collect:"XPlat Code Coverage" ^
                            --results-directory TestResults\Product ^
                            --logger "trx;LogFileName=product-results.trx"

                        dotnet test Tests\Order.API.Tests\Order.API.Tests.csproj ^
                            --configuration Release ^
                            --collect:"XPlat Code Coverage" ^
                            --results-directory TestResults\Order ^
                            --logger "trx;LogFileName=order-results.trx"

                        dotnet test Tests\Recommendation.API.Tests\Recommendation.API.Tests.csproj ^
                            --configuration Release ^
                            --collect:"XPlat Code Coverage" ^
                            --results-directory TestResults\Recommendation ^
                            --logger "trx;LogFileName=recommendation-results.trx"
                    '''
                }
            }
            post {
                always {
                    junit allowEmptyResults: true,
                          testResults: 'D:/marketplace-clean/**/TestResults/**/*.trx'
                }
            }
        }

        // ─── 5. SonarQube Analysis (.NET Scanner) ─────────────────────
        //
        //  PRÉ-REQUIS (une seule fois sur la machine Jenkins) :
        //  ─────────────────────────────────────────────────────
        //  1) Installer le scanner .NET globalement :
        //       dotnet tool install --global dotnet-sonarscanner
        //
        //  2) Ajouter le credential dans Jenkins :
        //       Manage Jenkins > Credentials > Global > Add Credentials
        //       Kind    : Secret text
        //       ID      : sonar-token-id
        //       Secret  : squ_f2e70195d0c3235c6c65a373d3edd54f5976f648
        //
        //  3) Configurer le serveur SonarQube dans Jenkins :
        //       Manage Jenkins > Configure System > SonarQube servers
        //       Name             : SonarQube Local
        //       Server URL       : http://172.18.0.10:9000
        //       Server auth token: sonar-token-id
        //
        stage('SonarQube Analysis') {
            environment {
                SONAR_TOKEN = credentials('sonar-token-id')
            }
            steps {
                withSonarQubeEnv('SonarQube Local') {
                    dir('D:/marketplace-clean') {

                        // -- BEGIN : démarre l'analyse SonarQube
                        bat """
                            dotnet sonarscanner begin ^
                                /k:"marketplace" ^
                                /n:"Marketplace Microservices" ^
                                /v:"1.0" ^
                                /d:sonar.host.url=%SONAR_HOST_URL% ^
                                /d:sonar.token=%SONAR_TOKEN% ^
                                /d:sonar.cs.opencover.reportsPaths=**\\TestResults\\**\\coverage.opencover.xml ^
                                /d:sonar.exclusions=**\\bin\\**,**\\obj\\**,**\\Migrations\\** ^
                                /d:sonar.coverage.exclusions=**\\Tests\\**,**\\Program.cs ^
                                /d:sonar.test.inclusions=**\\Tests\\** ^
                                /d:sonar.sourceEncoding=UTF-8
                        """

                        // -- BUILD : build complet pour que SonarQube analyse les sources
                        bat 'dotnet build --configuration Release'

                        // -- TESTS avec couverture OpenCover pour SonarQube
                        bat '''
                            dotnet test Tests\Product.API.Tests\Product.API.Tests.csproj ^
                                --configuration Release ^
                                --no-build ^
                                /p:CollectCoverage=true ^
                                /p:CoverletOutputFormat=opencover ^
                                /p:CoverletOutput=TestResults\Product\coverage.opencover.xml

                            dotnet test Tests\Order.API.Tests\Order.API.Tests.csproj ^
                                --configuration Release ^
                                --no-build ^
                                /p:CollectCoverage=true ^
                                /p:CoverletOutputFormat=opencover ^
                                /p:CoverletOutput=TestResults\Order\coverage.opencover.xml

                            dotnet test Tests\Recommendation.API.Tests\Recommendation.API.Tests.csproj ^
                                --configuration Release ^
                                --no-build ^
                                /p:CollectCoverage=true ^
                                /p:CoverletOutputFormat=opencover ^
                                /p:CoverletOutput=TestResults\Recommendation\coverage.opencover.xml
                        '''

                        // -- END : envoie les résultats au serveur SonarQube
                        bat """
                            dotnet sonarscanner end ^
                                /d:sonar.token=%SONAR_TOKEN%
                        """
                    }
                }
            }
        }

        // ─── 6. Quality Gate ──────────────────────────────────────────
        stage('Quality Gate') {
            steps {
                script {
                    echo '⏳ Attente du résultat Quality Gate SonarQube...'
                    // Attend max 5 minutes le webhook de SonarQube vers Jenkins
                    def qg = waitForQualityGate abortPipeline: false
                    if (qg.status != 'OK') {
                        echo "⚠️ Quality Gate : ${qg.status}"
                        echo "🔍 Détails : ${SONAR_HOST_URL}/dashboard?id=marketplace"
                        // Décommenter pour bloquer le pipeline si Quality Gate échoue :
                        // error("Quality Gate FAILED: ${qg.status}")
                    } else {
                        echo "✅ Quality Gate : PASSED ✓"
                    }
                }
            }
        }

        // ─── 7. Docker Build ──────────────────────────────────────────
        stage('Docker Build') {
            steps {
                dir('D:/marketplace-clean') {
                    bat '''
                        docker build -t product-api:latest        ./Product.API
                        docker build -t order-api:latest          ./Order.API
                        docker build -t recommendation-api:latest ./Recommendation.API
                        docker build -t apigateway:latest         ./ApiGateway
                    '''
                }
            }
        }

        // ─── 8. Deploy ────────────────────────────────────────────────
        stage('Deploy') {
            steps {
                dir('D:/marketplace-clean') {
                    bat '''
                        docker-compose down
                        docker-compose up -d
                    '''
                }
            }
        }

        // ─── 9. Health Check ──────────────────────────────────────────
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
                            def tempFile = "health_${service.name.replace(' ', '_')}.txt"
                            bat """
                                curl -s -o nul -w "%%{http_code}" --connect-timeout 5 --max-time 10 ${service.url} > ${tempFile} 2>&1
                            """
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
                            echo "✅ Tous les services sont opérationnels !"
                            break
                        } else if (i < maxRetries - 1) {
                            echo "Nouvelle tentative dans 10 secondes..."
                            bat 'ping 127.0.0.1 -n 10 > nul'
                        }
                    }

                    if (!allHealthy) {
                        dir('D:/marketplace-clean') {
                            bat '''
                                echo === LOGS SERVICES ===
                                docker-compose logs --tail=30 apigateway
                                docker-compose logs --tail=30 product.api
                                docker-compose logs --tail=30 order.api
                                docker-compose logs --tail=30 recommendation.api
                                docker-compose ps
                            '''
                        }
                        error("❌ Health check échoué — certains services ne répondent pas")
                    }
                }
            }
        }

        // ─── 10. Résumé final ─────────────────────────────────────────
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