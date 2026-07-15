using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Domain.Models;
using BusinessLayer.Interfaces;
using BusinessLayer.Services;
using BusinessLayer.Services.Embedding;
using BusinessLayer.Models;
using PRN222_assigment2.Hubs;

namespace PRN222_assigment2
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add Razor Pages instead of MVC
            builder.Services.AddRazorPages();
            builder.Services.AddSignalR();

            builder.Services.AddDbContext<Prn222AssigmentContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Bind GeminiSettings từ config
            builder.Services.Configure<GeminiSettings>(
                builder.Configuration.GetSection("GeminiSettings"));

            // Bind MomoSettings từ config
            builder.Services.Configure<MomoSettings>(
                builder.Configuration.GetSection("MomoSettings"));

            // Đăng ký HttpClient cho Gemini
            builder.Services.AddHttpClient("GeminiClient", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            // Đăng ký HttpClient cho HuggingFace Inference API
            builder.Services.AddHttpClient("HuggingFaceClient", client =>
            {
                client.BaseAddress = new Uri("https://api-inference.huggingface.co");
                client.Timeout = TimeSpan.FromSeconds(60);
            });

            // Đăng ký HttpClient cho OpenAI
            builder.Services.AddHttpClient("OpenAIClient", client =>
            {
                client.BaseAddress = new Uri("https://api.openai.com");
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            // Register Repositories
            builder.Services.AddScoped(typeof(DataAccessLayer.Repository.IGenericRepository<>), typeof(DataAccessLayer.Repository.GenericRepository<>));

            // Register Services
            builder.Services.AddSingleton<SimulatedAIEngine>();
            builder.Services.AddScoped<EmailService>();
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IDocumentService, DocumentService>();
            builder.Services.AddScoped<IChatService, ChatService>();
            builder.Services.AddScoped<IGeminiService, GeminiService>();
            builder.Services.AddScoped<IGeminiEmbeddingService, GeminiEmbeddingService>();
            builder.Services.AddScoped<IBenchmarkService, BenchmarkService>();
            builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
            builder.Services.AddScoped<IMomoService, MomoService>();
            builder.Services.AddScoped<EmbeddingProviderFactory>();

            // Configure Authentication với Cookie
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/Account/Login";
                    options.AccessDeniedPath = "/Account/AccessDenied";
                });

            // Authorization
            builder.Services.AddAuthorization();

            var app = builder.Build();

            // Seed database
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider
                    .GetRequiredService<Prn222AssigmentContext>();
                var canConnect = await db.Database.CanConnectAsync();
                if (!canConnect)
                    Console.WriteLine("[DB ERROR] Cannot connect to database!");
                else
                {
                    Console.WriteLine("[DB OK] Database connected successfully.");

                    var adminEmail = builder.Configuration["AdminAccount:Email"] ?? "admin@fpt.edu.vn";
                    var adminPassword = builder.Configuration["AdminAccount:Password"] ?? "123456789";
                    var existingAdmin = db.Users.FirstOrDefault(u => u.Email == adminEmail);
                    if (existingAdmin == null)
                    {
                        using var sha256 = System.Security.Cryptography.SHA256.Create();
                        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(adminPassword));
                        var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

                        var adminUser = new User
                        {
                            Username = "admin",
                            PasswordHash = hashString,
                            FullName = "System Administrator",
                            Email = adminEmail,
                            Role = "Admin"
                        };
                        db.Users.Add(adminUser);
                        await db.SaveChangesAsync();
                        Console.WriteLine($"[SEED] Admin user created with UserId={adminUser.UserId}");
                    }
                    else
                    {
                        Console.WriteLine($"[SEED] Admin user already exists with UserId={existingAdmin.UserId}");
                    }

                    // Seed TestSet
                    if (db.TestSets.Count() < 50)
                    {
                        var subject = db.Subjects.FirstOrDefault(s => s.SubjectCode == "PRN222")
                                   ?? db.Subjects.FirstOrDefault();

                        if (subject != null)
                        {
                            var jsonPath = Path.Combine(builder.Environment.ContentRootPath, "Data", "TestSets_SWP391.json");
                            if (File.Exists(jsonPath))
                            {
                                var jsonString = File.ReadAllText(jsonPath);
                                var testSetsData = System.Text.Json.JsonSerializer.Deserialize<List<TestSet>>(jsonString);
                                if (testSetsData != null)
                                {
                                    foreach (var ts in testSetsData)
                                    {
                                        ts.SubjectId = subject.SubjectId;
                                        ts.CreatedAt = DateTime.UtcNow;
                                        db.TestSets.Add(ts);
                                    }
                                    await db.SaveChangesAsync();
                                    Console.WriteLine($"[SEED] Added {testSetsData.Count} Test Sets for subject {subject.SubjectCode}.");
                                }
                            }
                        }
                    }

                    // Seed PRN212
                    var prn212Subject = db.Subjects.FirstOrDefault(s => s.SubjectCode == "PRN212");
                    if (prn212Subject != null)
                    {
                        var prn212Count = db.TestSets.Count(t => t.SubjectId == prn212Subject.SubjectId);
                        if (prn212Count < 50)
                        {
                            var prn212JsonPath = Path.Combine(builder.Environment.ContentRootPath, "Data", "TestSets_PRN212.json");
                            if (File.Exists(prn212JsonPath))
                            {
                                var jsonString = File.ReadAllText(prn212JsonPath);
                                var rawItems = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.JsonElement>>(jsonString);
                                if (rawItems != null)
                                {
                                    int added = 0;
                                    foreach (var item in rawItems)
                                    {
                                        var question = item.GetProperty("Question").GetString() ?? "";
                                        var groundTruth = item.GetProperty("GroundTruth").GetString() ?? "";
                                        if (!string.IsNullOrWhiteSpace(question))
                                        {
                                            db.TestSets.Add(new TestSet
                                            {
                                                SubjectId = prn212Subject.SubjectId,
                                                Question = question,
                                                GroundTruth = groundTruth,
                                                CreatedAt = DateTime.UtcNow
                                            });
                                            added++;
                                        }
                                    }
                                    await db.SaveChangesAsync();
                                    Console.WriteLine($"[SEED] Added {added} PRN212 Test Questions.");
                                }
                            }
                        }
                    }


                    // Seed default subscription packages
                    var subscriptionService = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
                    await subscriptionService.SeedDefaultPackagesAsync();
                    Console.WriteLine("[SEED] Subscription packages seeding checked/completed.");
                }
            }

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            // Map Razor Pages thay vì MapControllerRoute
            app.MapRazorPages();
            // Map SignalR Hub for real-time chat
            app.MapHub<ChatHub>("/chatHub");

            app.Run();
        }
    }
}
