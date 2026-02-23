pipeline {
    agent any
    
    stages {
        stage('Checkout') {
            steps {
                // Pour le développement local, on utilise le répertoire existant
                dir('D:/marketplace-clean') {
                    echo 'Using local code from D:/marketplace-clean'
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
    }
    
    post {
        success {
            echo '✅ Pipeline completed successfully!'
            echo 'Services disponibles sur:'
            echo '- Gateway: http://localhost:8000'
            echo '- Product API: http://localhost:8001'
            echo '- Order API: http://localhost:8004'
            echo '- Recommendation API: http://localhost:8005'
        }
        failure {
            echo '❌ Pipeline failed! Check the logs.'
        }
    }
}