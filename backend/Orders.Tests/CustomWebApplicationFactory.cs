using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orders.Infrastructure.Data;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Orders.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // O Testcontainers baixa e roda as imagens limpas apenas para o teste
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("orders_test_db")
        .WithUsername("test_user")
        .WithPassword("test_password")
        .Build();

    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder()
        .WithImage("rabbitmq:3-management-alpine")
        .WithUsername("test_user")
        .WithPassword("test_password")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // 1. Injeta as configurações das portas aleatórias do RabbitMQ para o nosso OutboxPublisher
        builder.ConfigureAppConfiguration((context, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "RabbitMQ:HostName", _rabbitMqContainer.Hostname },
                { "RabbitMQ:Port", _rabbitMqContainer.GetMappedPublicPort(5672).ToString() },
                { "RabbitMQ:UserName", "test_user" },
                { "RabbitMQ:Password", "test_password" }
            });
        });

        builder.ConfigureServices(services =>
        {
            // 2. Remove o banco original da aplicação
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));

            // 3. Adiciona o banco conectando no Postgres efêmero do Testcontainers
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_dbContainer.GetConnectionString()));
        });
    }

    public async Task InitializeAsync()
    {
        // Sobe os containers antes do teste
        await _dbContainer.StartAsync();
        await _rabbitMqContainer.StartAsync();

        // Cria a migration DE FORMA ISOLADA (sem acionar o WebApplicationFactory que liga o BackgroundService)
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_dbContainer.GetConnectionString())
            .Options;
            
        using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        // Destrói os containers após o teste (evita lixo no computador)
        await _dbContainer.DisposeAsync();
        await _rabbitMqContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}
