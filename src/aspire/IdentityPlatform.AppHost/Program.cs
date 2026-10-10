using IdentityPlatform.AppHost.Composition;

var app = DistributedApplication.CreateBuilder(args)
    .AddConfigurations()
    .ConfigureServices()
    .Build();

app.Run();
