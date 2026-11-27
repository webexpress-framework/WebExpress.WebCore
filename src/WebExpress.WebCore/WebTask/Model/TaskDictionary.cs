using System;
using System.Collections.Concurrent;

namespace WebExpress.WebCore.WebTask.Model
{
    /// <summary>
    /// Directory with the current tasks.
    /// Key = The task id.
    /// Value = The task.
    /// </summary>
    /// <remarks>
    /// Tasks are created and removed from request threads at the same time; a plain dictionary
    /// written concurrently can throw or loop forever. Ids compare ordinally ignoring case, which
    /// does not depend on the culture of the thread the way lowering the id did.
    /// </remarks>
    internal class TaskDictionary : ConcurrentDictionary<string, ITask>
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public TaskDictionary()
            : base(StringComparer.OrdinalIgnoreCase)
        {
        }
    }
}
