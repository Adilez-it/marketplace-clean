pipeline {
    agent any

    environment {
        DOCKER_REGISTRY = 'your-registry.azurecr.io'
        SONAR_HOST = 'http://sonarqube:9000'
        SONAR_TOKEN = credentials('sonar-token')
    }

    stages {
        stage('Checkout') {
            steps {
                checkout scm
                echo "Branch: ${env.BRANCH_NAME}"
            }
        }

        stage('Restore & Build') {
            parallel {
                stage('Product API') {
                    steps {
                        dir('Product.API') {
                            sh 'dotnet restore'
                            sh 'dotnet build -c Release'
                        }
                    }
                }
                stage('Order API') {
                    steps {
                        dir('Order.API') {
                            sh 'dotnet restore'
                            sh 'dotnet build -c Release'
                        }
                    }
                }
                stage('Recommendation API') {
                    steps {
                        dir('Recommendation.API') {
                            sh 'dotnet restore'
                            sh 'dotnet build -c Release'
                        }
                    }
                }
            }
        }

        stage('Unit Tests') {
            parallel {
                stage('Product Tests') {
                    steps {
                        dir('Product.API') {
                            sh 'dotnet test --no-build -c Release --logger "trx;LogFileName=results.trx" --results-directory TestResults --collect:"XPlat Code Coverage"'
                        }
                    }
                }
                stage('Order Tests') {
                    steps {
                        dir('Order.API') {
                            sh 'dotnet test --no-build -c Release --logger "trx;LogFileName=results.trx" --results-directory TestResults --collect:"XPlat Code Coverage"'
                        }
                    }
                }
                stage('Recommendation Tests') {
                    steps {
                        dir('Recommendation.API') {
                            sh 'dotnet test --no-build -c Release --logger "trx;LogFileName=results.trx" --results-directory TestResults --collect:"XPlat Code Coverage"'
                        }
                    }
                }
            }
        }

        stage('Publish Results') {
            steps {
                junit allowEmptyResults: true, testResults: '**/TestResults/*.trx'
                publishCoverage adapters: [coberturaAdapter('**/coverage.cobertura.xml')]
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
                            /d:sonar.cs.opencover.reportsPaths="**/coverage.opencover.xml"
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
            parallel {
                stage('Build Product API') {
                    steps {
                        sh """
                            docker build -t ${DOCKER_REGISTRY}/product-api:${BUILD_NUMBER} ./Product.API
                            docker push ${DOCKER_REGISTRY}/product-api:${BUILD_NUMBER}
                            docker tag ${DOCKER_REGISTRY}/product-api:${BUILD_NUMBER} ${DOCKER_REGISTRY}/product-api:latest
                            docker push ${DOCKER_REGISTRY}/product-api:latest
                        """
                    }
                }
                stage('Build Order API') {
                    steps {
                        sh """
                            docker build -t ${DOCKER_REGISTRY}/order-api:${BUILD_NUMBER} ./Order.API
                            docker push ${DOCKER_REGISTRY}/order-api:${BUILD_NUMBER}
                            docker tag ${DOCKER_REGISTRY}/order-api:${BUILD_NUMBER} ${DOCKER_REGISTRY}/order-api:latest
                            docker push ${DOCKER_REGISTRY}/order-api:latest
                        """
                    }
                }
                stage('Build Recommendation API') {
                    steps {
                        sh """
                            docker build -t ${DOCKER_REGISTRY}/recommendation-api:${BUILD_NUMBER} ./Recommendation.API
                            docker push ${DOCKER_REGISTRY}/recommendation-api:${BUILD_NUMBER}
                            docker tag ${DOCKER_REGISTRY}/recommendation-api:${BUILD_NUMBER} ${DOCKER_REGISTRY}/recommendation-api:latest
                            docker push ${DOCKER_REGISTRY}/recommendation-api:latest
                        """
                    }
                }
                stage('Build API Gateway') {
                    steps {
                        sh """
                            docker build -t ${DOCKER_REGISTRY}/apigateway:${BUILD_NUMBER} ./ApiGateway
                            docker push ${DOCKER_REGISTRY}/apigateway:${BUILD_NUMBER}
                            docker tag ${DOCKER_REGISTRY}/apigateway:${BUILD_NUMBER} ${DOCKER_REGISTRY}/apigateway:latest
                            docker push ${DOCKER_REGISTRY}/apigateway:latest
                        """
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
                retry(10) {
                    sleep 10
                    sh 'curl -f http://localhost:8001/health'
                }
                sh 'curl -f http://localhost:8004/health'
                sh 'curl -f http://localhost:8005/health'
                sh 'curl -f http://localhost:8000/health'
                echo "All health checks passed"
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
                to: "ezarfi.adil.it@gmail.com",
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
                to: "ezarfi.adil.it@gmail.com",
                mimeType: 'text/html'
            )
        }
        always {
            node('') {
                cleanWs()
            }
        }
    }
}