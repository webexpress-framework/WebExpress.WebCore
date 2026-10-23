namespace WebExpress.WebCore.WebComponent
{
    /// <summary>
    /// The identifier of a component, stored in a normalized (lower-case) form so lookups are
    /// case-insensitive. Implicit conversions to and from <see cref="string"/> let an id be used
    /// wherever a plain string is convenient.
    /// </summary>
    public class ComponentId : IComponentId
    {
        private readonly string _id;

        /// <summary>
        /// Initializes a new instance of the <see cref="ComponentId"/> class with the specified identifier.
        /// </summary>
        /// <param name="id">The identifier of the component.</param>
        public ComponentId(string id)
        {
            _id = id?.ToLower();
        }

        /// <summary>
        /// Defines an implicit conversion of a string to a <see cref="ComponentId"/>.
        /// </summary>
        /// <param name="id">The identifier of the component.</param>
        /// <returns>A new instance of <see cref="ComponentId"/> initialized with the specified identifier.</returns>
        public static implicit operator ComponentId(string id)
        {
            return new ComponentId(id);
        }

        /// <summary>
        /// Defines an implicit conversion of a <see cref="ComponentId"/> to a string.
        /// </summary>
        /// <param name="componentId">The <see cref="ComponentId"/> to convert.</param>
        /// <returns>The identifier of the component as a string.</returns>
        public static implicit operator string(ComponentId componentId)
        {
            return componentId._id;
        }

        /// <summary>
        /// Determines whether two specified <see cref="ComponentId"/> objects have the same value.
        /// </summary>
        /// <param name="lhs">The first <see cref="ComponentId"/> to compare.</param>
        /// <param name="rhs">The second <see cref="IComponentId"/> to compare.</param>
        /// <returns><c>true</c> if the value of <paramref name="lhs"/> is the same as the value of <paramref name="rhs"/>; otherwise, <c>false</c>.</returns>
        public static bool operator ==(ComponentId lhs, IComponentId rhs)
        {
            if (ReferenceEquals(lhs, rhs))
            {
                return true;
            }
            if (lhs is null || rhs is null)
            {
                return false;
            }

            return lhs.ToString().Equals(rhs.ToString());
        }

        /// <summary>
        /// Determines whether two specified <see cref="ComponentId"/> objects have the same value.
        /// </summary>
        /// <param name="lhs">The first <see cref="ComponentId"/> to compare.</param>
        /// <param name="rhs">The second string to compare.</param>
        /// <returns><c>true</c> if the value of <paramref name="lhs"/> is the same as the value of <paramref name="rhs"/>; otherwise, <c>false</c>.</returns>
        public static bool operator ==(ComponentId lhs, string rhs)
        {
            if (ReferenceEquals(lhs, rhs))
            {
                return true;
            }
            if (lhs is null || rhs is null)
            {
                return false;
            }

            return lhs._id == rhs;
        }

        /// <summary>
        /// Determines whether two specified <see cref="ComponentId"/> objects have different values.
        /// </summary>
        /// <param name="lhs">The first <see cref="ComponentId"/> to compare.</param>
        /// <param name="rhs">The second <see cref="ComponentId"/> to compare.</param>
        /// <returns><c>true</c> if the value of <paramref name="lhs"/> is different from the value of <paramref name="rhs"/>; otherwise, <c>false</c>.</returns>
        public static bool operator !=(ComponentId lhs, IComponentId rhs)
        {
            return !(lhs == rhs);
        }

        /// <summary>
        /// Determines whether two specified <see cref="ComponentId"/> objects have different values.
        /// </summary>
        /// <param name="lhs">The first <see cref="ComponentId"/> to compare.</param>
        /// <param name="rhs">The second string to compare.</param>
        /// <returns><c>true</c> if the value of <paramref name="lhs"/> is different from the value of <paramref name="rhs"/>; otherwise, <c>false</c>.</returns>
        public static bool operator !=(ComponentId lhs, string rhs)
        {
            return !(lhs == rhs);
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current <see cref="ComponentId"/>.
        /// </summary>
        /// <param name="obj">The object to compare with the current <see cref="ComponentId"/>.</param>
        /// <returns><c>true</c> if the specified object is equal to the current <see cref="ComponentId"/>; otherwise, <c>false</c>.</returns>
        public bool Equals(string obj)
        {
            return _id.Equals(obj);
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current <see cref="ComponentId"/>.
        /// </summary>
        /// <param name="obj">The object to compare with the current <see cref="ComponentId"/>.</param>
        /// <returns><c>true</c> if the specified object is equal to the current <see cref="ComponentId"/>; otherwise, <c>false</c>.</returns>
        public override bool Equals(object obj)
        {
            if (obj is ComponentId other)
            {
                return _id == other._id;
            }
            return false;
        }

        /// <summary>
        /// Serves as the default hash function.
        /// </summary>
        /// <returns>A hash code for the current <see cref="ComponentId"/>.</returns>
        public override int GetHashCode()
        {
            return _id.GetHashCode();
        }

        /// <summary>
        /// Returns the string representation of the component identifier.
        /// </summary>
        /// <returns>The identifier of the component as a string.</returns>
        public override string ToString()
        {
            return _id;
        }
    }
}
