using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.UnityConverters;
using System.Reflection;

namespace Readymade.Persistence {
    /// <summary>
    /// Custom contract resolver to make sure <see cref="JsonIgnoreAttribute"/> works on serialized fields.
    /// </summary>
    public class PackContractResolver : UnityTypeContractResolver {
        /// <inheritdoc/>
        protected override JsonProperty CreateProperty ( MemberInfo member, MemberSerialization memberSerialization ) {
            JsonProperty jsonProperty = base.CreateProperty ( member, memberSerialization );

            if ( !jsonProperty.Ignored && member.GetCustomAttribute<JsonIgnoreAttribute> () != null )
                jsonProperty.Ignored = true;

            return jsonProperty;
        }
    }
}