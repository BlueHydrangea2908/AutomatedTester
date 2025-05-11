using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Grpc.Net.Client;
using WorkflowGrpc;
using Google.Protobuf;
using System;

class Program
{
    static async Task Main()
    {
        // 1) Load step DLL bytes
        var dllBytes = await File.ReadAllBytesAsync(
            @"J:\CompanyProjects\AutomatedTester\TestWorkflowSteps\bin\Debug\net8.0\TestWorkflowSteps.dll"
        );

        using var channel = GrpcChannel.ForAddress("http://localhost:6000");
        var client = new WorkflowService.WorkflowServiceClient(channel);

        // 2) Define the workflow JSON 
        var wfJson = JsonSerializer.Serialize(new
        {
            Id = "DemoWF",
            Version = 1,
            Steps = new[] {
                new {
                    Id       = "s1",
                    Name     = "Hello",
                    StepType = "TestWorkflowSteps.HelloWorldStep, TestWorkflowSteps",
                    // Wrap in C# double-quotes so Dynamic LINQ sees a string, not a char
                    Inputs   = new { Message = "\"Hello In-Memory!\"" }
                }
            }
        });


        // 3) Send JSON + DLL bytes
        var uploadReq = new UploadWorkflowRequest
        {
            Id = "DemoWF",
            Version = 1,
            JsonDefinition = wfJson,
            PluginAssembly = ByteString.CopyFrom(dllBytes)                      // raw bytes :contentReference[oaicite:2]{index=2}
        };
        var up = await client.UploadWorkflowAsync(uploadReq);
        Console.WriteLine(up.Success ? "Uploaded" : $"Error: {up.Error}");

        // 4) Trigger execution
        var start = await client.StartWorkflowAsync(new StartRequest
        {
            WorkflowId = "DemoWF",
            Version = 1,
            JsonData = "{}"
        });
        Console.WriteLine(start.Success ? $"Started: {start.InstanceId}" : $"Error: {start.Error}");
    }
}
