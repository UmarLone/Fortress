using Prism.Events;

namespace Fortress.Mobile.Core.EventAggregators
{
    /// <summary>
    /// Event published when the autofill extension enabled status changes
    /// The payload is true if autofill is now enabled, false if disabled
    /// </summary>
    public class AutofillStatusChangedEvent : PubSubEvent<bool>
    {
    }
}
