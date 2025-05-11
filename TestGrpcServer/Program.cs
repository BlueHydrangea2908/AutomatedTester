using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkflowCore.Interface;
using WorkflowCore.Services.DefinitionStorage;
using WorkflowGrpc;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(l => l.AddConsole())
    .ConfigureServices((_, svc) => {
        svc.AddWorkflow()                          // adds WorkflowCore :contentReference[oaicite:4]{index=4}
           .AddWorkflowDSL();                      // registers JSON/YAML loader 
        svc.AddSingleton<WorkflowServiceImpl>();
    })
    .Build();

var wfHost = host.Services.GetRequiredService<IWorkflowHost>();
wfHost.Start();                                // start engine polling

var server = new Server
{
    Services = { WorkflowService.BindService(
        host.Services.GetRequiredService<WorkflowServiceImpl>()) },
    Ports = { new ServerPort("localhost", 6000, ServerCredentials.Insecure) }
};
server.Start();
Console.WriteLine("Listening on 6000");
Console.ReadLine();
await server.ShutdownAsync();
wfHost.Stop();

public class WorkflowServiceImpl : WorkflowService.WorkflowServiceBase
{
    private readonly IWorkflowHost _host;
    private readonly IDefinitionLoader _loader;
    private readonly Dictionary<(string, int), string> _defs = new();

    public WorkflowServiceImpl(IWorkflowHost h, IDefinitionLoader l)
    {
        _host = h;
        _loader = l;
    }

    public override Task<UploadReply> UploadWorkflow(UploadWorkflowRequest req, ServerCallContext _)
    {
        // 1) Load the DLL bytes into default context
        if (req.PluginAssembly.Length > 0)
        {
            var asm = Assembly.Load(req.PluginAssembly.ToByteArray());
            Console.WriteLine($"Loaded plugin: {asm.FullName}");      // confirm load :contentReference[oaicite:6]{index=6}
        }

        // 2) Store and register JSON DSL
        _defs[(req.Id, req.Version)] = req.JsonDefinition;
        try
        {
            Assembly pluginAsm = null;
            var asmBytes = req.PluginAssembly.ToByteArray();
            AssemblyLoadContext.Default.Resolving += (alc, name) =>
            {
                if (name.Name == "TestWorkflowSteps")
                {
                    pluginAsm ??= Assembly.Load(asmBytes);
                    return pluginAsm;
                }
                return null;
            };

            // Now call LoadDefinition; Type.GetType will hit your Resolving handler
            _loader.LoadDefinition(req.JsonDefinition, Deserializers.Json);

            //_loader.LoadDefinition(req.JsonDefinition, Deserializers.Json);  // resolves step types via reflection 
            return Task.FromResult(new UploadReply { Success = true });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new UploadReply { Success = false, Error = ex.Message });
        }
    }

    public override async Task<StartReply> StartWorkflow(StartRequest req, ServerCallContext _)
    {
        if (!_defs.ContainsKey((req.WorkflowId, req.Version)))
            return new StartReply { Success = false, Error = "Not found" };

        try
        {
            var id = await _host.StartWorkflow(req.WorkflowId, req.Version, req.JsonData);
            return new StartReply { Success = true, InstanceId = id };
        }
        catch (Exception ex)
        {
            return new StartReply { Success = false, Error = ex.Message };
        }
    }
}
