using WorkflowCore.Models;
using WorkflowCore.Interface;

namespace TestWorkflowSteps;

public class HelloWorldStep : StepBody
{
    public string Message { get; set; }

    public override ExecutionResult Run(IStepExecutionContext context)
    {
        Console.WriteLine(Message);
        return ExecutionResult.Next();
    }
}