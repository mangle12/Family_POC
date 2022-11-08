var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

ConfigurationManager configuration = builder.Configuration;

//redis
var section = configuration.GetSection("Redis:Default");

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = section.GetSection("Connection").Value;
});

builder.Services.AddScoped<IDbService, DbService>();
builder.Services.AddScoped<IPromotionService, PromotionService>();

var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
