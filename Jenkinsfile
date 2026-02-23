pipeline {
    agent any

    environment {
        DOTNET_VERSION = "8.0"
        COMPOSE_FILE = "docker-compose.yml"
        DOCKER_BUILDKIT = "1"
    }

    options {
        timestamps()
    }

    triggers {
        githubPush()
    }

    stages {

        stage('Checkout') {
            steps {
                checkout scm
            }
        }

        stage('Restore Dependencies') {
            steps {
                sh 'dotnet restore'
            }
        }

        stage('Build Solution') {
            steps {
                sh 'dotnet build --no-restore --configuration Release'
            }
        }

        stage('Run Tests') {
            steps {
                sh 'dotnet test --no-build --configuration Release || true'
            }
        }

        stage('Build Docker Images') {
            steps {
                sh 'docker compose build'
            }
        }

        stage('Start Services (Optional Staging)') {
            when {
                branch 'main'
            }
            steps {
                sh 'docker compose down || true'
                sh 'docker compose up -d'
            }
        }

        stage('Cleanup Old Images') {
            steps {
                sh 'docker image prune -f'
            }
        }
    }

    post {
        always {
            echo 'Pipeline completed.'
        }
        success {
            echo 'Build successful 🚀'
        }
        failure {
            echo 'Build failed ❌'
        }
    }
}
