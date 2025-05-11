//using System.Threading.Tasks;
//using Xunit;
//using Grpc.Net.Client;
//using Microsoft.AspNetCore.Mvc.Testing;
//using Workflow.Protos;
//using FluentAssertions;
//using Grpc.Core;
////using Microsoft.VisualStudio.TestPlatform.TestHost;
//using TestRunner;
//using Microsoft.AspNetCore.Hosting;

//namespace TestRunner.Tests
//{
//    public class GrpcWorkerTests : IClassFixture<WebApplicationFactory<Program>>
//    {
//        private readonly WebApplicationFactory<Program> _factory;
//        public GrpcWorkerTests(WebApplicationFactory<Program> factory)
//            => _factory = factory;

//        [Fact]
//        public async Task DeployAndStartWorkflow_Should_Return_InstanceId()
//        {
//            // Arrange
//            var httpClient = _factory.CreateDefaultClient();
//            var channel = GrpcChannel.ForAddress(httpClient.BaseAddress!, new() { HttpClient = httpClient });
//            var client = new WorkflowRunner.WorkflowRunnerClient(channel);

//            // Deploy DSL
//            var dslJson = @"
//            {
//              ""Id"": ""MyWorkflow"",
//              ""Version"": 1,
//              ""Steps"": [
//                { ""Id"": ""S1"", ""StepType"": ""Runner.Workflows.InitializeStep, Runner"", ""NextStepId"": ""S2"" },
//                { ""Id"": ""S2"", ""StepType"": ""Runner.Workflows.FinalizeStep, Runner"" }
//              ]
//            }";
//            var deploy = await client.DeployDefinitionAsync(new DefinitionRequest { DefinitionJson = dslJson });
//            deploy.Success.Should().BeTrue();

//            // Act
//            var start = await client.StartWorkflowAsync(new StartRequest
//            {
//                DefinitionId = "MyWorkflow",
//                Version = 1,
//                Payload = new YourDto { TestName = "t1" }
//            });

//            // Assert
//            start.InstanceId.Should().NotBeNullOrEmpty();
//        }

//        [Fact]
//        public async Task DeployAndRunWorkflowStream_Should_Stream_Status()
//        {
//            var httpClient = _factory.CreateDefaultClient();
//            var channel = GrpcChannel.ForAddress(httpClient.BaseAddress!, new() { HttpClient = httpClient });
//            var client = new WorkflowRunner.WorkflowRunnerClient(channel);

//            // Deploy same DSL
//            var dslJson = @"{ ""Id"": ""MyWorkflow"", ""Version"": 1, ""Steps"": [ { ""Id"": ""S1"", ""StepType"": ""Runner.Workflows.InitializeStep, Runner"", ""NextStepId"": ""S2"" },{ ""Id"": ""S2"", ""StepType"": ""Runner.Workflows.FinalizeStep, Runner"" } ] }";
//            await client.DeployDefinitionAsync(new DefinitionRequest { DefinitionJson = dslJson });

//            // Open streaming RPC
//            using var call = client.RunWorkflow();

//            // Send Start
//            await call.RequestStream.WriteAsync(new RunRequest
//            {
//                Start = new StartWorkflow
//                {
//                    DefinitionId = "MyWorkflow",
//                    Version = 1,
//                    Payload = new YourDto { TestName = "t2" }
//                }
//            });

//            // Assert a status update arrives
//            Assert.True(await call.ResponseStream.MoveNext());
//            var status = call.ResponseStream.Current.Status;
//            status.InstanceId.Should().NotBeNullOrEmpty();
//            status.Message.Should().Contain("Step");
//        }
//    }

//    public class GrpcWorkerTestFactory : WebApplicationFactory<Program>
//    {
//        protected override void ConfigureWebHost(IWebHostBuilder builder)
//        {
//            builder.ConfigureServices(services =>
//            {
//                // Remove real database registrations
//                var descriptor = services.SingleOrDefault(
//                    d => d.ServiceType == typeof(DbContextOptions<YourDbContext>));
//                if (descriptor != null)
//                {
//                    services.Remove(descriptor);
//                }

//                // Add in-memory database instead
//                services.AddDbContext<YourDbContext>(options =>
//                {
//                    options.UseInMemoryDatabase("TestDb");
//                });
//            });
//        }
//    }
//}
