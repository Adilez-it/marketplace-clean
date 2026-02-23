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
        DOCKER_REGISTRY = credentials('docker-registry-credentials')
        IMAGE_TAG = "${BUILD_NUMBER}-${GIT_COMMIT.take(8)}"
        
        // SonarQube
        SONAR_HOST_URL = 'http://sonarqube:9000'
        SONAR_TOKEN = credentials('sonar-token')
        PROJECT_KEY = 'com.marketplace.marketplace'
        PROJECT_NAME = 'Marketplace Microservices'
        
        // NuGet
        NUGET_RESTORE = 'dotnet restore'
        DOTNET_VERSION = '8.0.x'
        
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
    // Pipeline Options
    // ---------------------------------------------
    options {
        buildDiscarder(logRotator(numToKeepStr: '10', artifactNumToKeepStr: '5'))
        timeout(time: 60, unit: 'MINUTES')
        timestamps()
        ansiColor('xterm')
        disableConcurrentBuilds()
        skipStagesAfterUnstable()
    }

    // ---------------------------------------------
    // Triggers
    // ---------------------------------------------
    triggers {
        pollSCM('H/5 * * * *')  // Poll every 5 minutes
        upstream(upstreamProjects: 'marketplace-shared-library', threshold: hudson.model.Result.SUCCESS)
    }

    // ---------------------------------------------
    // Tools
    // ---------------------------------------------
    tools {
        dotnet "${DOTNET_VERSION}"
        maven 'maven-3.9'
        jdk 'jdk-17'
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
            defaultValue: true,
            description: 'Run SonarQube analysis?'
        )
        booleanParam(
            name: 'DEPLOY',
            defaultValue: false,
            description: 'Deploy after build?'
        )
        string(
            name: 'DEPLOY_ENVIRONMENT',
            defaultValue: 'staging',
            description: 'Deployment environment (staging/production)'
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
                
                checkout([
                    $class: 'GitSCM',
                    branches: [[name: '*/main']],
                    extensions: [
                        [$class: 'RelativeTargetDirectory', relativeTargetDir: 'marketplace'],
                        [$class: 'CloneOption', timeout: 10]
                    ],
                    userRemoteConfigs: [[
                        url: 'https://github.com/your-org/marketplace.git',
                        credentialsId: 'github-credentials'
                    ]]
                ])
                
                script {
                    env.GIT_COMMIT_SHORT = sh(script: 'git rev-parse --short HEAD', returnStdout: true).trim()
                    env.BRANCH_NAME = sh(script: 'git rev-parse --abbrev-ref HEAD', returnStdout: true).trim()
                }
                
                echo """
                ========================================
                Building Marketplace Microservices
                Build #${BUILD_NUMBER}
                Commit: ${GIT_COMMIT_SHORT}
                Branch: ${BRANCH_NAME}
                Configuration: ${params.BUILD_CONFIGURATION}
                ========================================
                """
            }
        }

        // =============================================
        // STAGE 2: Initialize Workspace
        // =============================================
        stage('Initialize') {
            steps {
                dir('marketplace') {
                    // Clean previous builds
                    sh 'dotnet clean'
                    
                    // Restore tools
                    sh 'dotnet tool restore'
                    
                    // Display versions
                    sh '''
                        dotnet --version
                        docker --version
                        docker-compose --version
                    '''
                }
            }
        }

        // =============================================
        // STAGE 3: SonarQube Begin (if enabled)
        // =============================================
        stage('SonarQube Begin') {
            when {
                expression { params.RUN_SONAR }
            }
            steps {
                dir('marketplace') {
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
                                /d:sonar.cpd.exclusions="**/Models/**/*" \
                                /d:sonar.test.inclusions="**/Tests/**/*" \
                                /d:sonar.qualitygate.wait=true \
                                /d:sonar.qualitygate.timeout=300
                        """
                    }
                }
            }
        }

        // =============================================
        // STAGE 4: Restore & Build (Parallel)
        // =============================================
        stage('Restore & Build') {
            parallel {
                stage('Product.API') {
                    steps {
                        dir('marketplace/Product.API') {
                            sh 'dotnet restore'
                            sh "dotnet build -c ${params.BUILD_CONFIGURATION} --no-restore"
                        }
                    }
                }
                stage('Order.API') {
                    steps {
                        dir('marketplace/Order.API') {
                            sh 'dotnet restore'
                            sh "dotnet build -c ${params.BUILD_CONFIGURATION} --no-restore"
                        }
                    }
                }
                stage('Recommendation.API') {
                    steps {
                        dir('marketplace/Recommendation.API') {
                            sh 'dotnet restore'
                            sh "dotnet build -c ${params.BUILD_CONFIGURATION} --no-restore"
                        }
                    }
                }
                stage('ApiGateway') {
                    steps {
                        dir('marketplace/ApiGateway') {
                            sh 'dotnet restore'
                            sh "dotnet build -c ${params.BUILD_CONFIGURATION} --no-restore"
                        }
                    }
                }
            }
        }

        // =============================================
        // STAGE 5: Unit Tests (Parallel)
        // =============================================
        stage('Unit Tests') {
            when {
                expression { params.RUN_TESTS }
            }
            parallel {
                stage('Test Product.API') {
                    steps {
                        dir('marketplace/Tests/Product.API.Tests') {
                            sh """
                                dotnet test -c ${params.BUILD_CONFIGURATION} \
                                    --logger "trx;LogFileName=product-results.trx" \
                                    --collect:"XPlat Code Coverage" \
                                    --results-directory ./TestResults \
                                    --settings coverlet.runsettings \
                                    /p:CollectCoverage=true \
                                    /p:CoverletOutputFormat=opencover
                            """
                        }
                    }
                    post {
                        always {
                            junit allowEmptyResults: true,
                                  testResults: 'marketplace/Tests/Product.API.Tests/TestResults/*.trx'
                        }
                    }
                }
                stage('Test Order.API') {
                    steps {
                        dir('marketplace/Tests/Order.API.Tests') {
                            sh """
                                dotnet test -c ${params.BUILD_CONFIGURATION} \
                                    --logger "trx;LogFileName=order-results.trx" \
                                    --collect:"XPlat Code Coverage" \
                                    --results-directory ./TestResults \
                                    --settings coverlet.runsettings \
                                    /p:CollectCoverage=true \
                                    /p:CoverletOutputFormat=opencover
                            """
                        }
                    }
                    post {
                        always {
                            junit allowEmptyResults: true,
                                  testResults: 'marketplace/Tests/Order.API.Tests/TestResults/*.trx'
                        }
                    }
                }
                stage('Test Recommendation.API') {
                    steps {
                        dir('marketplace/Tests/Recommendation.API.Tests') {
                            sh """
                                dotnet test -c ${params.BUILD_CONFIGURATION} \
                                    --logger "trx;LogFileName=recommendation-results.trx" \
                                    --collect:"XPlat Code Coverage" \
                                    --results-directory ./TestResults \
                                    --settings coverlet.runsettings \
                                    /p:CollectCoverage=true \
                                    /p:CoverletOutputFormat=opencover
                            """
                        }
                    }
                    post {
                        always {
                            junit allowEmptyResults: true,
                                  testResults: 'marketplace/Tests/Recommendation.API.Tests/TestResults/*.trx'
                        }
                    }
                }
                stage('Integration Tests') {
                    steps {
                        dir('marketplace') {
                            sh '''
                                # Start infrastructure for integration tests
                                docker-compose up -d productdb orderdb neo4j redis rabbitmq
                                sleep 30
                                
                                # Run integration tests
                                dotnet test Tests/Integration.Tests/Integration.Tests.csproj \
                                    -c ${params.BUILD_CONFIGURATION} \
                                    --logger "trx;LogFileName=integration-results.trx"
                                
                                # Cleanup
                                docker-compose down
                            '''
                        }
                    }
                    post {
                        always {
                            junit allowEmptyResults: true,
                                  testResults: 'marketplace/Tests/Integration.Tests/TestResults/*.trx'
                        }
                    }
                }
            }
        }

        // =============================================
        // STAGE 6: SonarQube End
        // =============================================
        stage('SonarQube End') {
            when {
                expression { params.RUN_SONAR }
            }
            steps {
                dir('marketplace') {
                    withSonarQubeEnv('SonarQube') {
                        sh """
                            dotnet sonarscanner end \
                                /d:sonar.token="${SONAR_TOKEN}"
                        """
                    }
                }
            }
        }

        // =============================================
        // STAGE 7: Quality Gate
        // =============================================
        stage('Quality Gate') {
            when {
                expression { params.RUN_SONAR }
            }
            steps {
                timeout(time: 5, unit: 'MINUTES') {
                    waitForQualityGate abortPipeline: true
                }
            }
        }

        // =============================================
        // STAGE 8: Publish Test Results & Coverage
        // =============================================
        stage('Publish Reports') {
            steps {
                dir('marketplace') {
                    // Publish test results
                    junit allowEmptyResults: true,
                          testResults: '**/TestResults/*.trx'
                    
                    // Publish code coverage
                    publishHTML([
                        allowMissing: false,
                        alwaysLinkToLastBuild: true,
                        keepAll: true,
                        reportDir: '**/TestResults/**/coverage',
                        reportFiles: 'index.html',
                        reportName: 'Code Coverage Report'
                    ])
                    
                    // Archive artifacts
                    archiveArtifacts artifacts: '**/TestResults/**/*.trx,**/TestResults/**/*.xml,**/logs/*.log',
                                     fingerprint: true,
                                     allowEmptyArchive: true
                }
            }
        }

        // =============================================
        // STAGE 9: Security Scan
        // =============================================
        stage('Security Scan') {
            when {
                branch 'main'
            }
            steps {
                dir('marketplace') {
                    // Run OWASP dependency check
                    dependencyCheckAnalyzer(
                        datadir: 'dependency-check-data',
                        hintsFile: '',
                        includeHtmlReport: true,
                        includeVulnReport: true,
                        outputDir: 'dependency-check-report',
                        scanPath: '.',
                        suppressionFile: 'dependency-check-suppressions.xml'
                    )
                    
                    // Publish security results
                    dependencyCheckPublisher(
                        canComputeNew: false,
                        defaultEncoding: '',
                        pattern: '**/dependency-check-report/dependency-check-report.xml'
                    )
                    
                    // SAST with Roslyn analyzers
                    sh '''
                        dotnet build -c Release /p:RunAnalyzers=true /p:EnforceCodeStyleInBuild=true
                    '''
                }
            }
        }

        // =============================================
        // STAGE 10: Docker Build
        // =============================================
        stage('Docker Build') {
            when {
                anyOf {
                    branch 'main'
                    branch 'develop'
                    expression { params.DEPLOY }
                }
            }
            parallel {
                stage('Build Product Image') {
                    steps {
                        dir('marketplace') {
                            sh """
                                docker build -t ${PRODUCT_IMAGE} \
                                    -t product-api:latest \
                                    --build-arg BUILD_CONFIGURATION=${params.BUILD_CONFIGURATION} \
                                    --build-arg BUILD_NUMBER=${BUILD_NUMBER} \
                                    --build-arg GIT_COMMIT=${GIT_COMMIT_SHORT} \
                                    -f Product.API/Dockerfile .
                            """
                        }
                    }
                }
                stage('Build Order Image') {
                    steps {
                        dir('marketplace') {
                            sh """
                                docker build -t ${ORDER_IMAGE} \
                                    -t order-api:latest \
                                    --build-arg BUILD_CONFIGURATION=${params.BUILD_CONFIGURATION} \
                                    --build-arg BUILD_NUMBER=${BUILD_NUMBER} \
                                    --build-arg GIT_COMMIT=${GIT_COMMIT_SHORT} \
                                    -f Order.API/Dockerfile .
                            """
                        }
                    }
                }
                stage('Build Recommendation Image') {
                    steps {
                        dir('marketplace') {
                            sh """
                                docker build -t ${RECOMMENDATION_IMAGE} \
                                    -t recommendation-api:latest \
                                    --build-arg BUILD_CONFIGURATION=${params.BUILD_CONFIGURATION} \
                                    --build-arg BUILD_NUMBER=${BUILD_NUMBER} \
                                    --build-arg GIT_COMMIT=${GIT_COMMIT_SHORT} \
                                    -f Recommendation.API/Dockerfile .
                            """
                        }
                    }
                }
                stage('Build Gateway Image') {
                    steps {
                        dir('marketplace') {
                            sh """
                                docker build -t ${GATEWAY_IMAGE} \
                                    -t apigateway:latest \
                                    --build-arg BUILD_CONFIGURATION=${params.BUILD_CONFIGURATION} \
                                    --build-arg BUILD_NUMBER=${BUILD_NUMBER} \
                                    --build-arg GIT_COMMIT=${GIT_COMMIT_SHORT} \
                                    -f ApiGateway/Dockerfile .
                            """
                        }
                    }
                }
            }
        }

        // =============================================
        // STAGE 11: Push to Registry
        // =============================================
        stage('Push to Registry') {
            when {
                branch 'main'
            }
            steps {
                script {
                    docker.withRegistry("https://${DOCKER_REGISTRY_URL}", 'docker-registry-credentials') {
                        parallel(
                            "Push Product": {
                                sh "docker tag ${PRODUCT_IMAGE} ${DOCKER_REGISTRY}/${PRODUCT_IMAGE}"
                                sh "docker push ${DOCKER_REGISTRY}/${PRODUCT_IMAGE}"
                            },
                            "Push Order": {
                                sh "docker tag ${ORDER_IMAGE} ${DOCKER_REGISTRY}/${ORDER_IMAGE}"
                                sh "docker push ${DOCKER_REGISTRY}/${ORDER_IMAGE}"
                            },
                            "Push Recommendation": {
                                sh "docker tag ${RECOMMENDATION_IMAGE} ${DOCKER_REGISTRY}/${RECOMMENDATION_IMAGE}"
                                sh "docker push ${DOCKER_REGISTRY}/${RECOMMENDATION_IMAGE}"
                            },
                            "Push Gateway": {
                                sh "docker tag ${GATEWAY_IMAGE} ${DOCKER_REGISTRY}/${GATEWAY_IMAGE}"
                                sh "docker push ${DOCKER_REGISTRY}/${GATEWAY_IMAGE}"
                            }
                        )
                    }
                }
            }
        }

        // =============================================
        // STAGE 12: Deploy
        // =============================================
        stage('Deploy') {
            when {
                allOf {
                    branch 'main'
                    expression { params.DEPLOY }
                }
            }
            steps {
                script {
                    // Select deployment environment
                    def composeFile = params.DEPLOY_ENVIRONMENT == 'production' 
                        ? 'docker-compose.prod.yml' 
                        : 'docker-compose.yml'
                    
                    dir('marketplace') {
                        sh """
                            # Pull latest images
                            docker-compose -f ${composeFile} pull
                            
                            # Stop existing containers
                            docker-compose -f ${composeFile} down --remove-orphans
                            
                            # Start services
                            docker-compose -f ${composeFile} up -d
                            
                            # Wait for services to be healthy
                            echo "Waiting for services to be healthy..."
                            sleep 30
                            
                            # Run smoke tests
                            curl -f --retry 5 --retry-delay 5 http://localhost:8000/health
                            curl -f --retry 5 --retry-delay 5 http://localhost:8001/health
                            curl -f --retry 5 --retry-delay 5 http://localhost:8004/health
                            curl -f --retry 5 --retry-delay 5 http://localhost:8005/health
                            
                            echo "Deployment successful!"
                        """
                    }
                }
            }
        }

        // =============================================
        // STAGE 13: Performance Tests
        // =============================================
        stage('Performance Tests') {
            when {
                branch 'main'
            }
            steps {
                dir('marketplace') {
                    sh '''
                        # Run k6 performance tests
                        docker run --rm -i \
                            -v $(pwd)/Tests/Performance:/scripts \
                            grafana/k6 run /scripts/load-test.js
                    '''
                }
            }
        }

        // =============================================
        // STAGE 14: Cleanup
        // =============================================
        stage('Cleanup') {
            steps {
                dir('marketplace') {
                    // Clean Docker resources
                    sh '''
                        docker system prune -f
                        docker image prune -f
                        docker container prune -f
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
    }

    // ---------------------------------------------
    // Post-Build Actions
    // ---------------------------------------------
    post {
        always {
            script {
                // Send notifications
                def recipient = 'devops@company.com'
                def subject = "${currentBuild.result}: ${env.JOB_NAME} - Build #${BUILD_NUMBER}"
                def body = """
                    <h2>Build ${currentBuild.result}</h2>
                    <ul>
                        <li><b>Project:</b> Marketplace Microservices</li>
                        <li><b>Build:</b> <a href="${env.BUILD_URL}">#${BUILD_NUMBER}</a></li>
                        <li><b>Branch:</b> ${env.BRANCH_NAME}</li>
                        <li><b>Commit:</b> ${env.GIT_COMMIT_SHORT}</li>
                        <li><b>Duration:</b> ${currentBuild.durationString}</li>
                        <li><b>Tests:</b> ${currentBuild.testSummary ?: 'N/A'}</li>
                        <li><b>Coverage:</b> ${currentBuild.coverageReport ?: 'N/A'}</li>
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
                    <p>Check <a href="${env.BUILD_URL}/console">console output</a> for details.</p>
                """
                
                emailext(
                    to: recipient,
                    subject: subject,
                    body: body,
                    mimeType: 'text/html',
                    attachmentsPattern: '**/TestResults/**/*.trx,**/TestResults/**/*.xml'
                )
            }
            
            // Archive additional artifacts
            archiveArtifacts artifacts: 'marketplace/logs/**/*.log',
                             fingerprint: true,
                             allowEmptyArchive: true
        }
        
        success {
            echo "✅ Pipeline completed successfully!"
            
            // Update GitHub status
            step([
                $class: 'GitHubCommitStatusSetter',
                reposSource: [$class: "ManuallyEnteredRepositorySource", url: "https://github.com/your-org/marketplace"],
                contextSource: [$class: "ManuallyEnteredCommitContextSource", context: "ci/jenkins/build-status"],
                statusResultSource: [
                    $class: "ConditionalStatusResultSource",
                    results: [[
                        condition: [$class: "BetterThanOrEqualBuildResult", result: "SUCCESS"],
                        state: "SUCCESS",
                        message: "Build succeeded"
                    ]]
                ]
            ])
        }
        
        failure {
            echo "❌ Pipeline failed!"
            
            // Notify Slack
            slackSend(
                color: 'danger',
                message: "Failed: ${env.JOB_NAME} #${BUILD_NUMBER} (<${env.BUILD_URL}|Open>)",
                channel: '#devops-alerts'
            )
        }
        
        unstable {
            echo "⚠️ Pipeline is unstable!"
        }
        
        aborted {
            echo "🛑 Pipeline was aborted!"
        }
        
        cleanup {
            // Always run cleanup
            echo "Cleaning up workspace..."
        }
    }
}