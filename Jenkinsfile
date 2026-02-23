pipeline {
    agent any

    stages {

        stage('Checkout') {
            steps {
                checkout scm
            }
        }

        stage('Debug Environment') {
            steps {
                bat 'echo Running on Windows'
                bat 'where dotnet'
                bat 'where docker'
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

        stage('Docker Build') {
            steps {
                bat 'docker compose build'
            }
        }
    }
}
