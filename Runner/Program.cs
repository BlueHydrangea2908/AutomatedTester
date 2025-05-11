using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WorkflowCore.Interface;
//using WorkflowCore.DSL;                  // for AddWorkflowDSL()
using TestRunner.Services;              // your gRPC services namespace

public class Program
{
    public static void Main(string[] args)
        => CreateHostBuilder(args).Build().Run();

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostCtx, config) =>
            {
                // Load CLI args last (override JSON & env)
                config.AddCommandLine(args);
            })
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.ConfigureKestrel((context, opts) =>
                {
                    // Read Kestrel port(s) from config
                    var port = context.Configuration.GetValue<int>("Grpc:Port", 5000);
                    opts.ListenAnyIP(port, lo => lo.Protocols = HttpProtocols.Http2);
                });

                webBuilder.ConfigureServices((context, services) =>
                {
                    var cfg = context.Configuration;

                    // 1) Workflow-Core
                    services.AddWorkflow(workflow =>
                    {
                        // Decision: in-memory vs. SQL/Redis comes from config
                        if (cfg.GetValue<bool>("Workflow:UseInMemory", false))
                        {
                            // no extra calls → defaults to in-memory persistence & single-node queue
                        }
                        else
                        {
                            workflow.UseSqlServer(
                                cfg.GetConnectionString("WorkflowSql"),
                                true,
                                false
                            );
                            var redisConn = cfg.GetValue<string>("Redis:Connection")!;
                            workflow.UseRedisQueues(redisConn, "TestRunner");
                            workflow.UseRedisLocking(redisConn);
                            workflow.UseRedisEventHub(redisConn, "TestRunner_Events");
                        }

                        // control parallelism from config
                        workflow.UseMaxConcurrentWorkflows(
                            cfg.GetValue<int>("Workflow:Parallelism", 4)
                        );
                    })
                    .AddWorkflowDSL();  // enable JSON/YAML DSL

                    // 2) gRPC services
                    services.AddGrpc();
                    services.AddSingleton<WorkflowRunnerGrpcService>();
                    services.AddSingleton<WorkflowRunnerService>();
                });

                webBuilder.Configure(app =>
                {
                    // Kick off the workflow host (pollers, etc.)
                    app.ApplicationServices.GetRequiredService<IWorkflowHost>().Start();

                    // 3) Endpoint routing & gRPC mappings
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGrpcService<WorkflowRunnerGrpcService>();
                        endpoints.MapGrpcService<WorkflowRunnerService>();
                    });
                });
            });
}
