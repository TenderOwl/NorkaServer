using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Norka.API.Data;
using Norka.API.Entities;
using Norka.API.Services;
using Norka.API.Extensions;

namespace Norka.API;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);

        var config = builder.Configuration;
        config.AddEnvironmentVariables();

        builder.Services.AddControllers().AddJsonOptions(
            options => options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles
        );
        builder.Services.AddSignalR().AddJsonProtocol(options =>
        {
            options.PayloadSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        });
        builder.Services.AddEndpointsApiExplorer();

        // Custom Swagger UI
        builder.Services.AddUptimedSwagger();

        builder.Services.AddProblemDetails();
        builder.Services.AddRouting(options => options.LowercaseUrls = true);

        // Add DB Contexts
        // Move the connection string to user secrets for release
        builder.Services.AddDbContext<NorkaDbContext>(opt =>
            opt.UseSqlite(builder.Configuration.GetConnectionString("NorkaContext")));

        // Register our TokenService dependency
        builder.Services.AddScoped<TokenService, TokenService>();

        // Specify identity requirements
        // Must be added before .AddAuthentication otherwise a 404 is thrown on authorized endpoints
        builder.Services
            .AddIdentity<ApplicationUser, UptimedRole>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.User.RequireUniqueEmail = true;
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 6;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
            })
            .AddRoles<UptimedRole>()
            .AddEntityFrameworkStores<NorkaDbContext>();

        // Add custom authentication through JWT
        builder.Services.AddUptimedAuthentication(config);

        // Build the app
        var app = builder.Build();

        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // app.UseHttpsRedirection();
        app.UseStatusCodePages();

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        app.Run();
    }
}