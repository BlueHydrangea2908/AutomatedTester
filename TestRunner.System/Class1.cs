using System.Reactive.Disposables;
using static System.Formats.Asn1.AsnWriter;

namespace TestRunner.System;

public interface IEngine<TFuel, TEnergy>
{
    public Task<TEnergy> Start(TFuel fuel);
    public Task Stop();
}

public interface IDebugger<TExecutionStatus,TExecutionCommand> : IObserver<TExecutionStatus>, IObservable<TExecutionCommand>
{

}

public abstract class AttachableDebuggerWorkflowExecutionEngine<TInput, TOutput, TInstruction, TWorkflowExecutionStatus, TWorkflowExecutionCommand> : IEngine<TInput, TOutput>, IObserver<TWorkflowExecutionCommand>, IObservable<TWorkflowExecutionStatus>
{
    protected TInstruction Instruction { get; private set; }

    public AttachableDebuggerWorkflowExecutionEngine(TInstruction instruction, IDebugger<TWorkflowExecutionStatus, TWorkflowExecutionCommand> debugger)
    {
        this.Instruction = instruction;
        AttachDebugger(debugger);
    }

    private CompositeDisposable _debuggerDetachSwitch = new CompositeDisposable();

    public virtual void AttachDebugger(IDebugger<TWorkflowExecutionStatus, TWorkflowExecutionCommand> debugger)
    {
        this._debuggerDetachSwitch.Add(debugger.Subscribe(this));
        this._debuggerDetachSwitch.Add(this.Subscribe(debugger));
    }

    public virtual void DettachDebugger()
    {
        this._debuggerDetachSwitch.Dispose();
        this._debuggerDetachSwitch.Clear();
    }

    public abstract void OnCompleted();

    public abstract void OnError(Exception error);

    public abstract void OnNext(TWorkflowExecutionCommand command);

    public abstract Task<TOutput> Start(TInput input);

    public abstract Task Stop();

    public abstract IDisposable Subscribe(IObserver<TWorkflowExecutionStatus> observer);
}