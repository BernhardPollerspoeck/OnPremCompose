# 🚀 DeploymentRunner

A modern, containerized service for centralized Docker Compose deployment management with real-time monitoring and web interface.

## 🎯 Features

- **Minimal API** for CI/CD integration
- **Blazor Server** web interface with real-time updates
- **SignalR** for live logs and deployment status
- **Multi-Engine Support** for local and remote Docker engines
- **Email Notifications** for deployment events
- **API Key Management** for secure automation
- **Health Monitoring** with automatic alerts
- **One-Click Installation** via Docker container

## 🏗️ Architecture

```mermaid
graph TB
    subgraph "Host Server"
        DS[Docker Socket<br/>/var/run/docker.sock]
        DC1[Docker Compose App 1]
        DC2[Docker Compose App 2]
        DC3[Docker Compose App 3]
        ENV[Environment Files<br/>.env, configs]
    end

    subgraph "Deployment Runner Container"
        subgraph "ASP.NET Core App"
            API[Minimal APIs<br/>- Deploy endpoints<br/>- Status endpoints<br/>- Log endpoints]
            
            subgraph "Blazor Server"
                UI[Blazor Pages<br/>- Dashboard<br/>- Service Management<br/>- Log Viewer]
                HUB[SignalR Hub<br/>Real-time Updates]
            end
            
            subgraph "Services"
                DS_SVC[Docker Service<br/>- Execute docker-compose<br/>- Monitor containers<br/>- Health checks]
                FILE_SVC[File Service<br/>- Manage .env files<br/>- Config templates]
                NOTIFY[Notification Service<br/>- Email alerts<br/>- Status updates]
                AUTH[Auth Service<br/>- API Key management<br/>- User management]
            end
            
            subgraph "Background Services"
                MONITOR[Health Monitor<br/>Hosted Service]
                SCHEDULER[Task Scheduler<br/>Quartz.NET]
            end
            
            DB[(SQLite Database<br/>- Users<br/>- API Keys<br/>- Deploy History<br/>- Service Status)]
        end
    end

    subgraph "External Clients"
        CI[CI/CD Pipeline<br/>GitHub Actions<br/>GitLab CI]
        BROWSER[Web Browser<br/>Admin Interface]
        REMOTE[Remote Docker Engines<br/>Optional]
    end

    %% Connections
    CI -->|API Calls<br/>POST /api/deploy| API
    BROWSER -->|HTTPS| UI
    UI ---|WebSocket<br/>Real-time| HUB
    
    API --> DS_SVC
    API --> FILE_SVC
    API --> AUTH
    
    DS_SVC -->|Mount| DS
    DS_SVC --> DC1
    DS_SVC --> DC2
    DS_SVC --> DC3
    
    FILE_SVC --> ENV
    
    MONITOR --> DS_SVC
    MONITOR --> HUB
    NOTIFY --> HUB
    
    SCHEDULER --> MONITOR
    SCHEDULER --> NOTIFY
    
    %% Optional Remote
    DS_SVC -.->|TCP/TLS<br/>Optional| REMOTE
    
    %% Data flow
    AUTH <--> DB
    DS_SVC <--> DB
    MONITOR <--> DB

    classDef container fill:#e1f5fe
    classDef service fill:#f3e5f5
    classDef external fill:#fff3e0
    classDef storage fill:#e8f5e8
    
    class "Deployment Runner Container" container
    class DS_SVC,FILE_SVC,NOTIFY,AUTH,MONITOR,SCHEDULER service
    class CI,BROWSER,REMOTE external
    class DB,DS,ENV storage
```

## 🚀 Quick Start

### Prerequisites

- Docker & Docker Compose installed
- Linux/Windows Server with Docker socket access

### Installation

1. **Download the deployment configuration:**
```bash
curl -o docker-compose.yml https://raw.githubusercontent.com/your-repo/deployment-runner/main/docker/docker-compose.yml
```

2. **Start the service:**
```bash
docker-compose up -d
```

3. **Access the web interface:**
   - Open `http://your-server:5000`
   - Create initial admin user on first login

### CI/CD Integration

```bash
# Deploy via API
curl -X POST http://your-server:5000/api/deploy \
  -H "Authorization: Bearer YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "serviceName": "my-app",
    "composeFile": "version: \"3.8\"\nservices:\n  web:\n    image: nginx:latest",
    "environment": {
      "ENV": "production"
    }
  }'
```

## 📋 API Documentation

### Authentication

All API requests require an API key in the Authorization header:
```
Authorization: Bearer YOUR_API_KEY
```

### Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/deploy` | Deploy or update a service |
| GET | `/api/services` | List all managed services |
| GET | `/api/services/{name}` | Get service details |
| GET | `/api/services/{name}/logs` | Stream service logs |
| DELETE | `/api/services/{name}` | Stop and remove service |
| POST | `/api/services/{name}/restart` | Restart service |
| GET | `/api/health` | Health check endpoint |

## 🔧 Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Environment (Development/Production) | `Production` |
| `ConnectionStrings__DefaultConnection` | Database connection string | SQLite file |
| `Email__SmtpHost` | SMTP server hostname | - |
| `Email__SmtpPort` | SMTP server port | `587` |
| `Email__Username` | SMTP username | - |
| `Email__Password` | SMTP password | - |
| `Docker__SocketPath` | Docker socket path | `/var/run/docker.sock` |
| `Security__JwtSecret` | JWT signing secret | Auto-generated |

### Docker Compose Mount

```yaml
version: '3.8'
services:
  deployment-runner:
    image: deployment-runner:latest
    ports:
      - "5000:80"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro
      - ./data:/app/data
      - ./configs:/app/configs
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - Email__SmtpHost=smtp.gmail.com
      - Email__Username=your-email@gmail.com
      - Email__Password=your-app-password
```

## 🔐 Security Considerations

- **Docker Socket Access**: The container needs access to the Docker socket for container management
- **API Keys**: Generate strong API keys and rotate them regularly
- **Network Security**: Use HTTPS in production and restrict access to trusted networks
- **File Permissions**: Ensure proper file permissions for mounted volumes

## 📊 Monitoring & Alerts

### Health Checks
- Automatic container health monitoring
- Configurable check intervals
- Email notifications for failures

### Logging
- Real-time log streaming via SignalR
- Log retention policies
- Structured logging with Serilog

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🆘 Support

- 📖 [Documentation](./docs/)
- 🐛 [Issue Tracker](https://github.com/your-repo/deployment-runner/issues)
- 💬 [Discussions](https://github.com/your-repo/deployment-runner/discussions)
