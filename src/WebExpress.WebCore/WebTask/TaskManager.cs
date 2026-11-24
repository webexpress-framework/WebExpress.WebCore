using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebTask.Model;

namespace WebExpress.WebCore.WebTask
{
    /// <summary>
    /// Management of ad-hoc tasks.
    /// </summary>
    public class TaskManager : ITaskManager, ISystemComponent
    {
        private readonly IComponentHub _componentHub;
        private readonly IHttpServerContext _httpServerContext;
        private readonly TaskDictionary _dictionary = new();

        // serializes the creation of a task, so two requests asking for one id get the same task
        // and its handlers are subscribed exactly once
        private readonly System.Threading.Lock _lock = new();

        /// <summary>
        /// Event is triggered when a task's changes.
        /// </summary>
        public event EventHandler<TaskEventArgs> TaskChanged;

        /// <summary>
        /// Gets the collection of tasks.
        /// </summary>
        public IEnumerable<ITask> Tasks => [.. _dictionary.Values];

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The reference to the context of the host.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via Reflection.")]
        private TaskManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _componentHub = componentHub;
            _httpServerContext = httpServerContext;

            _httpServerContext?.Log?.Debug
            (
                I18N.Translate("webexpress.webcore:applicationmanager.initialization")
            );
        }

        /// <summary>
        /// Checks if a task has already been created.
        /// </summary>
        /// <param name="id">The id of the task.</param>
        /// <returns>True if this task already exists, false otherwise.</returns>
        public bool ContainsTask(string id)
        {
            return id is not null && _dictionary.ContainsKey(id);
        }

        /// <summary>
        /// Returns an existing task.
        /// </summary>
        /// <param name="id">The id of the task.</param>
        /// <returns>The task or null.</returns>
        public ITask GetTask(string id)
        {
            return id is not null && _dictionary.TryGetValue(id, out var task) ? task : null;
        }

        /// <summary>
        /// Creates a new task or returns an existing task.
        /// </summary>
        /// <param name="id">The id of the task.</param>
        /// <param name="args">The event argument.</param>
        /// <returns>The task or null.</returns>
        public ITask CreateTask(string id, params object[] args)
        {
            ArgumentNullException.ThrowIfNull(id);

            lock (_lock)
            {
                if (_dictionary.TryGetValue(id, out var value))
                {
                    return value;
                }

                var task = ComponentActivator.CreateInstance<Task>(_httpServerContext, _componentHub, [id, args]);
                task.Lifetime = _httpServerContext.Lifetime;

                // register events for the newly created task
                SubscribeTaskEvents(task);

                _dictionary[id] = task;

                return task;
            }
        }

        /// <summary>
        /// Creates a new task or returns an existing task.
        /// </summary>
        /// <param name="id">The id of the task.</param>
        /// <param name="handler">The event handler.</param>
        /// <param name="args">The event argument.</param>
        /// <returns>The task or null.</returns>
        public ITask CreateTask(string id, EventHandler<TaskEventArgs> handler, params object[] args)
        {
            return CreateTask<Task>(id, handler, args);
        }

        /// <summary>
        /// Creates a new task or returns an existing task.
        /// </summary>
        /// <param name="id">The id of the task.</param>
        /// <param name="handler">The event handler.</param>
        /// <param name="args">The event argument.</param>
        /// <typeparam name="TTask">The type of the task.</typeparam>
        /// <returns>The task or null.</returns>
        public ITask CreateTask<TTask>(string id, EventHandler<TaskEventArgs> handler, params object[] args)
            where TTask : Task
        {
            ArgumentNullException.ThrowIfNull(id);

            lock (_lock)
            {
                if (_dictionary.TryGetValue(id, out var value))
                {
                    return value;
                }

                var task = ComponentActivator.CreateInstance<TTask>(_httpServerContext, _componentHub, [id, args]);
                task.Lifetime = _httpServerContext.Lifetime;

                // register events for the newly created task
                SubscribeTaskEvents(task);

                _dictionary[id] = task;

                task.Process += handler;

                return task;
            }
        }

        /// <summary>
        /// Removes a task.
        /// </summary>
        /// <param name="task">The task.</param>
        public void RemoveTask(ITask task)
        {
            if (task?.Id is null)
            {
                return;
            }

            // only the stored instance is removed, never a newer task that took over the id
            if (_dictionary.TryRemove(new KeyValuePair<string, ITask>(task.Id, task)) && task is Task t)
            {
                // unregister events to prevent memory leaks
                UnsubscribeTaskEvents(t);
            }
        }

        /// <summary>
        /// Release of unmanaged resources reserved during use.
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Handles the changed event from a task.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnTaskChanged(object sender, TaskEventArgs e)
        {
            TaskChanged?.Invoke(sender, e);
        }

        /// <summary>
        /// Subscribes the manager to every task event relevant to downstream
        /// consumers (progress, message, start, finish) so a single
        /// <see cref="TaskChanged"/> notification covers the complete task
        /// lifecycle.
        /// </summary>
        private void SubscribeTaskEvents(Task task)
        {
            task.ProgressChanged += OnTaskChanged;
            task.MessageChanged += OnTaskChanged;
            task.Process += OnTaskChanged;
            task.Finish += OnTaskChanged;
        }

        /// <summary>
        /// Removes every task event subscription registered by
        /// <see cref="SubscribeTaskEvents"/>.
        /// </summary>
        private void UnsubscribeTaskEvents(Task task)
        {
            task.ProgressChanged -= OnTaskChanged;
            task.MessageChanged -= OnTaskChanged;
            task.Process -= OnTaskChanged;
            task.Finish -= OnTaskChanged;
        }
    }
}
