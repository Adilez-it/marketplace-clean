pipeline {
    agent any

    environment {
        SONAR_HOST  = 'http://sonarqube:9000'
        SONAR_TOKEN = credentials('sonar-token')
        PROJECT_KEY = 'marketplace'
    }

    options {
        buildDiscarder(logRotator(numToKeepStr: '10'))
        timeout(time: 45, unit: 'MINUTES')
        disableConcurrentBuilds()
    }

    stages {

        // ══════════════════════════════════════════
        // 1. CHECKOUT
        // ══════════════════════════════════════════
        stage('Checkout') {
            steps {
                checkout scm
                echo "Branch: ${env.BRANCH_NAME ?: 'local'} | Build: #${BUILD_NUMBER}"
            }
        }

        // ══════════════════════════════════════════
        // 2. BUILD (parallèle)
        // ══════════════════════════════════════════
        stage('Build') {
            parallel {
                stage('Product.API') {
                    steps {
                        dir('Product.API') {
                            sh 'dotnet restore'
                            sh 'dotnet build -c Release --no-restore'
                        }
                    }
                }
                stage('Order.API') {
                    steps {
                        dir('Order.API') {
                            sh 'dotnet restore'
                            sh 'dotnet build -c Release --no-restore'
                        }
                    }
                }
                stage('Recommendation.API') {
                    steps {
                        dir('Recommendation.API') {
                            sh 'dotnet restore'
                            sh 'dotnet build -c Release --no-restore'
                        }
                    }
                }
                stage('ApiGateway') {
                    steps {
                        dir('ApiGateway') {
                            sh 'dotnet restore'
                            sh 'dotnet build -c Release --no-restore'
                        }
                    }
                }
            }
        }

        // ══════════════════════════════════════════
        // 3. UNIT TESTS (parallèle)
        // ══════════════════════════════════════════
        stage('Unit Tests') {
            parallel {
                stage('Test Product.API') {
                    steps {
                        dir('Tests/Product.API.Tests') {
                            sh '''
                                dotnet test -c Release \
                                    --logger "trx;LogFileName=product-results.trx" \
                                    --collect:"XPlat Code Coverage" \
                                    --results-directory ./TestResults
                            '''
                        }
                    }
                }
                stage('Test Order.API') {
                    steps {
                        dir('Tests/Order.API.Tests') {
                            sh '''
                                dotnet test -c Release \
                                    --logger "trx;LogFileName=order-results.trx" \
                                    --collect:"XPlat Code Coverage" \
                                    --results-directory ./TestResults
                            '''
                        }
                    }
                }
                stage('Test Recommendation.API') {
                    steps {
                        dir('Tests/Recommendation.API.Tests') {
                            sh '''
                                dotnet test -c Release \
                                    --logger "trx;LogFileName=recommendation-results.trx" \
                                    --collect:"XPlat Code Coverage" \
                                    --results-directory ./TestResults
                            '''
                        }
                    }
                }
            }
        }

        // ══════════════════════════════════════════
        // 4. PUBLISH TEST RESULTS
        // ══════════════════════════════════════════
        stage('Publish Test Results') {
            steps {
                junit allowEmptyResults: true,
                      testResults: '**/TestResults/*.trx'
                echo "Test results published"
            }
        }

        // ══════════════════════════════════════════
        // 5. SONARQUBE ANALYSIS
        // ══════════════════════════════════════════
        stage('SonarQube Analysis') {
            steps {
                withSonarQubeEnv('SonarQube') {
                    sh """
                        dotnet sonarscanner begin \
                            /k:"${PROJECT_KEY}" \
                            /d:sonar.host.url="${SONAR_HOST}" \
                            /d:sonar.token="${SONAR_TOKEN}" \
                            /d:sonar.cs.opencover.reportsPaths="**/TestResults/**/coverage.opencover.xml" \
                            /d:sonar.exclusions="**/bin/**,**/obj/**,**/Tests/**"

                        dotnet build -c Release

                        dotnet sonarscanner end \
                            /d:sonar.token="${SONAR_TOKEN}"
                    """
                }
            }
        }

        // ══════════════════════════════════════════
        // 6. QUALITY GATE
        // ══════════════════════════════════════════
        stage('Quality Gate') {
            steps {
                timeout(time: 5, unit: 'MINUTES') {
                    waitForQualityGate abortPipeline: true
                }
            }
        }

        // ══════════════════════════════════════════
        // 7. DOCKER BUILD (main/master uniquement)
        // ══════════════════════════════════════════
        stage('Docker Build') {
            when {
                anyOf { branch 'main'; branch 'master' }
            }
            parallel {
                stage('Image Product.API') {
                    steps {
                        sh "docker build -t product-api:${BUILD_NUMBER} -t product-api:latest ./Product.API"
                    }
                }
                stage('Image Order.API') {
                    steps {
                        sh "docker build -t order-api:${BUILD_NUMBER} -t order-api:latest ./Order.API"
                    }
                }
                stage('Image Recommendation.API') {
                    steps {
                        sh "docker build -t recommendation-api:${BUILD_NUMBER} -t recommendation-api:latest ./Recommendation.API"
                    }
                }
                stage('Image ApiGateway') {
                    steps {
                        sh "docker build -t apigateway:${BUILD_NUMBER} -t apigateway:latest ./ApiGateway"
                    }
                }
            }
        }

        // ══════════════════════════════════════════
        // 8. DEPLOY (main/master uniquement)
        // ══════════════════════════════════════════
        stage('Deploy') {
            when {
                anyOf { branch 'main'; branch 'master' }
            }
            steps {
                sh '''
                    docker-compose down --remove-orphans || true
                    docker-compose up -d
                    echo "Waiting for services..."
                    sleep 30
                '''
                echo "Deployed — Build #${BUILD_NUMBER}"
            }
        }

        // ══════════════════════════════════════════
        // 9. INTEGRATION TESTS (main/master uniquement)
        // ══════════════════════════════════════════
        stage('Integration Tests') {
            when {
                anyOf { branch 'main'; branch 'master' }
            }
            steps {
                sh '''
                    curl -f --retry 5 --retry-delay 5 http://localhost:8000/health && echo "API Gateway OK"
                    curl -f --retry 5 --retry-delay 5 http://localhost:8001/health && echo "Product.API OK"
                    curl -f --retry 5 --retry-delay 5 http://localhost:8004/health && echo "Order.API OK"
                    curl -f --retry 5 --retry-delay 5 http://localhost:8005/health && echo "Recommendation.API OK"
                    echo "All health checks passed!"
                '''
            }
        }

        // ══════════════════════════════════════════
        // 10. CLEANUP WORKSPACE
        // ══════════════════════════════════════════
        stage('Cleanup') {
            steps {
                cleanWs()
                echo "Workspace cleaned"
            }
        }
    }

    // ══════════════════════════════════════════
    // POST ACTIONS (logs uniquement, sans FilePath)
    // ══════════════════════════════════════════
    post {
        success {
            echo "Pipeline reussi — Build #${BUILD_NUMBER}"
        }
        failure {
            echo "Pipeline echoue — Build #${BUILD_NUMBER}"
            emailext(
                subject: "ECHEC: ${env.JOB_NAME} [#${env.BUILD_NUMBER}]",
                body: """
                    Pipeline echoue.
                    Job: ${env.JOB_NAME}
                    Build: #${env.BUILD_NUMBER}
                    Branch: ${env.BRANCH_NAME ?: 'N/A'}
                    Logs: ${env.BUILD_URL}
                """,
                to: "team@company.com"
            )
        }
    }
}