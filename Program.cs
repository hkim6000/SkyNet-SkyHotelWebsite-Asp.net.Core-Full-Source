using SkyNet;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();

var app = builder.Build();
app.UseMiddleware<IHandler>();

app.UseStaticHttpCurrent();

app.Run();
