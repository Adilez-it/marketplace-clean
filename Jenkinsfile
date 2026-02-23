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
                            sh 'dotnet test --no-build -c Release --logger trx --collect:"XPlat Code Coverage"'
                        }
                    }
                }
                stage('Order Tests') {
                    steps {
                        dir('Order.API') {
                            sh 'dotnet test --no-build -c Release --logger trx --collect:"XPlat Code Coverage"'
                        }
                    }
                }
                stage('Recommendation Tests') {
                    steps {
                        dir('Recommendation.API') {
                            sh 'dotnet test --no-build -c Release --logger trx --collect:"XPlat Code Coverage"'
                        }
                    }
                }
            }
        }
        
        stage('SonarQube Analysis') {
            steps {
                withSonarQubeEnv('SonarQube') {
                    sh """
                        dotnet sonarscanner begin \
                            /k:"marketplace" \
                            /d:sonar.host.url="${SONAR_HOST}" \
                            /d:sonar.login="${SONAR_TOKEN}" \
                            /d:sonar.cs.opencover.reportsPaths="**/coverage.opencover.xml"
                        dotnet build
                        dotnet sonarscanner end /d:sonar.login="${SONAR_TOKEN}"
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
                        """
                    }
                }
                stage('Build Recommendation API') {
                    steps {
                        sh """
                            docker build -t ${DOCKER_REGISTRY}/recommendation-api:${BUILD_NUMBER} ./Recommendation.API
                            docker push ${DOCKER_REGISTRY}/recommendation-api:${BUILD_NUMBER}
                        """
                    }
                }
                stage('Build API Gateway') {
                    steps {
                        sh """
                            docker build -t ${DOCKER_REGISTRY}/apigateway:${BUILD_NUMBER} ./ApiGateway
                            docker push ${DOCKER_REGISTRY}/apigateway:${BUILD_NUMBER}
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
                sh 'sleep 30'
                sh 'curl -f http://localhost:8001/health || exit 1'
                sh 'curl -f http://localhost:8004/health || exit 1'
                sh 'curl -f http://localhost:8005/health || exit 1'
                sh 'curl -f http://localhost:8000/health || exit 1'
                echo "All health checks passed"
            }
        }
    }
    
    post {
        always {
            junit '**/TestResults/*.trx'
            publishCoverage adapters: [coberturaAdapter('**/coverage.cobertura.xml')]
        }
        success {
            echo "Pipeline succeeded! Build ${BUILD_NUMBER}"
        }
        failure {
            echo "Pipeline failed! Check logs."
            emailext(
                subject: "FAILED: Pipeline ${env.JOB_NAME} [${env.BUILD_NUMBER}]",
                body: "Check console output at ${env.BUILD_URL}",
                to: "team@company.com"
            )
        }
    }
}
