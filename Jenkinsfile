pipeline {
    agent any

    environment {
        DOCKER_REGISTRY = 'your-registry.azurecr.io'
        SONAR_HOST = 'http://sonarqube:9000'
        SONAR_TOKEN = credentials('sonar-token')
        SERVICES = "Product.API,Order.API,Recommendation.API,ApiGateway"
    }

    stages {
        stage('Checkout') {
            steps { checkout scm }
        }

        stage('Restore & Build') {
            steps {
                script {
                    def services = env.SERVICES.split(',')
                    for (s in services) {
                        dir(s) {
                            sh 'dotnet restore'
                            sh 'dotnet build -c Release'
                        }
                    }
                }
            }
        }

        stage('Unit Tests & Coverage') {
            steps {
                script {
                    def services = env.SERVICES.split(',').findAll { it != 'ApiGateway' }
                    for (s in services) {
                        dir(s) {
                            sh '''
                                dotnet test --no-build -c Release \
                                    --logger "trx;LogFileName=results.trx" \
                                    --results-directory TestResults \
                                    --collect:"XPlat Code Coverage"
                            '''
                        }
                    }
                }
            }
        }

        stage('Publish Test Results & Coverage') {
            steps {
                junit allowEmptyResults: true, testResults: '**/TestResults/**/*.trx'
                publishCoverage adapters: [coberturaAdapter('**/TestResults/coverage.cobertura.xml')]
                archiveArtifacts artifacts: '**/TestResults/**', allowEmptyArchive: true
            }
        }

        stage('SonarQube Analysis') {
            steps {
                withSonarQubeEnv('SonarQube') {
                    sh """
                        dotnet sonarscanner begin \
                            /k:"marketplace" \
                            /d:sonar.host.url="${SONAR_HOST}" \
                            /d:sonar.token="${SONAR_TOKEN}" \
                            /d:sonar.cs.opencover.reportsPaths="**/TestResults/coverage.opencover.xml"
                        dotnet build -c Release
                        dotnet sonarscanner end /d:sonar.token="${SONAR_TOKEN}"
                    """
                }
            }
        }

        // باقي ال stages (Docker, Deploy, Integration) كما كانوا
    }

    post {
        always { cleanWs() }
    }
}