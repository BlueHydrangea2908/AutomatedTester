using Grpc.Core;
using WorkflowCore.Interface;
using Workflow.Protos;
using System.Text.Json;
using Google.Protobuf.WellKnownTypes;
using WorkflowCore.Services.DefinitionStorage;

namespace TestRunner.Services
{
    public class WorkflowRunnerGrpcService : WorkflowRunner.WorkflowRunnerBase
    {
        private readonly IDefinitionLoader _loader;
        private readonly IWorkflowHost _host;

        public WorkflowRunnerGrpcService(IDefinitionLoader loader,
                                         IWorkflowHost host)
        {
            _loader = loader;
            _host = host;
        }

        public override Task<DefinitionReply> DeployDefinition(
            DefinitionRequest request,
            ServerCallContext context)
        {
            try
            {
                // 1) Load & register the JSON definition
                _loader.LoadDefinition(request.DefinitionJson, Deserializers.Json);

                // 2) Optionally start the host if not already running
                _host.Start();

                return Task.FromResult(new DefinitionReply { Success = true });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new DefinitionReply
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        public override async Task<StartReply> StartWorkflow(StartRequest request, ServerCallContext context)
        {
            // Kick off a simple start-and-forget workflow
            var id = await _host.StartWorkflow(
                request.DefinitionId,
                request.Version,
                request.Payload
            );
            return new StartReply { InstanceId = id };
        }
    }
}
