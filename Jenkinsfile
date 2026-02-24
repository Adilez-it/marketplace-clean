pipeline {
    agent any
    
    environment {
        JENKINS_PORT = '7070'
        NGROK_REGION = 'eu'
        SONAR_HOST_URL = 'http://172.18.0.10:9000'
        SONAR_TOKEN = 'squ_f2e70195d0c3235c6c65a373d3edd54f5976f648'
        PATH = "${env.PATH};C:\\Users\\adile\\.dotnet\\tools"
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
                    echo '🚀 Démarrage du tunnel ngrok pour Jenkins...'
                    
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
                                if ($json.tunnels.Count -gt 0) {
                                    $json.tunnels[0].public_url
                                } else {
                                    ""
                                }
                            } catch {
                                ""
                            }
                        ''',
                        returnStdout: true
                    ).trim()
                    
                    if (ngrokUrl) {
                        env.WEBHOOK_URL = ngrokUrl
                        echo "✅ Jenkins Webhook URL: ${env.WEBHOOK_URL}"
                        echo "📌 GitHub Webhook: ${env.WEBHOOK_URL}/github-webhook/"
                    } else {
                        echo "⚠️ Impossible de récupérer l'URL ngrok"
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
                            bat 'dotnet build --configuration Release'
                            bat 'dotnet test --configuration Release --collect:"XPlat Code Coverage"'
                        }
                    }
                }
                stage('Order API') {
                    steps {
                        dir('D:/marketplace-clean/Order.API') {
                            bat 'dotnet restore'
                            bat 'dotnet build --configuration Release'
                            bat 'dotnet test --configuration Release --collect:"XPlat Code Coverage"'
                        }
                    }
                }
                stage('Recommendation API') {
                    steps {
                        dir('D:/marketplace-clean/Recommendation.API') {
                            bat 'dotnet restore'
                            bat 'dotnet build --configuration Release'
                            bat 'dotnet test --configuration Release --collect:"XPlat Code Coverage"'
                        }
                    }
                }
                stage('ApiGateway') {
                    steps {
                        dir('D:/marketplace-clean/ApiGateway') {
                            bat 'dotnet restore'
                            bat 'dotnet build --configuration Release'
                            bat 'dotnet test --configuration Release --collect:"XPlat Code Coverage"'
                        }
                    }
                }
            }
        }
        
        stage('SonarQube Analysis') {
            parallel {
                stage('Product API Analysis') {
                    steps {
                        dir('D:/marketplace-clean/Product.API') {
                            bat """
                                dotnet sonarscanner begin /k:"product-api" /n:"Product API" /v:"1.0" /d:sonar.host.url=${SONAR_HOST_URL} /d:sonar.token=${SONAR_TOKEN}
                                dotnet build --configuration Release
                                dotnet sonarscanner end /d:sonar.token=${SONAR_TOKEN}
                            """
                        }
                    }
                }
                stage('Order API Analysis') {
                    steps {
                        dir('D:/marketplace-clean/Order.API') {
                            bat """
                                dotnet sonarscanner begin /k:"order-api" /n:"Order API" /v:"1.0" /d:sonar.host.url=${SONAR_HOST_URL} /d:sonar.token=${SONAR_TOKEN}
                                dotnet build --configuration Release
                                dotnet sonarscanner end /d:sonar.token=${SONAR_TOKEN}
                            """
                        }
                    }
                }
                stage('Recommendation API Analysis') {
                    steps {
                        dir('D:/marketplace-clean/Recommendation.API') {
                            bat """
                                dotnet sonarscanner begin /k:"recommendation-api" /n:"Recommendation API" /v:"1.0" /d:sonar.host.url=${SONAR_HOST_URL} /d:sonar.token=${SONAR_TOKEN}
                                dotnet build --configuration Release
                                dotnet sonarscanner end /d:sonar.token=${SONAR_TOKEN}
                            """
                        }
                    }
                }
                stage('ApiGateway Analysis') {
                    steps {
                        dir('D:/marketplace-clean/ApiGateway') {
                            bat """
                                dotnet sonarscanner begin /k:"apigateway" /n:"API Gateway" /v:"1.0" /d:sonar.host.url=${SONAR_HOST_URL} /d:sonar.token=${SONAR_TOKEN}
                                dotnet build --configuration Release
                                dotnet sonarscanner end /d:sonar.token=${SONAR_TOKEN}
                            """
                        }
                    }
                }
            }
        }
        
        stage('Quality Gate Check') {
            steps {
                script {
                    sleep(15)
                    
                    def projects = ['product-api', 'order-api', 'recommendation-api', 'apigateway']
                    
                    for (project in projects) {
                        echo "Vérification du Quality Gate pour ${project}..."
                        
                        def qualityGate = bat(
                            script: "curl -s -u ${SONAR_TOKEN}: \"${SONAR_HOST_URL}/api/qualitygates/project_status?projectKey=${project}\"",
                            returnStdout: true
                        ).trim()
                        
                        echo "Résultat: ${qualityGate}"
                        
                        if (qualityGate.contains('"status":"OK"')) {
                            echo "✅ Quality Gate OK pour ${project}"
                        } else {
                            echo "⚠️ Quality Gate non vérifié pour ${project}"
                        }
                    }
                }
            }
        }
        
        stage('Docker Build') {
            steps {
                dir('D:/marketplace-clean') {
                    bat '''
                        docker build -t product-api:latest ./Product.API
                        docker build -t order-api:latest ./Order.API
                        docker build -t recommendation-api:latest ./Recommendation.API
                        docker build -t apigateway:latest ./ApiGateway
                    '''
                }
            }
        }
        
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
        
        stage('Health Check') {
            steps {
                script {
                    bat 'ping 127.0.0.1 -n 30 > nul'
                    
                    def services = [
                        [name: 'API Gateway', url: 'http://localhost:8000/health'],
                        [name: 'Product API', url: 'http://localhost:8001/health'],
                        [name: 'Order API', url: 'http://localhost:8004/health'],
                        [name: 'Recommendation API', url: 'http://localhost:8005/health'],
                        [name: 'SonarQube', url: 'http://localhost:9000']
                    ]
                    
                    def allHealthy = true
                    
                    for (service in services) {
                        def result = bat(
                            script: "powershell -Command \"try { \$r = Invoke-WebRequest -Uri '${service.url}' -Method Head -UseBasicParsing -TimeoutSec 5; if (\$r.StatusCode -eq 200 -or \$r.StatusCode -eq 302) { '200' } else { \$r.StatusCode } } catch { '000' }\"",
                            returnStdout: true
                        ).trim()
                        
                        if (result == '200' || result == '302') {
                            echo "✅ ${service.name} - OK"
                        } else {
                            echo "❌ ${service.name} - Échec (Code: ${result})"
                            allHealthy = false
                        }
                    }
                    
                    if (!allHealthy) {
                        error("Health check failed")
                    }
                }
            }
        }
        
        stage('Display Info') {
            steps {
                script {
                    echo """
╔══════════════════════════════════════════════════════════════╗
║                                                              ║
║        🚀 MARKETPLACE DÉPLOYÉ AVEC SUCCÈS                   ║
║                                                              ║
╠══════════════════════════════════════════════════════════════╣
║                                                              ║
║   📊 SonarQube          : http://localhost:9000             ║
║      • admin / admin (changez le mot de passe)              ║
║                                                              ║
║   🔗 Jenkins Public     : ${env.WEBHOOK_URL}                ║
║   🚀 API Gateway        : http://localhost:8000/swagger     ║
║   📊 Portainer          : http://localhost:8888             ║
║   🐰 RabbitMQ           : http://localhost:15672            ║
║   🗄️ Neo4j Browser      : http://localhost:7474             ║
║                                                              ║
║   📝 Projets analysés :                                     ║
║      • Product API                                          ║
║      • Order API                                            ║
║      • Recommendation API                                   ║
║      • API Gateway                                          ║
║                                                              ║
╚══════════════════════════════════════════════════════════════╝
                    """
                }
            }
        }
    }
    
    post {
        success {
            script {
                echo '✅ Pipeline completed successfully!'
                if (env.WEBHOOK_URL) {
                    writeFile file: 'last_webhook_url.txt', text: env.WEBHOOK_URL
                }
            }
        }
        failure {
            script {
                echo '❌ Pipeline failed! Vérifiez les logs ci-dessus.'
            }
        }
        always {
            script {
                junit '**/TestResults/*.xml'
            }
        }
    }
}