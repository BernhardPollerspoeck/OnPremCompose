# Build Instructions

## Prerequisites

- .NET 10 SDK (RC or later)
- Docker & Docker Compose
- Git

## Local Development

### 1. Restore Dependencies

```bash
dotnet restore
```

### 2. Run Locally

```bash
cd src/DeploymentRunner
dotnet run
```

The application will be available at `http://localhost:5000`

**Default Credentials:**
- Username: `admin`
- Password: `admin`
- Default API Key: Check logs on first startup

### 3. Database Migrations

The database is automatically created on first run using SQLite.

If you need to create migrations manually:

```bash
cd src/DeploymentRunner
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## Docker Build

### Build the Image

```bash
docker build -t onpremcompose:latest .
```

### Run with Docker Compose

```bash
docker-compose up -d
```

### View Logs

```bash
docker-compose logs -f onpremcompose
```

### Stop the Container

```bash
docker-compose down
```

## Production Deployment

### Option 1: Using Pre-built Image

1. Download the docker-compose.yml from the `docker/` directory:

```bash
curl -o docker-compose.yml https://raw.githubusercontent.com/BernhardPollerspoeck/OnPremCompose/main/docker/docker-compose.yml
```

2. Start the service:

```bash
docker-compose up -d
```

### Option 2: Build from Source

1. Clone the repository:

```bash
git clone https://github.com/BernhardPollerspoeck/OnPremCompose.git
cd OnPremCompose
```

2. Build and start:

```bash
docker-compose up -d --build
```

## Configuration

### Environment Variables

Edit the `docker-compose.yml` file to configure:

- `ASPNETCORE_ENVIRONMENT`: Set to `Production` or `Development`
- `Email__SmtpHost`: SMTP server for notifications
- `Email__SmtpPort`: SMTP port (default: 587)
- `Email__Username`: SMTP username
- `Email__Password`: SMTP password

### Volumes

The following directories are mounted:
- `./data`: SQLite database
- `./deployments`: Docker Compose files for managed services
- `./backups`: Backup files
- `./logs`: Application logs

## API Usage Example

### Deploy a Service

```bash
curl -X POST http://localhost:5000/api/deploy \
  -H "Authorization: Bearer YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "serviceName": "nginx-test",
    "composeFile": "version: '\''3.8'\''\nservices:\n  web:\n    image: nginx:latest\n    ports:\n      - \"8080:80\"",
    "pullImages": true
  }'
```

### List Services

```bash
curl -X GET http://localhost:5000/api/services \
  -H "Authorization: Bearer YOUR_API_KEY"
```

### Get Service Logs

```bash
curl -X GET "http://localhost:5000/api/services/nginx-test/logs?lines=100" \
  -H "Authorization: Bearer YOUR_API_KEY"
```

### Restart Service

```bash
curl -X POST http://localhost:5000/api/services/nginx-test/restart \
  -H "Authorization: Bearer YOUR_API_KEY"
```

### Delete Service

```bash
curl -X DELETE http://localhost:5000/api/services/nginx-test \
  -H "Authorization: Bearer YOUR_API_KEY"
```

## Troubleshooting

### Docker Socket Permission Denied

If you get permission errors accessing the Docker socket, ensure the container has access:

```bash
sudo chmod 666 /var/run/docker.sock
```

Or add the container to the docker group (requires rebuilding with a non-root user).

### Database Issues

Delete the database and restart:

```bash
rm -rf ./data/deploymentrunner.db*
docker-compose restart
```

### View Application Logs

```bash
# Docker logs
docker-compose logs -f

# Application logs
tail -f ./logs/deployment-runner-*.log
```

## Development

### Project Structure

```
OnPremCompose/
├── src/
│   └── DeploymentRunner/
│       ├── Components/          # Blazor UI components
│       │   ├── Layout/         # Layout components
│       │   └── Pages/          # Blazor pages
│       ├── Data/               # EF Core entities and DbContext
│       ├── Models/             # Request/Response models
│       ├── Services/           # Business logic services
│       └── Program.cs          # Application entry point
├── docker/                     # Deployment configs
├── Dockerfile                  # Container build definition
└── docker-compose.yml         # Local development compose file
```

### Adding New Features

1. Create a new branch
2. Make your changes
3. Test locally with `dotnet run`
4. Test with Docker: `docker-compose up --build`
5. Submit a pull request

## Security Notes

- Change the default admin password immediately after first login
- Use strong API keys
- Consider putting OnPremCompose behind a reverse proxy with HTTPS
- Regularly update the Docker image
- Monitor access logs

## Support

For issues and questions, please use the [GitHub Issues](https://github.com/BernhardPollerspoeck/OnPremCompose/issues) page.
