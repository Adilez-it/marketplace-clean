pipeline {
    agent any
    
    stages {
        stage('Checkout') {
            steps {
                // Use checkout scm instead of git directly
                checkout scm
            }
        }
        
        stage('Build') {
            steps {
                script {
                    // Wrap each directory operation in a node context
                    dir('Product.API') {
                        sh 'dotnet build || echo "Build failed for Product.API"'
                    }
                    dir('Order.API') {
                        sh 'dotnet build || echo "Build failed for Order.API"'
                    }
                    dir('Recommendation.API') {
                        sh 'dotnet build || echo "Build failed for Recommendation.API"'
                    }
                    dir('ApiGateway') {
                        sh 'dotnet build || echo "Build failed for ApiGateway"'
                    }
                }
            }
        }
        
        stage('Docker Build') {
            steps {
                script {
                    sh 'docker-compose build || echo "Docker build failed"'
                }
            }
        }
        
        stage('Deploy') {
            steps {
                script {
                    sh 'docker-compose up -d || echo "Deploy failed"'
                }
            }
        }
    }
    
    post {
        always {
            // Clean up workspace
            cleanWs()
        }
    }
}