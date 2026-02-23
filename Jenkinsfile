pipeline {
    agent any

    environment {
        DOCKER_REGISTRY = 'your-registry.azurecr.io'
        SONAR_HOST = 'http://sonarqube:9000'
        SONAR_TOKEN = credentials('sonar-token')
        SERVICES = "Product.API,Order.API,Recommendation.API,ApiGateway"
        EMAIL_RECIPIENT = "ezarfi.adil.it@gmail.com"
    }

    stages {
        stage('Checkout') {
            steps {
                checkout scm
                echo "Branch: ${env.BRANCH_NAME}"
            }
        }

        stage('Restore & Build') {
            steps {
                script {
                    def services = env.SERVICES.split(',')
                    for (s in services) {
                        dir(s) {
                            echo "Building ${s}..."
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
                            echo "Running tests for ${s}..."
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
                // JUnit results
                junit allowEmptyResults: true, testResults: '**/TestResults/**/*.trx'
                // Coverage
                publishCoverage adapters: [coberturaAdapter('**/TestResults/coverage.cobertura.xml')]
                // Archive artifacts
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

        stage('Quality Gate') {
            steps {
                timeout(time: 5, unit: 'MINUTES') {
                    waitForQualityGate abortPipeline: true
                }
            }
        }

        stage('Docker Login') {
            when { branch 'main' }
            steps {
                withCredentials([usernamePassword(
                    credentialsId: 'acr-credentials',
                    usernameVariable: 'ACR_USER',
                    passwordVariable: 'ACR_PASS'
                )]) {
                    sh "echo $ACR_PASS | docker login ${DOCKER_REGISTRY} -u $ACR_USER --password-stdin"
                }
            }
        }

        stage('Docker Build & Push') {
            when { branch 'main' }
            steps {
                script {
                    def services = env.SERVICES.split(',')
                    for (s in services) {
                        dir(s) {
                            sh """
                                docker build --pull -t ${DOCKER_REGISTRY}/${s.toLowerCase()}:${BUILD_NUMBER} .
                                docker push ${DOCKER_REGISTRY}/${s.toLowerCase()}:${BUILD_NUMBER}
                                docker tag ${DOCKER_REGISTRY}/${s.toLowerCase()}:${BUILD_NUMBER} ${DOCKER_REGISTRY}/${s.toLowerCase()}:latest
                                docker push ${DOCKER_REGISTRY}/${s.toLowerCase()}:latest
                            """
                        }
                    }
                }
            }
        }

        stage('Deploy') {
            when { branch 'main' }
            steps {
                sh 'docker-compose pull'
                sh 'docker-compose up -d --remove-orphans'
                echo "Deployed successfully - Build ${BUILD_NUMBER}"
            }
        }

        stage('Integration Tests') {
            when { branch 'main' }
            steps {
                script {
                    def healthPorts = [8001, 8004, 8005, 8000]
                    for (port in healthPorts) {
                        retry(10) {
                            timeout(time: 30, unit: 'SECONDS') {
                                sh "curl -f http://localhost:${port}/health"
                            }
                        }
                    }
                    echo "All health checks passed"
                }
            }
        }
    }

    post {
        success {
            emailext(
                subject: "✅ SUCCESS: Pipeline ${env.JOB_NAME} [${env.BUILD_NUMBER}]",
                body: """
                    <h2>Build Succeeded!</h2>
                    <p><b>Job:</b> ${env.JOB_NAME}</p>
                    <p><b>Build:</b> #${env.BUILD_NUMBER}</p>
                    <p><b>Branch:</b> ${env.BRANCH_NAME}</p>
                    <p><b>Duration:</b> ${currentBuild.durationString}</p>
                    <p><a href="${env.BUILD_URL}">View Build</a></p>
                """,
                to: "${EMAIL_RECIPIENT}",
                mimeType: 'text/html'
            )
        }
        failure {
            emailext(
                subject: "❌ FAILED: Pipeline ${env.JOB_NAME} [${env.BUILD_NUMBER}]",
                body: """
                    <h2>Build Failed!</h2>
                    <p><b>Job:</b> ${env.JOB_NAME}</p>
                    <p><b>Build:</b> #${env.BUILD_NUMBER}</p>
                    <p><b>Branch:</b> ${env.BRANCH_NAME}</p>
                    <p><b>Duration:</b> ${currentBuild.durationString}</p>
                    <p><a href="${env.BUILD_URL}console">View Console Log</a></p>
                """,
                to: "${EMAIL_RECIPIENT}",
                mimeType: 'text/html'
            )
        }
        always {
            script {
                cleanWs() // ✅ داخل node context عبر script
            }
        }
    }
}