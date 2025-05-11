using Grpc.Core;
using WorkflowCore.Interface;
using Workflow.Protos;
using WorkflowCore.Models.LifeCycleEvents;

public class WorkflowRunnerService : WorkflowRunner.WorkflowRunnerBase
{
    private readonly IWorkflowHost _host;
    public WorkflowRunnerService(IWorkflowHost host) => _host = host;

    public override async Task RunWorkflow(
        IAsyncStreamReader<RunRequest> requestStream,
        IServerStreamWriter<RunResponse> responseStream,
        ServerCallContext context)
    {
        string? instanceId = null;
        var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);

        // 1) Handle incoming Start + Control messages
        var readerTask = Task.Run(async () =>
        {
            await foreach (var msg in requestStream.ReadAllAsync(cts.Token))
            {
                if (msg.PayloadCase == RunRequest.PayloadOneofCase.Start)
                {
                    instanceId = await _host.StartWorkflow(
                        msg.Start.DefinitionId,
                        msg.Start.Version,
                        msg.Start.Payload
                    );
                }
                else if (msg.PayloadCase == RunRequest.PayloadOneofCase.Control)
                {
                    var ctrl = msg.Control;
                    if (!string.IsNullOrEmpty(ctrl.InstanceId))
                    {
                        switch (ctrl.Type)
                        {
                            case ControlType.Pause:
                                _host.SuspendWorkflow(ctrl.InstanceId);
                                break;
                            case ControlType.Resume:
                                _host.ResumeWorkflow(ctrl.InstanceId);
                                break;
                            case ControlType.Cancel:
                                _host.TerminateWorkflow(ctrl.InstanceId);
                                break;
                            case ControlType.Heartbeat:
                                // no-op or ack
                                break;
                        }
                    }
                }
            }
        }, cts.Token);

        // 2) Handle Step Errors
        _host.OnStepError += async (wf, step, ex) =>
        {
            await responseStream.WriteAsync(new RunResponse
            {
                Error = new StepError
                {
                    InstanceId = wf.Id,
                    StepId = step.Id.ToString(),
                    Error = ex.Message
                }
            });
        };

        // 3) Handle LifeCycle Events
        _host.OnLifeCycleEvent += async evt =>
        {
            switch (evt)
            {
                case StepCompleted sc:
                    await responseStream.WriteAsync(new RunResponse
                    {
                        Status = new WorkflowStatus
                        {
                            InstanceId = sc.WorkflowInstanceId,    // ← use this
                            CurrentStep = sc.StepId.ToString(),
                            Message = "Step executed"
                        }
                    });
                    break;

                case WorkflowCompleted wc:
                    await responseStream.WriteAsync(new RunResponse
                    {
                        Complete = new WorkflowComplete
                        {
                            InstanceId = wc.WorkflowInstanceId     // ← and this
                        }
                    });
                    break;

                    // …other event types if you need them…
            }
        };

        // 3) Wait for cancellation or readerTask to end
        await Task.WhenAny(readerTask, Task.Delay(Timeout.Infinite, cts.Token));
        cts.Cancel();

        // 4) Unsubscribe
        _host.OnStepError -= null!;
        _host.OnLifeCycleEvent -= null!;
    }
}
