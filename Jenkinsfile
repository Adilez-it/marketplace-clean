pipeline {
    agent any

    environment {
        JENKINS_PORT   = '7070'
        NGROK_REGION   = 'eu'
        SONAR_HOST_URL = 'http://localhost:9000'
        // Chemin absolu : Jenkins tourne sous SYSTEM, pas sous l'user adile
        // %USERPROFILE% pointe vers C:\windows\system32\config\systemprofile depuis Jenkins
        SONAR_SCANNER  = 'C:\\Users\\adile\\.dotnet\\tools\\dotnet-sonarscanner.exe'
    }

    stages {

        stage('Checkout') {
            steps {
                dir('D:/marketplace-clean') {
                    echo 'Using local code from D:/marketplace-clean'
                }
            }
        }

        stage('Setup Webhook URL') {
            steps {
                script {
                    echo 'Demarrage du tunnel ngrok...'
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
                        echo "Webhook URL : ${env.WEBHOOK_URL}"
                    } else {
                        echo "ngrok indisponible"
                        env.WEBHOOK_URL = "http://localhost:${JENKINS_PORT}"
                    }
                }
            }
        }

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

        stage('Install SonarScanner') {
            steps {
                script {
                    def installResult = bat(
                        script: 'dotnet tool install --global dotnet-sonarscanner',
                        returnStatus: true
                    )
                    if (installResult != 0) {
                        bat 'dotnet tool update --global dotnet-sonarscanner || echo Already up to date'
                    }
                    echo 'dotnet-sonarscanner OK'
                }
            }
        }

        stage('Start SonarQube') {
            steps {
                dir('D:/marketplace-clean') {
                    bat 'docker-compose up -d sonarqube'
                    bat 'ping 127.0.0.1 -n 15 > nul'
                    script {
                        def sonarReady = false
                        for (int i = 0; i < 12; i++) {
                            def status = bat(
                                script: 'curl -s -o nul -w "%%{http_code}" http://localhost:9000 || echo 0',
                                returnStdout: true
                            ).trim().replaceAll('[^0-9]', '')
                            if (status == '200' || status == '302') {
                                sonarReady = true
                                echo 'SonarQube est pret !'
                                break
                            }
                            echo "SonarQube pas encore pret (tentative ${i+1}/12)..."
                            bat 'ping 127.0.0.1 -n 6 > nul'
                        }
                        if (!sonarReady) {
                            echo 'SonarQube ne repond pas, mais on continue...'
                        }
                    }
                }
            }
        }

        // ---------------------------------------------------------------
        // SonarQube Analysis + Quality Gate dans le MEME withSonarQubeEnv
        //
        // POURQUOI :
        //   waitForQualityGate lit le task ID depuis report-task.txt
        //   Ce fichier est genere par "sonarscanner end" dans D:\marketplace-clean\.sonarqube\out\
        //   Jenkins le cherche dans %WORKSPACE% = C:\ProgramData\Jenkins\.jenkins\workspace\Marketplace
        //   => On copie report-task.txt dans %WORKSPACE% apres le end
        //   => waitForQualityGate doit rester dans withSonarQubeEnv pour avoir les variables injectees
        // ---------------------------------------------------------------
        stage('SonarQube Analysis + Quality Gate') {
    environment {
        SONAR_TOKEN = credentials('sonar-token-id')
    }
    steps {
        withSonarQubeEnv('SonarQube Local') {
            script {
                dir('D:/marketplace-clean') {

                    // Vérifie que le scanner existe
                    bat 'if not exist "%SONAR_SCANNER%" (echo ERREUR: sonarscanner introuvable && exit 1)'

                    // BEGIN
                    bat "\"%SONAR_SCANNER%\" begin /k:\"marketplace\" /n:\"Marketplace Microservices\" /v:\"1.0\" /d:sonar.host.url=%SONAR_HOST_URL% /d:sonar.token=%SONAR_TOKEN% /d:sonar.cs.opencover.reportsPaths=**/TestResults/**/coverage.opencover.xml /d:sonar.exclusions=**/bin/**,**/obj/**,**/Migrations/** /d:sonar.coverage.exclusions=**/Tests/**,**/Program.cs /d:sonar.sourceEncoding=UTF-8"

                    // BUILD
                    bat 'dotnet build Product.API/Product.API.csproj --configuration Release'
                    bat 'dotnet build Order.API/Order.API.csproj --configuration Release'
                    bat 'dotnet build Recommendation.API/Recommendation.API.csproj --configuration Release'
                    bat 'dotnet build ApiGateway/ApiGateway.csproj --configuration Release'

                    // TESTS
                    bat 'dotnet test Tests/Product.API.Tests/Product.API.Tests.csproj --configuration Release --no-build /p:CollectCoverage=true /p:CoverletOutputFormat=opencover /p:CoverletOutput=TestResults/Product/coverage.opencover.xml'
                    bat 'dotnet test Tests/Order.API.Tests/Order.API.Tests.csproj --configuration Release --no-build /p:CollectCoverage=true /p:CoverletOutputFormat=opencover /p:CoverletOutput=TestResults/Order/coverage.opencover.xml'
                    bat 'dotnet test Tests/Recommendation.API.Tests/Recommendation.API.Tests.csproj --configuration Release --no-build /p:CollectCoverage=true /p:CoverletOutputFormat=opencover /p:CoverletOutput=TestResults/Recommendation/coverage.opencover.xml'

                    // END
                    bat "\"%SONAR_SCANNER%\" end /d:sonar.token=%SONAR_TOKEN%"

                    // ✅ Copie report-task.txt du bon endroit (maintenant qu'on connaît le chemin exact)
                    echo '📋 Copie de report-task.txt vers le workspace Jenkins...'
                    bat '''
                        copy /Y "D:\\marketplace-clean\\.sonarqube\\out\\.sonar\\report-task.txt" "%WORKSPACE%\\report-task.txt"
                        echo ✅ Fichier copié avec succès
                        echo 📄 Contenu du fichier:
                        type "%WORKSPACE%\\report-task.txt"
                    '''
                }

                // waitForQualityGate dans withSonarQubeEnv
                echo '⏳ Attente du Quality Gate SonarQube...'
                timeout(time: 5, unit: 'MINUTES') {
                    def qg = waitForQualityGate abortPipeline: false
                    if (qg.status != 'OK') {
                        echo "⚠️ Quality Gate : ${qg.status} - voir ${SONAR_HOST_URL}/dashboard?id=marketplace"
                        // Décommenter pour bloquer le pipeline si qualité insuffisante
                        // error "Quality Gate FAILED: ${qg.status}"
                    } else {
                        echo '✅ Quality Gate : PASSED'
                    }
                }
            }
        }
    }
}

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

        stage('Deploy') {
            steps {
                dir('D:/marketplace-clean') {
                    bat 'docker-compose down'
                    bat 'docker-compose up -d'
                }
            }
        }

        stage('Health Check') {
            steps {
                script {
                    bat 'ping 127.0.0.1 -n 20 > nul'

                    def services = [
                        [name: 'API_Gateway',       url: 'http://localhost:8000/health'],
                        [name: 'Product_API',        url: 'http://localhost:8001/health'],
                        [name: 'Order_API',          url: 'http://localhost:8004/health'],
                        [name: 'Recommendation_API', url: 'http://localhost:8005/health'],
                    ]

                    def maxRetries = 12
                    def allHealthy = false

                    for (int i = 0; i < maxRetries; i++) {
                        echo "Tentative ${i+1}/${maxRetries}..."
                        allHealthy = true
                        for (service in services) {
                            def tempFile = "health_${service.name}.txt"
                            bat "curl -s -o nul -w \"%%{http_code}\" --connect-timeout 5 --max-time 10 ${service.url} > ${tempFile} 2>&1"
                            def result = readFile(file: tempFile).trim().replaceAll('[^0-9]', '')
                            bat "del ${tempFile} 2>nul || exit 0"
                            if (result == '200') {
                                echo "${service.name} - OK (HTTP 200)"
                            } else {
                                echo "${service.name} - HTTP ${result ?: 'no response'}"
                                allHealthy = false
                            }
                        }
                        if (allHealthy) {
                            echo 'Tous les services sont operationnels !'
                            break
                        } else if (i < maxRetries - 1) {
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
                        error('Health check echoue - certains services ne repondent pas')
                    }
                }
            }
        }

        stage('Display Info') {
            when { expression { env.WEBHOOK_URL != null } }
            steps {
                script {
                    echo "Webhook Jenkins   : ${env.WEBHOOK_URL}"
                    echo "GitHub Webhook    : ${env.WEBHOOK_URL}/github-webhook/"
                    echo "SonarQube         : ${SONAR_HOST_URL}/dashboard?id=marketplace"
                    echo "API Gateway       : http://localhost:8000"
                    echo "Product API       : http://localhost:8001"
                    echo "Order API         : http://localhost:8004"
                    echo "Recommendation    : http://localhost:8005"
                    echo "Mongo Express (P) : http://localhost:8081"
                    echo "Mongo Express (O) : http://localhost:8082"
                    echo "RabbitMQ          : http://localhost:15672"
                    echo "Neo4j             : http://localhost:7474"
                    echo "SonarQube         : http://localhost:9000"
                }
            }
        }
    }

    post {
        success {
            echo 'Pipeline termine avec succes !'
        }
        failure {
            echo 'Pipeline echoue - consultez les logs.'
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