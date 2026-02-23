# 🛒 Marketplace Microservices Project

A complete marketplace platform built with 3 microservices using ASP.NET Core 8, MongoDB, Neo4j, and RabbitMQ.

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    Clients / Web App                     │
└─────────────────────┬───────────────────────────────────┘
                      │ HTTP
┌─────────────────────▼───────────────────────────────────┐
│              API Gateway (YARP) :8000                    │
└──────┬───────────────────┬──────────────────┬───────────┘
       │                   │                  │
┌──────▼──────┐   ┌────────▼────────┐  ┌─────▼──────────────┐
│ Product API │   │   Order API     │  │ Recommendation API  │
│   :8001     │   │    :8004        │  │      :8005          │
│  MongoDB    │   │   MongoDB       │  │      Neo4j          │
└──────┬──────┘   └────────┬────────┘  └─────┬──────────────┘
       │                   │                  │
       └───────────────────▼──────────────────┘
                    RabbitMQ :5672
                 (marketplace.events)
```

## 📦 Microservices

| Service | Port | Database | Responsibilities |
|---------|------|----------|-----------------|
| Product API | 8001 | MongoDB | Catalog, search, stock management |
| Order API | 8004 | MongoDB | Order workflow, payment processing |
| Recommendation API | 8005 | Neo4j | Personalized recommendations, collaborative filtering |
| API Gateway | 8000 | - | YARP reverse proxy |

## 🚀 Quick Start

### Prerequisites
- Docker >= 24.0
- Docker Compose >= 2.20

### Deploy

```bash
# Clone the repository
git clone https://github.com/your-org/marketplace.git
cd marketplace

# Build all images
docker-compose build

# Start databases and message broker
docker-compose up -d productdb orderdb neo4j redis rabbitmq

# Wait 30 seconds for services to be ready
sleep 30

# Start microservices
docker-compose up -d product.api order.api recommendation.api apigateway

# Verify health
curl http://localhost:8001/health   # Product Service
curl http://localhost:8004/health   # Order Service
curl http://localhost:8005/health   # Recommendation Service
curl http://localhost:8000/health   # API Gateway
```

### Management UIs

| Tool | URL | Credentials |
|------|-----|-------------|
| API Gateway | http://localhost:8000 | - |
| Product Swagger | http://localhost:8001/swagger | - |
| Order Swagger | http://localhost:8004/swagger | - |
| Recommendation Swagger | http://localhost:8005/swagger | - |
| Neo4j Browser | http://localhost:7474 | neo4j/password123 |
| RabbitMQ Management | http://localhost:15672 | guest/guest |
| Jenkins | http://localhost:8080 | admin (set on first login) |
| SonarQube | http://localhost:9000 | admin/admin |
| Portainer | http://localhost:9443 | - |

## 📡 API Endpoints

### Product Service (`/api/products`)
- `GET /api/products` - List all products (with ?category= or ?search= filters)
- `GET /api/products/{id}` - Get product by ID
- `POST /api/products` - Create product
- `PUT /api/products/{id}` - Update product
- `DELETE /api/products/{id}` - Delete product
- `POST /api/products/{id}/view` - Record product view
- `POST /api/products/{id}/decrement-stock` - Decrement stock
- `POST /api/products/batch` - Get products by IDs (internal)

### Order Service (`/api/orders`)
- `GET /api/orders` - List all orders
- `GET /api/orders/{id}` - Get order by ID
- `GET /api/orders/user/{userId}` - Get user's orders
- `GET /api/orders/{id}/tracking` - Get order tracking
- `POST /api/orders` - Create order
- `PUT /api/orders/{id}/status` - Update order status
- `DELETE /api/orders/{id}` - Cancel order

### Recommendation Service (`/api/recommendations`)
- `GET /api/recommendations/{userId}` - Personalized recommendations
- `GET /api/recommendations/similar/{productId}` - Similar products
- `GET /api/recommendations/trending` - Trending products
- `GET /api/recommendations/history/{userId}` - User purchase history
- `POST /api/recommendations/view` - Record view
- `POST /api/recommendations/purchase` - Record purchase

## 📨 RabbitMQ Events

| Event | Publisher | Consumer(s) |
|-------|-----------|-------------|
| `product.created` | Product API | - |
| `product.viewed` | Product API | Recommendation API |
| `stock.updated` | Product API | - |
| `order.created` | Order API | Recommendation API |
| `order.statuschan ged` | Order API | - |
| `order.cancelled` | Order API | - |

## 🗄️ Neo4j Cypher Queries

```cypher
-- Get personalized recommendations
MATCH (u:User {userId: $userId})-[:PURCHASED]->(p:Product)
      <-[:PURCHASED]-(similar:User)-[:PURCHASED]->(rec:Product)
WHERE NOT (u)-[:PURCHASED]->(rec)
WITH rec, COUNT(DISTINCT similar) as popularity
ORDER BY popularity DESC LIMIT 10
RETURN rec

-- Get trending products (last 7 days)
MATCH (u:User)-[r:PURCHASED]->(p:Product)
WHERE r.purchaseDate >= datetime() - duration({days: 7})
WITH p, COUNT(r) as recentPurchases
ORDER BY recentPurchases DESC LIMIT 10
RETURN p
```

## 🧪 Running Tests

```bash
# Unit tests - Product Service
cd Product.API
dotnet test

# Unit tests - Order Service
cd Order.API
dotnet test

# Unit tests - Recommendation Service
cd Recommendation.API
dotnet test
```

## 📊 Quality Gates (SonarQube)

| Metric | Target |
|--------|--------|
| Code Coverage | ≥ 80% |
| Duplications | < 3% |
| Maintainability Rating | A |
| Reliability Rating | A |
| Security Rating | A |

## 🏛️ Project Structure

```
marketplace/
├── Product.API/          # Product microservice (MongoDB)
├── Order.API/            # Order microservice (MongoDB)
├── Recommendation.API/   # Recommendation microservice (Neo4j)
├── ApiGateway/           # YARP reverse proxy
├── docker-compose.yml    # Full stack deployment
├── Jenkinsfile           # CI/CD pipeline
└── marketplace.postman_collection.json  # API tests
```

## 👥 Team

| Developer | Microservice |
|-----------|-------------|
| Dev 1 | Product Service |
| Dev 2 | Order Service |
| Dev 3 | Recommendation Service |
| Dev 4 | API Gateway & DevOps |

## 📅 Sprint Plan (5 Days)

- **Day 1**: Setup & Foundations (structure, DB contexts, basic models)
- **Day 2**: Business Logic (CRUD operations, services, controllers)
- **Day 3**: Event Integration (RabbitMQ publishers/consumers)
- **Day 4**: Recommendations & Tests (algorithms, performance)
- **Day 5**: Finalization & Demo (integration tests, documentation)
