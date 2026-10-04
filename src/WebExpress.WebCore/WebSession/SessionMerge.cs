using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace WebExpress.WebCore.WebSession
{
    /// <summary>
    /// Combines two concurrent changes of one session. Two requests of the same user may run at
    /// the same time on different instances - two tabs, a page and its background calls - and
    /// each writes the session it loaded. Writing one over the other would silently drop the
    /// first change, so the changes are merged against the state both started from.
    /// </summary>
    /// <remarks>
    /// The merge is three-way: whatever only one side changed is taken from that side. Objects are
    /// merged member by member and arrays as sets - an element one side added or removed is added
    /// or removed in the result - which covers keyed dictionaries and lists such as the
    /// notifications of a session. Only where both sides changed the same scalar value differently
    /// does the side being written win, since it is the more recent change.
    /// </remarks>
    internal static class SessionMerge
    {
        /// <summary>
        /// Merges two versions of a json value that both derive from a common base.
        /// </summary>
        /// <param name="original">The state both sides started from, or null when there was none.</param>
        /// <param name="ours">The version being written.</param>
        /// <param name="theirs">The version another instance wrote meanwhile.</param>
        /// <returns>The merged value.</returns>
        internal static JsonNode Merge(JsonNode original, JsonNode ours, JsonNode theirs)
        {
            if (JsonNode.DeepEquals(ours, original))
            {
                return theirs?.DeepClone();
            }

            if (JsonNode.DeepEquals(theirs, original) || JsonNode.DeepEquals(theirs, ours))
            {
                return ours?.DeepClone();
            }

            if (ours is JsonObject ourObject && theirs is JsonObject theirObject)
            {
                return MergeObjects(original as JsonObject, ourObject, theirObject);
            }

            if (ours is JsonArray ourArray && theirs is JsonArray theirArray)
            {
                return MergeArrays(original as JsonArray, ourArray, theirArray);
            }

            return ours?.DeepClone();
        }

        /// <summary>
        /// Merges two objects member by member.
        /// </summary>
        /// <param name="original">The common base, or null.</param>
        /// <param name="ours">The version being written.</param>
        /// <param name="theirs">The version another instance wrote.</param>
        /// <returns>The merged object.</returns>
        private static JsonObject MergeObjects(JsonObject original, JsonObject ours, JsonObject theirs)
        {
            var result = new JsonObject();
            var names = (original?.Select(x => x.Key) ?? [])
                .Concat(ours.Select(x => x.Key))
                .Concat(theirs.Select(x => x.Key))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            foreach (var name in names)
            {
                JsonNode baseValue = null;
                var inBase = original?.TryGetPropertyValue(name, out baseValue) ?? false;
                var inOurs = ours.TryGetPropertyValue(name, out var ourValue);
                var inTheirs = theirs.TryGetPropertyValue(name, out var theirValue);

                if (!inBase)
                {
                    // added by one side or both; with both, the side being written wins on conflict
                    if (inOurs && inTheirs)
                    {
                        result[name] = Merge(null, ourValue, theirValue);
                    }
                    else
                    {
                        result[name] = (inOurs ? ourValue : theirValue)?.DeepClone();
                    }

                    continue;
                }

                if (!inOurs)
                {
                    // removed here; even a change there yields to the more recent removal
                    continue;
                }

                if (!inTheirs)
                {
                    // removed there; it stays removed unless it was changed here
                    if (!JsonNode.DeepEquals(ourValue, baseValue))
                    {
                        result[name] = ourValue?.DeepClone();
                    }

                    continue;
                }

                result[name] = Merge(baseValue, ourValue, theirValue);
            }

            return result;
        }

        /// <summary>
        /// Merges two arrays as sets of elements, keeping the order of the other instance's version
        /// and appending what was added here.
        /// </summary>
        /// <param name="original">The common base, or null.</param>
        /// <param name="ours">The version being written.</param>
        /// <param name="theirs">The version another instance wrote.</param>
        /// <returns>The merged array.</returns>
        private static JsonArray MergeArrays(JsonArray original, JsonArray ours, JsonArray theirs)
        {
            var baseElements = original ?? [];
            var removed = baseElements.Where(x => !ours.Any(y => JsonNode.DeepEquals(x, y))).ToList();
            var added = ours.Where(x => !baseElements.Any(y => JsonNode.DeepEquals(x, y))).ToList();
            var result = new JsonArray();

            foreach (var element in theirs)
            {
                if (!removed.Any(x => JsonNode.DeepEquals(x, element)))
                {
                    result.Add(element?.DeepClone());
                }
            }

            foreach (var element in added)
            {
                if (!result.Any(x => JsonNode.DeepEquals(x, element)))
                {
                    result.Add(element?.DeepClone());
                }
            }

            return result;
        }
    }
}
