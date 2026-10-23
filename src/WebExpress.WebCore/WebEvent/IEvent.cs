namespace WebExpress.WebCore.WebEvent
{
    /// <summary>
    /// Marks a type as an event that can be raised within the framework. Components register
    /// handlers (<see cref="IEventHandler"/>) against such events to react when they occur,
    /// allowing loosely coupled communication between parts of an application.
    /// </summary>
    public interface IEvent
    {
    }
}
