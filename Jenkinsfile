pipeline {
    agent any

    options {
        timestamps()
    }

    stages {

        stage('Checkout') {
            steps {
                checkout scm
            }
        }

        stage('Restore') {
            steps {
                bat 'dotnet restore'
            }
        }

        stage('Build') {
            steps {
                bat 'dotnet build --configuration Release --no-restore'
            }
        }

        stage('Test') {
            steps {
                bat 'dotnet test --configuration Release --no-build'
            }
        }

        stage('Docker Build') {
            steps {
                bat 'docker compose build'
            }
        }

        stage('Docker Up') {
            steps {
                bat 'docker compose down'
                bat 'docker compose up -d'
            }
        }

        stage('Cleanup') {
            steps {
                bat 'docker image prune -f'
            }
        }
    }

    post {
        success {
            echo '✅ Build Successful'
        }
        failure {
            echo '❌ Build Failed'
        }
    }
}
