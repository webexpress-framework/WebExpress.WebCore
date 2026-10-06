using WebExpress.WebCore.WebComponent;

namespace WebExpress.WebCore.WebEvent
{
    /// <summary>
    /// A component that reacts to an event: when the matching <see cref="IEvent"/> is raised, the
    /// framework calls <see cref="Process(object, IEventArgument)"/> with the sender and the event's
    /// argument. Register one to run custom logic in response to something that happens in the system.
    /// </summary>
    public interface IEventHandler : IComponent
    {
        /// <summary>
        /// Process the event.
        /// </summary>
        /// <param name="sender">The object that triggered the event.</param>
        /// <param name="eventArgument">The argument for the event.</param>
        void Process(object sender, IEventArgument eventArgument);
    }

    /// <summary>
    /// Strongly typed variant of <see cref="IEventHandler"/> that receives its event argument as the
    /// concrete type <typeparamref name="T"/>, avoiding a cast in the handler.
    /// </summary>
    public interface IEventHandler<T> : IComponent where T : class, IEventArgument
    {
        /// <summary>
        /// Process the event.
        /// </summary>
        /// <param name="sender">The object that triggered the event.</param>
        /// <param name="eventArgument">The argument for the event.</param>
        void Process(object sender, T eventArgument);
    }
}
