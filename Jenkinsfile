pipeline {
    agent any

    environment {
        COMPOSE_FILE     = 'docker-compose.yml'
        SONAR_HOST       = 'http://sonarqube:9000'
        SONAR_TOKEN      = credentials('sonar-token')
        PROJECT_KEY      = 'marketplace'
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
                echo "✅ Branch: ${env.BRANCH_NAME ?: 'local'} | Build: #${BUILD_NUMBER}"
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
        // 3. TESTS UNITAIRES (parallèle)
        // ══════════════════════════════════════════
        stage('Unit Tests') {
            parallel {
                stage('Test Product.API') {
                    steps {
                        dir('Tests/Product.API.Tests') {
                            sh '''
                                dotnet test \
                                    -c Release \
                                    --logger "trx;LogFileName=product-results.trx" \
                                    --collect:"XPlat Code Coverage" \
                                    --results-directory ./TestResults
                            '''
                        }
                    }
                    post {
                        always {
                            junit 'Tests/Product.API.Tests/TestResults/*.trx'
                        }
                    }
                }
                stage('Test Order.API') {
                    steps {
                        dir('Tests/Order.API.Tests') {
                            sh '''
                                dotnet test \
                                    -c Release \
                                    --logger "trx;LogFileName=order-results.trx" \
                                    --collect:"XPlat Code Coverage" \
                                    --results-directory ./TestResults
                            '''
                        }
                    }
                    post {
                        always {
                            junit 'Tests/Order.API.Tests/TestResults/*.trx'
                        }
                    }
                }
                stage('Test Recommendation.API') {
                    steps {
                        dir('Tests/Recommendation.API.Tests') {
                            sh '''
                                dotnet test \
                                    -c Release \
                                    --logger "trx;LogFileName=recommendation-results.trx" \
                                    --collect:"XPlat Code Coverage" \
                                    --results-directory ./TestResults
                            '''
                        }
                    }
                    post {
                        always {
                            junit 'Tests/Recommendation.API.Tests/TestResults/*.trx'
                        }
                    }
                }
            }
        }

        // ══════════════════════════════════════════
        // 4. SONARQUBE ANALYSIS
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
                            /d:sonar.exclusions="**/bin/**,**/obj/**,**/Tests/**,**/Migrations/**"

                        dotnet build -c Release

                        dotnet sonarscanner end \
                            /d:sonar.token="${SONAR_TOKEN}"
                    """
                }
            }
        }

        // ══════════════════════════════════════════
        // 5. QUALITY GATE
        // ══════════════════════════════════════════
        stage('Quality Gate') {
            steps {
                timeout(time: 5, unit: 'MINUTES') {
                    waitForQualityGate abortPipeline: true
                }
            }
        }

        // ══════════════════════════════════════════
        // 6. DOCKER BUILD (main uniquement)
        // ══════════════════════════════════════════
        stage('Docker Build') {
            when {
                anyOf {
                    branch 'main'
                    branch 'master'
                }
            }
            parallel {
                stage('Image Product.API') {
                    steps {
                        sh """
                            docker build \
                                -t product-api:${BUILD_NUMBER} \
                                -t product-api:latest \
                                ./Product.API
                        """
                    }
                }
                stage('Image Order.API') {
                    steps {
                        sh """
                            docker build \
                                -t order-api:${BUILD_NUMBER} \
                                -t order-api:latest \
                                ./Order.API
                        """
                    }
                }
                stage('Image Recommendation.API') {
                    steps {
                        sh """
                            docker build \
                                -t recommendation-api:${BUILD_NUMBER} \
                                -t recommendation-api:latest \
                                ./Recommendation.API
                        """
                    }
                }
                stage('Image ApiGateway') {
                    steps {
                        sh """
                            docker build \
                                -t apigateway:${BUILD_NUMBER} \
                                -t apigateway:latest \
                                ./ApiGateway
                        """
                    }
                }
            }
        }

        // ══════════════════════════════════════════
        // 7. DEPLOY (main uniquement)
        // ══════════════════════════════════════════
        stage('Deploy') {
            when {
                anyOf {
                    branch 'main'
                    branch 'master'
                }
            }
            steps {
                sh '''
                    docker-compose down --remove-orphans || true
                    docker-compose up -d
                    echo "⏳ Waiting for services to start..."
                    sleep 30
                '''
                echo "✅ Deployed — Build #${BUILD_NUMBER}"
            }
        }

        // ══════════════════════════════════════════
        // 8. INTEGRATION TESTS (main uniquement)
        // ══════════════════════════════════════════
        stage('Integration Tests') {
            when {
                anyOf {
                    branch 'main'
                    branch 'master'
                }
            }
            steps {
                sh '''
                    echo "🔍 Health checks..."

                    curl -f --retry 5 --retry-delay 5 http://localhost:8000/health \
                        && echo "✅ API Gateway OK" || exit 1

                    curl -f --retry 5 --retry-delay 5 http://localhost:8001/health \
                        && echo "✅ Product.API OK" || exit 1

                    curl -f --retry 5 --retry-delay 5 http://localhost:8004/health \
                        && echo "✅ Order.API OK" || exit 1

                    curl -f --retry 5 --retry-delay 5 http://localhost:8005/health \
                        && echo "✅ Recommendation.API OK" || exit 1

                    echo "🎉 All health checks passed!"
                '''
            }
        }
    }

    // ══════════════════════════════════════════
    // POST ACTIONS
    // ══════════════════════════════════════════
    post {
        always {
            node('') {
                echo "📊 Publishing test results..."
                junit allowEmptyResults: true,
                      testResults: '**/TestResults/*.trx'
                cleanWs()
            }
        }
        success {
            echo "✅ Pipeline réussi — Build #${BUILD_NUMBER}"
        }
        failure {
            echo "❌ Pipeline échoué — Build #${BUILD_NUMBER}"
            emailext(
                subject: "❌ ECHEC: ${env.JOB_NAME} [#${env.BUILD_NUMBER}]",
                body: """
                    <h3>Pipeline échoué</h3>
                    <p><b>Job:</b> ${env.JOB_NAME}</p>
                    <p><b>Build:</b> #${env.BUILD_NUMBER}</p>
                    <p><b>Branch:</b> ${env.BRANCH_NAME ?: 'N/A'}</p>
                    <p><a href="${env.BUILD_URL}">Voir les logs</a></p>
                """,
                mimeType: 'text/html',
                to: "team@company.com"
            )
        }
    }
}