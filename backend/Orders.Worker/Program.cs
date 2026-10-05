using Microsoft.EntityFrameworkCore;
using Orders.Infrastructure.Data;
using Orders.Worker;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                       ?? "Host=localhost;Database=ordersdb;Username=user;Password=password";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Registra a nova classe de negócio
builder.Services.AddScoped<OrderProcessor>();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
