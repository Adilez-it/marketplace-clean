pipeline {
    agent any
    // Variables d'environnement pour la configuration du tunnel ngrok
    environment {
        JENKINS_PORT = '7070'
        NGROK_REGION = 'eu'
        SONAR_HOST_URL = 'http://172.18.0.10:9000'
        SONAR_TOKEN = 'squ_f2e70195d0c3235c6c65a373d3edd54f5976f648'  // Token en dur pour test
    }
    // Options pour améliorer la lisibilité des logs
    stages {
        stage('Checkout') {
            steps {
                dir('D:/marketplace-clean') {
                    echo 'Using local code from D:/marketplace-clean'
                }
            }
        }
        // Étape pour configurer le tunnel ngrok et récupérer l'URL publique
        stage('Setup Webhook URL') {
            steps {
                script {
                    echo '🚀 Démarrage du tunnel ngrok pour Jenkins...'
                    
                    // Tuer les anciens tunnels
                    bat 'ngrok kill || exit 0'
                    sleep(5)
                    
                    // Démarrer ngrok
                    bat """
                        start /B cmd /c "ngrok http ${JENKINS_PORT} --region=${NGROK_REGION} --log=stdout > ngrok_jenkins.log 2>&1"
                    """
                    
                    sleep(10)
                    
                    // Récupérer l'URL
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
        // Étape pour construire et tester les microservices en parallèle
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

        // ====== SONARQUBE ANALYSIS STAGES ======
        // Note: If using .NET projects, you can use either the Scanner for .NET or the generic Scanner CLI
        // Below are two approaches: choose one based on your project structure
        
        // APPROACH 1: Using dotnet-sonarscanner (recommended for .NET projects)
        stage('SonarQube Analysis - .NET') {
            steps {
                script {
                    echo '🔍 Starting SonarQube Analysis for .NET Projects...'
                    dir('D:/marketplace-clean') {
                        // Install SonarQube scanner for .NET if not already installed
                        bat '''
                            dotnet tool update --global dotnet-sonarscanner || dotnet tool install --global dotnet-sonarscanner
                        '''
                        
                        // Begin analysis
                        bat '''
                            dotnet sonarscanner begin /k:"marketplace-clean" /n:"Marketplace Clean" /d:sonar.host.url=http://172.18.0.10:9000 /d:sonar.login=squ_f2e70195d0c3235c6c65a373d3edd54f5976f648 /d:sonar.cs.opencover.reportsPaths="**/coverage.opencover.xml"
                        '''
                        
                        // Build the projects
                        bat 'dotnet build --configuration Release'
                        
                        // End analysis and send results
                        bat '''
                            dotnet sonarscanner end /d:sonar.login=squ_f2e70195d0c3235c6c65a373d3edd54f5976f648
                        '''
                        
                        echo '✅ SonarQube Analysis completed'
                    }
                }
            }
        }

        // APPROACH 2: Using SonarQube Scanner CLI (generic approach)
        // Uncomment this section if you prefer the generic Scanner
        /*
        stage('SonarQube Analysis - Generic Scanner') {
            environment {
                SONAR_SCANNER_HOME = 'C:/sonar-scanner'  // Adjust path as needed
            }
            steps {
                script {
                    echo '🔍 Starting SonarQube Analysis with Scanner CLI...'
                    dir('D:/marketplace-clean') {
                        bat """
                            ${SONAR_SCANNER_HOME}/bin/sonar-scanner.bat ^
                            -Dsonar.projectKey=marketplace-clean ^
                            -Dsonar.projectName="Marketplace Clean" ^
                            -Dsonar.projectVersion=1.0 ^
                            -Dsonar.sources=. ^
                            -Dsonar.exclusions=**/bin/**,**/obj/** ^
                            -Dsonar.host.url=http://172.18.0.10:9000 ^
                            -Dsonar.token=squ_f2e70195d0c3235c6c65a373d3edd54f5976f648
                        """
                        echo '✅ SonarQube Analysis completed'
                    }
                }
            }
        }
        */

        // ====== QUALITY GATE ======
        // Optionally wait for SonarQube Quality Gate results
        stage('SonarQube Quality Gate') {
            steps {
                script {
                    echo '⏳ Waiting for SonarQube Quality Gate...'
                    timeout(time: 5, unit: 'MINUTES') {
                        waitForQualityGate abortPipeline: false
                    }
                }
            }
        }

        // Étape pour construire les images Docker et déployer avec docker-compose
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
        // Étape pour déployer les services avec docker-compose
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
                    // Remplacer timeout par ping (fonctionne dans Jenkins)
                    bat 'ping 127.0.0.1 -n 20 > nul'
                    
                    // Vérifier chaque service avec une approche plus robuste
                    def services = [
                        [name: 'API Gateway', url: 'http://localhost:8000/health', port: 8000],
                        [name: 'Product API', url: 'http://localhost:8001/health', port: 8001],
                        [name: 'Order API', url: 'http://localhost:8004/health', port: 8004],
                        [name: 'Recommendation API', url: 'http://localhost:8005/health', port: 8005]
                    ]
                    
                    // Attendre que tous les services soient prêts (max 2 minutes)
                    def maxRetries = 12
                    def allHealthy = false
                    
                    for (int i = 0; i < maxRetries; i++) {
                        echo "Tentative ${i+1}/${maxRetries} de vérification des services..."
                        allHealthy = true
                        
                        for (service in services) {
                            // Utiliser un fichier temporaire pour capturer proprement la sortie
                            def tempFile = "health_check_${service.name.replace(' ', '_')}.txt"
                            
                            // Exécuter curl et capturer le code HTTP dans un fichier
                            bat """
                                curl -s -o nul -w "%%{http_code}" --connect-timeout 5 --max-time 10 ${service.url} > ${tempFile} 2>&1
                            """
                            
                            // Lire le résultat
                            def result = readFile(file: tempFile).trim()
                            
                            // Nettoyer le résultat (enlever les caractères indésirables)
                            result = result.replaceAll('[^0-9]', '')
                            
                            if (result == '200') {
                                echo "✅ ${service.name} - OK (HTTP 200)"
                            } else if (result == '000' || result == '') {
                                echo "⚠️ ${service.name} - Pas de réponse"
                                allHealthy = false
                            } else {
                                echo "⚠️ ${service.name} - Réponse HTTP ${result}"
                                allHealthy = false
                            }
                            
                            // Nettoyer le fichier temporaire
                            bat "del ${tempFile} 2>nul || exit 0"
                        }
                        
                        if (allHealthy) {
                            echo "✅ Tous les services sont en bonne santé !"
                            break
                        } else {
                            if (i < maxRetries - 1) {
                                echo "Attente de 10 secondes avant nouvelle tentative..."
                                bat 'ping 127.0.0.1 -n 10 > nul'
                            }
                        }
                    }
                    
                    if (!allHealthy) {
                        echo "❌ Certains services ne répondent pas correctement"
                        
                        // Afficher les logs des services problématiques
                        bat '''
                            echo "=== Logs détaillés des services ==="
                            
                            echo "----- API Gateway -----"
                            docker-compose logs --tail=30 apigateway
                            
                            echo "----- Product API -----"
                            docker-compose logs --tail=30 product.api
                            
                            echo "----- Order API -----"
                            docker-compose logs --tail=30 order.api
                            
                            echo "----- Recommendation API -----"
                            docker-compose logs --tail=30 recommendation.api
                            
                            echo "----- Vérification des conteneurs -----"
                            docker-compose ps
                            
                            echo "----- Test direct avec curl verbose -----"
                            curl -v http://localhost:8000/health
                            curl -v http://localhost:8001/health
                        '''
                        
                        error("Health check failed - Les services ne répondent pas correctement")
                    }
                }
            }
        }
        
        stage('Display Webhook Info') {
            when {
                expression { env.WEBHOOK_URL != null }
            }
            steps {
                script {
                    echo """
╔══════════════════════════════════════════════════════════════╗
║                                                              ║
║        🌐 WEBHOOK JENKINS PUBLIC - PRÊT À L'EMPLOI         ║
║                                                              ║
╚══════════════════════════════════════════════════════════════╝

📡 URL Publique Jenkins    : ${env.WEBHOOK_URL}
🔗 Webhook GitHub          : ${env.WEBHOOK_URL}/github-webhook/

📊 Interface ngrok         : http://localhost:4040

⚙️ Configuration GitHub:
   1. Allez dans Settings → Webhooks
   2. Ajoutez: ${env.WEBHOOK_URL}/github-webhook/
   3. Content type: application/json

📝 Les URLs de vos services:
   • API Gateway     : http://localhost:8000
   • Product API     : http://localhost:8001
   • Order API       : http://localhost:8004
   • Recommendation  : http://localhost:8005
   
📊 SonarQube Analysis:
   • Dashboard       : ${SONAR_HOST_URL}/dashboard?id=marketplace-clean
   • Project Key    : marketplace-clean
                    """
                }
            }
        }
    }
    
    post {
        success {
            echo '✅ Pipeline completed successfully!'
        }
        failure {
            echo '❌ Pipeline failed! Vérifiez les logs ci-dessus.'
        }
        always {
            script {
                // Sauvegarder l'URL pour référence
                if (env.WEBHOOK_URL) {
                    writeFile file: 'last_webhook_url.txt', text: env.WEBHOOK_URL
                }
            }
        }
    }
}