using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RealEstateProject.Models;
using RealEstateProject.Services;
using System.Text;

namespace RealEstateProject
{
    public class Program
    {
        public static void Main(string[] args)
        {
            

            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
                    options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
                });
            builder.Services.AddHttpClient();
            // Session (if using)
            builder.Services.AddSession();
            builder.Services.AddSignalR();
            builder.Services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
            });
            builder.Services.AddScoped<EmailService>();
            builder.Services.AddHostedService<RentReminderBackgroundService>();
            builder.Services.AddDbContext<RealEstateProjectContext>(options =>
     options.UseSqlServer(
         builder.Configuration.GetConnectionString("conn"),
         sqlOptions =>
         {
             sqlOptions.CommandTimeout(120);
             sqlOptions.EnableRetryOnFailure(
                 maxRetryCount: 5,
                 maxRetryDelay: TimeSpan.FromSeconds(10),
                 errorNumbersToAdd: null);
         }));
            var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]);
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
                .AddJwtBearer(options =>
                {
                    options.RequireHttpsMetadata = false;
                    options.SaveToken = true;

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateIssuerSigningKey = true,
                        ValidateLifetime = true,

                        ValidIssuer = builder.Configuration["Jwt:Issuer"],
                        ValidAudience = builder.Configuration["Jwt:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(key)
                    };
                });
            builder.Services.AddAuthorization();
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAndroid",
                    policy =>
                    {
                        policy.AllowAnyOrigin()
                              .AllowAnyMethod()
                              .AllowAnyHeader();
                    });
            });
            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    ctx.Context.Response.Headers.Append(
                        "Cache-Control", "public,max-age=604800");
                }
            });
            app.UseResponseCompression();
            app.UseRouting();
            app.UseSession(); // if using session
            app.UseCors("AllowAndroid");
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapHub<RealEstateProject.Hubs.NotificationHub>("/notificationHub");
            app.MapControllers();

           
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Account}/{action=Login}/{id?}");

            app.Run();
        }
    }
}
