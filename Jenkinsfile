// ============================================================
// Jenkinsfile - Marketplace Microservices CI/CD Pipeline
// ============================================================

pipeline {
    agent any

    // ---------------------------------------------
    // Environment Variables
    // ---------------------------------------------
    environment {
        // Docker Registry
        DOCKER_REGISTRY = 'docker.io'
        IMAGE_TAG = "${BUILD_NUMBER}-${GIT_COMMIT.take(8)}"
        
        // SonarQube
        SONAR_HOST_URL = 'http://sonarqube:9000'
        SONAR_TOKEN = credentials('sonar-token')
        PROJECT_KEY = 'com.marketplace.marketplace'
        PROJECT_NAME = 'Marketplace Microservices'
        
        // Build Configuration
        BUILD_CONFIG = 'Release'
        
        // Test Paths
        TEST_RESULTS = '**/TestResults/*.trx'
        COVERAGE_RESULTS = '**/TestResults/**/coverage.opencover.xml'
        
        // Docker Images
        PRODUCT_IMAGE = "product-api:${IMAGE_TAG}"
        ORDER_IMAGE = "order-api:${IMAGE_TAG}"
        RECOMMENDATION_IMAGE = "recommendation-api:${IMAGE_TAG}"
        GATEWAY_IMAGE = "apigateway:${IMAGE_TAG}"
    }

    // ---------------------------------------------
    // Pipeline Options (removed ansiColor)
    // ---------------------------------------------
    options {
        buildDiscarder(logRotator(numToKeepStr: '10', artifactNumToKeepStr: '5'))
        timeout(time: 60, unit: 'MINUTES')
        timestamps()
        disableConcurrentBuilds()
        skipStagesAfterUnstable()
    }

    // ---------------------------------------------
    // Parameters
    // ---------------------------------------------
    parameters {
        choice(
            name: 'BUILD_CONFIGURATION',
            choices: ['Release', 'Debug'],
            description: 'Build configuration'
        )
        booleanParam(
            name: 'RUN_TESTS',
            defaultValue: true,
            description: 'Run unit tests?'
        )
        booleanParam(
            name: 'RUN_SONAR',
            defaultValue: false,
            description: 'Run SonarQube analysis?'
        )
        booleanParam(
            name: 'DEPLOY',
            defaultValue: false,
            description: 'Deploy after build?'
        )
    }

    // ---------------------------------------------
    // Stages
    // ---------------------------------------------
    stages {
        
        // =============================================
        // STAGE 1: Checkout
        // =============================================
        stage('Checkout') {
            steps {
                script {
                    currentBuild.displayName = "#${BUILD_NUMBER} - ${params.BUILD_CONFIGURATION}"
                }
                
                git branch: 'main',
                    url: 'https://github.com/Adilez-it/marketplace-clean.git',
                    credentialsId: 'github-credentials'
                
                script {
                    env.GIT_COMMIT_SHORT = sh(script: 'git rev-parse --short HEAD', returnStdout: true).trim()
                    env.BRANCH_NAME = sh(script: 'git rev-parse --abbrev-ref HEAD', returnStdout: true).trim()
                }
                
                echo """
                ========================================
                Building Marketplace Microservices
                Build #${BUILD_NUMBER}
                Commit: ${env.GIT_COMMIT_SHORT}
                Branch: ${env.BRANCH_NAME}
                Configuration: ${params.BUILD_CONFIGURATION}
                ========================================
                """
            }
        }

        // =============================================
        // STAGE 2: Install .NET SDK (if not available)
        // =============================================
        stage('Setup .NET') {
            steps {
                script {
                    // Check if dotnet is available, if not install it
                    sh '''
                        if ! command -v dotnet &> /dev/null; then
                            echo "Installing .NET SDK 8.0..."
                            curl -L https://dot.net/v1/dotnet-install.sh | bash -s -- --version 8.0.404 --install-dir $HOME/.dotnet
                            export PATH="$HOME/.dotnet:$PATH"
                            echo "PATH=$HOME/.dotnet:$PATH" >> $HOME/.bashrc
                        fi
                        dotnet --version
                    '''
                }
            }
        }

        // =============================================
        // STAGE 3: Restore & Build
        // =============================================
        stage('Restore & Build') {
            parallel {
                stage('Product.API') {
                    steps {
                        dir('Product.API') {
                            sh 'dotnet restore'
                            sh "dotnet build -c ${params.BUILD_CONFIGURATION} --no-restore"
                        }
                    }
                }
                stage('Order.API') {
                    steps {
                        dir('Order.API') {
                            sh 'dotnet restore'
                            sh "dotnet build -c ${params.BUILD_CONFIGURATION} --no-restore"
                        }
                    }
                }
                stage('Recommendation.API') {
                    steps {
                        dir('Recommendation.API') {
                            sh 'dotnet restore'
                            sh "dotnet build -c ${params.BUILD_CONFIGURATION} --no-restore"
                        }
                    }
                }
                stage('ApiGateway') {
                    steps {
                        dir('ApiGateway') {
                            sh 'dotnet restore'
                            sh "dotnet build -c ${params.BUILD_CONFIGURATION} --no-restore"
                        }
                    }
                }
            }
        }

        // =============================================
        // STAGE 4: Unit Tests
        // =============================================
        stage('Unit Tests') {
            when {
                expression { params.RUN_TESTS }
            }
            parallel {
                stage('Test Product.API') {
                    steps {
                        dir('Tests/Product.API.Tests') {
                            sh """
                                dotnet test -c ${params.BUILD_CONFIGURATION} \
                                    --logger "trx;LogFileName=product-results.trx" \
                                    --collect:"XPlat Code Coverage" \
                                    --results-directory ./TestResults
                            """
                        }
                    }
                    post {
                        always {
                            junit allowEmptyResults: true,
                                  testResults: 'Tests/Product.API.Tests/TestResults/*.trx'
                        }
                    }
                }
                stage('Test Order.API') {
                    steps {
                        dir('Tests/Order.API.Tests') {
                            sh """
                                dotnet test -c ${params.BUILD_CONFIGURATION} \
                                    --logger "trx;LogFileName=order-results.trx" \
                                    --collect:"XPlat Code Coverage" \
                                    --results-directory ./TestResults
                            """
                        }
                    }
                    post {
                        always {
                            junit allowEmptyResults: true,
                                  testResults: 'Tests/Order.API.Tests/TestResults/*.trx'
                        }
                    }
                }
                stage('Test Recommendation.API') {
                    steps {
                        dir('Tests/Recommendation.API.Tests') {
                            sh """
                                dotnet test -c ${params.BUILD_CONFIGURATION} \
                                    --logger "trx;LogFileName=recommendation-results.trx" \
                                    --collect:"XPlat Code Coverage" \
                                    --results-directory ./TestResults
                            """
                        }
                    }
                    post {
                        always {
                            junit allowEmptyResults: true,
                                  testResults: 'Tests/Recommendation.API.Tests/TestResults/*.trx'
                        }
                    }
                }
            }
        }

        // =============================================
        // STAGE 5: SonarQube Analysis
        // =============================================
        stage('SonarQube Analysis') {
            when {
                expression { params.RUN_SONAR }
            }
            steps {
                script {
                    // Check if SonarQube scanner is available
                    sh '''
                        if ! command -v dotnet-sonarscanner &> /dev/null; then
                            dotnet tool install --global dotnet-sonarscanner
                        fi
                    '''
                    
                    withSonarQubeEnv('SonarQube') {
                        sh """
                            dotnet sonarscanner begin \
                                /k:"${PROJECT_KEY}" \
                                /n:"${PROJECT_NAME}" \
                                /v:"${IMAGE_TAG}" \
                                /d:sonar.host.url="${SONAR_HOST_URL}" \
                                /d:sonar.token="${SONAR_TOKEN}" \
                                /d:sonar.cs.opencover.reportsPaths="${COVERAGE_RESULTS}" \
                                /d:sonar.exclusions="**/bin/**/*,**/obj/**/*,**/Migrations/**/*,**/Tests/**/*.cs" \
                                /d:sonar.coverage.exclusions="**/Program.cs,**/Startup.cs,**/Migrations/**/*" \
                                /d:sonar.test.inclusions="**/Tests/**/*"

                            dotnet build -c ${params.BUILD_CONFIGURATION}

                            dotnet sonarscanner end \
                                /d:sonar.token="${SONAR_TOKEN}"
                        """
                    }
                }
            }
        }

        // =============================================
        // STAGE 6: Publish Test Results
        // =============================================
        stage('Publish Reports') {
            steps {
                // Publish test results
                junit allowEmptyResults: true,
                      testResults: '**/TestResults/*.trx'
                
                // Archive artifacts
                archiveArtifacts artifacts: '**/TestResults/**/*.trx,**/TestResults/**/*.xml',
                                 fingerprint: true,
                                 allowEmptyArchive: true
            }
        }

        // =============================================
        // STAGE 7: Docker Build
        // =============================================
        stage('Docker Build') {
            when {
                anyOf {
                    branch 'main'
                    branch 'master'
                    expression { params.DEPLOY }
                }
            }
            steps {
                script {
                    docker.build("product-api:${IMAGE_TAG}", "-f Product.API/Dockerfile ./Product.API")
                    docker.build("order-api:${IMAGE_TAG}", "-f Order.API/Dockerfile ./Order.API")
                    docker.build("recommendation-api:${IMAGE_TAG}", "-f Recommendation.API/Dockerfile ./Recommendation.API")
                    docker.build("apigateway:${IMAGE_TAG}", "-f ApiGateway/Dockerfile ./ApiGateway")
                    
                    // Also tag as latest
                    sh """
                        docker tag product-api:${IMAGE_TAG} product-api:latest
                        docker tag order-api:${IMAGE_TAG} order-api:latest
                        docker tag recommendation-api:${IMAGE_TAG} recommendation-api:latest
                        docker tag apigateway:${IMAGE_TAG} apigateway:latest
                    """
                }
            }
        }

        // =============================================
        // STAGE 8: Push to Registry (if credentials exist)
        // =============================================
        stage('Push to Registry') {
            when {
                allOf {
                    branch 'main'
                    expression { params.DEPLOY }
                }
            }
            steps {
                script {
                    try {
                        withDockerRegistry([credentialsId: 'docker-registry-credentials', url: "https://${DOCKER_REGISTRY}"]) {
                            sh """
                                docker push product-api:${IMAGE_TAG}
                                docker push order-api:${IMAGE_TAG}
                                docker push recommendation-api:${IMAGE_TAG}
                                docker push apigateway:${IMAGE_TAG}
                            """
                        }
                    } catch (Exception e) {
                        echo "Docker push failed: ${e.message}"
                        echo "Continuing without pushing to registry..."
                    }
                }
            }
        }

        // =============================================
        // STAGE 9: Deploy (Local)
        // =============================================
        stage('Deploy Locally') {
            when {
                allOf {
                    branch 'main'
                    expression { params.DEPLOY }
                }
            }
            steps {
                dir('marketplace-clean') {
                    sh '''
                        # Stop existing containers
                        docker-compose down --remove-orphans || true
                        
                        # Start services
                        docker-compose up -d
                        
                        # Wait for services to be ready
                        echo "Waiting for services to start..."
                        sleep 30
                        
                        # Test health endpoints
                        curl -f --retry 5 --retry-delay 5 http://localhost:8000/health || echo "Gateway not ready"
                        curl -f --retry 5 --retry-delay 5 http://localhost:8001/health || echo "Product API not ready"
                        curl -f --retry 5 --retry-delay 5 http://localhost:8004/health || echo "Order API not ready"
                        curl -f --retry 5 --retry-delay 5 http://localhost:8005/health || echo "Recommendation API not ready"
                        
                        echo "Deployment completed!"
                        docker-compose ps
                    '''
                }
            }
        }

        // =============================================
        // STAGE 10: Smoke Tests
        // =============================================
        stage('Smoke Tests') {
            when {
                branch 'main'
            }
            steps {
                sh '''
                    # Test API Gateway
                    curl -s http://localhost:8000/health | grep -q "Healthy" || exit 1
                    
                    # Test Product API
                    curl -s http://localhost:8001/health | grep -q "Healthy" || exit 1
                    
                    # Test Order API
                    curl -s http://localhost:8004/health | grep -q "Healthy" || exit 1
                    
                    # Test Recommendation API
                    curl -s http://localhost:8005/health | grep -q "Healthy" || exit 1
                    
                    echo "All smoke tests passed!"
                '''
            }
        }

        // =============================================
        // STAGE 11: Cleanup
        // =============================================
        stage('Cleanup') {
            steps {
                // Clean Docker resources (optional, comment out if not needed)
                sh '''
                    docker system prune -f || true
                    docker image prune -f || true
                '''
                
                // Clean workspace
                cleanWs(
                    cleanWhenAborted: true,
                    cleanWhenFailure: true,
                    cleanWhenNotBuilt: true,
                    cleanWhenSuccess: true,
                    cleanWhenUnstable: true,
                    deleteDirs: true
                )
            }
        }
    }

    // ---------------------------------------------
    // Post-Build Actions
    // ---------------------------------------------
    post {
        always {
            script {
                // Send email notification
                def recipient = 'adil.ezarfi@company.com'
                def subject = "${currentBuild.result}: ${env.JOB_NAME} - Build #${BUILD_NUMBER}"
                def body = """
                    <h2>Build ${currentBuild.result}</h2>
                    <ul>
                        <li><b>Project:</b> Marketplace Microservices</li>
                        <li><b>Build:</b> <a href="${env.BUILD_URL}">#${BUILD_NUMBER}</a></li>
                        <li><b>Branch:</b> ${env.BRANCH_NAME}</li>
                        <li><b>Commit:</b> ${env.GIT_COMMIT_SHORT}</li>
                        <li><b>Duration:</b> ${currentBuild.durationString}</li>
                    </ul>
                    <h3>Deployed Services</h3>
                    <ul>
                        <li>API Gateway: http://localhost:8000</li>
                        <li>Product API: http://localhost:8001/swagger</li>
                        <li>Order API: http://localhost:8004/swagger</li>
                        <li>Recommendation API: http://localhost:8005/swagger</li>
                        <li>RabbitMQ: http://localhost:15672 (guest/guest)</li>
                        <li>Neo4j: http://localhost:7474 (neo4j/password123)</li>
                        <li>Portainer: http://localhost:9443</li>
                    </ul>
                """
                
                try {
                    emailext(
                        to: recipient,
                        subject: subject,
                        body: body,
                        mimeType: 'text/html'
                    )
                } catch (Exception e) {
                    echo "Email notification failed: ${e.message}"
                }
            }
            
            // Archive test results
            archiveArtifacts artifacts: '**/TestResults/**/*.trx',
                             fingerprint: true,
                             allowEmptyArchive: true
        }
        
        success {
            echo "✅ Pipeline completed successfully!"
        }
        
        failure {
            echo "❌ Pipeline failed!"
        }
        
        unstable {
            echo "⚠️ Pipeline is unstable!"
        }
        
        aborted {
            echo "🛑 Pipeline was aborted!"
        }
    }
}