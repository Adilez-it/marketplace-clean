pipeline {
    agent any
    
    stages {
        stage('Checkout') {
            steps {
                git url: 'https://github.com/Adilez-it/marketplace-clean.git', branch: 'main'
            }
        }
        
        stage('Build') {
            steps {
                dir('Product.API') {
                    sh 'dotnet build'
                }
                dir('Order.API') {
                    sh 'dotnet build'
                }
                dir('Recommendation.API') {
                    sh 'dotnet build'
                }
                dir('ApiGateway') {
                    sh 'dotnet build'
                }
            }
        }
        
        stage('Docker Build') {
            steps {
                sh 'docker-compose build'
            }
        }
        
        stage('Deploy') {
            steps {
                sh 'docker-compose up -d'
            }
        }
    }
}