pipeline {
    agent any
    
    environment {
        // Définir le port Jenkins (celui que ngrok va exposer)
        JENKINS_PORT = '8080'
        NGROK_REGION = 'eu' // ou 'us', 'eu', 'ap', etc.
    }
    
    stages {
        stage('Checkout') {
            steps {
                // Pour le développement local, on utilise le répertoire existant
                dir('D:/marketplace-clean') {
                    echo 'Using local code from D:/marketplace-clean'
                }
            }
        }
        
        stage('Setup Webhook URL') {
            steps {
                script {
                    echo '🚀 Démarrage du tunnel ngrok pour Jenkins...'
                    
                    // Tuer les anciens tunnels ngrok
                    bat 'ngrok kill || exit 0'
                    
                    // Démarrer ngrok pour Jenkins (port 8080)
                    bat """
                        start /B cmd /c "ngrok http ${JENKINS_PORT} --region=${NGROK_REGION} --log=stdout > ngrok_jenkins.log 2>&1"
                    """
                    
                    // Attendre que ngrok démarre
                    echo 'Attente du démarrage de ngrok...'
                    sleep(10)
                    
                    // Récupérer l'URL publique de Jenkins
                    def ngrokData = bat(
                        script: '''
                            @echo off
                            curl -s http://localhost:4040/api/tunnels > ngrok_response.json
                            type ngrok_response.json
                        ''',
                        returnStdout: true
                    ).trim()
                    
                    echo "Réponse ngrok: ${ngrokData}"
                    
                    // Parser l'URL publique
                    def ngrokUrl = ''
                    if (ngrokData.contains('public_url')) {
                        // Extraction simple de l'URL
                        ngrokUrl = bat(
                            script: '''
                                @echo off
                                powershell -Command "$json = Get-Content ngrok_response.json | ConvertFrom-Json; $json.tunnels[0].public_url"
                            ''',
                            returnStdout: true
                        ).trim()
                    }
                    
                    if (ngrokUrl) {
                        env.WEBHOOK_URL = ngrokUrl
                        echo "✅ Jenkins Webhook URL: ${env.WEBHOOK_URL}"
                        
                        // Sauvegarder l'URL dans un fichier
                        bat "echo ${env.WEBHOOK_URL} > jenkins_webhook_url.txt"
                        
                        // Afficher l'URL pour GitHub webhook
                        echo "📌 Configurez votre webhook GitHub avec: ${env.WEBHOOK_URL}/github-webhook/"
                    } else {
                        echo "⚠️ Impossible de récupérer l'URL ngrok. Utilisation de localhost."
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
                bat '''
                    echo Waiting for services to start...
                    timeout /t 20
                    
                    echo Testing API Gateway...
                    curl -f http://localhost:8000/health || exit /b 1
                    
                    echo Testing Product API...
                    curl -f http://localhost:8001/health || exit /b 1
                    
                    echo Testing Order API...
                    curl -f http://localhost:8004/health || exit /b 1
                    
                    echo Testing Recommendation API...
                    curl -f http://localhost:8005/health || exit /b 1
                    
                    echo ✅ All services are healthy!
                '''
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
🔗 Webhook Générique       : ${env.WEBHOOK_URL}/generic-webhook-trigger/invoke

📊 Interface ngrok         : http://localhost:4040

⚙️ Configuration GitHub:
   1. Allez dans Settings → Webhooks
   2. Ajoutez: ${env.WEBHOOK_URL}/github-webhook/
   3. Content type: application/json
   4. Events: Just the push event

⏱️  Le tunnel expire dans: 2 heures
🔄 Pour renouveler: Redémarrez le pipeline

📝 Les URLs de vos services:
   • API Gateway     : http://localhost:8000
   • Product API     : http://localhost:8001
   • Order API       : http://localhost:8004
   • Recommendation  : http://localhost:8005
                    """
                }
            }
        }
    }
    
    post {
        success {
            echo '✅ Pipeline completed successfully!'
            
            // Envoyer l'URL par email (optionnel)
            // mail to: 'team@example.com',
            //      subject: "Jenkins Public URL: ${env.WEBHOOK_URL}",
            //      body: "Votre Jenkins est accessible sur: ${env.WEBHOOK_URL}"
            
            // Afficher le résumé
            script {
                echo """
╔══════════════════════════════════════════════════════════════╗
║                                                              ║
║   ✅ PIPELINE EXÉCUTÉ AVEC SUCCÈS                            ║
║                                                              ║
╠══════════════════════════════════════════════════════════════╣
║                                                              ║
║   📡 Jenkins Public: ${env.WEBHOOK_URL ?: 'Non disponible'}  ║
║   🚀 API Gateway   : http://localhost:8000                  ║
║   📊 Portainer     : http://localhost:8888                  ║
║   🐰 RabbitMQ      : http://localhost:15672                 ║
║                                                              ║
╚══════════════════════════════════════════════════════════════╝
                """
            }
        }
        failure {
            echo '❌ Pipeline failed! Check the logs.'
            
            // Tentative de récupération
            script {
                try {
                    bat 'ngrok kill'
                } catch (err) {
                    echo 'Ngrok already stopped'
                }
            }
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