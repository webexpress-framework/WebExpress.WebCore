using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WebExpress.WebCore.WebTask
{
    /// <summary>
    /// Represents a task that can be executed asynchronously.
    /// </summary>
    public class Task : ITask
    {
        private int _progress;
        private string _message;

        /// <summary>
        /// Gets or sets the host lifetime assigned by the task manager before execution.
        /// </summary>
        internal ServerLifetime Lifetime { get; set; } = new(null);

        /// <summary>
        /// Event is triggered when the task is executed.
        /// </summary>
        public event EventHandler<TaskEventArgs> Process;

        /// <summary>
        /// Event is triggered when the task is terminated.
        /// </summary>
        public event EventHandler<TaskEventArgs> Finish;

        /// <summary>
        /// Event is triggered when the progress changes.
        /// </summary>
        public event EventHandler<TaskEventArgs> ProgressChanged;

        /// <summary>
        /// Event is triggered when the message changes.
        /// </summary>
        public event EventHandler<TaskEventArgs> MessageChanged;

        /// <summary>
        /// Gets the id of the task.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Gets the state in which the task is located.
        /// </summary>
        public TaskState State { get; protected set; } = TaskState.Created;

        /// <summary>
        /// Gets the arguments.
        /// </summary>
        public ICollection<object> Arguments { get; private set; }

        /// <summary>
        /// Gets the thread termination of the task.
        /// </summary>
        private CancellationTokenSource TokenSource { get; } = new CancellationTokenSource();

        /// <summary>
        /// Gets the progress of the task. The value range is from 0 to 100.
        /// </summary>
        public int Progress
        {
            get { return _progress; }
            set
            {
                var newValue = Math.Min(value, 100);
                if (_progress != newValue)
                {
                    _progress = newValue;
                    // trigger progress changed event
                    OnProgressChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets a message that provides information about the processing.
        /// </summary>
        public string Message
        {
            get { return _message; }
            set
            {
                if (_message != value)
                {
                    _message = value;
                    // trigger message changed event
                    OnMessageChanged();
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="id">The unique identifier for the task.</param>
        /// <param name="args">The arguments for the task.</param>
        public Task(string id, params object[] args)
        {
            Id = id;
            Arguments = args;
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        protected virtual void OnProcess()
        {
            Process?.Invoke(this, new TaskEventArgs(this, 0));
        }

        /// <summary>
        /// Triggered when the task is complete.
        /// </summary>
        protected virtual void OnFinish()
        {
            Finish?.Invoke(this, new TaskEventArgs(this, 100));
        }

        /// <summary>
        /// Triggered when the progress changes.
        /// </summary>
        protected virtual void OnProgressChanged()
        {
            ProgressChanged?.Invoke(this, new TaskEventArgs(this, Progress));
        }

        /// <summary>
        /// Triggered when the message changes.
        /// </summary>
        protected virtual void OnMessageChanged()
        {
            MessageChanged?.Invoke(this, new TaskEventArgs(this, Progress, Message));
        }

        /// <summary>
        /// Starts the execution concurrently.
        /// </summary>
        public void Run()
        {
            if (!Lifetime.TryRun(() =>
            {
                if (TokenSource.IsCancellationRequested)
                {
                    return;
                }

                State = TaskState.Run;

                Progress = 0;

                OnProcess();

                Progress = 100;

                State = TaskState.Finish;

                OnFinish();

                _ = ScheduleRemovalAsync(Lifetime.Stopping);
            }))
            {
                State = TaskState.Canceled;
            }
        }

        /// <summary>
        /// Abandonment of an existing processing.
        /// </summary>
        public void Cancel()
        {
            TokenSource.Cancel();

            State = TaskState.Canceled;

            WebEx.ComponentHub.TaskManager.RemoveTask(this);
        }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            Cancel();
        }

        /// <summary>
        /// Schedules the removal of the current task after a delay.
        /// </summary>
        /// <param name="token">
        /// A <see cref="CancellationToken"/> that can be used to cancel the scheduled removal.
        /// </param>
        /// <returns>
        /// True if the task was successfully removed after the delay; otherwise, 
        /// false if the operation was canceled.
        /// </returns>
        public async Task<bool> ScheduleRemovalAsync(CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                return false;
            }

            try
            {
                await System.Threading.Tasks.Task.Delay(30000, token);
                WebEx.ComponentHub.TaskManager.RemoveTask(this);
                return true;
            }
            catch (TaskCanceledException)
            {
                return false;
            }
        }

    }
}
